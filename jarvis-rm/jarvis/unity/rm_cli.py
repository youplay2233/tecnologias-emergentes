"""Jarvis en realidad mixta: el cerebro en la laptop, la interfaz en Unity (Meta Quest 3).

  uv run jarvis-rm            por voz: «Hey Jarvis» o el botón A del control derecho
  uv run jarvis-rm --texto    escribes las peticiones en la terminal (sin micrófono)
  uv run jarvis-rm --demo     prueba el enlace con una secuencia fija, sin usar Claude

En el visor abre la escena Jarvis_RM: encuentra la laptop sola (pregunta por UDP) y se conecta.
"""

import argparse
import socket
import threading
import time

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


class Sesion:
    """Une el orquestador con el visor: avisa en qué fase va y mide cuánto tarda cada una."""

    def __init__(self, servidor: ServidorUnity, preguntar, hablar=None):
        self.servidor = servidor
        self._preguntar = preguntar   # texto -> respuesta (Jarvis.ask)
        self._hablar = hablar         # texto -> None (voz en la bocina de la laptop), opcional

    def estado(self, valor: str) -> None:
        self.servidor.enviar({"tipo": "estado", "valor": valor})

    def atender(self, texto: str, escucha_s: float = 0.0) -> str:
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


def _crear_jarvis(servidor: ServidorUnity, confirmar):
    from jarvis.agents.orchestrator import Jarvis
    from jarvis.unity.herramientas import INSTRUCCIONES, crear_herramientas

    return Jarvis(confirm=confirmar, extra_tools=crear_herramientas(servidor), system_extra=INSTRUCCIONES)


def modo_demo(servidor: ServidorUnity) -> None:
    """Secuencia fija para comprobar el enlace y la latencia sin gastar créditos de la API."""
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
    for clase, mensaje in pasos:
        if callable(mensaje):
            mensaje = mensaje()
        if clase == "orden":
            t = time.perf_counter()
            acuse = servidor.orden(mensaje)
            print(f"  {mensaje['tipo']:9s} -> {acuse}   [{(time.perf_counter() - t) * 1000:.1f} ms ida y vuelta]")
        else:
            servidor.enviar(mensaje)
            print(f"  {mensaje['tipo']:9s} {mensaje.get('valor', mensaje.get('texto', ''))}")
        time.sleep(1.2)
    print(servidor.resumen())


def modo_texto(servidor: ServidorUnity) -> None:
    jarvis = _crear_jarvis(servidor, _confirmar_por_texto)
    sesion = Sesion(servidor, jarvis.ask)
    print("Escribe tu petición (Ctrl+D para salir).")
    while True:
        try:
            texto = input("Tú: ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return
        if texto:
            sesion.atender(texto)


def modo_voz(servidor: ServidorUnity, boton: threading.Event) -> None:
    print("Cargando modelos de voz...")
    from jarvis.voice.audio import Microphone, beep
    from jarvis.voice.loop import _is_yes
    from jarvis.voice.stt import SpeechToText
    from jarvis.voice.tts import TextToSpeech
    from jarvis.voice.wakeword import WakeWord

    wake, stt, tts = WakeWord(), SpeechToText(), TextToSpeech()

    with Microphone() as mic:

        def escuchar() -> tuple[str, float]:
            servidor.enviar({"tipo": "estado", "valor": "escuchando"})
            beep()
            mic.flush()
            t = time.perf_counter()
            texto = stt.transcribe(mic.record_command())
            return texto, time.perf_counter() - t

        def confirmar(pregunta: str) -> bool:
            servidor.enviar({"tipo": "decir", "texto": pregunta})
            tts.say(pregunta)
            si = _is_yes(escuchar()[0])
            if not si:
                tts.say("Va, no lo envío.")
            return si

        jarvis = _crear_jarvis(servidor, confirmar)
        sesion = Sesion(servidor, jarvis.ask, tts.say)
        print("Listo. Di «Hey Jarvis» o aprieta el botón A del control derecho.")

        for frame in mic.frames():
            if not (wake.detect(frame) or boton.is_set()):
                continue
            boton.clear()

            texto, escucha_s = escuchar()
            print(f"Tú: {texto or '(nada)'}  [{escucha_s:.2f}s]")
            if texto:
                sesion.atender(texto, escucha_s)
            else:
                sesion.estado("reposo")
                tts.say("No te escuché, repite por favor.")

            mic.flush()
            wake.reset()
            boton.clear()


def main() -> None:
    parser = argparse.ArgumentParser(prog="jarvis-rm", description="Jarvis con interfaz de realidad mixta en Unity.")
    parser.add_argument("--texto", action="store_true", help="peticiones por teclado en vez de voz")
    parser.add_argument("--demo", action="store_true", help="secuencia de prueba del enlace, sin Claude")
    args = parser.parse_args()

    boton = threading.Event()

    def al_evento(evento: dict) -> None:
        tipo = evento.get("tipo")
        if tipo == "conectado":
            print(f"[visor] conectado: {evento.get('dispositivo')} · escena {evento.get('escena')}")
        elif tipo == "desconectado":
            print(f"[visor] desconectado: {evento.get('dispositivo')}")
        elif tipo == "boton" and evento.get("nombre") == "A":
            boton.set()   # el botón A del visor equivale a decir «Hey Jarvis»
        elif tipo == "evento":
            print(f"[visor] {evento.get('nombre')}: {evento.get('detalle')}")

    servidor = ServidorUnity(settings.rm_host, settings.rm_port, settings.rm_beacon_port, settings.rm_token, al_evento)
    servidor.iniciar()
    print(f"Enlace de realidad mixta en ws://{ip_local()}:{settings.rm_port} (descubrimiento UDP en el puerto {settings.rm_beacon_port}).")

    try:
        if args.demo:
            modo_demo(servidor)
        elif args.texto:
            modo_texto(servidor)
        else:
            modo_voz(servidor, boton)
    except KeyboardInterrupt:
        print()
    finally:
        servidor.detener()


if __name__ == "__main__":
    main()
