"""Audio por el visor: su micrófono llega como frames y la voz de Jarvis le llega en binario."""

import json
import socket
import struct
import threading
import time

import numpy as np
from websockets.sync.client import connect

from jarvis.config import settings
from jarvis.unity.audio_visor import MICROFONO, VOZ, AudioVisor
from jarvis.unity.servidor import ServidorUnity


def _puerto_libre() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def test_microfono_y_voz_por_el_visor():
    frames, eventos = [], []
    audio_ref = {}

    def al_evento(e):
        eventos.append(e)
        if e.get("tipo") == "voz_fin":
            audio_ref["a"].evento(e)

    srv = ServidorUnity("127.0.0.1", _puerto_libre(), _puerto_libre(), al_evento=al_evento)
    audio = AudioVisor(srv, frames.append)
    audio_ref["a"] = audio
    srv.al_binario = audio.recibir
    srv.iniciar()
    voz_recibida = {}
    try:
        def visor():
            with connect(f"ws://127.0.0.1:{srv.puerto}") as ws:
                ws.send(json.dumps({"tipo": "hola", "dispositivo": "Visor simulado", "escena": "Jarvis"}))
                time.sleep(0.2)
                # 2.5 frames de micrófono en trozos irregulares: la laptop los debe re-cortar en frames exactos
                pcm = (np.arange(int(settings.frame_samples * 2.5)) % 100).astype("<i2")
                ws.send(bytes([MICROFONO]) + pcm[:1000].tobytes())
                ws.send(bytes([MICROFONO]) + pcm[1000:].tobytes())
                while True:
                    m = ws.recv(timeout=5)
                    if isinstance(m, bytes):
                        voz_recibida["datos"] = m
                        ws.send(json.dumps({"tipo": "voz_fin"}))
                        time.sleep(0.2)
                        return

        hilo = threading.Thread(target=visor, daemon=True)
        hilo.start()
        for _ in range(50):
            if len(frames) >= 2:
                break
            time.sleep(0.05)
        assert len(frames) == 2 and all(len(f) == settings.frame_samples for f in frames)
        assert audio.activo

        t = time.monotonic()
        voz = np.ones(22050, dtype=np.int16) * 7   # 1 s de "voz"
        audio.reproducir(voz, 22050)
        assert time.monotonic() - t < 3.0, "debió terminar con voz_fin, no por tiempo"
        hilo.join(timeout=3)
        datos = voz_recibida["datos"]
        assert datos[0] == VOZ and struct.unpack("<I", datos[1:5])[0] == 22050
        assert len(datos) == 5 + 2 * 22050
    finally:
        srv.detener()
