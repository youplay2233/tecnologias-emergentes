"""Modelos ya convertidos (modelos/glb/*.json) que el visor puede abrir."""

import json
from functools import lru_cache
from pathlib import Path

from jarvis.modelos.convertir import SALIDA


@lru_cache(maxsize=None)
def _leer(ruta: Path, _mtime: float) -> dict:
    return json.loads(ruta.read_text())


def info(clave: str) -> dict | None:
    ruta = SALIDA / f"{clave}.json"
    if not ruta.is_file() or not (SALIDA / f"{clave}.glb").is_file():
        return None
    return _leer(ruta, ruta.stat().st_mtime)


def modelos() -> list[dict]:
    """Resumen de cada modelo disponible, en orden alfabético por nombre."""
    resumen = []
    for ruta in sorted(SALIDA.glob("*.json")):
        datos = info(ruta.stem)
        if datos:
            resumen.append({
                "clave": datos["clave"],
                "nombre": datos["nombre"],
                "descripcion": datos["descripcion"],
                "piezas": len(datos["piezas"]),
            })
    return sorted(resumen, key=lambda m: m["nombre"])


def ruta_glb(clave: str) -> Path | None:
    return SALIDA / f"{clave}.glb" if info(clave) else None
