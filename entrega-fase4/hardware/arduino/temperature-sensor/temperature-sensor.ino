// Referencia: Arduino Uno, LM35 en A0 (VCC 5V, GND común).
// SIMULATE_SENSOR=1 genera datos de prueba sin conectar sensores.
#define SIMULATE_SENSOR 1
#define SENSOR_PIN A0
#define SERIAL_BAUD 9600
#define SAMPLE_INTERVAL_MS 2000UL
#define ADC_REFERENCE_VOLTS 5.0
#define DEVICE_ID "arduino-temp-001"

unsigned long previousSample = 0;
unsigned long sequenceNumber = 0;

void setup() {
  Serial.begin(SERIAL_BAUD);
  pinMode(SENSOR_PIN, INPUT);
}

void loop() {
  unsigned long now = millis();
  if (now - previousSample < SAMPLE_INTERVAL_MS) return;
  previousSample = now;
  float temperature;
#if SIMULATE_SENSOR
  // Oscila entre 20 y 40 °C para probar también las reglas de alerta.
  temperature = 30.0 + 10.0 * sin(sequenceNumber * 0.15);
#else
  temperature = analogRead(SENSOR_PIN) * (ADC_REFERENCE_VOLTS / 1023.0) * 100.0;
#endif
  if (isnan(temperature) || temperature < -55 || temperature > 150) {
    Serial.println(F("{\"protocolVersion\":1,\"deviceId\":\"" DEVICE_ID "\",\"error\":\"INVALID_TEMPERATURE\"}"));
    return;
  }
  Serial.print(F("{\"protocolVersion\":1,\"deviceId\":\"" DEVICE_ID "\",\"sensorType\":\"TEMPERATURE\",\"value\":"));
  Serial.print(temperature, 2);
  Serial.print(F(",\"unit\":\"C\",\"source\":\""));
#if SIMULATE_SENSOR
  Serial.print(F("SIMULATED"));
#else
  Serial.print(F("ARDUINO"));
#endif
  Serial.print(F("\",\"sequence\":"));
  Serial.print(sequenceNumber++);
  Serial.println(F("}"));
}
