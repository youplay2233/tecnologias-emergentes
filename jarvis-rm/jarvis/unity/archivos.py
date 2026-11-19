"""Servidor HTTP mínimo para que el visor descargue los modelos 3D (GLB).

Solo sirve `/modelos/<clave>.glb` y `/modelos/<clave>.json` (piezas y articulaciones) de los modelos
del catálogo; cualquier otra ruta da 404.
Si hay token del enlace, también lo exige aquí (`?token=...`).
"""

import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

from jarvis.modelos import catalogo

RUTA = re.compile(r"^/modelos/([a-z0-9_]+)\.(glb|json)$")


class ServidorArchivos:
    def __init__(self, host: str, puerto: int, token: str = ""):
        token_esperado = token

        class Manejador(BaseHTTPRequestHandler):
            def do_GET(self):  # noqa: N802 - nombre impuesto por http.server
                url = urlparse(self.path)
                m = RUTA.match(url.path)
                ruta = catalogo.ruta_glb(m.group(1)) if m else None
                if ruta is not None and m.group(2) == "json":
                    ruta = ruta.with_suffix(".json")
                if token_esperado and parse_qs(url.query).get("token", [""])[0] != token_esperado:
                    ruta = None
                if ruta is None:
                    self.send_error(404)
                    return
                datos = ruta.read_bytes()
                self.send_response(200)
                self.send_header("Content-Type", "model/gltf-binary" if ruta.suffix == ".glb" else "application/json")
                self.send_header("Content-Length", str(len(datos)))
                self.end_headers()
                self.wfile.write(datos)

            def log_message(self, *args):  # sin ruido en la terminal
                pass

        self._http = ThreadingHTTPServer((host, puerto), Manejador)
        self.puerto = self._http.server_address[1]
        self._hilo = threading.Thread(target=self._http.serve_forever, name="jarvis-archivos", daemon=True)

    def iniciar(self) -> None:
        self._hilo.start()

    def detener(self) -> None:
        self._http.shutdown()
