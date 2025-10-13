using UnityEngine;
using System.IO.Ports;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

public class el1 : MonoBehaviour
{
    public string portName = "/dev/cu.usbmodem1101";
    public int baudRate = 230400;
    public List<el0> imuReceivers;
    
    private SerialPort serialPort;
    private string buffer = "";
    private Thread serialThread;
    private bool isRunning = false;
    private readonly object bufferLock = new object();

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 100; // Reduced timeout
            serialPort.Open();
            
            // Start background thread for reading
            isRunning = true;
            serialThread = new Thread(ReadSerialData);
            serialThread.Start();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open serial port: {e.Message}");
        }
    }

    // Background thread method for reading serial data
    void ReadSerialData()
    {
        while (isRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    string data = serialPort.ReadExisting();
                    lock (bufferLock)
                    {
                        buffer += data;
                    }
                }
                else
                {
                    Thread.Sleep(10); // Small delay when no data available
                }
            }
            catch (System.TimeoutException)
            {
                // Timeout is expected when no data is available, just continue
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Serial read error: {e.Message}");
                break;
            }
        }
    }

    void Update()
    {
        if (serialPort == null || !serialPort.IsOpen) return;

        string localBuffer;
        lock (bufferLock)
        {
            localBuffer = buffer;
            buffer = "";
        }

        if (string.IsNullOrEmpty(localBuffer)) return;

        // Process the buffer for complete lines
        buffer += localBuffer; // Add back to main buffer for line processing
        
        while (buffer.Contains("\n"))
        {
            int idx = buffer.IndexOf("\n");
            string line = buffer.Substring(0, idx).Trim();
            buffer = buffer.Substring(idx + 1);
            HandleLine(line);
        }
    }

    void HandleLine(string line)
    {
        foreach (var receiver in imuReceivers)
        {
            if (line.StartsWith(receiver.imuPrefix))
            {
                string data = line.Substring(receiver.imuPrefix.Length);
                string[] parts = data.Split(',');
                if (parts.Length != 4) return;
                
                try
                {
                    float w = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
                    receiver.UpdateRotation(new Vector4(w, x, y, z));
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to parse IMU data: {e.Message}");
                }
                break;
            }
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        
        if (serialThread != null && serialThread.IsAlive)
        {
            serialThread.Join(1000); // Wait up to 1 second for thread to finish
        }
        
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }

    void OnDestroy()
    {
        OnApplicationQuit();
    }
}