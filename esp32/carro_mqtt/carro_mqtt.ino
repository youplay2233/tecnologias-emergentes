// Práctica MQTT, ejercicio 2: botones en una ESP32 que manejan el carro omnidireccional de Unity (escena MQTT_Carro).
// Publica lo mismo que el control virtual de la escena:
//   clase/te/quest3/carro/mover        adelante, atras, izquierda, derecha, adelante_izquierda, adelante_derecha,
//                                      atras_izquierda, atras_derecha, girar_izquierda, girar_derecha, alto
//   clase/te/quest3/carro/luces        encender / apagar          (cada pulsación cambia)
//   clase/te/quest3/carro/direccional  izquierda / derecha / apagar
// Mientras un botón de movimiento esté apretado el carro se mueve; al soltarlo se manda "alto". Dos a la vez
// (por ejemplo adelante + izquierda) dan la diagonal. "izquierda" y "derecha" van de lado, sin girar.
//
// Conexión: cada botón entre su pin y GND, sin resistencia (se usa la interna de la ESP32, INPUT_PULLUP).
// Librería: PubSubClient de Nick O'Leary (Herramientas > Administrar bibliotecas).
// Red y broker: copia secretos.ejemplo.h como secretos.h y llénalo. secretos.h no se sube a GitHub.

#include <WiFi.h>
#include <PubSubClient.h>
#include "secretos.h"

const char* TOPIC_MOVER = "clase/te/quest3/carro/mover";
const char* TOPIC_LUCES = "clase/te/quest3/carro/luces";
const char* TOPIC_DIRECCIONAL = "clase/te/quest3/carro/direccional";

// Botones, en pines que no estorban al arrancar la placa
enum { ADELANTE, ATRAS, IZQUIERDA, DERECHA, GIRAR_IZQ, GIRAR_DER, LUCES, DIR_IZQ, DIR_DER, NUM_BOTONES };
const int PINES[NUM_BOTONES] = { 13, 14, 27, 26, 25, 33, 32, 4, 23 };
const int PIN_LED = 2;                 // LED de la placa: prendido = luces del carro encendidas
const unsigned long REBOTE_MS = 30;

bool presionado[NUM_BOTONES];          // estado ya sin rebote
bool lecturaAnterior[NUM_BOTONES];
unsigned long ultimoCambio[NUM_BOTONES];
bool recienPresionado[NUM_BOTONES];    // true solo en la vuelta de loop() en que se apretó

WiFiClient red;
PubSubClient mqtt(red);
bool luces = false;
const char* direccional = "apagar";
String movimientoEnviado = "";

void publicar(const char* topic, const char* texto) {
  bool ok = mqtt.publish(topic, texto);
  Serial.printf("%s %s = %s\n", ok ? "Publicado" : "NO se pudo publicar", topic, texto);
}

void conectarWiFi() {
  if (WiFi.status() == WL_CONNECTED) return;
  Serial.printf("Conectando a la red %s", WIFI_SSID);
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.print("\nWi-Fi listo, IP de la ESP32: ");
  Serial.println(WiFi.localIP());
}

void conectarMQTT() {
  while (!mqtt.connected()) {
    conectarWiFi();
    String id = String("ESP32_carro_") + String((uint32_t)ESP.getEfuseMac(), HEX);
    Serial.printf("Conectando al broker %s:%d... ", MQTT_SERVIDOR, MQTT_PUERTO);
    if (mqtt.connect(id.c_str())) {
      Serial.println("listo");
      movimientoEnviado = "";          // al reconectar vuelve a mandar el movimiento actual
    } else {
      Serial.printf("falló (estado %d), otro intento en 2 s\n", mqtt.state());
      delay(2000);
    }
  }
}

void leerBotones() {
  unsigned long ahora = millis();
  for (int i = 0; i < NUM_BOTONES; i++) {
    bool lectura = digitalRead(PINES[i]) == LOW;   // con INPUT_PULLUP, apretado = LOW
    if (lectura != lecturaAnterior[i]) {
      lecturaAnterior[i] = lectura;
      ultimoCambio[i] = ahora;
    }
    recienPresionado[i] = false;
    if (ahora - ultimoCambio[i] >= REBOTE_MS && lectura != presionado[i]) {
      presionado[i] = lectura;
      recienPresionado[i] = lectura;
    }
  }
}

String movimiento() {
  if (presionado[GIRAR_IZQ] != presionado[GIRAR_DER]) return presionado[GIRAR_IZQ] ? "girar_izquierda" : "girar_derecha";
  int frente = (int)presionado[ADELANTE] - (int)presionado[ATRAS];
  int lado = (int)presionado[DERECHA] - (int)presionado[IZQUIERDA];
  if (frente == 0 && lado == 0) return "alto";
  String m = frente > 0 ? "adelante" : (frente < 0 ? "atras" : "");
  if (lado != 0) {
    if (m.length() > 0) m += "_";
    m += lado > 0 ? "derecha" : "izquierda";
  }
  return m;
}

void setup() {
  Serial.begin(115200);
  for (int i = 0; i < NUM_BOTONES; i++) pinMode(PINES[i], INPUT_PULLUP);
  pinMode(PIN_LED, OUTPUT);
  digitalWrite(PIN_LED, LOW);
  mqtt.setServer(MQTT_SERVIDOR, MQTT_PUERTO);
  conectarMQTT();
}

void loop() {
  if (!mqtt.connected()) conectarMQTT();
  mqtt.loop();
  leerBotones();

  String m = movimiento();
  if (m != movimientoEnviado) {
    publicar(TOPIC_MOVER, m.c_str());
    movimientoEnviado = m;
  }

  if (recienPresionado[LUCES]) {
    luces = !luces;
    digitalWrite(PIN_LED, luces ? HIGH : LOW);
    publicar(TOPIC_LUCES, luces ? "encender" : "apagar");
  }

  if (recienPresionado[DIR_IZQ] || recienPresionado[DIR_DER]) {
    const char* lado = recienPresionado[DIR_IZQ] ? "izquierda" : "derecha";
    direccional = strcmp(direccional, lado) == 0 ? "apagar" : lado;   // el mismo botón otra vez la apaga
    publicar(TOPIC_DIRECCIONAL, direccional);
  }

  delay(5);
}
