"""Catálogo de modelos y servidor HTTP que entrega los GLB al visor."""

import urllib.error
import urllib.request

import pytest

from jarvis.modelos import catalogo
from jarvis.unity.archivos import ServidorArchivos

pytestmark = pytest.mark.skipif(not catalogo.modelos(), reason="sin modelos convertidos (uv run jarvis-modelos)")


def _get(url: str) -> tuple[int, bytes]:
    try:
        with urllib.request.urlopen(url, timeout=5) as r:
            return r.status, r.read(4)
    except urllib.error.HTTPError as e:
        return e.code, b""


def test_catalogo_trae_piezas_y_articulaciones():
    clave = catalogo.modelos()[0]["clave"]
    info = catalogo.info(clave)
    assert info["piezas"] and all("nombre" in p and "direccion_despiece" in p for p in info["piezas"])
    assert catalogo.info("no_existe") is None


def test_servidor_solo_entrega_modelos_del_catalogo():
    s = ServidorArchivos("127.0.0.1", 0)
    s.iniciar()
    try:
        base = f"http://127.0.0.1:{s.puerto}"
        clave = catalogo.modelos()[0]["clave"]
        assert _get(f"{base}/modelos/{clave}.glb") == (200, b"glTF")
        assert _get(f"{base}/modelos/{clave}.json") == (200, b'{\n  ')
        assert _get(f"{base}/modelos/no_existe.glb")[0] == 404
        assert _get(f"{base}/modelos/../../.env")[0] == 404
        assert _get(f"{base}/otra/cosa")[0] == 404
    finally:
        s.detener()


def test_servidor_exige_token_si_hay():
    s = ServidorArchivos("127.0.0.1", 0, token="secreto")
    s.iniciar()
    try:
        clave = catalogo.modelos()[0]["clave"]
        base = f"http://127.0.0.1:{s.puerto}/modelos/{clave}.glb"
        assert _get(base)[0] == 404
        assert _get(base + "?token=secreto")[0] == 200
    finally:
        s.detener()
