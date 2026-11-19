"""Convierte robots descritos en MJCF (MuJoCo) a GLB con una pieza por cuerpo del robot.

Unity carga el GLB en vivo (glTFast) y Claude recibe el JSON con la lista de piezas y
articulaciones, así puede pedir "quita la base" o "gira el codo" usando nombres reales.

  uv run jarvis-modelos            # convierte todos los de CATALOGO
  uv run jarvis-modelos panda      # solo uno
"""

import argparse
import json
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path

import mujoco
import numpy as np
import trimesh

from jarvis.config import ROOT

FUENTES = ROOT / "modelos" / "fuentes" / "menagerie"
SALIDA = ROOT / "modelos" / "glb"

# MuJoCo usa Z hacia arriba; glTF (y Unity) usan Y hacia arriba.
Z_A_Y = np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=float)


@dataclass(frozen=True)
class Robot:
    clave: str
    nombre: str
    descripcion: str
    xml: str                       # MJCF de MuJoCo Menagerie, o "" si lo arma un generador
    licencia: str
    generador: Callable[[], "Ensamble"] | None = None   # para componentes hechos por código


@dataclass
class Ensamble:
    """Piezas ya colocadas (metros, Y hacia arriba): nombre -> (padre, mallas con material)."""

    piezas: dict[str, tuple[str, list[trimesh.Trimesh]]]
    articulaciones: list[dict]
    fuente: str
    # Dirección de despiece explícita (m) para piezas que comparten centro (aros de un rodamiento);
    # va en el GLB como un nodo vacío "<pieza>__despiece" que Unity usa en lugar de la radial.
    despiece: dict[str, tuple[float, float, float]] = field(default_factory=dict)


CATALOGO = [
    Robot("panda", "Franka Panda", "Brazo industrial de 7 ejes con pinza.",
          "franka_emika_panda/panda.xml", "Apache-2.0"),
    Robot("so101", "SO-101", "Brazo robótico de bajo costo, impreso en 3D.",
          "robotstudio_so101/so101.xml", "Apache-2.0"),
    Robot("spot", "Boston Dynamics Spot", "Robot cuadrúpedo.",
          "boston_dynamics_spot/spot.xml", "BSD-3-Clause"),
    Robot("g1", "Unitree G1", "Robot humanoide.",
          "unitree_g1/g1.xml", "BSD-3-Clause"),
    Robot("mano", "Shadow Hand", "Mano robótica de 24 articulaciones.",
          "shadow_hand/right_hand.xml", "Apache-2.0"),
    Robot("ur5e", "Universal Robots UR5e", "Brazo colaborativo de 6 ejes (de la familia del UR3).",
          "universal_robots_ur5e/ur5e.xml", "BSD-3-Clause"),
    Robot("iiwa", "KUKA LBR iiwa 14", "Brazo industrial de 7 ejes.",
          "kuka_iiwa_14/iiwa14.xml", "BSD-3-Clause"),
    Robot("go2", "Unitree Go2", "Perro robot de 12 articulaciones.",
          "unitree_go2/go2.xml", "BSD-3-Clause"),
]


def _componentes() -> list[Robot]:
    from jarvis.modelos import componentes as c

    return [
        Robot("servo_sg90", "Servo SG90", "Micro servo de 9 g: motor, reductor de engranes, potenciómetro y control.",
              "", "propio", c.servo_sg90),
        Robot("nema17", "Motor NEMA 17", "Motor a pasos: estator con bobinas, rotor, eje y rodamientos.",
              "", "propio", c.nema17),
        Robot("rodamiento_608", "Rodamiento 608", "Rodamiento de bolas de 8 x 22 x 7 mm (el de las patinetas).",
              "", "propio", c.rodamiento_608),
    ]

TIPOS_ARTICULACION = {0: "libre", 1: "esfera", 2: "lineal", 3: "giro"}


def _pose_inicial(model: mujoco.MjModel, data: mujoco.MjData) -> None:
    """Usa la pose 'home' (o 'stand') si el modelo la trae; si no, la de reposo."""
    nombres = [model.key(k).name for k in range(model.nkey)]
    for preferida in ("home", "stand"):
        if preferida in nombres:
            data.qpos[:] = model.key_qpos[nombres.index(preferida)]
            break
    mujoco.mj_forward(model, data)


