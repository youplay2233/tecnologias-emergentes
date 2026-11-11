# jarvis-rm: el enlace entre el cerebro y el visor

Esta carpeta es la parte de Jarvis que conecta la laptop con la escena `Jarvis_RM` de Unity.
Vive dentro del proyecto de Jarvis (`jarvis/unity`); aquí está la copia que documenta el proyecto final.

| Archivo | Qué hace |
|---|---|
| `jarvis/unity/servidor.py` | Servidor WebSocket (puerto 8765) y descubrimiento UDP (puerto 8766). Manda órdenes, espera acuses y recibe la latencia que mide el visor. |
| `jarvis/unity/herramientas.py` | Las herramientas `unity_*` que Claude puede usar: crear y lanzar objetos, mostrar un panel, gravedad, limpiar y estado. |
| `jarvis/unity/rm_cli.py` | El comando `jarvis-rm`: por voz, por texto (`--texto`) o una secuencia de prueba sin Claude (`--demo`). |
| `tests/test_unity.py` | Pruebas del enlace con un visor simulado. |

## Usarlo

```bash
cd ~/jarvis
uv sync --extra voice --extra rm
uv run jarvis-rm --demo     # prueba el enlace sin gastar créditos de la API
uv run jarvis-rm --texto    # peticiones por teclado
uv run jarvis-rm            # por voz: «Hey Jarvis» o el botón A del control derecho
```

En el visor abre la escena **Proyecto final · Jarvis en realidad mixta**. Si la laptop y el visor están en la misma
red Wi-Fi se encuentran solos; si no, escribe `ws://IP_DE_LA_LAPTOP:8765` en el campo Direccion del objeto `Jarvis_RM`.

## Mensajes

Todos son objetos JSON planos con un campo `tipo`.

| Dirección | Tipos |
|---|---|
| Laptop → visor | `estado`, `oido`, `decir`, `panel`, `crear`, `lanzar`, `gravedad`, `limpiar`, `metricas`, `pong` |
| Visor → laptop | `hola`, `ping`, `ack`, `boton`, `latencia`, `evento` |

Las órdenes que cambian la escena llevan `id`; el visor contesta con `ack` y un texto que dice qué pasó.
Ese texto regresa a Claude como resultado de la herramienta.

## Probarlo sin el visor

```bash
uv run pytest tests/test_unity.py
```
