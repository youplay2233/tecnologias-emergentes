#!/usr/bin/env python3
"""Genera index.html (la página del repositorio) a partir de contenido.py y de lo que haya en evidencias/.

  python3 herramientas/generar_sitio.py

Cada práctica busca sus evidencias en evidencias/<id>/:
  video.mp4, poster.jpg    video de 20 s grabado en el editor de Unity
  quest.mp4                video grabado desde el Quest 3 (opcional)
  escena.png               captura de la escena
  inspector_*.png          capturas del Inspector
  consola.png              captura de la consola
  jerarquia.txt            árbol de la escena tal como está en Unity
"""

import html
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))
from contenido import PRACTICAS, PROYECTO  # noqa: E402

UNITY = RAIZ / "Practica_1_RM"


def e(texto: str) -> str:
    return html.escape(texto, quote=True)


def evidencias(ident: str) -> dict:
    carpeta = RAIZ / "evidencias" / ident
    def rel(p: Path) -> str:
        return p.relative_to(RAIZ).as_posix()
    datos = {"video": None, "poster": None, "quest": None, "escena": None, "inspector": [], "consola": None, "jerarquia": None}
    if not carpeta.is_dir():
        return datos
    for nombre in ("video", "quest"):
        # MP4 (H.264) se ve en todos los navegadores; WebM es el respaldo si no se pudo convertir
        for extension in ("mp4", "webm"):
            if (carpeta / f"{nombre}.{extension}").is_file():
                datos[nombre] = rel(carpeta / f"{nombre}.{extension}")
                break
    for nombre in ("poster.jpg", "escena.png", "consola.png"):
        if (carpeta / nombre).is_file():
            datos[nombre.split(".")[0]] = rel(carpeta / nombre)
    datos["inspector"] = [rel(p) for p in sorted(carpeta.glob("inspector_*.png"))]
    if (carpeta / "jerarquia.txt").is_file():
        datos["jerarquia"] = (carpeta / "jerarquia.txt").read_text(encoding="utf-8")
    return datos


# ---------------------------------------------------------------------------- piezas

def arbol_de_escena(texto: str, limite: int = 400) -> str:
    """Convierte la jerarquía que exporta el puente (sangría de 2 espacios) en filas con guías."""
    filas = []
    lineas = [l for l in texto.splitlines() if l.strip()]
    for linea in lineas[:limite]:
        nivel = (len(linea) - len(linea.lstrip(" "))) // 2
        cuerpo = linea.strip()
        m = re.match(r"^(.*?)\s{2}pos\((.*?)\) rot\((.*?)\) esc\((.*?)\)(?:\s{2}\{(.*)\})?$", cuerpo)
        if m:
            nombre, pos, rot, esc, comps = m.groups()
            extra = ""
            if comps:
                cortos = [c.split("(")[0].strip() for c in re.split(r",\s(?=[A-ZÁÉÍÓÚ(])", comps)]
                cortos = [c for c in cortos if c and c not in ("MeshFilter", "MeshRenderer")]
                if cortos:
                    extra = " ".join(f'<span class="comp">{e(c)}</span>' for c in cortos[:6])
            filas.append(
                f'<div class="fila" style="--n:{nivel}"><span class="obj">{e(nombre)}</span>'
                f'<span class="tf">{terna("P", pos)}{terna("R", rot)}{terna("S", esc)}</span>{extra}</div>'
            )
        else:
            filas.append(f'<div class="fila" style="--n:{nivel}"><span class="obj">{e(cuerpo)}</span></div>')
    if len(lineas) > limite:
        filas.append(f'<div class="fila" style="--n:0"><span class="obj">… y {len(lineas) - limite} objetos más</span></div>')
    return "\n".join(filas)


def terna(letra: str, valores: str) -> str:
    """Tres números de un Transform, cada uno con el color de su eje (X, Y, Z), como en el Inspector."""
    partes = [v.strip() for v in valores.split(",")]
    if len(partes) != 3:
        return f"<b>{letra}</b> {e(valores)} "
    x, y, z = partes
    return f'<span class="terna"><b>{letra}</b><i class="x">{e(x)}</i><i class="y">{e(y)}</i><i class="z">{e(z)}</i></span>'


def bloque_codigo(ruta: str) -> str:
    archivo = UNITY / ruta
    if not archivo.is_file():
        return ""
    codigo = archivo.read_text(encoding="utf-8").rstrip()
    return f'<figure class="codigo"><figcaption>{e(ruta)}</figcaption><pre><code>{e(codigo)}</code></pre></figure>'


