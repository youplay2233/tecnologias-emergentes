"""Componentes comunes armados por código (casi ningún CAD trae los servos o motores por dentro).

Medidas aproximadas de las hojas de datos, en milímetros; Y hacia arriba. Cada pieza queda en su
lugar dentro del ensamble para que el visor pueda despiezarlo igual que un robot.
"""

import urllib.request

import numpy as np
import trimesh

from jarvis.config import ROOT
from jarvis.modelos.convertir import Ensamble, pintar

MM = 0.001

AZUL_SG90 = (40, 95, 215, 255)
NYLON = (236, 234, 226, 255)
ACERO = (196, 198, 204, 255)
ALUMINIO = (168, 172, 180, 255)
NEGRO = (32, 33, 36, 255)
COBRE = (186, 112, 52, 255)
LATON = (205, 165, 80, 255)
VERDE_PCB = (24, 120, 60, 255)


# ---------------------------------------------------------------------- primitivas (mm)

def _y(malla: trimesh.Trimesh) -> trimesh.Trimesh:
    """trimesh crea cilindros y anillos sobre Z; aquí van sobre Y."""
    malla.apply_transform(trimesh.transformations.rotation_matrix(-np.pi / 2, [1, 0, 0]))
    return malla


def caja(tam, centro, color, metalico=0.1, rugosidad=0.6):
    m = trimesh.creation.box(extents=tam)
    m.apply_translation(centro)
    return pintar(m, color, metalico, rugosidad)


def cilindro(radio, alto, centro, color, eje="y", metalico=0.2, rugosidad=0.5):
    m = trimesh.creation.cylinder(radius=radio, height=alto, sections=40)
    if eje == "y":
        _y(m)
    elif eje == "x":
        m.apply_transform(trimesh.transformations.rotation_matrix(np.pi / 2, [0, 1, 0]))
    m.apply_translation(centro)
    return pintar(m, color, metalico, rugosidad)


def anillo(r_in, r_out, alto, centro, color, metalico=0.8, rugosidad=0.3):
    m = _y(trimesh.creation.annulus(r_min=r_in, r_max=r_out, height=alto, sections=48))
    m.apply_translation(centro)
    return pintar(m, color, metalico, rugosidad)


def esfera(radio, centro, color, metalico=0.9, rugosidad=0.2):
    m = trimesh.creation.icosphere(subdivisions=3, radius=radio)
    m.apply_translation(centro)
    return pintar(m, color, metalico, rugosidad)


def engrane(radio, alto, dientes, centro, color):
    """Disco con dientes rectangulares alrededor del borde."""
    partes = [trimesh.creation.cylinder(radius=radio * 0.86, height=alto, sections=max(24, dientes * 2))]
    largo = radio * 0.22
    for i in range(dientes):
        a = 2 * np.pi * i / dientes
        d = trimesh.creation.box(extents=[largo, 2 * np.pi * radio / dientes * 0.5, alto])
        d.apply_translation([radio * 0.86 + largo / 2 - largo * 0.15, 0, 0])
        d.apply_transform(trimesh.transformations.rotation_matrix(a, [0, 0, 1]))
        partes.append(d)
    m = _y(trimesh.util.concatenate(partes))
    m.apply_translation(centro)
    return pintar(m, color, 0.05, 0.7)


def _ensamble(piezas_mm: dict[str, list[trimesh.Trimesh]], articulaciones: list[dict], fuente: str,
              despiece_mm: dict[str, tuple[float, float, float]] | None = None) -> Ensamble:
    piezas = {}
    for nombre, mallas in piezas_mm.items():
        for m in mallas:
            m.apply_scale(MM)
        piezas[nombre] = ("", mallas)
    despiece = {n: tuple(np.asarray(v, dtype=float) * MM) for n, v in (despiece_mm or {}).items()}
    return Ensamble(piezas, articulaciones, fuente, despiece)


# ---------------------------------------------------------------------- servo SG90

STEP_SG90 = ROOT / "modelos" / "fuentes" / "cad" / "SG90.step"
URL_STEP_SG90 = "https://github.com/PepperCat-YamanekoVillage/CAD_SG90_28BYJ-48 (dominio público)"
DESCARGA_SG90 = "https://raw.githubusercontent.com/PepperCat-YamanekoVillage/CAD_SG90_28BYJ-48/HEAD/SG90.step"


