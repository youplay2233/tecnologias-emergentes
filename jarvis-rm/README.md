# jarvis-rm: Jarvis en realidad mixta

Copia del código del proyecto final para que se pueda leer desde este repositorio. El original vive en dos proyectos
aparte: el cerebro en Python (`~/jarvis`) y la app de Unity para el Meta Quest 3 (`~/jarvis-rm`). La escena
`Jarvis_RM` de `Practica_1_RM` fue la primera versión y se queda como evidencia de clase.

## Qué hace

Le hablas a Jarvis («Oye Jarvis») con el visor puesto. El audio del micrófono del visor llega a la laptop, se pasa a
texto con faster-whisper en la GPU y Claude decide qué hacer con las herramientas que tiene. Jarvis contesta con voz
(Piper) y actúa en la escena: carga robots y los despieza, resalta o mueve sus articulaciones, crea objetos con física,
copia tu mano derecha en un robot (mano espejo), pone música en Spotify o busca un documento y lo manda por correo
(eso último siempre con confirmación).

## Carpetas

| Carpeta | Qué hay |
|---|---|
| `jarvis/unity/servidor.py` | Enlace con el visor: descubrimiento UDP (8766), WebSocket (8765) con órdenes, acuses, ping/pong y audio. |
| `jarvis/unity/audio_visor.py` | Micrófono y bocinas del visor por el WebSocket: PCM de 16 bits a 16 kHz en trozos de ~80 ms. |
| `jarvis/unity/archivos.py` | Servidor HTTP (8767) del que el visor descarga los modelos GLB. |
| `jarvis/unity/herramientas.py` | Las 22 herramientas `unity_*` que Claude puede usar en la escena. |
| `jarvis/unity/rm_cli.py` | El comando `jarvis-rm`: por voz, por texto (`--texto`) o con una secuencia de prueba sin Claude (`--demo`). |
| `jarvis/modelos/` | Catálogo y conversión a GLB: 8 robots de MuJoCo Menagerie y 3 componentes modelados por código, con piezas y articulaciones. |
| `unity/Assets/Jarvis/` | La app del visor: HUD (`JarvisHUD`), reactor (`NucleoJarvis`), robots (`ModeloRobot`), tableta, mano espejo, bote de basura y objetos con física. |
| `unity/Assets/Base/` | Rig, manos, entrada de los controles y arranque, heredados de las prácticas. |
| `unity/Assets/Editor/` | `ConstructorJarvisRM`: arma la escena por código. |
| `tests/` | Pruebas del enlace con un visor simulado, del audio del visor y de los modelos. |

## Usarlo

```bash
cd ~/jarvis
uv sync --extra voice --extra rm
uv run jarvis-rm --demo     # prueba el enlace sin gastar créditos de la API
uv run jarvis-rm --texto    # peticiones por teclado
uv run jarvis-rm            # por voz: «Oye Jarvis» o el botón A del control derecho
```

En el visor abre la app **Jarvis RM**. Si la laptop y el visor están en la misma red Wi-Fi se encuentran solos. Si la
red no deja pasar el broadcast, conecta el cable USB y corre `adb reverse tcp:8765 tcp:8765`, o escribe la dirección
`ws://IP_DE_LA_LAPTOP:8765` en `direccion.txt` dentro de la carpeta de la app.

Controles: **A** hablar, **B** lanzar al bote lo que tengas en la mano (si no tienes nada, centra el HUD), **X** tableta,
botón de agarre (o pinza con la mano) para tomar piezas.

## Mensajes

Todos son objetos JSON planos con un campo `tipo`.

| Dirección | Tipos |
|---|---|
| Laptop → visor | `estado`, `oido`, `decir`, `panel`, `crear`, `lanzar`, `gravedad`, `limpiar`, `modelo`, `tableta`, `catalogo`, `metricas`, `pong`, `pitido` |
| Visor → laptop | `hola`, `ping`, `ack`, `boton`, `latencia`, `evento`, `voz_fin` |
| Binario | `0x01` + audio del micrófono (visor → laptop); `0x02` + voz de Jarvis (laptop → visor) |

Las órdenes que cambian la escena llevan `id`; el visor contesta con `ack` y un texto que dice qué pasó. Ese texto
regresa a Claude como resultado de la herramienta.

## Probarlo sin el visor

```bash
uv run --with pytest pytest tests/
```
