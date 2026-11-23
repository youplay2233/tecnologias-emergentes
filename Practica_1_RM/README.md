# Practica_1_RM

Proyecto de Unity **2022.3.58f1** con las prácticas de Tecnologías Emergentes en **realidad mixta** para Meta Quest 3
(Meta XR Core SDK 85 + OpenXR, passthrough activado).

## Abrirlo

1. Unity Hub > **Add** > *Add project from disk* > esta carpeta.
2. Ábrelo con el editor 2022.3.58f1. La primera vez descarga el SDK de Meta y tarda varios minutos.
3. Al terminar de importar, el proyecto cambia solo a Android y construye las escenas de las prácticas en `Assets/Scene`.
   Si no lo hace: menú **Tecnologías Emergentes > 1. Construir todas las prácticas**.

### En Linux

El Meta XR Core SDK 85 no compila en el editor de Linux: `Editor/MetaXRSimulator/Installer.cs` solo declara
`downloadedInstallerPath` para Windows y macOS (error CS0103). En Windows y macOS no hace falta nada. En Linux:

1. Abre el proyecto una vez. Si pregunta *Enter Safe Mode?*, elige **Ignore**.
2. Desde la raíz del repositorio corre `python3 herramientas/parche_meta_linux.py`: copia el paquete a
   `Packages/com.meta.xr.sdk.core` (Unity usa esa copia en lugar de la del registro) y le agrega el caso de Linux.
3. Regresa a Unity: vuelve a resolver los paquetes y compila sin errores.

## Escenas

| Escena | Práctica | Presentación |
|---|---|---|
| `objetos` | Práctica 1 · Game Objects | Unity |
| `Parque_diversiones` | Práctica 1 · Parque de diversiones | Unity |
| `Proyecto_final` | Práctica 1 · Propuesta de proyecto final | Unity |
| `Canon_Autonomo` | Práctica 3 · El Cañón Autónomo | Objetos y Script |
| `Fisica_Sesion1` | Físicas S1 · Rigidbody y gravedad | Físicas 1 |
| `Fisica_Sesion2` | Físicas S2 · Colliders y colisiones | Físicas 2 |
| `Jarvis_RM` | Proyecto final · Jarvis en realidad mixta | Latencia y protocolos |
| `Inicio`, `Actividad1`, `Actividad2` | Conexión y navegación de escenas | Conexión de escenas |
| `MQTT_Figuras` | Conectividad IoT · MQTT (ejercicio 1) | MQTT Unity |

## Verlo en el Quest 3 (Linux)

En Linux no existe Quest Link, así que la app se instala en el visor:

1. Activa el modo desarrollador del Quest 3 y conéctalo por USB (acepta "Permitir depuración USB" dentro del visor).
2. Menú **Tecnologías Emergentes > 3. Compilar e instalar en el Quest 3 (cable USB)**.
3. La app abre en passthrough. Con **Y** pasas a la siguiente práctica y con **X** a la anterior.

## Controles

| Acción | Quest 3 | Editor |
|---|---|---|
| Siguiente / anterior práctica | Y / X | N / P |
| Disparar el cañón | Gatillo derecho | Clic izquierdo |
| Apuntar el cañón | Palanca derecha | Flechas |
| Repetir el experimento de física | A | R |
| Mismo empuje a los tres cubos (Física S1) | B | E |
| Quitar el Collider del piso (Física S2) | Gatillo izquierdo | C |
| Agarrar y lanzar objetos | Botón de agarre (grip) | — |
| Pulsar un botón o tocar una figura (Conexión de escenas, MQTT) | Tocarlo con la mano o el control, o apuntar y gatillo | Clic en el botón |

## Carpetas

- `Assets/Scripts`: los scripts de las presentaciones (`Bala`, `ControlCañon`, `DetectorColision`, `NavegacionEscenas`, `MQTTBasico`) tal como vienen en clase, más `Girar` y `MantenerVertical` del parque.
- `Assets/Packages`: MQTTnet 4.3.7.1207, instalado con NuGetForUnity como pide la presentación de MQTT.
- `Assets/RM`: lo que se agrega para realidad mixta (controles del Quest, agarre, menú de prácticas, consola flotante).
- `Assets/Editor`: el constructor que arma las escenas siguiendo el paso a paso de las presentaciones.
- `Assets/Jarvis`: la interfaz del proyecto final. `JarvisEnlace` es la red (UDP para encontrar la laptop, WebSocket para las órdenes, ping para medir latencia) y `JarvisRM` ejecuta las órdenes en la escena.
- `Assets/PuenteJarvis`: el puente de archivos con el que Claude controla el editor mientras se construye el proyecto.

## Proyecto final: Jarvis

1. En la laptop, dentro de la carpeta `jarvis`: `uv sync --extra voice --extra rm` y luego `uv run jarvis-rm` (o `uv run jarvis-rm --demo` para probar el enlace sin usar Claude).
2. En el visor abre la práctica **Proyecto final · Jarvis en realidad mixta** (botón Y hasta llegar).
3. El visor encuentra la laptop solo si los dos están en la misma red Wi-Fi. Si no la encuentra, escribe `ws://IP_DE_LA_LAPTOP:8765` en el campo **Direccion** del objeto `Jarvis_RM`.
4. Di «Hey Jarvis» o aprieta **A** y pide algo: "crea cinco cubos rojos", "lanza una esfera", "apaga la gravedad".
