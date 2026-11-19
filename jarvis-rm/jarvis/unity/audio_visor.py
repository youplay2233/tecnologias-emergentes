"""Micrófono y bocina del visor a través del enlace (mensajes binarios del WebSocket).

  visor -> laptop   0x01 + PCM int16 mono 16 kHz (trozos de ~80 ms)
  laptop -> visor   0x02 + frecuencia (uint32 LE) + PCM int16 mono: la voz de Jarvis
  visor -> laptop   {"tipo": "voz_fin"} cuando terminó de sonar

Mientras el visor manda audio, su micrófono reemplaza al de la laptop: así Jarvis te oye aunque te
alejes de la computadora. La voz sale por la laptop salvo que JARVIS_RM_VOZ=visor.
"""

import struct
import threading
import time

import numpy as np

from jarvis.config import settings

MICROFONO = 1
VOZ = 2


class AudioVisor:
    def __init__(self, servidor, destino):
        """`destino` recibe los frames del micrófono del visor (p. ej. Microphone.inyectar)."""
        self._servidor = servidor
        self._destino = destino
        self._resto = np.zeros(0, dtype=np.int16)
        self._ultimo = 0.0
        self._fin = threading.Event()

    @property
    def activo(self) -> bool:
        """True si el visor está mandando su micrófono (lo usa en lugar del de la laptop)."""
        return settings.rm_audio != "laptop" and time.monotonic() - self._ultimo < 1.0

    def recibir(self, datos: bytes) -> None:
        if not datos or datos[0] != MICROFONO:
            return
        self._ultimo = time.monotonic()
        muestras = np.frombuffer(datos[1:], dtype="<i2").astype(np.int16)
        self._resto = np.concatenate([self._resto, muestras])
        n = settings.frame_samples
        while len(self._resto) >= n:
            self._destino(self._resto[:n].copy())
            self._resto = self._resto[n:]

    def evento(self, evento: dict) -> None:
        if evento.get("tipo") == "voz_fin":
            self._fin.set()

    def reproducir(self, audio: np.ndarray, frecuencia: int) -> None:
        """Manda la voz al visor y espera a que termine de sonar allá."""
        self._fin.clear()
        cabecera = bytes([VOZ]) + struct.pack("<I", int(frecuencia))
        self._servidor.enviar_binario(cabecera + audio.astype("<i2").tobytes())
        self._fin.wait(timeout=len(audio) / frecuencia + 3.0)