def _descargar_step_sg90() -> bool:
    if STEP_SG90.is_file():
        return True
    try:
        STEP_SG90.parent.mkdir(parents=True, exist_ok=True)
        with urllib.request.urlopen(DESCARGA_SG90, timeout=30) as r:
            STEP_SG90.write_bytes(r.read())
        return True
    except OSError as e:
        print(f"No pude descargar el CAD del SG90 ({e}); uso la versión hecha con primitivas.")
        return False


def _carcasa_real_sg90() -> dict[str, trimesh.Trimesh] | None:
    """La carcasa del CAD real, partida donde se separa un SG90 de verdad. En mm, Y hacia arriba,
    base en y=0 y el eje de salida en x=0."""
    if not _descargar_step_sg90():
        return None
    escena = trimesh.load(str(STEP_SG90), tol_linear=0.01, tol_angular=0.1)
    malla = escena.to_geometry()
    malla.merge_vertices()
    carcasa = max(malla.split(only_watertight=False), key=lambda c: len(c.faces))
    carcasa.apply_scale(1000.0)   # cascadio entrega metros; aquí se trabaja en mm
    # El STEP tiene Z hacia arriba y la base en z = -27.9 mm: se pasa a Y hacia arriba con la base en 0
    carcasa.apply_transform(trimesh.transformations.rotation_matrix(-np.pi / 2, [1, 0, 0]))
    carcasa.apply_translation([0, -carcasa.bounds[0][1], 0])
    cortes = {"carcasa_inferior": (-1, 2.5), "carcasa_media": (2.5, 19.05), "tapa_superior": (19.05, 27.9),
              "eje_estriado": (27.9, 40)}
    piezas = {}
    for nombre, (abajo, arriba) in cortes.items():
        m = trimesh.intersections.slice_mesh_plane(carcasa, [0, 1, 0], [0, abajo, 0], cap=True)
        piezas[nombre] = trimesh.intersections.slice_mesh_plane(m, [0, -1, 0], [0, arriba, 0], cap=True)
    return piezas


def servo_sg90() -> Ensamble:
    carcasa = _carcasa_real_sg90()
    if carcasa is None:
        return _servo_sg90_basico()
    p = {
        "carcasa_inferior": [pintar(carcasa["carcasa_inferior"], AZUL_SG90, 0.1, 0.45)],
        "carcasa_media": [pintar(carcasa["carcasa_media"], AZUL_SG90, 0.1, 0.45)],     # con las orejas de montaje
        "tapa_superior": [pintar(carcasa["tapa_superior"], AZUL_SG90, 0.1, 0.45)],
        "eje_estriado": [pintar(carcasa["eje_estriado"], NYLON, 0.05, 0.6)],
        # Por dentro (no viene en el CAD): medidas aproximadas del SG90 abierto
        "placa_control": [caja((20.0, 1.0, 10.0), (-5.2, 1.6, 0), VERDE_PCB),
                          caja((4.0, 1.2, 4.0), (-9.0, 2.7, 0), NEGRO)],          # el chip controlador
        "motor_dc": [cilindro(5.5, 12.0, (-10.5, 9.5, 0), ACERO, metalico=0.8, rugosidad=0.3),
                     cilindro(1.0, 4.0, (-10.5, 17.5, 0), ACERO, metalico=0.9)],
        "potenciometro": [cilindro(3.5, 5.0, (0, 12.5, 0), NEGRO), cilindro(1.0, 4.0, (0, 17.0, 0), LATON)],
        "engrane_motor": [engrane(2.2, 2.0, 10, (-10.5, 20.0, 0), LATON)],
        "engrane_reductor_1": [engrane(6.0, 1.5, 30, (-6.5, 20.6, 0), NYLON)],
        "engrane_reductor_2": [engrane(4.8, 1.5, 24, (-3.4, 22.4, 0), NYLON)],
        "engrane_salida": [engrane(5.6, 2.5, 28, (0, 24.2, 0), NYLON)],
        "brazo": [cilindro(3.5, 3.0, (0, 32.6, 0), NYLON), caja((20.0, 1.6, 5.5), (0, 33.3, 0), NYLON)],
        "tornillos": [cilindro(0.9, 26.0, (x, 14.0, 0), NEGRO, metalico=0.7) for x in (-15.0, 4.6)],
        "cable": [cilindro(0.6, 20.0, (16.3, 4.5, z), c, eje="x")
                  for z, c in ((-1.3, (90, 55, 30, 255)), (0.0, (200, 30, 30, 255)), (1.3, (240, 140, 20, 255)))],
    }
    # Vista explotada en capas, como en un manual: la carcasa y los engranes se apilan sobre el eje,
    # el motor sale hacia el frente, el potenciómetro hacia atrás y el cable a un lado
    despiece = {
        "tornillos": (0, -40, 0), "carcasa_inferior": (0, -18, 0), "placa_control": (0, -10, 0),
        "carcasa_media": (0, 0, 0), "motor_dc": (0, 4, -24), "potenciometro": (0, 2, 22),
        "engrane_motor": (0, 10, 0), "engrane_reductor_1": (0, 17, 0), "engrane_reductor_2": (0, 24, 0),
        "engrane_salida": (0, 31, 0), "tapa_superior": (0, 40, 0), "eje_estriado": (0, 50, 0),
        "brazo": (0, 60, 0), "cable": (22, 0, 0),
    }
    juntas = [{"nombre": "salida", "tipo": "giro", "pieza": "eje_estriado", "eje": [0, 1, 0], "rango": [-1.571, 1.571]}]
    fuente = f"Carcasa: CAD real del SG90 ({URL_STEP_SG90}); piezas internas generadas por código"
    return _ensamble(p, juntas, fuente, despiece)


