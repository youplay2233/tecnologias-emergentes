"""Servidor del enlace Jarvis <-> Unity.

El visor (Unity en el Meta Quest 3) se conecta por WebSocket a esta laptop y queda a la espera
de órdenes. Los mensajes son objetos JSON planos con un campo `tipo`:

  Laptop -> visor   estado, oido, decir, panel, crear, lanzar, gravedad, limpiar, metricas, pong,
                    catalogo, tableta, modelo (accion: cargar, despiezar, girar, aislar, ocultar,
                    mostrar, resaltar, mostrar_todo, escala, cerrar), captura y salir (solo sin visor)
  Visor  -> laptop  hola, ping, ack, boton, latencia, evento (nombre "pedido": lo pulsado en la tableta),
                    voz_fin, y audio binario del micrófono (ver audio_visor.py)
  Laptop -> visor   también pitido (va a escuchar) y la voz de Jarvis como binario

Además contesta por UDP las preguntas de descubrimiento ("JARVIS-RM?") para que el visor encuentre
la laptop sin escribir la IP.

El servidor corre en su propio hilo con un bucle asyncio; el resto de Jarvis (que es síncrono)
lo usa con `enviar()` y `orden()`.
"""

import asyncio
import json
import socket
import threading
import time
from collections.abc import Callable
from concurrent.futures import TimeoutError as FutureTimeout
from dataclasses import dataclass, field

from websockets.asyncio.server import ServerConnection, serve
from websockets.exceptions import ConnectionClosed

VERSION_PROTOCOLO = 1
PREFIJO_BALIZA = "JARVIS-RM"


@dataclass
class Visor:
    """Un visor conectado."""

    conexion: ServerConnection
    dispositivo: str = "?"
    escena: str = "?"
    autenticado: bool = False
    rtt_ms: float | None = None      # ida y vuelta medida por el visor
    jitter_ms: float | None = None
    objetos: int = 0
    desde: float = field(default_factory=time.time)


