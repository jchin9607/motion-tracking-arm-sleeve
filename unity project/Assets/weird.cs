using UnityEngine;
using System.IO.Ports;
using System;
using System.Globalization;
using System.Collections.Generic;

public class QuaternionReader : MonoBehaviour
{
    [Header("Serial Configuration")]
    [SerializeField] private string portName = "/dev/cu.usbmodem1101";
    [SerializeField] private int baudRate = 230400;
    [SerializeField] private int readTimeout = 500;

    [Header("Prefix/Object Mapping")]
    [SerializeField] private List<string> prefixes = new List<string>();
    [SerializeField] private List<Transform> targetObjects = new List<Transform>();

    [Header("Settings")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private float smoothingFactor = 0.0f;
    [SerializeField] private bool debugValues = true;

    [Header("Axis Mapping")]
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool invertZ = false;

    private SerialPort serialPort;
    private bool isConnected = false;
    private string dataBuffer = "";

    // Internal state tracking (by index)
    private List<Quaternion> currentQuaternions = new List<Quaternion>();
    private List<Quaternion> initialRotations = new List<Quaternion>();
    private List<bool> isCalibrated = new List<bool>();

    void Start()
    {
        InitializeSerialPort();

        // Ensure internal lists match mapping sizes
        for (int i = 0; i < prefixes.Count; i++)
        {
            currentQuaternions.Add(Quaternion.identity);
            initialRotations.Add(Quaternion.identity);
            isCalibrated.Add(false);
        }
    }

    void InitializeSerialPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = readTimeout;
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.Open();

            isConnected = true;
            Debug.Log($"Connected to {portName} at {baudRate} baud.");
            serialPort.DiscardInBuffer();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect: {e.Message}");
            isConnected = false;
        }
    }

    void Update()
    {
        if (!isConnected || serialPort == null || !serialPort.IsOpen)
            return;

        

        ReadQuaternionData();
    }

    void ReadQuaternionData()
    {
        try
        {
            if (serialPort.BytesToRead > 0)
            {
                string newData = serialPort.ReadExisting();
                dataBuffer += newData;

                while (dataBuffer.Contains("\n"))
                {
                    int lineEnd = dataBuffer.IndexOf("\n");
                    string line = dataBuffer.Substring(0, lineEnd).Trim();
                    dataBuffer = dataBuffer.Substring(lineEnd + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        ParseQuaternionLine(line);
                    }
                }
            }
        }
        catch (TimeoutException) { }
        catch (InvalidOperationException)
        {
            Debug.LogWarning("Serial connection lost. Attempting to reconnect...");
            ReconnectSerialPort();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Serial read error: {e.Message}");
        }
    }

    void ParseQuaternionLine(string data)
    {
        for (int i = 0; i < prefixes.Count; i++)
        {
            if (!data.StartsWith(prefixes[i]))
                continue;

            string[] values = data.Substring(prefixes[i].Length).Split(',');

            if (values.Length != 4)
            {
                if (debugValues)
                    Debug.LogWarning($"Incorrect value count for {prefixes[i]}: {data}");
                return;
            }

            try
            {
                float w = float.Parse(values[0], CultureInfo.InvariantCulture);
                float x = float.Parse(values[1], CultureInfo.InvariantCulture);
                float y = float.Parse(values[2], CultureInfo.InvariantCulture);
                float z = float.Parse(values[3], CultureInfo.InvariantCulture);

                if (invertX) x = -x;
                if (invertY) y = -y;
                if (invertZ) z = -z;

                Quaternion newQuat = new Quaternion(z, -y, x, w).normalized;

                // Calibrate if needed
                if (!isCalibrated[i])
                {
                    initialRotations[i] = newQuat;
                    isCalibrated[i] = true;
                    Debug.Log($"<color=blue>Calibrated {prefixes[i]}</color>");
                }

                Quaternion relativeQuat = Quaternion.Inverse(initialRotations[i]) * newQuat;
                relativeQuat.Normalize();

                currentQuaternions[i] = (smoothingFactor > 0)
                    ? Quaternion.Slerp(currentQuaternions[i], relativeQuat, 1f - smoothingFactor)
                    : relativeQuat;

                if (enableRotation && targetObjects[i] != null)
                    targetObjects[i].rotation = currentQuaternions[i];

                if (debugValues)
                {
                    Debug.Log($"<color=green>{prefixes[i]} → Quaternion: {relativeQuat.ToString("F4")}</color>");
                    Debug.Log($"<color=yellow>{prefixes[i]} → Euler: {currentQuaternions[i].eulerAngles.ToString("F1")}°</color>");
                }

                return;
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"Failed to parse data from {prefixes[i]}: {e.Message}");
            }
        }
    }

    void ReconnectSerialPort()
    {
        CloseSerialPort();
        Invoke(nameof(InitializeSerialPort), 2f);
    }

    void CloseSerialPort()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Close();
                Debug.Log("Serial port closed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing serial port: {e.Message}");
            }
        }
        isConnected = false;
    }

    public void ResetAllIMUs()
    {
        for (int i = 0; i < targetObjects.Count; i++)
        {
            if (targetObjects[i] != null)
                targetObjects[i].rotation = Quaternion.identity;

            currentQuaternions[i] = Quaternion.identity;
            isCalibrated[i] = false;
        }

        Debug.Log("Reset all IMUs.");
    }

    public void CalibrateIMU(string prefix)
    {
        int index = prefixes.IndexOf(prefix);
        if (index >= 0)
        {
            initialRotations[index] = currentQuaternions[index];
            isCalibrated[index] = true;
            Debug.Log($"Manually calibrated {prefix}");
        }
    }

    void OnApplicationQuit() => CloseSerialPort();

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) CloseSerialPort();
        else if (!isConnected) InitializeSerialPort();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CloseSerialPort();
        else if (!isConnected) InitializeSerialPort();
    }
}
