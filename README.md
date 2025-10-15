# Motion Tracking Arm Sleeve

This is a final project for the MIT BWSI ETWT 2025 course/summer program.
(Massachusetts Institue of Technology: Beaver Works Summer Institue - E-Textiles and Wearable Technology course)

Our project uses 4 IMUs to map the arm and its movements to a 3D model on Unity

## Demo

![watch video here](media/demo.gif)

## Built by

- [Lucas (@jchin9607)](https://www.github.com/jchin9607)
- [Theo (@tp-saw)](https://www.github.com/tp-saw)

## Technology Used

**Hardware:** 4x Adafruit 9-DOF BNO085 IMU, Adafruit QT Py ESP32-S3, Adafruit PCA9546 4-Channel STEMMA I2C Multiplexer - We primarily soldered and used stemma cables

**Software:** Unity, Arduino (we had to edit the SparkFun BNO080 Library for it to work with multiple IMUs and a multiplexer)

## Markups

![technical diagram](/media/techdia.png)
![circuit diagram](/media/circuitdia.png)

## How it works

Each IMU sends back quarternion data (9 DOF) through the serial monitor, and we used a unity script to apply that data to a 3D model on Unity

Each line on the Serial Monitor has a prefix that indicates which IMU is sending that data.

Using a unity script, we scanned the line. We first looked at the prefix then the corresponding data.

The model on Unity was made with blender. We split the model into 4 parts: chest, bicep/shoulder, forearm, and wrist. In between each "part", we added a sphere to represent the joint. In Unity, the chest was the parent object, then the shoulder joint, then the shoulder, then the forearm/elbow joint, the forearm, then the wrist joint, then the wrist as children of each other.

The script applied the data to the joints, not the part. The script is located in the shoulder joint.

## Sewing

Theo sewed our glove, and then sewed it onto a compression shirt. We then sewed the IMUs to the compression shirt, and sewed velcro to hold the wires in place.

![compression](/media/compression.png)
![velcro](/media/velcro.png)
![clean](/media/clean.png)

## Problems

Our parts were not that compatible with each other. The libraries that supported the IMU could only process one IMU at a time so we had to edit the Sparkfun BNO080 library to make it work.

Also, we had faulty stemma wires so any force that tugged on the wire disconnected the IMU. We tried soldering, but it didn't work. To fix this we had to make sure the area where the wire was connected did not move and we used cardboard and tape to immobilize it (a little scuffed but it worked).
