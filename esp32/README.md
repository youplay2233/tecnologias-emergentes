# ESP32: control del carro omnidireccional por MQTT

Programa de la práctica de MQTT, ejercicio 2. La ESP32 lee nueve botones y publica por MQTT lo que tiene que hacer
el carro de la escena `MQTT_Carro` de Unity. Son los mismos mensajes que manda el control virtual de la escena.

## Conexiones

Cada botón va entre su pin y **GND**. No lleva resistencia: el programa usa la interna de la ESP32 (`INPUT_PULLUP`).

| Botón | Pin | Qué publica |
|---|---|---|
| Adelante | 13 | `mover`: `adelante` mientras está apretado, `alto` al soltarlo |
| Atrás | 14 | `mover`: `atras` |
| Izquierda (de lado) | 27 | `mover`: `izquierda` |
| Derecha (de lado) | 26 | `mover`: `derecha` |
| Girar a la izquierda | 25 | `mover`: `girar_izquierda` |
| Girar a la derecha | 33 | `mover`: `girar_derecha` |
| Luces | 32 | `luces`: `encender` / `apagar` (cada pulsación cambia) |
| Direccional izquierda | 4 | `direccional`: `izquierda` / `apagar` |
| Direccional derecha | 23 | `direccional`: `derecha` / `apagar` |

Dos botones de movimiento a la vez dan la diagonal: adelante + izquierda publica `adelante_izquierda`. El LED de la
placa (pin 2) se prende cuando las luces del carro están encendidas. Todos los topics empiezan con
`clase/te/quest3/carro/`.

## Subirlo a la placa

1. En el IDE de Arduino, Herramientas > Administrar bibliotecas: instala **PubSubClient** de Nick O'Leary.
2. Copia `carro_mqtt/secretos.ejemplo.h` como `carro_mqtt/secretos.h` y pon el nombre y la contraseña de la red.
   `secretos.h` está en `.gitignore`: no se sube a GitHub.
3. Abre `carro_mqtt/carro_mqtt.ino`, elige la placa **ESP32 Dev Module** y el puerto, y súbelo.
4. En el Monitor Serie (115200 baudios) se ve la conexión y cada mensaje publicado.

La ESP32 solo se conecta a redes de 2.4 GHz con contraseña simple. Si la red de la escuela pide usuario y contraseña
en una página, usa el hotspot del celular (la laptop y el visor también se conectan ahí).

## Broker

El programa y la escena usan `test.mosquitto.org`, como el ejercicio 1: basta con que la ESP32 y el visor tengan
internet. Para usar un broker en la laptop, como en las diapositivas 4 y 5:

```bash
sudo apt install mosquitto mosquitto-clients
sudo systemctl stop mosquitto
mosquitto -c herramientas/broker.conf -v
```

Luego pon la IP de la laptop en `MQTT_SERVIDOR` (en `secretos.h`) y en el campo **Servidor** de `CarroMQTT` (objeto
`MQTT_Manager` de la escena). Si el cortafuegos de la laptop está activo, hay que abrir el puerto 1883.