def _servo_sg90_basico() -> Ensamble:
    """Versión sin CAD (todo con primitivas), por si no está el STEP real."""
    ex = 5.9  # el eje de salida está corrido hacia un lado
    p = {
        "carcasa_inferior": [caja((22.5, 15.0, 11.8), (0, 7.5, 0), AZUL_SG90)],
        "placa_control": [caja((18.0, 1.0, 10.0), (0, 1.6, 0), VERDE_PCB),
                          caja((4.0, 1.2, 4.0), (-4.0, 2.6, 0), NEGRO)],          # el chip controlador
        "motor_dc": [cilindro(5.5, 12.0, (-5.0, 8.5, 0), ACERO, metalico=0.8, rugosidad=0.3),
                     cilindro(1.0, 3.0, (-5.0, 16.0, 0), ACERO, metalico=0.9)],
        "potenciometro": [cilindro(3.5, 5.0, (ex, 11.0, 0), NEGRO), cilindro(1.0, 4.0, (ex, 15.0, 0), LATON)],
        "carcasa_media": [caja((22.5, 4.0, 11.8), (0, 17.0, 0), AZUL_SG90),
                          caja((32.5, 2.5, 11.8), (0, 16.5, 0), AZUL_SG90)],      # orejas de montaje
        "engrane_motor": [engrane(2.2, 2.0, 10, (-5.0, 19.5, 0), LATON)],
        "engrane_reductor_1": [engrane(6.5, 1.5, 32, (-1.2, 20.0, 0), NYLON)],
        "engrane_reductor_2": [engrane(5.0, 1.5, 26, (1.5, 21.7, 0), NYLON)],
        "engrane_salida": [engrane(6.0, 2.5, 30, (ex, 22.3, 0), NYLON)],
        "tapa_superior": [caja((22.5, 4.5, 11.8), (0, 21.3, 0), AZUL_SG90),
                          cilindro(5.9, 4.0, (ex, 25.5, 0), AZUL_SG90)],
        "eje_salida": [cilindro(2.4, 3.5, (ex, 29.2, 0), NYLON)],
        "brazo": [cilindro(3.5, 3.0, (ex, 30.6, 0), NYLON), caja((20.0, 1.6, 5.5), (ex, 32.0, 0), NYLON)],
        "tornillos": [cilindro(0.9, 26.0, (x, 13.0, 0), NEGRO, metalico=0.7) for x in (-10.2, 10.2)],
        "cable": [cilindro(0.6, 30.0, (-26.2, 3.0, z), c, eje="x")
                  for z, c in ((-1.3, (90, 55, 30, 255)), (0.0, (200, 30, 30, 255)), (1.3, (240, 140, 20, 255)))],
    }
    juntas = [{"nombre": "salida", "tipo": "giro", "pieza": "eje_salida", "eje": [0, 1, 0], "rango": [-1.571, 1.571]}]
    return _ensamble(p, juntas, "Generado por código (medidas aproximadas del SG90 de TowerPro)")


# ---------------------------------------------------------------------- motor a pasos NEMA 17

