#!/usr/bin/env python3
"""Cliente del Puente Jarvis: le manda órdenes al editor de Unity y espera el resultado.

Unity debe estar abierto con el proyecto Practica_1_RM. El puente (Assets/PuenteJarvis) lee órdenes en
Practica_1_RM/PuenteJarvis/entrada/*.json y contesta en salida/*.json. Solo se usa la biblioteca estándar.

  python3 herramientas/puente.py estado                 qué está haciendo Unity y errores de compilación
  python3 herramientas/puente.py esperar                espera a que Unity termine de importar o compilar
  python3 herramientas/puente.py log [n]                últimas líneas de la consola de Unity
  python3 herramientas/puente.py ping
  python3 herramientas/puente.py orden '{"cmd":"paquetes"}'
  python3 herramientas/puente.py refrescar              importa archivos cambiados y recompila
  python3 herramientas/puente.py construir [escena]     vuelve a armar todas las prácticas, o una
  python3 herramientas/puente.py abrir Canon_Autonomo
  python3 herramientas/puente.py jerarquia [objeto] [--detalle]
  python3 herramientas/puente.py captura nombre [--ancho 1920 --alto 1080]
  python3 herramientas/puente.py ventana Inspector [objeto] --salida nombre [--voltear]
  python3 herramientas/puente.py grabar Canon_Autonomo  graba 20 s de una práctica y la deja en evidencias/
  python3 herramientas/puente.py evidencias [ids...]    jerarquía, captura, Inspector y video de cada práctica
  python3 herramientas/puente.py plataforma Android
  python3 herramientas/puente.py apk [--instalar]       compila el APK; con --instalar lo manda al Quest por USB
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

RAIZ = Path(os.environ.get("TE_RAIZ", Path(__file__).resolve().parent.parent))
PROYECTO = RAIZ / "Practica_1_RM"
PUENTE = PROYECTO / "PuenteJarvis"
EVIDENCIAS = RAIZ / "evidencias"

ESCENAS = ["objetos", "Parque_diversiones", "Proyecto_final", "Canon_Autonomo", "Fisica_Sesion1", "Fisica_Sesion2", "Jarvis_RM"]

# Objetos cuyo Inspector se captura como evidencia de cada práctica
INSPECTOR = {
    "objetos": ["Cube"],
    "Parque_diversiones": ["Rueda_giratoria", "Cabina_01"],
    "Proyecto_final": ["Panel_Cerebro"],
    "Canon_Autonomo": ["Cañon"],
    "Fisica_Sesion1": ["Cubo_masa_100", "Objeto_A_Esfera", "Objeto_B_Cubo", "Objeto_C_Capsula"],
    "Fisica_Sesion2": ["Pelota", "Pelota_Continuous", "Obstaculo_2_MeshCollider"],
    "Jarvis_RM": ["Jarvis_RM"],
}


def _simular(t: float, **mensaje) -> dict:
    """Orden escrita a mano para la escena Jarvis_RM (JarvisRM.SimularOrden), sin pasar por la red."""
    return {"t": t, "objetivo": "Jarvis_RM", "mensaje": "SimularOrden", "arg": json.dumps(mensaje, ensure_ascii=False)}


# Lo que pasa durante los 20 s de cada video. El puente llama objetivo.SendMessage(mensaje[, arg]) en el segundo t.
ACCIONES = {
    # El cañón dispara cada 1.2 s (lo mismo que hace el clic o el gatillo derecho)
    "Canon_Autonomo": [{"t": round(1.0 + 1.2 * i, 1), "objetivo": "Cañon", "mensaje": "Disparar"} for i in range(15)],
    # Mismo empuje a los cubos de masa 1, 10 y 100; después se repite la caída
    "Fisica_Sesion1": [
        {"t": 4.0, "objetivo": "Experimento_masa_y_gravedad", "mensaje": "Empujar"},
        {"t": 8.0, "objetivo": "Experimento_masa_y_gravedad", "mensaje": "Reiniciar"},
        {"t": 8.0, "objetivo": "Practica_caida", "mensaje": "Reiniciar"},
        {"t": 12.0, "objetivo": "Experimento_masa_y_gravedad", "mensaje": "Empujar"},
        {"t": 16.0, "objetivo": "Experimento_masa_y_gravedad", "mensaje": "Reiniciar"},
        {"t": 16.0, "objetivo": "Practica_caida", "mensaje": "Reiniciar"},
    ],
    # Se repite la pista dos veces y al final se desactiva el Collider del piso
    "Fisica_Sesion2": [
        {"t": 9.0, "objetivo": "Fisica_Sesion2", "mensaje": "Reiniciar"},
        {"t": 13.5, "objetivo": "Fisica_Sesion2", "mensaje": "Reiniciar"},
        {"t": 15.5, "objetivo": "Piso", "mensaje": "Alternar"},
    ],
    # Órdenes SIMULADAS (sin laptop ni Claude): en la página hay que decir que son simuladas.
    # Para grabar el enlace real, arranca antes `uv run jarvis-rm --demo` y usa `grabar Jarvis_RM --sin-acciones`.
    "Jarvis_RM": [
        _simular(1.0, tipo="estado", valor="escuchando"),
        _simular(2.5, tipo="oido", texto="Jarvis, crea cinco cubos azules"),
        _simular(2.6, tipo="estado", valor="pensando"),
        _simular(4.0, tipo="estado", valor="hablando"),
        _simular(4.0, tipo="decir", texto="Listo: cinco cubos azules frente a ti."),
        _simular(4.1, tipo="crear", forma="cubo", cantidad=5, color="azul", tamano=0.2),
        _simular(7.0, tipo="oido", texto="Lanza una esfera roja"),
        _simular(7.1, tipo="estado", valor="pensando"),
        _simular(8.0, tipo="decir", texto="Va una esfera roja."),
        _simular(8.0, tipo="lanzar", forma="esfera", color="rojo", tamano=0.18, fuerza=7),
        _simular(11.0, tipo="oido", texto="Apaga la gravedad"),
        _simular(12.0, tipo="decir", texto="Gravedad apagada: los objetos flotan."),
        _simular(12.0, tipo="gravedad", activa=False),
        _simular(15.5, tipo="decir", texto="Gravedad encendida."),
        _simular(15.5, tipo="gravedad", activa=True),
        _simular(18.0, tipo="panel", titulo="DEMOSTRACIÓN", texto="Órdenes simuladas desde el editor, sin la laptop."),
        _simular(18.5, tipo="estado", valor="reposo"),
    ],
}


class ErrorDePuente(Exception):
    pass


# ----------------------------------------------------------------------------- estado y registro

def leer_json(ruta: Path):
    try:
        return json.loads(ruta.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None


def estado():
    """(estado, segundos desde el último latido). El puente escribe estado.json cada 3 s."""
    archivo = PUENTE / "estado.json"
    datos = leer_json(archivo)
    if datos is None:
        return None, None
    return datos, time.time() - archivo.stat().st_mtime


def describir_estado() -> str:
    e, edad = estado()
    if e is None:
        return ("No hay estado.json: Unity todavía no ha abierto el proyecto, sigue en la primera importación, "
                "o el puente no compiló (revisa ~/.config/unity3d/Editor.log).")
    lineas = [
        f"Latido hace {edad:.0f} s" + ("  << SIN LATIDO: Unity cerrado, recargando scripts o con un diálogo abierto" if edad > 20 else ""),
        f"Unity {e.get('unity')} en {e.get('editor')} · plataforma {e.get('plataformaActiva')} · escena {e.get('escenaActiva') or '(ninguna)'}",
        f"evento={e.get('evento')} compilando={e.get('compilando')} importando={e.get('importando')} play={e.get('reproduciendo')}"
        f" trabajo={e.get('trabajo') or '-'}",
        f"Android instalado={e.get('androidInstalado')} · Meta XR SDK={e.get('metaXR')} · compilación fallida={e.get('compilacionFallida')}"
        f" · recargas={e.get('recargas')} · órdenes={e.get('ordenesProcesadas')}",
    ]
    c = leer_json(PUENTE / "compilacion.json")
    if c:
        lineas.append(f"Última compilación {c.get('hora')}: {c.get('errores')} errores, {c.get('advertencias')} advertencias")
        for m in [m for m in c.get("mensajes", []) if m.get("tipo") == "error"][:30]:
            lineas.append(f"  ERROR {m.get('archivo')}({m.get('linea')},{m.get('columna')}): {m.get('mensaje')}")
    return "\n".join(lineas)


def cola_del_registro(n: int = 40) -> str:
    try:
        lineas = (PUENTE / "registro.log").read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except OSError:
        return "(sin registro.log)"
    return "\n".join(lineas[-n:])


def esperar_listo(limite: float = 900.0, callado: bool = False) -> dict:
    """Espera a que Unity esté vivo y sin importar, compilar ni trabajos pendientes."""
    inicio = time.time()
    ultimo_aviso = 0.0
    while True:
        e, edad = estado()
        listo = e is not None and edad < 12 and not e.get("compilando") and not e.get("importando") and not e.get("trabajo")
        if listo:
            return e
        transcurrido = time.time() - inicio
        if transcurrido > limite:
            raise ErrorDePuente("Unity no quedó listo en %.0f s.\n%s" % (limite, describir_estado()))
        if not callado and transcurrido - ultimo_aviso >= 15:
            ultimo_aviso = transcurrido
            motivo = "sin latido" if e is None or edad >= 12 else f"compilando={e.get('compilando')} importando={e.get('importando')} trabajo={e.get('trabajo') or '-'}"
            print(f"  esperando a Unity ({transcurrido:.0f} s): {motivo}", file=sys.stderr)
        time.sleep(1.0)


# ----------------------------------------------------------------------------- órdenes

_contador = 0


def enviar(orden: dict, espera: float = 120.0, callado: bool = False) -> dict:
    """Escribe la orden en entrada/ y espera su resultado en salida/. Lanza ErrorDePuente si falla."""
    global _contador
    if not (PUENTE / "entrada").is_dir():
        raise ErrorDePuente("No existe %s: abre el proyecto en Unity primero.\n%s" % (PUENTE / "entrada", describir_estado()))
    _contador += 1
    ident = "%s_%02d_%s" % (time.strftime("%H%M%S"), _contador, orden.get("cmd", "orden"))
    orden = dict(orden, id=ident)

    # Se escribe con otro nombre y se renombra: el puente nunca ve un archivo a medias
    temporal = PUENTE / "entrada" / (ident + ".tmp")
    temporal.write_text(json.dumps(orden, ensure_ascii=False), encoding="utf-8")
    os.replace(temporal, PUENTE / "entrada" / (ident + ".json"))

    salida = PUENTE / "salida" / (ident + ".json")
    inicio = time.time()
    ultimo_aviso = 0.0
    while True:
        if salida.is_file():
            resultado = leer_json(salida)
            if resultado is not None:
                break
        transcurrido = time.time() - inicio
        if transcurrido > espera:
            raise ErrorDePuente("La orden %s no contestó en %.0f s.\n%s\n--- consola de Unity ---\n%s"
                                % (ident, espera, describir_estado(), cola_del_registro(25)))
        if not callado and transcurrido - ultimo_aviso >= 15:
            ultimo_aviso = transcurrido
            e, edad = estado()
            detalle = "sin latido" if e is None else f"latido hace {edad:.0f} s, evento={e.get('evento')}, trabajo={e.get('trabajo') or '-'}"
            print(f"  {orden['cmd']}: {transcurrido:.0f} s ({detalle})", file=sys.stderr)
        time.sleep(0.2)

    if not resultado.get("ok", False):
        raise ErrorDePuente("Unity rechazó la orden %s: %s\n%s" % (orden["cmd"], resultado.get("mensaje"), resultado.get("datos") or ""))
    return resultado


def ruta_de_escena(ident: str) -> str:
    return ident if ident.endswith(".unity") else f"Assets/Scene/{ident}.unity"


def abrir(ident: str) -> dict:
    return enviar({"cmd": "abrir_escena", "ruta": ruta_de_escena(ident)})


def construir(escena: str = "") -> str:
    if escena:
        r = enviar({"cmd": "llamar", "tipo": "TecEmergentes.ConstructorPracticas", "metodo": "Construir", "args": [escena]}, espera=600)
    else:
        r = enviar({"cmd": "llamar", "tipo": "TecEmergentes.ConstructorPracticas", "metodo": "ConstruirTodo"}, espera=900)
    return r.get("datos", "")


def captura(salida: str, ancho: int = 1920, alto: int = 1080) -> Path:
    r = enviar({"cmd": "captura", "salida": salida, "ancho": ancho, "alto": alto})
    return Path(r["datos"])


def ventana(nombre: str, objeto: str, salida: str, voltear: bool = False) -> Path:
    r = enviar({"cmd": "ventana", "nombre": nombre, "ruta": objeto, "salida": salida, "voltear": voltear}, espera=60)
    # datos = "<archivo> (<ancho>x<alto>, <método>)"
    return Path(r["datos"].rsplit(" (", 1)[0])


# ----------------------------------------------------------------------------- video

def ffmpeg():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    try:
        import imageio_ffmpeg  # type: ignore
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return None


def convertir_video(datos: str, carpeta: Path, fps: int) -> str:
    """Deja carpeta/video.mp4 y poster.jpg a partir de lo que grabó el puente. Devuelve un resumen."""
    campos = dict(l.split("=", 1) for l in datos.splitlines() if "=" in l)
    cuadros = Path(campos.get("cuadros", "")) if campos.get("cuadros") else None
    webm = Path(campos.get("video", "")) if campos.get("video") else None
    exe = ffmpeg()
    carpeta.mkdir(parents=True, exist_ok=True)
    mp4 = carpeta / "video.mp4"

    if exe is None:
        if webm and webm.is_file():
            shutil.copyfile(webm, carpeta / "video.webm")
            return ("SIN ffmpeg: se dejó video.webm (se ve en Chrome y Firefox). Para MP4: `sudo apt install ffmpeg` "
                    "o `uv run --with imageio-ffmpeg python3 herramientas/puente.py ...`, y repite la grabación.")
        raise ErrorDePuente("No hay ffmpeg y Unity no generó WebM; los cuadros quedaron en %s" % cuadros)

    if cuadros and cuadros.is_file() and cuadros.stat().st_size > 0:
        entrada = ["-f", "mjpeg", "-framerate", str(fps), "-i", str(cuadros)]
    elif webm and webm.is_file():
        entrada = ["-i", str(webm)]
    else:
        raise ErrorDePuente("El puente no dejó ni cuadros ni video: " + datos)

    subprocess.run([exe, "-y", "-loglevel", "error", *entrada, "-c:v", "libx264", "-preset", "slow", "-crf", "23",
                    "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-an", str(mp4)], check=True)
    subprocess.run([exe, "-y", "-loglevel", "error", "-ss", "6", "-i", str(mp4), "-frames:v", "1", "-q:v", "3",
                    str(carpeta / "poster.jpg")], check=True)
    viejo = carpeta / "video.webm"
    if viejo.exists():
        viejo.unlink()
    return "video.mp4 (%.1f MB) y poster.jpg" % (mp4.stat().st_size / 1048576)


def grabar(ident: str, segundos: float = 20.0, fps: int = 30, ancho: int = 1280, alto: int = 720, sin_acciones: bool = False) -> str:
    acciones = [] if sin_acciones else ACCIONES.get(ident, [])
    esperar_listo(callado=True)
    # Con ffmpeg basta con los cuadros JPEG; sin el WebM de Unity cada cuadro tarda menos y la grabación va más cerca del tiempo real
    r = enviar({"cmd": "grabar", "ruta": ruta_de_escena(ident), "segundos": segundos, "fps": fps, "ancho": ancho, "alto": alto,
                "salida": ident, "acciones": acciones, "sinwebm": ffmpeg() is not None}, espera=900)
    datos = r.get("datos", "")
    notas = [l for l in datos.splitlines() if l and not l.startswith(("video=", "cuadros=", "total="))]
    resumen = convertir_video(datos, EVIDENCIAS / ident, fps)
    return "%s: %s · %s%s" % (ident, r.get("mensaje"), resumen, ("\n  notas del puente: " + " | ".join(notas)) if notas else "")


def evidencias(ids, voltear: bool = False, sin_ventanas: bool = False) -> None:
    """Jerarquía, captura de la escena, Inspector y video de 20 s de cada práctica; al final regenera la página."""
    for ident in ids:
        carpeta = EVIDENCIAS / ident
        carpeta.mkdir(parents=True, exist_ok=True)
        print(f"== {ident}")
        esperar_listo(callado=True)
        print("  " + abrir(ident)["mensaje"])

        (carpeta / "jerarquia.txt").write_text(enviar({"cmd": "jerarquia"})["datos"], encoding="utf-8")
        (carpeta / "jerarquia_detalle.txt").write_text(enviar({"cmd": "jerarquia", "ejecutar": True})["datos"], encoding="utf-8")
        shutil.copyfile(captura(ident + "_escena"), carpeta / "escena.png")
        print("  jerarquia.txt, jerarquia_detalle.txt y escena.png")

        if not sin_ventanas:
            for objeto in INSPECTOR.get(ident, []):
                try:
                    shutil.copyfile(ventana("Inspector", objeto, f"{ident}_inspector_{objeto}", voltear), carpeta / f"inspector_{objeto}.png")
                    print(f"  inspector_{objeto}.png")
                except (ErrorDePuente, OSError) as e:
                    print(f"  (sin captura del Inspector de {objeto}: {str(e).splitlines()[0]})")

        print("  " + grabar(ident))

        if ident == "Fisica_Sesion2" and not sin_ventanas:
            try:
                shutil.copyfile(ventana("Console", "", f"{ident}_consola", voltear), carpeta / "consola.png")
                print("  consola.png")
            except (ErrorDePuente, OSError) as e:
                print(f"  (sin captura de la consola: {str(e).splitlines()[0]})")

    generador = RAIZ / "herramientas" / "generar_sitio.py"
    if generador.is_file():
        subprocess.run([sys.executable, str(generador)], check=False)


# ----------------------------------------------------------------------------- línea de órdenes

def main() -> int:
    p = argparse.ArgumentParser(description="Cliente del Puente Jarvis (Unity abierto con Practica_1_RM).")
    sub = p.add_subparsers(dest="accion", required=True)
    sub.add_parser("estado")
    sub.add_parser("esperar")
    s = sub.add_parser("log"); s.add_argument("n", nargs="?", type=int, default=40)
    sub.add_parser("ping")
    s = sub.add_parser("orden"); s.add_argument("json"); s.add_argument("--espera", type=float, default=120)
    sub.add_parser("refrescar")
    s = sub.add_parser("construir"); s.add_argument("escena", nargs="?", default="")
    s = sub.add_parser("abrir"); s.add_argument("escena")
    s = sub.add_parser("jerarquia"); s.add_argument("objeto", nargs="?", default=""); s.add_argument("--detalle", action="store_true")
    s = sub.add_parser("captura"); s.add_argument("salida"); s.add_argument("--ancho", type=int, default=1920); s.add_argument("--alto", type=int, default=1080)
    s = sub.add_parser("ventana"); s.add_argument("nombre"); s.add_argument("objeto", nargs="?", default="")
    s.add_argument("--salida", required=True); s.add_argument("--voltear", action="store_true")
    s = sub.add_parser("grabar"); s.add_argument("escena"); s.add_argument("--segundos", type=float, default=20)
    s.add_argument("--sin-acciones", action="store_true")
    s = sub.add_parser("evidencias"); s.add_argument("ids", nargs="*"); s.add_argument("--voltear", action="store_true")
    s.add_argument("--sin-ventanas", action="store_true")
    s = sub.add_parser("plataforma"); s.add_argument("nombre")
    s = sub.add_parser("apk"); s.add_argument("--instalar", action="store_true")
    a = p.parse_args()

    try:
        if a.accion == "estado":
            print(describir_estado())
        elif a.accion == "esperar":
            esperar_listo()
            print(describir_estado())
        elif a.accion == "log":
            print(cola_del_registro(a.n))
        elif a.accion == "ping":
            r = enviar({"cmd": "ping"}, espera=30)
            print(r["mensaje"], "·", r.get("datos", ""))
        elif a.accion == "orden":
            print(json.dumps(enviar(json.loads(a.json), espera=a.espera), ensure_ascii=False, indent=1))
        elif a.accion == "refrescar":
            print(enviar({"cmd": "refrescar"})["mensaje"])
            time.sleep(3)
            esperar_listo()
            print(describir_estado())
        elif a.accion == "construir":
            print(construir(a.escena))
        elif a.accion == "abrir":
            print(abrir(a.escena)["mensaje"])
        elif a.accion == "jerarquia":
            print(enviar({"cmd": "jerarquia", "nombre": a.objeto, "ejecutar": a.detalle})["datos"])
        elif a.accion == "captura":
            print(captura(a.salida, a.ancho, a.alto))
        elif a.accion == "ventana":
            print(ventana(a.nombre, a.objeto, a.salida, a.voltear))
        elif a.accion == "grabar":
            print(grabar(a.escena, segundos=a.segundos, sin_acciones=a.sin_acciones))
        elif a.accion == "evidencias":
            evidencias(a.ids or ESCENAS, voltear=a.voltear, sin_ventanas=a.sin_ventanas)
        elif a.accion == "plataforma":
            print(enviar({"cmd": "plataforma", "nombre": a.nombre}, espera=1800)["mensaje"])
        elif a.accion == "apk":
            r = enviar({"cmd": "compilar_apk", "ejecutar": a.instalar}, espera=3600)
            print(r["mensaje"])
            if r.get("datos"):
                print(r["datos"])
    except ErrorDePuente as e:
        print("ERROR:", e, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
