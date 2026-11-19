"""Herramientas con las que Claude actúa sobre la escena de realidad mixta.

Cada herramienta manda una orden al visor y devuelve el acuse como texto, para que el modelo
sepa qué pasó realmente (por ejemplo, que el visor no está conectado).
"""

import math

from anthropic import beta_tool

from jarvis.unity.servidor import ServidorUnity

FORMAS = ("cubo", "esfera", "capsula", "cilindro")
COLORES = ("rojo", "naranja", "amarillo", "verde", "azul", "morado", "blanco", "gris")

INSTRUCCIONES = """
Además tienes presencia en realidad mixta: el usuario te ve y ve lo que haces con unas gafas Meta Quest 3.
Con las herramientas unity_* puedes crear objetos físicos frente a él, lanzarlos, mostrar un panel de texto,
apagar o encender la gravedad y limpiar la escena.
- Úsalas cuando el usuario lo pida y después confirma en una sola frase lo que hiciste.
- Si una herramienta responde que el visor no está conectado, dilo tal cual y no lo intentes de nuevo.
- Las formas disponibles son cubo, esfera, capsula y cilindro; los colores: rojo, naranja, amarillo, verde, azul, morado, blanco y gris."""


def _forma(valor: str) -> str:
    v = valor.strip().lower().replace("á", "a").replace("é", "e")
    return v if v in FORMAS else "cubo"


def _color(valor: str) -> str:
    v = valor.strip().lower()
    return v if v in COLORES else "azul"


def crear_herramientas(servidor: ServidorUnity) -> list:
    @beta_tool
    def unity_crear_objetos(forma: str, cantidad: int = 1, color: str = "azul", tamano_cm: float = 20) -> str:
        """Crea objetos con física (caen y chocan) frente al usuario en la escena de realidad mixta.

        Args:
            forma: "cubo", "esfera", "capsula" o "cilindro".
            cantidad: Cuántos objetos crear, de 1 a 20.
            color: "rojo", "naranja", "amarillo", "verde", "azul", "morado", "blanco" o "gris".
            tamano_cm: Tamaño de cada objeto en centímetros, de 5 a 60.
        """
        return servidor.orden({
            "tipo": "crear",
            "forma": _forma(forma),
            "cantidad": max(1, min(20, int(cantidad))),
            "color": _color(color),
            "tamano": max(5.0, min(60.0, float(tamano_cm))) / 100.0,
        })

    @beta_tool
    def unity_lanzar_objeto(forma: str = "esfera", color: str = "rojo", fuerza: float = 6) -> str:
        """Lanza un objeto hacia el frente del usuario, como la bala del cañón de la práctica.

        Args:
            forma: "cubo", "esfera", "capsula" o "cilindro".
            color: Color del objeto.
            fuerza: Velocidad de salida en metros por segundo, de 1 a 20.
        """
        return servidor.orden({
            "tipo": "lanzar",
            "forma": _forma(forma),
            "color": _color(color),
            "tamano": 0.15,
            "fuerza": max(1.0, min(20.0, float(fuerza))),
        })

    @beta_tool
    def unity_mostrar_panel(titulo: str, texto: str) -> str:
        """Muestra un panel holográfico con un título y un texto corto frente al usuario.

        Args:
            titulo: Título del panel, máximo unas cuatro palabras.
            texto: Texto del panel, máximo unas 40 palabras; usa saltos de línea cortos.
        """
        return servidor.orden({"tipo": "panel", "titulo": titulo[:60], "texto": texto[:400]})

    @beta_tool
    def unity_gravedad(activada: bool) -> str:
        """Enciende o apaga la gravedad de los objetos virtuales.

        Args:
            activada: true para gravedad normal, false para que los objetos floten.
        """
        return servidor.orden({"tipo": "gravedad", "activa": bool(activada)})

    @beta_tool
    def unity_limpiar() -> str:
        """Borra todos los objetos que has creado en la escena de realidad mixta."""
        return servidor.orden({"tipo": "limpiar"})

    @beta_tool
    def unity_estado() -> str:
        """Dice si el visor está conectado, la latencia de red medida y cuántos objetos hay en la escena."""
        return servidor.resumen()

    return [unity_crear_objetos, unity_lanzar_objeto, unity_mostrar_panel, unity_gravedad, unity_limpiar, unity_estado]


# ---------------------------------------------------------------------- robots (jarvis-rm)