def nema17() -> Ensamble:
    lado = 42.3
    bobinas = []
    for i in range(8):
        a = 2 * np.pi * i / 8
        b = trimesh.creation.box(extents=(4.5, 22.0, 7.0))
        b.apply_transform(trimesh.transformations.rotation_matrix(-a, [0, 1, 0]))
        b.apply_translation((16.0 * np.cos(a), 20.0, 16.0 * np.sin(a)))
        bobinas.append(pintar(b, COBRE, 0.7, 0.35))
    p = {
        "tapa_trasera": [caja((lado, 8.0, lado), (0, 4.0, 0), ALUMINIO, 0.8, 0.35)],
        "estator": [caja((lado, 24.0, lado), (0, 20.0, 0), NEGRO, 0.3, 0.6)],
        "bobinas": bobinas,
        "rotor": [cilindro(12.5, 24.0, (0, 20.0, 0), ACERO, metalico=0.85, rugosidad=0.25)],
        "tapa_frontal": [caja((lado, 8.0, lado), (0, 36.0, 0), ALUMINIO, 0.8, 0.35),
                         cilindro(11.0, 2.0, (0, 41.0, 0), ALUMINIO, metalico=0.8)],
        "eje": [cilindro(2.5, 56.0, (0, 34.0, 0), ACERO, metalico=0.95, rugosidad=0.15)],
        "rodamiento_frontal": [anillo(2.5, 8.0, 5.0, (0, 37.5, 0), ACERO)],
        "rodamiento_trasero": [anillo(2.5, 8.0, 5.0, (0, 4.5, 0), ACERO)],
        "tornillos": [cilindro(1.5, 46.0, (x, 21.0, z), NEGRO, metalico=0.7) for x in (-15.5, 15.5) for z in (-15.5, 15.5)],
        "conector": [caja((16.0, 6.0, 6.0), (0, 10.0, -lado / 2 - 3.0), NYLON)],
    }
    juntas = [{"nombre": "eje", "tipo": "giro", "pieza": "eje", "eje": [0, 1, 0], "rango": None}]
    # Todo está apilado sobre el eje: se separa en capas a lo largo de él
    despiece = {"tapa_trasera": (0, -20, 0), "rodamiento_trasero": (0, -10, 0), "estator": (0, 0, 0),
                "bobinas": (0, 0, 0), "rotor": (0, 14, 0), "rodamiento_frontal": (0, 34, 0),
                "tapa_frontal": (0, 24, 0), "eje": (0, 50, 0), "tornillos": (0, -40, 0), "conector": (0, -6, -22)}
    despiece["bobinas"] = (0, 0, 30)   # las bobinas salen hacia atrás para verlas
    return _ensamble(p, juntas, "Generado por código (medidas del estándar NEMA 17)", despiece)


# ---------------------------------------------------------------------- rodamiento 608

def rodamiento_608() -> Ensamble:
    p = {
        "aro_exterior": [anillo(9.5, 11.0, 7.0, (0, 0, 0), ACERO)],
        "aro_interior": [anillo(4.0, 5.5, 7.0, (0, 0, 0), ACERO)],
        "jaula": [anillo(6.6, 8.4, 2.2, (0, 0, 0), LATON, 0.7, 0.4)],
        "sello_superior": [anillo(5.6, 9.6, 0.6, (0, 3.2, 0), (190, 40, 40, 255), 0.05, 0.8)],
        "sello_inferior": [anillo(5.6, 9.6, 0.6, (0, -3.2, 0), (190, 40, 40, 255), 0.05, 0.8)],
    }
    for i in range(7):
        a = 2 * np.pi * i / 7
        p[f"bola_{i + 1}"] = [esfera(1.98, (7.5 * np.cos(a), 0, 7.5 * np.sin(a)), ACERO)]
    juntas = [{"nombre": "giro", "tipo": "giro", "pieza": "aro_interior", "eje": [0, 1, 0], "rango": None}]
    # Los aros y la jaula comparten centro: se separan hacia arriba en capas; las bolas salen hacia afuera
    despiece = {"aro_interior": (0, 16, 0), "jaula": (0, 8, 0), "sello_superior": (0, 24, 0), "sello_inferior": (0, -12, 0)}
    for i in range(7):
        a = 2 * np.pi * i / 7
        despiece[f"bola_{i + 1}"] = (12 * np.cos(a), 4, 12 * np.sin(a))
    return _ensamble(p, juntas, "Generado por código (rodamiento 608-2RS: 8 x 22 x 7 mm)", despiece)