def tipo_de_video(ruta: str) -> str:
    return "video/webm" if ruta.endswith(".webm") else "video/mp4"


def bloque_evidencia(p: dict, ev: dict) -> str:
    partes = []
    if ev["video"]:
        poster = f' poster="{e(ev["poster"])}"' if ev["poster"] else ""
        partes.append(
            f'<figure class="video"><video controls preload="metadata" playsinline{poster}>'
            f'<source src="{e(ev["video"])}" type="{tipo_de_video(ev["video"])}"></video>'
            f"<figcaption>{e(p.get('pie_video') or 'Video de 20 segundos grabado en el editor de Unity con la cámara de simulación de la escena.')}</figcaption></figure>"
        )
    else:
        partes.append(
            '<div class="vacio"><strong>Video pendiente.</strong> Se graba desde el menú Tecnologías Emergentes del proyecto '
            f"con la escena <code>{e(Path(p['escena']).stem)}</code> abierta en Unity.</div>"
        )
    if ev["quest"]:
        partes.append(
            f'<figure class="video"><video controls preload="metadata" playsinline><source src="{e(ev["quest"])}" type="{tipo_de_video(ev["quest"])}"></video>'
            "<figcaption>Grabado desde el Meta Quest 3, con el cuarto real de fondo.</figcaption></figure>"
        )
    fotos = []
    if ev["escena"]:
        fotos.append((ev["escena"], "La escena en el editor de Unity."))
    for ruta in ev["inspector"]:
        nombre = Path(ruta).stem.replace("inspector_", "").replace("_", " ")
        fotos.append((ruta, f"Inspector de {nombre}."))
    if ev["consola"]:
        fotos.append((ev["consola"], "Consola de Unity con los mensajes de DetectorColision."))
    if fotos:
        partes.append('<div class="fotos">' + "".join(
            f'<figure><a href="{e(r)}"><img src="{e(r)}" alt="{e(t)}" loading="lazy"></a><figcaption>{e(t)}</figcaption></figure>' for r, t in fotos
        ) + "</div>")
    return "\n".join(partes)


def seccion_practica(p: dict) -> str:
    ev = evidencias(p["id"])
    pasos = "\n".join(
        f'<li><p class="pide">{e(pide)}</p><p class="hecho">{e(hecho)}</p></li>' for pide, hecho in p["pasos"]
    )
    observado = "\n".join(f"<li>{e(t)}</li>" for t in p["observado"])
    preguntas = ""
    if p.get("preguntas"):
        preguntas = '<h4>Preguntas de comprobación</h4><dl class="preguntas">' + "".join(
            f"<dt>{e(q)}</dt><dd>{e(r)}</dd>" for q, r in p["preguntas"]
        ) + "</dl>"
    codigo = "".join(bloque_codigo(r) for r in p.get("codigo", []))
    if codigo:
        codigo = "<h4>Scripts de la presentación</h4>" + codigo
    jerarquia = ""
    if ev["jerarquia"]:
        jerarquia = (
            '<details class="jerarquia"><summary>Jerarquía de la escena en Unity</summary>'
            '<p class="nota">Cada renglón es un GameObject con su Transform local: P es posición, R rotación y S escala. '
            'Cada número lleva el color de su eje: <i class="x">X</i>, <i class="y">Y</i>, <i class="z">Z</i>.</p>'
            f'<div class="arbol">{arbol_de_escena(ev["jerarquia"])}</div></details>'
        )
    return f"""
<article class="practica" id="{e(p['id'])}">
  <header>
    <h3>{e(p['titulo'])}</h3>
    <p class="origen">Presentación: {e(p['presentacion'])}. Escena: <code>{e(p['escena'])}</code></p>
    <p class="objetivo">{e(p['objetivo'])}</p>
  </header>
  {bloque_evidencia(p, ev)}
  <h4>Paso a paso</h4>
  <ol class="pasos">
{pasos}
  </ol>
  <h4>Lo observado y comprendido</h4>
  <ul class="observado">
{observado}
  </ul>
  {preguntas}
  {codigo}
  {jerarquia}
  <aside class="al-proyecto">
    <h4>Cómo entra en el proyecto final</h4>
    <p>{e(p['proyecto'])} <a href="#proyecto">Ver el proyecto</a></p>
  </aside>
  <p class="controles">{e(p['controles'])}</p>
</article>"""