class ServidorUnity:
    def __init__(
        self,
        host: str = "0.0.0.0",
        puerto: int = 8765,
        puerto_baliza: int = 8766,
        token: str = "",
        al_evento: Callable[[dict], None] | None = None,
        al_binario: Callable[[bytes], None] | None = None,
    ):
        self.host, self.puerto, self.puerto_baliza, self.token = host, puerto, puerto_baliza, token
        self._al_evento = al_evento
        self.al_binario = al_binario   # audio del micrófono del visor (ver audio_visor.py)
        self._visores: list[Visor] = []
        self._pendientes: dict[int, asyncio.Future] = {}
        self._siguiente_id = 0
        self._loop: asyncio.AbstractEventLoop | None = None
        self._hilo: threading.Thread | None = None
        self._listo = threading.Event()
        self._parar: asyncio.Event | None = None
        self._error: BaseException | None = None

    # ------------------------------------------------------------------ ciclo de vida

    def iniciar(self, espera: float = 5.0) -> None:
        self._hilo = threading.Thread(target=self._correr, name="jarvis-unity", daemon=True)
        self._hilo.start()
        if not self._listo.wait(espera):
            raise RuntimeError("El servidor de Unity no arrancó a tiempo.")
        if self._error is not None:
            raise RuntimeError(f"No pude abrir el puerto {self.puerto}: {self._error}")

    def detener(self) -> None:
        if self._loop is not None and self._parar is not None:
            self._loop.call_soon_threadsafe(self._parar.set)
        if self._hilo is not None:
            self._hilo.join(timeout=3)

    def _correr(self) -> None:
        try:
            asyncio.run(self._principal())
        except BaseException as e:  # noqa: BLE001 - se reporta al hilo que llamó a iniciar()
            self._error = e
            self._listo.set()

    async def _principal(self) -> None:
        self._loop = asyncio.get_running_loop()
        self._parar = asyncio.Event()
        async with serve(self._atender, self.host, self.puerto, ping_interval=20, max_size=2**20):
            descubrimiento = await self._abrir_descubrimiento()
            self._listo.set()
            await self._parar.wait()
            if descubrimiento is not None:
                descubrimiento.close()

    # ------------------------------------------------------------------ descubrimiento UDP

    async def _abrir_descubrimiento(self) -> asyncio.DatagramTransport | None:
        """Contesta las preguntas "JARVIS-RM?" que el visor manda por broadcast.

        El visor pregunta y la laptop responde directo a quien preguntó: así funciona también en
        Android, que a veces no entrega a las apps los paquetes de broadcast que llegan.
        """
        respuesta = f"{PREFIJO_BALIZA} {json.dumps({'puerto': self.puerto, 'nombre': socket.gethostname()})}".encode()
        pregunta = f"{PREFIJO_BALIZA}?".encode()

        class Descubrimiento(asyncio.DatagramProtocol):
            def connection_made(self, transporte):
                self.transporte = transporte

            def datagram_received(self, datos, origen):
                if datos.strip() == pregunta:
                    self.transporte.sendto(respuesta, origen)

        try:
            transporte, _ = await asyncio.get_running_loop().create_datagram_endpoint(
                Descubrimiento, local_addr=("0.0.0.0", self.puerto_baliza), allow_broadcast=True
            )
        except OSError as e:
            print(f"[unity] sin descubrimiento automático (puerto UDP {self.puerto_baliza} ocupado: {e}). "
                  f"Escribe la dirección de la laptop en Unity.")
            return None
        return transporte

    # ------------------------------------------------------------------ conexiones

    async def _atender(self, conexion: ServerConnection) -> None:
        visor = Visor(conexion, autenticado=not self.token)
        self._visores.append(visor)
        try:
            await conexion.send(json.dumps({"tipo": "bienvenida", "version": VERSION_PROTOCOLO}))
            async for crudo in conexion:
                if isinstance(crudo, bytes):
                    if visor.autenticado and self.al_binario is not None:
                        try:
                            self.al_binario(crudo)
                        except Exception as e:  # noqa: BLE001 - un error de audio no debe tirar la conexión
                            print(f"[unity] error con el audio del visor: {e!r}")
                    continue
                try:
                    mensaje = json.loads(crudo)
                    tipo = mensaje.get("tipo", "")
                except (ValueError, AttributeError):
                    continue
                await self._recibir(visor, tipo, mensaje)
        except ConnectionClosed:
            pass
        finally:
            self._visores.remove(visor)
            self._emitir({"tipo": "desconectado", "dispositivo": visor.dispositivo})

    async def _recibir(self, visor: Visor, tipo: str, m: dict) -> None:
        if tipo == "ping":
            # Se devuelve tal cual la marca de tiempo del visor: él calcula la latencia de ida y vuelta
            await visor.conexion.send(json.dumps({"tipo": "pong", "t": m.get("t", 0), "n": m.get("n", 0)}))
            return

        if tipo == "hola":
            if self.token and m.get("token") != self.token:
                await visor.conexion.close(code=4401, reason="token incorrecto")
                return
            visor.autenticado = True
            visor.dispositivo = str(m.get("dispositivo", "?"))
            visor.escena = str(m.get("escena", "?"))
            self._emitir({"tipo": "conectado", "dispositivo": visor.dispositivo, "escena": visor.escena})
            return

        if not visor.autenticado:
            return

        if tipo == "ack":
            visor.objetos = int(m.get("objetos", visor.objetos))
            pendiente = self._pendientes.pop(int(m.get("id", -1)), None)
            if pendiente is not None and not pendiente.done():
                pendiente.set_result(m)
        elif tipo == "latencia":
            visor.rtt_ms = float(m.get("rtt_ms", 0.0))
            visor.jitter_ms = float(m.get("jitter_ms", 0.0))
        elif tipo in ("boton", "evento", "voz_fin"):
            self._emitir(m)

    def _emitir(self, evento: dict) -> None:
        if self._al_evento is None:
            return
        try:
            self._al_evento(evento)
        except Exception as e:  # noqa: BLE001 - un error en el oyente no debe tirar la conexión
            print(f"[unity] error en el manejador de eventos: {e!r}")

    # ------------------------------------------------------------------ uso desde Jarvis

    @property
    def visores(self) -> list[Visor]:
        return [v for v in self._visores if v.autenticado]

    @property
    def conectado(self) -> bool:
        return bool(self.visores)

    def enviar(self, mensaje: dict) -> bool:
        """Manda un mensaje a todos los visores sin esperar respuesta. Devuelve False si no hay ninguno."""
        if self._loop is None or not self.conectado:
            return False
        asyncio.run_coroutine_threadsafe(self._difundir(json.dumps(mensaje, ensure_ascii=False)), self._loop)
        return True

    def enviar_binario(self, datos: bytes) -> bool:
        """Manda datos binarios (la voz de Jarvis) a todos los visores sin esperar respuesta."""
        if self._loop is None or not self.conectado:
            return False
        asyncio.run_coroutine_threadsafe(self._difundir(datos), self._loop)
        return True

    def orden(self, mensaje: dict, espera: float = 3.0) -> str:
        """Manda una orden y espera el acuse (`ack`) del visor. Devuelve un texto para el modelo."""
        if self._loop is None or not self.conectado:
            return "El visor no está conectado: abre la app Jarvis RM en el Quest 3."
        try:
            ack = asyncio.run_coroutine_threadsafe(self._ordenar(mensaje, espera), self._loop).result(espera + 1)
        except (FutureTimeout, asyncio.TimeoutError):
            return "El visor no respondió a tiempo."
        if not ack.get("ok", False):
            return f"El visor no pudo hacerlo: {ack.get('detalle', 'sin detalle')}"
        return str(ack.get("detalle", "Listo."))

    async def _ordenar(self, mensaje: dict, espera: float) -> dict:
        self._siguiente_id += 1
        ident = self._siguiente_id
        futuro: asyncio.Future = asyncio.get_running_loop().create_future()
        self._pendientes[ident] = futuro
        await self._difundir(json.dumps({**mensaje, "id": ident}, ensure_ascii=False))
        try:
            return await asyncio.wait_for(futuro, espera)
        finally:
            self._pendientes.pop(ident, None)

    async def _difundir(self, texto: str | bytes) -> None:
        for visor in self.visores:
            try:
                await visor.conexion.send(texto)
            except ConnectionClosed:
                pass

    def resumen(self) -> str:
        """Estado del enlace en una frase, para que Jarvis lo diga en voz alta."""
        if not self.conectado:
            return "El visor no está conectado."
        v = self.visores[0]
        if v.rtt_ms is None:
            return f"Visor conectado ({v.dispositivo}). Aún no hay medición de latencia."
        return (
            f"Visor conectado ({v.dispositivo}). Latencia de red de ida y vuelta {v.rtt_ms:.0f} milisegundos, "
            f"variación {v.jitter_ms:.0f} milisegundos. Hay {v.objetos} objetos creados."
        )
