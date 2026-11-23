// Copia este archivo como secretos.h (en la misma carpeta) y pon tus datos. secretos.h no se sube a GitHub.
#pragma once

// La ESP32 solo se conecta a redes de 2.4 GHz con contraseña simple (WPA2 personal). Si la red de la escuela pide
// usuario y contraseña en una página, usa el hotspot del celular.
#define WIFI_SSID "nombre_de_tu_red"
#define WIFI_PASS "contrasena_de_tu_red"

// Broker MQTT: el mismo que tenga el campo Servidor de CarroMQTT en Unity.
//   "test.mosquitto.org": por internet, como el ejercicio 1.
//   La IP de la laptop con Mosquitto (diapositivas 4 y 5), por ejemplo "192.168.1.50".
#define MQTT_SERVIDOR "test.mosquitto.org"
#define MQTT_PUERTO 1883