INSTRUCCIONES_ROBOTS = """
En el visor también puedes abrir modelos 3D de robots, despiezarlos y manipularlos (como Tony Stark):
- unity_listar_modelos dice qué robots hay; unity_abrir_modelo abre uno y te devuelve los nombres de sus piezas.
- Para pedir piezas usa EXACTAMENTE esos nombres (por ejemplo link3 o left_elbow_link). Si el usuario dice
  "el codo" o "la pinza", elige las piezas que correspondan por nombre y posición en la cadena.
- "Despiézalo" = unity_despiezar; "ármalo" = unity_despiezar con separación 0.
- "Enséñame la parte de atrás" = unity_girar_modelo con vista "atras".
- Al describir una pieza, explica para qué sirve en el robot en una o dos frases.
- La tableta (unity_tableta) muestra los modelos y documentos disponibles.
- El usuario puede agarrar con la mano (pinza) o con el control: el robot entero si está armado, y cada pieza
  si está despiezado; al tomar una pieza se agranda en su mano y queda donde la suelta. "Regresa las piezas a su
  lugar" = unity_despiezar con la misma separación (o 0 para armarlo); eso también restaura las tiradas a la basura.
- "Enséñame link0", "quiero ver el motor de cerca" = unity_inspeccionar_pieza: la pieza viene en grande y girando.
- Hay un bote de basura (minijuego): "tira la tapa a la basura" = unity_desechar_piezas. El usuario también puede
  lanzar piezas con la mano o con el botón B; si encesta suma puntos y, encesten o no, desaparecen hasta volver a armar.
- "Pon el bote a mi izquierda" = unity_mover_bote; el usuario también puede agarrarlo y dejarlo donde quiera.
- El HUD (tú, el panel y el robot) sigue al usuario si camina; "quédate aquí" / "sígueme" / "ponte enfrente" = unity_hud.
- Los robots se mueven: "dobla el codo", "levanta la pata" = unity_mover_articulacion (elige la articulación por
  nombre y posición en la cadena); "muéstrame cómo se mueve" = unity_animar "demostracion"; "camina" = "caminar".
- "Que la mano copie la mía" = abre la Shadow Hand y activa unity_mano_espejo: copia los dedos y el giro de la
  muñeca derecha. El visor solo ve la mano si no sostiene el control derecho (el izquierdo sí puede usarlo para
  mover la mano robótica). "Reconecta el espejo" = vuelve a llamar unity_mano_espejo(true): recalibra.
- "Dime el nombre de las piezas" = unity_piezas "etiquetar" con ["todas"] (no "resaltar", que cambia el color).
- Además de robots hay componentes (servo SG90, motor NEMA 17, rodamiento 608) con sus piezas internas."""

VISTAS = {  # (giro horizontal, inclinación) en grados
    "frente": (0, 0), "atras": (180, 0), "izquierda": (90, 0), "derecha": (-90, 0),
    "arriba": (0, 80), "abajo": (0, -80),
}
ACCIONES_PIEZAS = ("aislar", "ocultar", "mostrar", "resaltar", "etiquetar")


def _describir_articulacion(a: dict) -> str:
    lineal = a["tipo"] == "lineal"
    f = (lambda v: f"{v * 100:.0f} cm") if lineal else (lambda v: f"{math.degrees(v):.0f}°")
    rango = f" [{f(a['rango'][0])} a {f(a['rango'][1])}]" if a.get("rango") else ""
    return f"{a['nombre']} ({a['pieza']}, {f(a['reposo'])}{rango})"