DIAGRAMA = """
<svg class="diagrama" viewBox="0 0 880 330" role="img" aria-labelledby="dt dd">
  <title id="dt">Arquitectura de Jarvis en realidad mixta</title>
  <desc id="dd">El Meta Quest 3 con la escena de Unity se comunica por Wi-Fi con la laptop: una pregunta UDP para encontrarla y un WebSocket para las órdenes. La laptop consulta a Claude por internet.</desc>
  <defs>
    <marker id="f" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0L10 5L0 10z" fill="currentColor"/></marker>
  </defs>
  <g class="caja"><rect x="10" y="30" width="270" height="270" rx="6"/>
    <text x="28" y="62" class="t1">Meta Quest 3</text>
    <text x="28" y="84" class="t2">Unity, escena Jarvis_RM</text>
    <rect class="sub" x="28" y="104" width="234" height="52" rx="4"/><text x="40" y="126" class="t3">JarvisRM</text><text x="40" y="145" class="t4">ejecuta las órdenes en la escena</text>
    <rect class="sub" x="28" y="166" width="234" height="52" rx="4"/><text x="40" y="188" class="t3">JarvisEnlace</text><text x="40" y="207" class="t4">red, acuses y medición de latencia</text>
    <rect class="sub" x="28" y="228" width="234" height="52" rx="4"/><text x="40" y="250" class="t3">OVRCameraRig + passthrough</text><text x="40" y="269" class="t4">Meta XR Core SDK</text>
  </g>
  <g class="caja"><rect x="520" y="30" width="350" height="270" rx="6"/>
    <text x="538" y="62" class="t1">Laptop (Ubuntu)</text>
    <text x="538" y="84" class="t2">Jarvis en Python: uv run jarvis-rm</text>
    <rect class="sub" x="538" y="104" width="314" height="52" rx="4"/><text x="550" y="126" class="t3">Servidor del enlace</text><text x="550" y="145" class="t4">WebSocket 8765 y descubrimiento UDP 8766</text>
    <rect class="sub" x="538" y="166" width="314" height="52" rx="4"/><text x="550" y="188" class="t3">Orquestador + herramientas unity_*</text><text x="550" y="207" class="t4">le pregunta a Claude qué hacer</text>
    <rect class="sub" x="538" y="228" width="314" height="52" rx="4"/><text x="550" y="250" class="t3">Oído y voz</text><text x="550" y="269" class="t4">«Hey Jarvis», faster-whisper y Piper</text>
  </g>
  <g class="flecha z"><line x1="292" y1="118" x2="508" y2="118" marker-start="url(#f)" marker-end="url(#f)"/><text x="400" y="108" text-anchor="middle">descubrimiento UDP, puerto 8766</text></g>
  <g class="flecha y"><line x1="292" y1="180" x2="508" y2="180" marker-start="url(#f)" marker-end="url(#f)"/><text x="400" y="170" text-anchor="middle">órdenes y acuses (WebSocket)</text></g>
  <g class="flecha x"><line x1="292" y1="242" x2="508" y2="242" marker-start="url(#f)" marker-end="url(#f)"/><text x="400" y="232" text-anchor="middle">ping y pong: latencia</text></g>
  <text x="400" y="290" text-anchor="middle" class="t4">Wi-Fi de la red local</text>
</svg>"""


