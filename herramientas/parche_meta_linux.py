#!/usr/bin/env python3
"""Incrusta el Meta XR Core SDK en Practica_1_RM/Packages/ y le aplica el parche para el editor de Linux.

El paquete com.meta.xr.sdk.core 85.0.0 no compila en el editor de Linux: Editor/MetaXRSimulator/Installer.cs solo
declara `downloadedInstallerPath` para Windows y macOS (error CS0103). Este script copia el paquete de
Library/PackageCache/ a Packages/com.meta.xr.sdk.core (Unity usa la copia incrustada en lugar de la del registro) y
agrega el caso de Linux. En Windows y macOS no hace falta: ahí el paquete del registro compila tal cual.

La copia incrustada no se sube al repositorio (está en .gitignore). En otra máquina con Linux:
abre el proyecto una vez (para que Unity descargue el paquete), corre este script y vuelve a enfocar Unity.

  python3 herramientas/parche_meta_linux.py
"""

import shutil
import stat
import sys
from pathlib import Path

PROYECTO = Path(__file__).resolve().parent.parent / "Practica_1_RM"
DESTINO = PROYECTO / "Packages" / "com.meta.xr.sdk.core"
ARCHIVO = Path("Editor/MetaXRSimulator/Installer.cs")

ORIGINAL = """#elif UNITY_EDITOR_OSX
            var downloadedInstallerPath =
                            Path.Combine(XRSimConstants.DownloadFolderPath, $"meta_xr_simulator.dmg");
#endif"""

PARCHADO = """#elif UNITY_EDITOR_OSX
            var downloadedInstallerPath =
                            Path.Combine(XRSimConstants.DownloadFolderPath, $"meta_xr_simulator.dmg");
#else
            // Parche local (Tecnologías Emergentes) para el editor de Linux: Meta no publica el simulador para
            // Linux; la ruta solo se declara para que el paquete compile.
            var downloadedInstallerPath =
                            Path.Combine(XRSimConstants.DownloadFolderPath, $"meta_xr_simulator.zip");
#endif"""


def main() -> int:
    if not DESTINO.exists():
        candidatos = sorted((PROYECTO / "Library" / "PackageCache").glob("com.meta.xr.sdk.core@*"))
        if not candidatos:
            print("No encontré com.meta.xr.sdk.core en Library/PackageCache: abre el proyecto en Unity una vez.")
            return 1
        origen = candidatos[-1]
        print(f"Copiando {origen.name} a Packages/com.meta.xr.sdk.core ...")
        shutil.copytree(origen, DESTINO)
        for ruta in [DESTINO, *DESTINO.rglob("*")]:
            ruta.chmod(ruta.stat().st_mode | stat.S_IWUSR)

    instalador = DESTINO / ARCHIVO
    texto = instalador.read_text(encoding="utf-8")
    if PARCHADO in texto:
        print("El parche ya estaba aplicado.")
        return 0
    if texto.count(ORIGINAL) != 1:
        print(f"{ARCHIVO} no tiene el texto esperado (¿otra versión del SDK?). Revísalo a mano.")
        return 1
    instalador.write_text(texto.replace(ORIGINAL, PARCHADO), encoding="utf-8")
    print(f"Parche aplicado en Packages/com.meta.xr.sdk.core/{ARCHIVO}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