def _geoms_visuales(model: mujoco.MjModel) -> list[int]:
    mallas = [g for g in range(model.ngeom) if model.geom_type[g] == mujoco.mjtGeom.mjGEOM_MESH]
    # Convención de Menagerie: grupo 2 = visual, grupo 3 = colisión (mallas simplificadas).
    visuales = [g for g in mallas if model.geom_group[g] == 2]
    if visuales:
        return visuales
    return [g for g in mallas if model.geom_group[g] != 3] or mallas


def _color(model: mujoco.MjModel, g: int) -> np.ndarray:
    mat = model.geom_matid[g]
    rgba = model.mat_rgba[mat] if mat >= 0 else model.geom_rgba[g]
    return (np.clip(rgba, 0, 1) * 255).astype(np.uint8)


def pintar(malla: trimesh.Trimesh, rgba, metalico: float = 0.3, rugosidad: float = 0.55) -> trimesh.Trimesh:
    """Color como material glTF (baseColorFactor): Unity le pone su propio material con ese color."""
    rgba = np.asarray(rgba, dtype=float)
    if rgba.max() <= 1.0:
        rgba = rgba * 255
    malla.visual = trimesh.visual.TextureVisuals(
        material=trimesh.visual.material.PBRMaterial(
            baseColorFactor=np.clip(rgba, 0, 255).astype(np.uint8), metallicFactor=metalico, roughnessFactor=rugosidad
        )
    )
    return malla


def _desde_mujoco(robot: Robot) -> Ensamble:
    model = mujoco.MjModel.from_xml_path(str(FUENTES / robot.xml))
    data = mujoco.MjData(model)
    _pose_inicial(model, data)

    # 1. Mallas en coordenadas del mundo, agrupadas por cuerpo (pieza)
    por_cuerpo: dict[int, list[trimesh.Trimesh]] = {}
    for g in _geoms_visuales(model):
        m = model.geom_dataid[g]
        va, vn = model.mesh_vertadr[m], model.mesh_vertnum[m]
        fa, fn = model.mesh_faceadr[m], model.mesh_facenum[m]
        verts = model.mesh_vert[va:va + vn] @ data.geom_xmat[g].reshape(3, 3).T + data.geom_xpos[g]
        malla = pintar(trimesh.Trimesh(verts @ Z_A_Y.T, model.mesh_face[fa:fa + fn], process=False), _color(model, g))
        por_cuerpo.setdefault(model.geom_bodyid[g], []).append(malla)

    def nombre_de(b: int) -> str:
        return model.body(b).name or f"pieza_{b}"

    def padre_visible(b: int) -> str:
        """El ancestro más cercano que sí tiene mallas (algunos cuerpos son solo articulaciones)."""
        p = model.body_parentid[b]
        while p > 0 and p not in por_cuerpo:
            p = model.body_parentid[p]
        return nombre_de(p) if p > 0 and p in por_cuerpo else ""

    piezas = {nombre_de(b): (padre_visible(b), mallas) for b, mallas in por_cuerpo.items()}

    articulaciones = []
    for j in range(model.njnt):
        tipo = TIPOS_ARTICULACION.get(int(model.jnt_type[j]), "?")
        rango = model.jnt_range[j] if model.jnt_limited[j] else None
        cuerpo = model.jnt_bodyid[j]
        articulaciones.append({
            "nombre": model.joint(j).name,
            "tipo": tipo,
            "pieza": nombre_de(cuerpo),
            "eje": np.round(Z_A_Y @ data.xaxis[j], 4).tolist(),       # en el mundo, ya en Y hacia arriba
            "rango": None if rango is None else np.round(rango, 4).tolist(),
            "reposo": round(float(data.qpos[model.jnt_qposadr[j]]), 4) if tipo in ("giro", "lineal") else 0.0,
            "_ancla": data.xanchor[j] @ Z_A_Y.T,                       # se centra al exportar
            "movible": tipo in ("giro", "lineal") and cuerpo in por_cuerpo,
        })
    return Ensamble(piezas, articulaciones, f"MuJoCo Menagerie: {robot.xml}")


