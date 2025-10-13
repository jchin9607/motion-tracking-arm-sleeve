using UnityEngine;
using System.IO.Ports;
public class reader2 : MonoBehaviour
{
    SerialPort serial;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string portName = "/dev/cu.usbmodem101";
    public int baudRate = 115200;
    void Start()
    {
        serial = new SerialPort(portName, baudRate);
        serial.ReadTimeout = 500;
        try
        {
            serial.Open();
            Debug.Log("Serial port opened.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to open serial port: " + e.Message);
        }
        }
        // Update is called once per frame
        void Update()
        {
            if (serial != null && serial.IsOpen)
        {
        try
        {
            string line = serial.ReadLine();
            Debug.Log("Received: " + line);
            string[] parts = line.Split(',');
            float ax = float.Parse(parts[0]);
            float ay = float.Parse(parts[1]);
            float az = float.Parse(parts[2]);
            float gx = float.Parse(parts[3]);
            float gy = float.Parse(parts[4]);
            float gz = float.Parse(parts[5]);
            Debug.Log("Accelerometer: " + ax + ", " + ay + ", " + az);
            Debug.Log("Gyroscope: " + gx + ", " + gy + ", " + gz);
            Vector3 gyro = new Vector3(gx, gy, gz) * Time.deltaTime * 2f;
            transform.Rotate(gyro, Space.Self);
        }
        catch (System.TimeoutException)
        {
         Debug.Log("Timeout reading serial data.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error reading serial data: " + e.Message);
        }
        }
    }
    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen)
        {
            serial.Close();
            Debug.Log("Serial port closed.");
        }
    }
}