def seccion_proyecto() -> str:
    ev = evidencias(PROYECTO["id"])
    partes = "".join(f"<div><dt>{e(n)}</dt><dd>{e(t)}</dd></div>" for n, t in PROYECTO["partes"])
    protocolo = "".join(f"<tr><th scope='row'>{e(a)}</th><td>{e(b)}</td><td>{e(c)}</td></tr>" for a, b, c in PROYECTO["protocolo"])
    latencia = "".join(f"<tr><th scope='row'>{e(a)}</th><td>{e(b)}</td><td>{e(c)}</td></tr>" for a, b, c in PROYECTO["latencia"])
    herramientas = "".join(f"<tr><th scope='row'><code>{e(a)}</code></th><td>{e(b)}</td></tr>" for a, b in PROYECTO["herramientas"])
    de_practicas = "".join(
        f"<tr><th scope='row'><a href='#{e(p['id'])}'>{e(p['titulo'])}</a></th><td>{e(p['proyecto'])}</td></tr>" for p in PRACTICAS
    )
    pendiente = "".join(f"<li>{e(t)}</li>" for t in PROYECTO["pendiente"])
    medida = ""
    archivo = RAIZ / "evidencias" / PROYECTO["id"] / "latencia.txt"
    if archivo.is_file():
        medida = f'<pre class="medicion">{e(archivo.read_text(encoding="utf-8").strip())}</pre>'
    else:
        medida = ('<div class="vacio"><strong>Mediciones pendientes.</strong> Se llenan al correr <code>uv run jarvis-rm --demo</code> '
                  "con el visor conectado a la red de la casa.</div>")
    jerarquia = ""
    if ev["jerarquia"]:
        jerarquia = ('<details class="jerarquia"><summary>Jerarquía de la escena Jarvis_RM en Unity</summary>'
                     f'<div class="arbol">{arbol_de_escena(ev["jerarquia"])}</div></details>')
    video = bloque_evidencia({"escena": PROYECTO["escena"], "pie_video": PROYECTO.get("pie_video")}, ev)
    return f"""
<section id="proyecto">
  <h2>Proyecto final</h2>
  <article class="practica proyecto" id="{e(PROYECTO['id'])}">
    <header>
      <h3>{e(PROYECTO['titulo'])}</h3>
      <p class="objetivo">{e(PROYECTO['resumen'])}</p>
    </header>
    {video}
    <h4>Cómo está armado</h4>
    {DIAGRAMA}
    <dl class="partes">{partes}</dl>

    <h4>Protocolos del enlace</h4>
    <div class="tabla"><table>
      <thead><tr><th scope="col">Para qué</th><th scope="col">Protocolo</th><th scope="col">Por qué ese</th></tr></thead>
      <tbody>{protocolo}</tbody>
    </table></div>

    <h4>Latencia del ciclo completo</h4>
    <p>Como en cualquier sistema ciberfísico, la latencia total es la suma de cuatro retrasos. Jarvis mide cada uno y los muestra en el tablero de estado del visor.</p>
    <div class="tabla"><table>
      <thead><tr><th scope="col">Componente</th><th scope="col">En Jarvis</th><th scope="col">Cómo se mide</th></tr></thead>
      <tbody>{latencia}</tbody>
    </table></div>
    {medida}

    <h4>Lo que Claude puede hacer en la escena</h4>
    <div class="tabla"><table>
      <thead><tr><th scope="col">Herramienta</th><th scope="col">Qué hace</th></tr></thead>
      <tbody>{herramientas}</tbody>
    </table></div>

    <h4>De las prácticas al proyecto</h4>
    <div class="tabla"><table>
      <thead><tr><th scope="col">Práctica</th><th scope="col">Qué se reutiliza</th></tr></thead>
      <tbody>{de_practicas}
        <tr><th scope="row">Latencia y protocolos en CPS</th><td>UDP para el descubrimiento, TCP para las órdenes, medición de RTT y jitter, y la latencia separada en sensado, red, cómputo y actuación.</td></tr>
      </tbody>
    </table></div>

    <h4>Cómo se construyó</h4>
    <p>Las escenas las arma un script de editor (<code>Assets/Editor/ConstructorPracticas.cs</code>) que sigue el paso a paso de las presentaciones. Claude lo opera desde fuera de Unity con el Puente Jarvis (<code>Assets/PuenteJarvis</code>): deja órdenes como archivos JSON en una carpeta y el editor las ejecuta, reporta errores de compilación, saca capturas y graba los videos de esta página.</p>
    {jerarquia}

    <h4>Lo que sigue</h4>
    <ul class="observado">{pendiente}</ul>
  </article>
</section>"""


def navegacion() -> str:
    hijos = "".join(f'<li><a href="#{e(p["id"])}">{e(p["titulo"])}</a></li>' for p in PRACTICAS)
    return f"""
<nav class="jerarquia-nav" aria-label="Contenido">
  <details open>
    <summary>Tecnologías Emergentes</summary>
    <ul>
      <li><details open><summary><a href="#proyecto">Proyecto final</a></summary>
        <ul><li><a href="#{e(PROYECTO['id'])}">{e(PROYECTO['titulo'])}</a></li></ul></details></li>
      <li><details open><summary><a href="#practicas">Prácticas</a></summary>
        <ul>{hijos}</ul></details></li>
      <li><a href="#abrir">Abrir el proyecto</a></li>
    </ul>
  </details>
</nav>"""


