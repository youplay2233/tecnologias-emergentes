"""Jarvis en realidad mixta: el cerebro en la laptop, la interfaz en Unity (Meta Quest 3).

  uv run jarvis-rm            por voz: «Oye Jarvis» o el botón A del control derecho
  uv run jarvis-rm --texto    escribes las peticiones en la terminal (sin micrófono)
  uv run jarvis-rm --demo     prueba el enlace con una secuencia fija, sin usar Claude
  uv run jarvis-rm --demo --capturas DIR   además guarda capturas (solo con la app de escritorio)

En el visor abre la app Jarvis RM (proyecto ~/jarvis-rm): encuentra la laptop sola (pregunta por UDP)
y se conecta. Los modelos 3D los descarga por HTTP del puerto settings.rm_files_port.
"""

import argparse
import queue
import socket
import threading
import time
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path

from jarvis.config import settings
from jarvis.unity.servidor import ServidorUnity


def ip_local() -> str:
    """IP de esta laptop en la red local (la que hay que poner en Unity si falla el descubrimiento)."""
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(("10.255.255.255", 1))  # no envía nada: solo elige la interfaz de salida
        return s.getsockname()[0]
    except OSError:
        return "127.0.0.1"
    finally:
        s.close()


@dataclass
class Contexto:
    servidor: ServidorUnity
    url_modelo: Callable[[str], str]                     # clave -> URL HTTP del GLB
    pedidos: queue.Queue = field(default_factory=queue.Queue)   # lo que se pulsa en la tableta
    audio: object | None = None   # AudioVisor: micrófono y bocina del visor (solo en modo voz)
    anuncios: queue.Queue = field(default_factory=queue.Queue)  # frases que Jarvis dice por su cuenta


def catalogo() -> dict:
    """Modelos y documentos que muestra la tableta del visor."""
    from jarvis.connectors import files
    from jarvis.modelos import catalogo as modelos

    documentos = [
        {"nombre": files.fix_filename(p.name), "carpeta": p.parent.name, "mb": round(p.stat().st_size / 1e6, 1)}
        for p in files.recientes(15)
    ]
    return {"tipo": "catalogo", "modelos": modelos.modelos(), "documentos": documentos}


class Sesion:
    """Une el orquestador con el visor: avisa en qué fase va y mide cuánto tarda cada una."""

    def __init__(self, servidor: ServidorUnity, preguntar, hablar=None):
        self.servidor = servidor
        self._preguntar = preguntar   # texto -> respuesta (Jarvis.ask)
        self._hablar = hablar         # texto -> None (voz en la bocina de la laptop), opcional
        self._turno = threading.Lock()  # la voz y la tableta no deben hablar con Jarvis a la vez

    def estado(self, valor: str) -> None:
        self.servidor.enviar({"tipo": "estado", "valor": valor})

    def atender(self, texto: str, escucha_s: float = 0.0) -> str:
        with self._turno:
            return self._atender(texto, escucha_s)

    def _atender(self, texto: str, escucha_s: float) -> str:
        self.servidor.enviar({"tipo": "oido", "texto": texto})
        self.estado("pensando")
        t = time.perf_counter()
        try:
            respuesta = self._preguntar(texto)
        except Exception as e:  # noqa: BLE001 - que un error de red no tumbe el asistente
            print(f"Error: {e!r}")
            respuesta = "Tuve un problema al procesar eso."
        cerebro_s = time.perf_counter() - t

        self.servidor.enviar({"tipo": "decir", "texto": respuesta})
        self.estado("hablando")
        t = time.perf_counter()
        if self._hablar is not None:
            self._hablar(respuesta)
        voz_s = time.perf_counter() - t

        # Componentes de la latencia del ciclo: sensado (escucha), cómputo (cerebro) y actuación (voz)
        self.servidor.enviar({
            "tipo": "metricas",
            "escucha_ms": round(escucha_s * 1000),
            "cerebro_ms": round(cerebro_s * 1000),
            "voz_ms": round(voz_s * 1000),
        })
        self.estado("reposo")
        print(f"Jarvis: {respuesta}   [cerebro {cerebro_s:.2f}s · voz {voz_s:.2f}s]")
        return respuesta


