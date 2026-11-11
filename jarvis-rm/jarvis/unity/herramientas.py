"""Herramientas con las que Claude actúa sobre la escena de realidad mixta.

Cada herramienta manda una orden al visor y devuelve el acuse como texto, para que el modelo
sepa qué pasó realmente (por ejemplo, que el visor no está conectado).
"""

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