def pagina() -> str:
    css = (Path(__file__).resolve().parent / "estilo.css").read_text(encoding="utf-8")
    practicas = "\n".join(seccion_practica(p) for p in PRACTICAS)
    return f"""<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Tecnologías Emergentes: realidad mixta con Unity</title>
<meta name="description" content="Prácticas y proyecto final de Tecnologías Emergentes: realidad mixta con Unity y Meta Quest 3.">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@500;600;700&family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:ital,wght@0,400;0,500;0,600;1,400&display=swap" rel="stylesheet">
<style>
{css}
</style>
</head>
<body>
<div class="marco">
  <aside class="lateral">
    <p class="pestana">Hierarchy</p>
    {navegacion()}
  </aside>
  <main>
    <header class="portada">
      <svg class="gizmo" viewBox="0 0 120 120" aria-hidden="true">
        <line x1="60" y1="64" x2="104" y2="80" class="gx"/><circle cx="106" cy="81" r="9" class="gx"/><text x="106" y="85" text-anchor="middle">X</text>
        <line x1="60" y1="64" x2="60" y2="20" class="gy"/><circle cx="60" cy="17" r="9" class="gy"/><text x="60" y="21" text-anchor="middle">Y</text>
        <line x1="60" y1="64" x2="22" y2="88" class="gz"/><circle cx="20" cy="89" r="9" class="gz"/><text x="20" y="93" text-anchor="middle">Z</text>
        <rect x="52" y="56" width="16" height="16" class="gc"/>
      </svg>
      <h1>Tecnologías Emergentes</h1>
      <p class="bajada">Realidad mixta con Unity 2022.3 y Meta Quest 3. Carlos Ariel Ramos, otoño de 2026.</p>
      <p>Esta página reúne las prácticas de la clase y el proyecto final. Todo vive en un solo proyecto de Unity, <code>Practica_1_RM</code>, que se instala en el visor: cada práctica es una escena y se cambia de una a otra con los botones X y Y del control.</p>
    </header>

    {seccion_proyecto()}

    <section id="practicas">
      <h2>Prácticas</h2>
      <p class="entrada">Seis prácticas, cada una en su escena. Los objetos se ven sobre el cuarto real y a escala real: una unidad de Unity es un metro.</p>
      {practicas}
    </section>

    <section id="abrir">
      <h2>Abrir el proyecto</h2>
      <ol class="pasos simples">
        <li><p class="hecho">Instala Unity 2022.3.58f1 desde Unity Hub con los módulos Android Build Support, OpenJDK y Android SDK &amp; NDK Tools.</p></li>
        <li><p class="hecho">En Unity Hub elige Add, luego Add project from disk, y abre la carpeta <code>Practica_1_RM</code> de este repositorio.</p></li>
        <li><p class="hecho">La primera vez descarga el Meta XR Core SDK, cambia la plataforma a Android y construye las escenas. Si no lo hace, usa el menú Tecnologías Emergentes, opción 1.</p></li>
        <li><p class="hecho">Conecta el Quest 3 por USB con el modo desarrollador activo y usa el menú Tecnologías Emergentes, opción 3, para compilar e instalar.</p></li>
      </ol>
    </section>

    <footer>
      <p>Universidad Iberoamericana Ciudad de México. Los scripts <code>Bala</code>, <code>ControlCañon</code> y <code>DetectorColision</code> son los de las presentaciones de la clase.</p>
    </footer>
  </main>
</div>
<script>
// Resalta en la jerarquía la sección que se está leyendo
(function () {{
  // En pantallas angostas la jerarquía empieza cerrada para que el contenido quede a la vista
  if (window.matchMedia('(max-width: 56rem)').matches) {{
    var raiz = document.querySelector('.jerarquia-nav > details');
    if (raiz) raiz.removeAttribute('open');
  }}
  var enlaces = Array.prototype.slice.call(document.querySelectorAll('.jerarquia-nav a'));
  var porId = {{}};
  enlaces.forEach(function (a) {{ porId[a.getAttribute('href').slice(1)] = a; }});
  if (!('IntersectionObserver' in window)) return;
  var visto = new IntersectionObserver(function (entradas) {{
    entradas.forEach(function (en) {{
      if (!en.isIntersecting) return;
      enlaces.forEach(function (a) {{ a.removeAttribute('aria-current'); }});
      var a = porId[en.target.id];
      if (a) a.setAttribute('aria-current', 'true');
    }});
  }}, {{ rootMargin: '-15% 0px -70% 0px' }});
  Object.keys(porId).forEach(function (id) {{ var el = document.getElementById(id); if (el) visto.observe(el); }});
}})();
</script>
</body>
</html>
"""


if __name__ == "__main__":
    destino = RAIZ / "index.html"
    destino.write_text(pagina(), encoding="utf-8")
    con_video = sum(1 for p in PRACTICAS if evidencias(p["id"])["video"])
    print(f"index.html generado ({destino.stat().st_size // 1024} KB). Prácticas con video: {con_video}/{len(PRACTICAS)}")