def _confirmar_por_texto(pregunta: str) -> bool:
    return input(f"Jarvis: {pregunta} [s/N] ").strip().lower() in ("s", "si", "sí")


def _crear_jarvis(ctx: Contexto, confirmar):
    from jarvis.agents.orchestrator import Jarvis
    from jarvis.unity.herramientas import (
        INSTRUCCIONES,
        INSTRUCCIONES_ROBOTS,
        crear_herramientas,
        crear_herramientas_robots,
    )

    herramientas = crear_herramientas(ctx.servidor) + crear_herramientas_robots(ctx.servidor, ctx.url_modelo)
    return Jarvis(confirm=confirmar, extra_tools=herramientas, system_extra=INSTRUCCIONES + INSTRUCCIONES_ROBOTS)


def modo_demo(ctx: Contexto, capturas: Path | None = None) -> None:
    """Secuencia fija para comprobar el enlace y la latencia sin gastar créditos de la API."""
    from jarvis.modelos import catalogo as modelos

    servidor = ctx.servidor
    print("Esperando al visor...")
    while not servidor.conectado:
        time.sleep(0.2)
    time.sleep(1.5)  # deja que lleguen las primeras mediciones de latencia
    pasos = [
        ("estado", {"tipo": "estado", "valor": "escuchando"}),
        ("oido", {"tipo": "oido", "texto": "Jarvis, enséñame lo que sabes hacer"}),
        ("estado", {"tipo": "estado", "valor": "pensando"}),
        ("decir", {"tipo": "decir", "texto": "Enlace listo. Voy a crear unos objetos frente a ti."}),
        ("orden", {"tipo": "crear", "forma": "cubo", "cantidad": 3, "color": "azul", "tamano": 0.2}),
        ("orden", {"tipo": "crear", "forma": "esfera", "cantidad": 2, "color": "amarillo", "tamano": 0.15}),
        ("orden", {"tipo": "lanzar", "forma": "esfera", "color": "rojo", "tamano": 0.15, "fuerza": 6.0}),
        # El resumen se arma al llegar a este paso, ya con las mediciones de latencia que mandó el visor
        ("orden", lambda: {"tipo": "panel", "titulo": "ENLACE", "texto": servidor.resumen()}),
        ("orden", {"tipo": "gravedad", "activa": False}),
        ("orden", {"tipo": "gravedad", "activa": True}),
        ("estado", {"tipo": "estado", "valor": "reposo"}),
    ]
    if modelos.info("panda"):
        robot = {"tipo": "modelo", "accion": "cargar", "clave": "panda", "texto": "Franka Panda", "url": ctx.url_modelo("panda")}
        pasos += [
            ("orden", {"tipo": "limpiar"}),
            ("decir", {"tipo": "decir", "texto": "Cargué el brazo Franka Panda. Ahora lo despiezo."}),
            ("orden", robot),
            ("captura", "1_armado"),
            ("orden", {"tipo": "modelo", "accion": "despiezar", "numero": 1.2}),
            ("captura", "2_despiezado"),
            ("orden", {"tipo": "modelo", "accion": "resaltar", "piezas": ["link4", "link5"]}),
            ("orden", {"tipo": "modelo", "accion": "girar", "numero": 180.0, "numero2": 0.0, "texto": "atras"}),
            ("captura", "3_atras_resaltado"),
            ("orden", {"tipo": "tableta", "activa": True}),
            ("captura", "4_tableta"),
        ]
    for clase, mensaje in pasos:
        if callable(mensaje):
            mensaje = mensaje()
        if clase == "captura":
            if capturas is not None:
                time.sleep(1.5)  # deja terminar las animaciones
                ruta = capturas.resolve() / f"{mensaje}.png"
                print(f"  captura   -> {servidor.orden({'tipo': 'captura', 'texto': str(ruta)})}")
            continue
        if clase == "orden":
            t = time.perf_counter()
            acuse = servidor.orden(mensaje, espera=30.0 if mensaje.get("accion") == "cargar" else 3.0)
            print(f"  {mensaje['tipo']:9s} -> {acuse}   [{(time.perf_counter() - t) * 1000:.1f} ms ida y vuelta]")
        else:
            servidor.enviar(mensaje)
            print(f"  {mensaje['tipo']:9s} {mensaje.get('valor', mensaje.get('texto', ''))}")
        time.sleep(1.2)
    print(servidor.resumen())


