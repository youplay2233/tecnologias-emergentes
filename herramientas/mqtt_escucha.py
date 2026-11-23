#!/usr/bin/env python3
"""Escucha (y si se pide, publica) el topic de la práctica MQTT sin instalar nada: MQTT 3.1.1 con sockets.

Para la evidencia hace lo mismo que MQTT Explorer: se suscribe a clase/te/quest3/texto en test.mosquitto.org:1883
y anota cada mensaje con la hora en que llegó a la laptop.

  python3 herramientas/mqtt_escucha.py                                   # escucha hasta Ctrl+C
  python3 herramientas/mqtt_escucha.py --segundos 120 --salida evidencias/MQTT_Figuras/mensajes.txt
  python3 herramientas/mqtt_escucha.py --publicar "Hola desde la laptop"  # publica una vez y sale
"""
import argparse
import socket
import struct
import sys
import time
import uuid

HOST = "test.mosquitto.org"
PUERTO = 1883
TOPIC = "clase/te/quest3/texto"


def _largo(n):
    # "Remaining length" de MQTT: 7 bits por byte, el bit alto dice si sigue otro byte
    salida = bytearray()
    while True:
        byte, n = n % 128, n // 128
        salida.append(byte | (0x80 if n else 0))
        if not n:
            return bytes(salida)


def _cadena(texto):
    datos = texto.encode("utf-8")
    return struct.pack("!H", len(datos)) + datos


def _paquete(tipo, cuerpo):
    return bytes([tipo]) + _largo(len(cuerpo)) + cuerpo


def _leer(sock, n):
    datos = b""
    while len(datos) < n:
        trozo = sock.recv(n - len(datos))
        if not trozo:
            raise ConnectionError("el broker cerró la conexión")
        datos += trozo
    return datos


def _leer_paquete(sock):
    tipo = _leer(sock, 1)[0]
    n, mult = 0, 1
    while True:
        byte = _leer(sock, 1)[0]
        n += (byte & 0x7F) * mult
        if not byte & 0x80:
            break
        mult *= 128
    return tipo, (_leer(sock, n) if n else b"")


def conectar(host, puerto, keepalive=30):
    sock = socket.create_connection((host, puerto), timeout=10)
    cliente = "laptop_" + uuid.uuid4().hex[:10]
    # CONNECT: protocolo "MQTT" nivel 4 (3.1.1), sesión limpia, sin usuario ni contraseña
    sock.sendall(_paquete(0x10, _cadena("MQTT") + bytes([4, 0x02]) + struct.pack("!H", keepalive) + _cadena(cliente)))
    tipo, datos = _leer_paquete(sock)
    if tipo >> 4 != 2 or len(datos) < 2 or datos[1] != 0:
        raise ConnectionError("el broker rechazó la conexión (CONNACK %r)" % datos)
    return sock


def suscribir(sock, topic):
    sock.sendall(_paquete(0x82, struct.pack("!H", 1) + _cadena(topic) + b"\x00"))   # QoS 0
    while True:
        tipo, datos = _leer_paquete(sock)
        if tipo >> 4 == 9:   # SUBACK
            if datos[-1:] == b"\x80":
                raise ConnectionError("el broker rechazó la suscripción a " + topic)
            return


def publicar(sock, topic, texto):
    sock.sendall(_paquete(0x30, _cadena(topic) + texto.encode("utf-8")))   # QoS 0, sin retain


def hora():
    t = time.time()
    return time.strftime("%H:%M:%S", time.localtime(t)) + ".%03d" % int((t % 1) * 1000)


def escuchar(sock, topic, segundos, salida, keepalive=30):
    fin = time.time() + segundos if segundos else None
    ultimo_envio = time.time()
    sock.settimeout(1.0)
    archivo = open(salida, "a", encoding="utf-8") if salida else None
    linea = "# %s  suscrito a %s en %s:%d (herramientas/mqtt_escucha.py, en la laptop)" % (
        time.strftime("%Y-%m-%d %H:%M:%S"), topic, HOST, PUERTO)
    print(linea, flush=True)
    if archivo:
        archivo.write(linea + "\n")
        archivo.flush()
    recibidos = 0
    try:
        while fin is None or time.time() < fin:
            if time.time() - ultimo_envio > keepalive / 2:
                sock.sendall(b"\xc0\x00")   # PINGREQ, para que el broker no corte la conexión
                ultimo_envio = time.time()
            try:
                tipo, datos = _leer_paquete(sock)
            except socket.timeout:
                continue
            if tipo >> 4 != 3:   # solo interesa PUBLISH
                continue
            qos = (tipo >> 1) & 3
            largo = struct.unpack("!H", datos[:2])[0]
            topic_rx = datos[2:2 + largo].decode("utf-8", "replace")
            inicio = 2 + largo + (2 if qos else 0)
            texto = datos[inicio:].decode("utf-8", "replace")
            recibidos += 1
            # El topic solo se anota si no es el suscrito (por ejemplo, con comodines # o +)
            linea = "%s  %s" % (hora(), texto) if topic_rx == topic else "%s  %s  %s" % (hora(), topic_rx, texto)
            print(linea, flush=True)
            if archivo:
                archivo.write(linea + "\n")
                archivo.flush()
    except KeyboardInterrupt:
        pass
    finally:
        if archivo:
            archivo.close()
    return recibidos


def main():
    p = argparse.ArgumentParser(description="Cliente MQTT mínimo para la práctica (escucha o publica).")
    p.add_argument("--topic", default=TOPIC)
    p.add_argument("--segundos", type=float, default=0, help="cuánto escuchar (0 = hasta Ctrl+C)")
    p.add_argument("--salida", help="archivo donde también se anotan los mensajes")
    p.add_argument("--publicar", metavar="TEXTO", help="publica TEXTO en el topic y sale")
    a = p.parse_args()

    sock = conectar(HOST, PUERTO)
    try:
        if a.publicar is not None:
            publicar(sock, a.topic, a.publicar)
            print("%s  publicado en %s: %s" % (hora(), a.topic, a.publicar))
            sock.sendall(b"\xe0\x00")   # DISCONNECT
            return 0
        suscribir(sock, a.topic)
        n = escuchar(sock, a.topic, a.segundos, a.salida)
        print("# %d mensajes" % n, file=sys.stderr)
        sock.sendall(b"\xe0\x00")
    finally:
        sock.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