def crear_herramientas_robots(servidor: ServidorUnity, url_modelo) -> list:
    """`url_modelo(clave)` da la URL HTTP desde la que el visor descarga el GLB."""
    from jarvis.modelos import catalogo

    actual: dict = {}

    def _sin_acentos(t: str) -> str:
        return t.strip().lower().translate(str.maketrans("áéíóú", "aeiou"))

    @beta_tool
    def unity_listar_modelos() -> str:
        """Lista los modelos 3D de robots que se pueden abrir en el visor."""
        modelos = catalogo.modelos()
        if not modelos:
            return "No hay modelos convertidos. En la laptop: uv run jarvis-modelos"
        return "\n".join(f"{m['clave']}: {m['nombre']} — {m['descripcion']} ({m['piezas']} piezas)" for m in modelos)

    @beta_tool
    def unity_abrir_modelo(clave: str) -> str:
        """Abre un modelo 3D frente al usuario (reemplaza al que hubiera) y devuelve los nombres de sus piezas.

        Args:
            clave: La clave del modelo, tal como la da unity_listar_modelos (p. ej. "panda").
        """
        datos = catalogo.info(clave.strip().lower())
        if datos is None:
            return f"No existe el modelo «{clave}». Disponibles: " + ", ".join(m["clave"] for m in catalogo.modelos())
        acuse = servidor.orden({
            "tipo": "modelo", "accion": "cargar", "clave": datos["clave"], "texto": datos["nombre"],
            "url": url_modelo(datos["clave"]),
        }, espera=30.0)
        if not acuse.startswith(("El visor no", "No pude", "Descargué", "Ya estoy")):
            actual.clear()
            actual.update(datos)
        piezas = ", ".join(p["nombre"] for p in datos["piezas"])
        juntas = ", ".join(_describir_articulacion(a) for a in datos["articulaciones"] if a.get("marcador"))
        texto = f"{acuse}\nPiezas de {datos['nombre']}: {piezas}"
        return texto + (f"\nArticulaciones (valor de reposo y rango): {juntas}" if juntas else "")

    def _orden_modelo(accion: str, **extra) -> str:
        if not actual:
            return "No hay ningún modelo abierto. Abre uno con unity_abrir_modelo."
        return servidor.orden({"tipo": "modelo", "accion": accion, **extra})

    @beta_tool
    def unity_despiezar(separacion: float = 1.0) -> str:
        """Separa las piezas del modelo abierto (vista explotada) o lo vuelve a armar.

        Args:
            separacion: 0 = armado; 1 = despiezado normal; 2 = muy separado.
        """
        return _orden_modelo("despiezar", numero=max(0.0, min(2.5, float(separacion))))

    @beta_tool
    def unity_girar_modelo(vista: str) -> str:
        """Gira el modelo abierto para mostrar un lado.

        Args:
            vista: "frente", "atras", "izquierda", "derecha", "arriba" o "abajo".
        """
        v = _sin_acentos(vista)
        if v not in VISTAS:
            return "Vista no válida. Usa: " + ", ".join(VISTAS)
        giro, inclinacion = VISTAS[v]
        return _orden_modelo("girar", numero=float(giro), numero2=float(inclinacion), texto=v)

    @beta_tool
    def unity_piezas(accion: str, piezas: list[str]) -> str:
        """Actúa sobre piezas concretas del modelo abierto.

        Args:
            accion: "aislar" (solo se ven esas), "ocultar", "mostrar", "resaltar" (brillan y se etiquetan) o
                "etiquetar" (solo muestran su nombre, sin cambiar de color).
            piezas: Nombres exactos de las piezas, como los devolvió unity_abrir_modelo, o ["todas"].
        """
        if not actual:
            return "No hay ningún modelo abierto."
        a = _sin_acentos(accion)
        if a not in ACCIONES_PIEZAS:
            return "Acción no válida. Usa: " + ", ".join(ACCIONES_PIEZAS)
        validas = {p["nombre"] for p in actual["piezas"]}
        if [x.lower() for x in piezas] == ["todas"]:
            piezas = sorted(validas)
        desconocidas = [p for p in piezas if p not in validas]
        if desconocidas or not piezas:
            return f"Piezas desconocidas: {', '.join(desconocidas) or '(ninguna)'}. Válidas: {', '.join(sorted(validas))}"
        return _orden_modelo(a, piezas=list(piezas))

    @beta_tool
    def unity_inspeccionar_pieza(pieza: str) -> str:
        """Trae una pieza del modelo abierto frente al usuario, en grande y girando, para analizarla de cerca.

        Args:
            pieza: Nombre exacto de la pieza, como lo devolvió unity_abrir_modelo.
        """
        if not actual:
            return "No hay ningún modelo abierto."
        validas = {p["nombre"] for p in actual["piezas"]}
        if pieza not in validas:
            return f"No existe «{pieza}». Válidas: {', '.join(sorted(validas))}"
        return _orden_modelo("inspeccionar", piezas=[pieza])

    @beta_tool
    def unity_desechar_piezas(piezas: list[str]) -> str:
        """Lanza piezas del modelo abierto al bote de basura holográfico (vuelven al armar o despiezar de nuevo).

        Args:
            piezas: Nombres exactos de las piezas a tirar.
        """
        if not actual:
            return "No hay ningún modelo abierto."
        validas = {p["nombre"] for p in actual["piezas"]}
        desconocidas = [p for p in piezas if p not in validas]
        if desconocidas or not piezas:
            return f"Piezas desconocidas: {', '.join(desconocidas) or '(ninguna)'}. Válidas: {', '.join(sorted(validas))}"
        return _orden_modelo("desechar", piezas=list(piezas))

    @beta_tool
    def unity_mover_bote(lugar: str) -> str:
        """Mueve el bote de basura holográfico respecto a donde mira el usuario.

        Args:
            lugar: "izquierda", "derecha", "frente" o "atras".
        """
        lugar = _sin_acentos(lugar)
        if lugar not in ("izquierda", "derecha", "frente", "atras"):
            return "Lugar no válido. Usa: izquierda, derecha, frente o atras."
        return servidor.orden({"tipo": "bote", "texto": lugar})

    @beta_tool
    def unity_mover_articulacion(articulacion: str, valor: float) -> str:
        """Mueve una articulación del modelo abierto a un valor absoluto (el robot se mueve con animación).

        Args:
            articulacion: Nombre exacto, como lo listó unity_abrir_modelo (p. ej. "joint4" o "fl_kn").
            valor: Grados para las de giro o centímetros para las lineales, dentro de su rango.
        """
        if not actual:
            return "No hay ningún modelo abierto."
        nombres = {a["nombre"] for a in actual["articulaciones"] if a.get("marcador")}
        if articulacion not in nombres:
            return f"No existe «{articulacion}». Articulaciones: {', '.join(sorted(nombres)) or '(ninguna)'}"
        return _orden_modelo("articulacion", texto=articulacion, numero=float(valor))

    @beta_tool
    def unity_animar(modo: str) -> str:
        """Anima las articulaciones del modelo abierto.

        Args:
            modo: "demostracion" (mueve todas sus articulaciones), "caminar" (trote en su lugar; para Spot,
                Go2 y el humanoide G1) o "reposo" (vuelve a su postura y detiene la animación).
        """
        m = _sin_acentos(modo)
        if m not in ("demostracion", "caminar", "reposo"):
            return "Modo no válido. Usa: demostracion, caminar o reposo."
        return _orden_modelo("animar", texto=m)

    @beta_tool
    def unity_mano_espejo(activa: bool) -> str:
        """Hace que la Shadow Hand imite los dedos y el giro de la muñeca derecha del usuario (seguimiento de manos).

        Args:
            activa: true para encenderla (abre antes la Shadow Hand, clave "mano"), false para apagarla.
        """
        return servidor.orden({"tipo": "espejo", "activa": bool(activa)})

    @beta_tool
    def unity_hud(modo: str) -> str:
        """Controla si el HUD (Jarvis, panel y robot) acompaña al usuario cuando camina.

        Args:
            modo: "seguir" (lo acompaña), "fijo" (se queda donde está) o "frente" (lo pone enfrente ya).
        """
        modo = _sin_acentos(modo)
        if modo not in ("seguir", "fijo", "frente"):
            return "Modo no válido. Usa: seguir, fijo o frente."
        return servidor.orden({"tipo": "hud", "texto": modo})

    @beta_tool
    def unity_mostrar_todo() -> str:
        """Vuelve a mostrar todas las piezas del modelo y quita resaltados (no cambia el despiece)."""
        return _orden_modelo("mostrar_todo")

    @beta_tool
    def unity_tamano_modelo(alto_cm: float) -> str:
        """Cambia el tamaño con el que se ve el modelo.

        Args:
            alto_cm: Alto con el que se muestra, de 10 a 250 cm (tamaño real del robot: dilo si te lo piden).
        """
        return _orden_modelo("escala", numero=max(10.0, min(250.0, float(alto_cm))) / 100.0)

    @beta_tool
    def unity_cerrar_modelo() -> str:
        """Quita el modelo abierto de la escena."""
        respuesta = _orden_modelo("cerrar")
        actual.clear()
        return respuesta

    @beta_tool
    def unity_tableta(visible: bool) -> str:
        """Muestra u oculta la tableta holográfica con los modelos y documentos disponibles.

        Args:
            visible: true para mostrarla, false para ocultarla.
        """
        return servidor.orden({"tipo": "tableta", "activa": bool(visible)})

    return [unity_listar_modelos, unity_abrir_modelo, unity_despiezar, unity_girar_modelo, unity_piezas,
            unity_inspeccionar_pieza, unity_desechar_piezas, unity_mover_bote, unity_hud, unity_mover_articulacion, unity_animar, unity_mano_espejo, unity_mostrar_todo, unity_tamano_modelo,
            unity_cerrar_modelo, unity_tableta]