def modo_texto(ctx: Contexto) -> None:
    jarvis = _crear_jarvis(ctx, _confirmar_por_texto)
    sesion = Sesion(ctx.servidor, jarvis.ask)

    def atender_tableta():
        while True:
            sesion.atender(ctx.pedidos.get())

    threading.Thread(target=atender_tableta, name="jarvis-tableta", daemon=True).start()
    print("Escribe tu petición (Ctrl+D para salir).")
    while True:
        try:
            texto = input("Tú: ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return
        if texto:
            sesion.atender(texto)


def modo_voz(ctx: Contexto, boton: threading.Event) -> None:
    servidor = ctx.servidor
    print("Cargando modelos de voz...")
    from jarvis.agents import personalidad
    from jarvis.voice.activacion import FraseDeActivacion
    from jarvis.voice.audio import Microphone, beep
    from jarvis.voice.loop import _is_yes
    from jarvis.voice.stt import SpeechToText
    from jarvis.voice.tts import TextToSpeech

    from jarvis.unity.audio_visor import AudioVisor

    stt, tts = SpeechToText(), TextToSpeech()
    activacion = FraseDeActivacion(stt)
    audio: AudioVisor | None = None

    # Si el visor manda su micrófono, ese reemplaza al de la laptop (y la voz sale por el visor)
    with Microphone(usar_local=lambda: audio is None or not audio.activo) as mic:
        audio = AudioVisor(servidor, mic.inyectar)
        ctx.audio = audio
        servidor.al_binario = audio.recibir

        def hablar(texto: str) -> None:
            if audio.activo and settings.rm_voz == "visor":
                voz, frecuencia = tts.synthesize(texto)
                audio.reproducir(voz, frecuencia)
            else:
                tts.say(texto)

        def escuchar() -> tuple[str, float]:
            servidor.enviar({"tipo": "estado", "valor": "escuchando"})
            if audio.activo and settings.rm_voz == "visor":
                servidor.enviar({"tipo": "pitido"})
            else:
                beep()
            mic.flush()
            t = time.perf_counter()
            texto = stt.transcribe(mic.record_command())
            return texto, time.perf_counter() - t

        def confirmar(pregunta: str) -> bool:
            servidor.enviar({"tipo": "decir", "texto": pregunta})
            hablar(pregunta)
            si = _is_yes(escuchar()[0])
            if not si:
                hablar(personalidad.cancelado())
            return si

        jarvis = _crear_jarvis(ctx, confirmar)
        sesion = Sesion(servidor, jarvis.ask, hablar)
        print("Listo. Di «Oye Jarvis» (o aprieta el botón A del control derecho).")
        hablar(personalidad.saludo())
        mic.flush()

        for frame in mic.frames():
            if not ctx.anuncios.empty():
                anuncio = ctx.anuncios.get()
                servidor.enviar({"tipo": "decir", "texto": anuncio})
                servidor.enviar({"tipo": "estado", "valor": "hablando"})
                hablar(anuncio)
                servidor.enviar({"tipo": "estado", "valor": "reposo"})
                mic.flush()
                activacion.reset()
                continue
            orden = activacion.alimentar(frame)
            if orden is None and not boton.is_set() and ctx.pedidos.empty():
                continue
            boton.clear()

            if orden is None and not ctx.pedidos.empty():
                # Lo pulsado en la tableta llega como si lo hubieras dicho
                texto, escucha_s = ctx.pedidos.get(), 0.0
            elif orden:
                # «Oye Jarvis, despieza el robot»: la orden vino en la misma frase
                texto, escucha_s = orden, 0.0
            else:
                servidor.enviar({"tipo": "estado", "valor": "hablando"})
                hablar(personalidad.acuse())
                texto, escucha_s = escuchar()
            print(f"Tú: {texto or '(nada)'}  [{escucha_s:.2f}s]")
            if texto:
                sesion.atender(texto, escucha_s)
            else:
                sesion.estado("reposo")
                hablar(personalidad.no_escuche())

            # Lo que se oyó mientras Jarvis hablaba es su propia voz: se descarta
            mic.flush()
            activacion.reset()
            boton.clear()


def main() -> None:
    parser = argparse.ArgumentParser(prog="jarvis-rm", description="Jarvis con interfaz de realidad mixta en Unity.")
    parser.add_argument("--texto", action="store_true", help="peticiones por teclado en vez de voz")
    parser.add_argument("--demo", action="store_true", help="secuencia de prueba del enlace, sin Claude")
    parser.add_argument("--capturas", type=Path, help="con --demo: carpeta donde la app de escritorio guarda capturas")
    args = parser.parse_args()

    boton = threading.Event()
    pedidos: queue.Queue = queue.Queue()
    ctx: Contexto | None = None
    ultimo_pedido = {"texto": "", "cuando": 0.0}
    servidor: ServidorUnity | None = None

    def al_evento(evento: dict) -> None:
        tipo = evento.get("tipo")
        if tipo == "voz_fin":
            if ctx is not None and ctx.audio is not None:
                ctx.audio.evento(evento)
        elif tipo == "conectado":
            print(f"[visor] conectado: {evento.get('dispositivo')} · escena {evento.get('escena')}")
            servidor.enviar(catalogo())   # la tableta del visor muestra modelos y documentos
            if ctx is not None:
                from jarvis.agents import personalidad

                ctx.anuncios.put(personalidad.bienvenida())
        elif tipo == "desconectado":
            print(f"[visor] desconectado: {evento.get('dispositivo')}")
        elif tipo == "boton" and evento.get("nombre") == "A":
            boton.set()   # el botón A del visor equivale a decir «Oye Jarvis»
        elif tipo == "evento" and evento.get("nombre") == "pedido" and evento.get("detalle"):
            texto = str(evento["detalle"])
            # Un toque repetido mientras Jarvis todavía atiende el primero no se vuelve a pedir
            if texto == ultimo_pedido["texto"] and time.time() - ultimo_pedido["cuando"] < 8.0:
                return
            ultimo_pedido.update(texto=texto, cuando=time.time())
            print(f"[tableta] {texto}")
            pedidos.put(texto)
        elif tipo == "evento":
            print(f"[visor] {evento.get('nombre')}: {evento.get('detalle')}")

    from jarvis.unity.archivos import ServidorArchivos

    archivos = ServidorArchivos(settings.rm_host, settings.rm_files_port, settings.rm_token)
    archivos.iniciar()
    ip = ip_local()

    def url_modelo(clave: str) -> str:
        url = f"http://{ip}:{archivos.puerto}/modelos/{clave}.glb"
        return f"{url}?token={settings.rm_token}" if settings.rm_token else url

    servidor = ServidorUnity(settings.rm_host, settings.rm_port, settings.rm_beacon_port, settings.rm_token, al_evento)
    servidor.iniciar()
    ctx = Contexto(servidor, url_modelo, pedidos)
    print(f"Enlace de realidad mixta en ws://{ip}:{settings.rm_port} (descubrimiento UDP en el puerto "
          f"{settings.rm_beacon_port}; modelos por HTTP en el puerto {archivos.puerto}).")

    try:
        if args.demo:
            if args.capturas:
                args.capturas.mkdir(parents=True, exist_ok=True)
            modo_demo(ctx, args.capturas)
        elif args.texto:
            modo_texto(ctx)
        else:
            modo_voz(ctx, boton)
    except KeyboardInterrupt:
        print()
    finally:
        servidor.detener()
        archivos.detener()


if __name__ == "__main__":
    main()