def convertir(robot: Robot) -> Path:
    ensamble = robot.generador() if robot.generador else _desde_mujoco(robot)

    # Centrar: base en y=0 y centrado en x/z
    todos = np.vstack([m.vertices for _, ms in ensamble.piezas.values() for m in ms])
    lo, hi = todos.min(0), todos.max(0)
    desplazar = -np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])
    centro_robot = (lo + hi) / 2 + desplazar

    # 3. Escena glTF: un nodo por pieza (con su nombre) y sus mallas como hijos
    escena = trimesh.Scene()
    piezas = []
    for nombre, (padre, mallas) in ensamble.piezas.items():
        for m in mallas:
            m.apply_translation(desplazar)
        caja = np.vstack([m.vertices for m in mallas])
        centro = (caja.min(0) + caja.max(0)) / 2
        escena.graph.update(frame_to=nombre, frame_from=escena.graph.base_frame)
        for i, m in enumerate(mallas):
            escena.add_geometry(m, geom_name=f"{nombre}__{i}", node_name=f"{nombre}__{i}", parent_node_name=nombre)
        direccion = centro - centro_robot
        if nombre in ensamble.despiece:
            direccion = np.asarray(ensamble.despiece[nombre], dtype=float)
            escena.graph.update(frame_to=f"{nombre}__despiece", frame_from=nombre,
                                matrix=trimesh.transformations.translation_matrix(direccion))
        piezas.append({
            "nombre": nombre,
            "padre": padre,
            "centro": np.round(centro, 4).tolist(),
            "direccion_despiece": np.round(direccion, 4).tolist(),
            "triangulos": int(sum(len(m.faces) for m in mallas)),
        })

    # Articulaciones: un nodo vacío en el ancla y otro a 5 cm sobre el eje ("<pieza>__art_<n>" y su
    # "__punta"). Unity saca de ahí el pivote y el eje, ya convertidos a su sistema de coordenadas.
    articulaciones = []
    cuenta: dict[str, int] = {}
    for a in ensamble.articulaciones:
        a = dict(a)
        ancla = a.pop("_ancla", None)
        if a.pop("movible", False) and ancla is not None and a["pieza"] in ensamble.piezas:
            ancla = np.asarray(ancla) + desplazar
            n = cuenta.get(a["pieza"], 0)
            cuenta[a["pieza"]] = n + 1
            marca = f"{a['pieza']}__art_{n}"
            escena.graph.update(frame_to=marca, frame_from=a["pieza"],
                                matrix=trimesh.transformations.translation_matrix(ancla))
            escena.graph.update(frame_to=f"{marca}__punta", frame_from=marca,
                                matrix=trimesh.transformations.translation_matrix(np.asarray(a["eje"]) * 0.05))
            a["marcador"] = marca
            a["ancla"] = np.round(ancla, 4).tolist()
        articulaciones.append(a)

    SALIDA.mkdir(parents=True, exist_ok=True)
    glb = SALIDA / f"{robot.clave}.glb"
    glb.write_bytes(escena.export(file_type="glb", include_normals=True))  # sin normales se ve plano
    (SALIDA / f"{robot.clave}.json").write_text(json.dumps({
        "clave": robot.clave,
        "nombre": robot.nombre,
        "descripcion": robot.descripcion,
        "licencia": robot.licencia,
        "fuente": ensamble.fuente,
        "alto_m": round(float(hi[1] - lo[1]), 3),
        "piezas": piezas,
        "articulaciones": articulaciones,
    }, ensure_ascii=False, indent=2))
    return glb


def main() -> None:
    parser = argparse.ArgumentParser(prog="jarvis-modelos")
    parser.add_argument("claves", nargs="*", help="robots a convertir (por defecto, todos)")
    args = parser.parse_args()
    for robot in CATALOGO + _componentes():
        if args.claves and robot.clave not in args.claves:
            continue
        glb = convertir(robot)
        info = json.loads(glb.with_suffix(".json").read_text())
        print(f"{robot.nombre:22} {len(info['piezas']):3} piezas  {len(info['articulaciones']):3} articulaciones  "
              f"{info['alto_m']:.2f} m  {glb.stat().st_size / 1e6:5.1f} MB  -> {glb.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
