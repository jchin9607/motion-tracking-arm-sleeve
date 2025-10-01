#include <Wire.h>
#include "SparkFun_BNO080_Arduino_Library.h"

#define SDA_PIN 41
#define SCL_PIN 40

#define PCA9546_ADDR 0x70
#define BNO080_ADDR  0x4A
#define MUX_CHANNEL_0 0
#define MUX_CHANNEL_1 1
#define MUX_CHANNEL_2 2 
#define MUX_CHANNEL_3 3

BNO080 imu0;
BNO080 imu1;
BNO080 imu2;
BNO080 imu3;

void selectMuxChannel(uint8_t channel) {
  Wire.beginTransmission(PCA9546_ADDR);
  Wire.write(1 << channel);
  Wire.endTransmission();
  delay(10); // Let the channel switch settle
}

bool initSensor(BNO080 &imu, uint8_t channel, const char* name) {
  selectMuxChannel(channel);

  Serial.print("Initializing ");
  Serial.println(name);

  if (!imu.begin(BNO080_ADDR, Wire)) {
    Serial.print(name);
    Serial.println(" not detected. Check wiring.");
    return false;
  }

  imu.enableRotationVector(50);
  Serial.print(name);
  Serial.println(" initialized.");
  return true;
}

void setup() {
  Serial.begin(230400);
  while (!Serial);

  Serial.println("Starting dual BNO085 setup...");

  Wire.begin(SDA_PIN, SCL_PIN);
  Wire.setClock(600000); // 400kHz I2C


  if (!initSensor(imu0, MUX_CHANNEL_0, "Sensor 0")) {
    while (1); // Stop here if failed
  }

  if (!initSensor(imu1, MUX_CHANNEL_1, "Sensor 1")) {
    while (1); // Stop here if failed
  }

  if (!initSensor(imu2, MUX_CHANNEL_2, "Sensor 2")){
    while (1);
  }

  if (!initSensor(imu3, MUX_CHANNEL_3, "Sensor 3")){
    while (1);
  }
}

void readSensor(BNO080 &imu, uint8_t channel, const char* label) {
  selectMuxChannel(channel);

  if (imu.dataAvailable()) {
    float quatI = imu.getQuatI(); // x
    float quatJ = imu.getQuatJ(); // y
    float quatK = imu.getQuatK(); // z
    float quatReal = imu.getQuatReal(); // w

    // Optional: normalize (not strictly needed, sensor already gives normalized quat)
    float magnitude = sqrt(quatI * quatI + quatJ * quatJ + quatK * quatK + quatReal * quatReal);
    quatI /= magnitude;
    quatJ /= magnitude;
    quatK /= magnitude;
    quatReal /= magnitude;

    Serial.print(label);
    Serial.print(": ");
    Serial.print(quatReal, 6); Serial.print(",");
    Serial.print(quatI, 6);    Serial.print(",");
    Serial.print(quatJ, 6);    Serial.print(",");
    Serial.print(quatK, 6);    Serial.println();
  }
}



void loop() {
  readSensor(imu0, MUX_CHANNEL_0, "S0");
  readSensor(imu1, MUX_CHANNEL_1, "S1");
  readSensor(imu2, MUX_CHANNEL_2, "S2");
  readSensor(imu3, MUX_CHANNEL_3, "S3");
  delay(15); // ~50Hz loop
}
