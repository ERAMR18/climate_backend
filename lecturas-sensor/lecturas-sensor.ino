
#include <Arduino.h>
#include <DHT.h>

constexpr uint8_t DHT_PIN = 2;
constexpr uint8_t DHT_TYPE = DHT11;
constexpr unsigned long READ_INTERVAL_MS = 3000;
constexpr char DEVICE_ID[] = "arduino-temp-001";

DHT dht(DHT_PIN, DHT_TYPE);
unsigned long lastReadMs = 0;
unsigned long sequenceNumber = 0;

void setup() {
  Serial.begin(9600);
  dht.begin();

  // Permite realizar la primera lectura inmediatamente.
  lastReadMs = millis() - READ_INTERVAL_MS;
}

void loop() {
  unsigned long currentMs = millis();

  if (currentMs - lastReadMs < READ_INTERVAL_MS) {
    return;
  }

  lastReadMs = currentMs;

  float temperature = dht.readTemperature();
  float humidity = dht.readHumidity();

  if (isnan(temperature) || isnan(humidity) || temperature < -55 || temperature > 150 || humidity < 0 || humidity > 100) {
    Serial.print(F("{\"protocolVersion\":1,\"deviceId\":\""));
    Serial.print(DEVICE_ID);
    Serial.println(F("\",\"error\":\"DHT11_READ_FAILED\"}"));
    return;
  }

  // Una trama por linea, compatible con Climate.ArduinoBridge.
  // Sin reloj UTC en la placa: el puente asigna la fecha de recepcion.
  Serial.print(F("{\"protocolVersion\":1,\"deviceId\":\""));
  Serial.print(DEVICE_ID);
  Serial.print(F("\",\"sensorType\":\"TEMPERATURE\",\"value\":"));
  Serial.print(temperature, 2);
  Serial.print(F(",\"unit\":\"C\",\"source\":\"ARDUINO\",\"sequence\":"));
  Serial.print(sequenceNumber++);
  // Dato auxiliar visible en Monitor Serial; el puente actual solo persiste temperatura.
  Serial.print(F(",\"sensor\":\"DHT11\",\"humidity\":"));
  Serial.print(humidity, 2);
  Serial.println(F("}"));
}
