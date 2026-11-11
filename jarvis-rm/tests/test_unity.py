"""Pruebas del enlace con Unity usando un visor simulado (no necesita el Quest ni la API de Claude)."""

import asyncio
import json
import socket
import threading
import time

import pytest
from websockets.asyncio.client import connect

from jarvis.unity.herramientas import crear_herramientas
from jarvis.unity.servidor import PREFIJO_BALIZA, ServidorUnity


class VisorSimulado:
    """Hace lo mismo que JarvisRM.cs en Unity: saluda, mide latencia y contesta con acuses."""

    def __init__(self, puerto: int, token: str = ""):
        self.puerto, self.token = puerto, token
        self.recibidos: list[dict] = []
        self.rtts: list[float] = []
        self.objetos = 0
        self.cerrado_con: int | None = None
        self._hilo = threading.Thread(target=lambda: asyncio.run(self._correr()), daemon=True)
        self._parar = False

    def iniciar(self):
        self._hilo.start()
        return self

    def detener(self):
        self._parar = True
        self._hilo.join(timeout=3)

    async def _correr(self):
        try:
            async with connect(f"ws://127.0.0.1:{self.puerto}") as ws:
                await ws.send(json.dumps({"tipo": "hola", "dispositivo": "Visor simulado", "escena": "Jarvis_RM", "token": self.token}))
                n = 0
                proximo_ping = 0.0
                while not self._parar:
                    if time.perf_counter() >= proximo_ping:
                        n += 1
                        await ws.send(json.dumps({"tipo": "ping", "t": time.perf_counter() * 1000, "n": n}))
                        proximo_ping = time.perf_counter() + 0.1
                    try:
                        crudo = await asyncio.wait_for(ws.recv(), 0.05)
                    except asyncio.TimeoutError:
                        continue
                    m = json.loads(crudo)
                    self.recibidos.append(m)
                    if m["tipo"] == "pong":
                        self.rtts.append(time.perf_counter() * 1000 - m["t"])
                        if len(self.rtts) == 5:
                            await ws.send(json.dumps({"tipo": "latencia", "rtt_ms": sum(self.rtts) / 5, "jitter_ms": 0.2}))
                    elif "id" in m:
                        detalle = "hecho"
                        if m["tipo"] == "crear":
                            self.objetos += m["cantidad"]
                            detalle = f"{m['cantidad']} {m['forma']}(s) de color {m['color']} creados"
                        elif m["tipo"] == "limpiar":
                            self.objetos = 0
                            detalle = "escena limpia"
                        await ws.send(json.dumps({"tipo": "ack", "id": m["id"], "ok": True, "detalle": detalle, "objetos": self.objetos}))
        except Exception as e:  # conexión rechazada o cerrada por el servidor
            self.cerrado_con = getattr(getattr(e, "rcvd", None), "code", -1)


def _puerto_libre() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def _esperar(condicion, segundos=3.0):
    limite = time.time() + segundos
    while time.time() < limite:
        if condicion():
            return True
        time.sleep(0.02)
    return False


@pytest.fixture
def servidor():
    eventos = []
    s = ServidorUnity("127.0.0.1", _puerto_libre(), _puerto_libre(), al_evento=eventos.append)
    s.eventos = eventos
    s.iniciar()
    yield s
    s.detener()


def test_sin_visor_la_orden_lo_dice(servidor):
    assert not servidor.conectado
    assert "no está conectado" in servidor.orden({"tipo": "limpiar"})
    assert servidor.enviar({"tipo": "estado", "valor": "reposo"}) is False


def test_saludo_orden_y_acuse(servidor):
    visor = VisorSimulado(servidor.puerto).iniciar()
    assert _esperar(lambda: servidor.conectado)
    assert servidor.eventos[0] == {"tipo": "conectado", "dispositivo": "Visor simulado", "escena": "Jarvis_RM"}

    respuesta = servidor.orden({"tipo": "crear", "forma": "cubo", "cantidad": 3, "color": "rojo", "tamano": 0.2})
    assert respuesta == "3 cubo(s) de color rojo creados"
    assert servidor.visores[0].objetos == 3

    assert servidor.enviar({"tipo": "decir", "texto": "Hola"}) is True
    assert _esperar(lambda: any(m["tipo"] == "decir" for m in visor.recibidos))
    visor.detener()
    assert _esperar(lambda: not servidor.conectado)


def test_latencia_ping_pong(servidor):
    visor = VisorSimulado(servidor.puerto).iniciar()
    assert _esperar(lambda: servidor.conectado and servidor.visores[0].rtt_ms is not None, 5)
    assert 0 < servidor.visores[0].rtt_ms < 200          # en la misma máquina son pocos milisegundos
    assert "Latencia de red" in servidor.resumen()
    visor.detener()


def test_herramientas_de_claude(servidor):
    visor = VisorSimulado(servidor.puerto).iniciar()
    assert _esperar(lambda: servidor.conectado)
    h = {t.name: t for t in crear_herramientas(servidor)}
    assert set(h) == {"unity_crear_objetos", "unity_lanzar_objeto", "unity_mostrar_panel", "unity_gravedad", "unity_limpiar", "unity_estado"}

    assert h["unity_crear_objetos"].call({"forma": "Esfera", "cantidad": 99, "color": "fucsia", "tamano_cm": 500}) == "20 esfera(s) de color azul creados"
    creado = next(m for m in visor.recibidos if m["tipo"] == "crear")
    assert creado["cantidad"] == 20 and creado["tamano"] == 0.6      # valores acotados antes de llegar al visor

    assert h["unity_limpiar"].call({}) == "escena limpia"
    assert h["unity_gravedad"].call({"activada": False}) == "hecho"
    assert next(m for m in visor.recibidos if m["tipo"] == "gravedad")["activa"] is False
    assert "Visor conectado" in h["unity_estado"].call({})
    for esquema in (t.to_dict() for t in h.values()):
        assert esquema["description"] and esquema["input_schema"]["type"] == "object"
    visor.detener()


def test_token_incorrecto_no_entra():
    s = ServidorUnity("127.0.0.1", _puerto_libre(), _puerto_libre(), token="secreto")
    s.iniciar()
    try:
        malo = VisorSimulado(s.puerto, token="otro").iniciar()
        time.sleep(0.5)
        assert not s.conectado
        malo.detener()
        bueno = VisorSimulado(s.puerto, token="secreto").iniciar()
        assert _esperar(lambda: s.conectado)
        bueno.detener()
    finally:
        s.detener()


def test_descubrimiento_udp(servidor):
    """El visor pregunta "JARVIS-RM?" y la laptop le contesta directo con el puerto del WebSocket."""
    visor = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    visor.settimeout(3)
    visor.sendto(f"{PREFIJO_BALIZA}?".encode(), ("127.0.0.1", servidor.puerto_baliza))
    datos, origen = visor.recvfrom(1024)
    visor.close()
    texto = datos.decode()
    assert texto.startswith(PREFIJO_BALIZA + " ")
    assert json.loads(texto.split(" ", 1)[1])["puerto"] == servidor.puerto
    assert origen[1] == servidor.puerto_baliza
