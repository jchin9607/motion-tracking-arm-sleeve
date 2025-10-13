using UnityEngine;
using System.IO.Ports;
using System;
using System.Globalization;

public class ISM330DHCXReader : MonoBehaviour
{
    [Header("Serial Configuration")]
    [SerializeField] private string portName = "/dev/cu.usbmodem101";
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private int readTimeout = 500;
    
    [Header("IMU Data")]
    [SerializeField] private Vector3 accel; // in g (9.81 m/s²)
    [SerializeField] private Vector3 gyro;  // in degrees per second
    
    [Header("ISM330DHCX Settings")]
    [SerializeField] private bool dataInMilliUnits = false; // Your data is already in correct units (g and dps)
    [SerializeField] private float gyroSensitivity = 1.0f; // Should be 1.0 for 1:1 rotation
    [SerializeField] private float deadZone = 0.1f; // Small dead zone to filter noise
    
    [Header("Rotation Settings")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private bool useAccelerometer = false;
    [SerializeField] private float accelerometerSmoothing = 0.1f;
    [SerializeField] private bool debugValues = true;
    
    private SerialPort serialPort;
    private bool isConnected = false;
    private Vector3 gyroOffset = Vector3.zero; // For calibration
    private bool isCalibrated = false;
    
    // For integrated rotation tracking
    private Vector3 currentRotation = Vector3.zero; // Accumulated rotation in degrees
    
    // Events
    public System.Action<Vector3, Vector3> OnIMUDataReceived;
    
    void Start()
    {
        InitializeSerialPort();
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
            Debug.Log($"ISM330DHCX connected on {portName} at {baudRate} baud.");
            
            serialPort.DiscardInBuffer();
            
            // Auto-calibrate after 2 seconds
            Invoke(nameof(CalibrateGyro), 2f);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to ISM330DHCX on {portName}: {e.Message}");
            isConnected = false;
        }
    }
    
    void Update()
    {
        if (!isConnected || serialPort == null || !serialPort.IsOpen)
            return;
            
        ReadIMUData();
    }
    
    void ReadIMUData()
    {
        try
        {
            if (serialPort.BytesToRead > 0)
            {
                string line = serialPort.ReadLine().Trim();
                
                if (!string.IsNullOrEmpty(line))
                {
                    ParseISM330DHCXData(line);
                }
            }
        }
        catch (TimeoutException)
        {
            // Normal when no new data
        }
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
    
    void ParseISM330DHCXData(string data)
    {
        string[] values = data.Split(',');
        
        if (values.Length != 6)
        {
            if (debugValues)
                Debug.LogWarning($"Unexpected data format ({values.Length} values): {data}");
            return;
        }
        
        try
        {
            // Parse accelerometer data (expecting g or milli-g)
            Vector3 rawAccel = new Vector3(
                float.Parse(values[0], CultureInfo.InvariantCulture),
                float.Parse(values[1], CultureInfo.InvariantCulture),
                float.Parse(values[2], CultureInfo.InvariantCulture)
            );
            
            // Parse gyroscope data (expecting dps or milli-dps)
            Vector3 rawGyro = new Vector3(
                float.Parse(values[3], CultureInfo.InvariantCulture),
                float.Parse(values[4], CultureInfo.InvariantCulture),
                float.Parse(values[5], CultureInfo.InvariantCulture)
            );
            
            // Convert from milli-units if needed
            if (dataInMilliUnits)
            {
                rawAccel /= 1000f; // milli-g to g
                rawGyro /= 1000f;  // milli-dps to dps
            }
            
            // Apply smoothing to accelerometer
            accel = Vector3.Lerp(accel, rawAccel, accelerometerSmoothing);
            
            // Apply calibration offset to gyro ONLY if calibrated
            if (isCalibrated)
            {
                gyro = rawGyro - gyroOffset;
            }
            else
            {
                gyro = rawGyro; // Use raw values until calibrated
            }
            
            if (debugValues)
            {
                Debug.Log($"Raw Gyro: {rawGyro.ToString("F4")}°/s | Calibrated: {gyro.ToString("F4")}°/s | Magnitude: {gyro.magnitude:F4}");
                Debug.Log($"Accel: {accel.ToString("F3")}g | Dead Zone: {deadZone:F3}");
                
                if (gyro.magnitude > deadZone)
                {
                    Debug.Log($"<color=green>GYRO ACTIVE - Magnitude {gyro.magnitude:F4} > {deadZone:F3}</color>");
                }
                else
                {
                    Debug.Log($"<color=yellow>Gyro below threshold - Magnitude {gyro.magnitude:F4} <= {deadZone:F3}</color>");
                }
            }
            
            OnIMUDataReceived?.Invoke(accel, gyro);
            
            if (enableRotation)
            {
                ApplyRotation();
            }
        }
        catch (FormatException e)
        {
            if (debugValues)
                Debug.LogWarning($"Failed to parse ISM330DHCX data: {data} - Error: {e.Message}");
        }
    }
    
    void ApplyRotation()
    {
        if (useAccelerometer)
        {
            ApplyAccelerometerRotation();
        }
        else
        {
            ApplyGyroscopeRotation();
        }
    }
    
    void ApplyGyroscopeRotation()
    {
        // Skip if not calibrated yet
        if (!isCalibrated)
        {
            if (debugValues)
                Debug.Log("<color=orange>Waiting for calibration...</color>");
            return;
        }
        
        // Apply dead zone to reduce noise
        Vector3 filteredGyro = gyro;
        
        // Apply dead zone based on magnitude
        if (filteredGyro.magnitude < deadZone)
        {
            if (debugValues && Time.frameCount % 60 == 0) // Only log every 60 frames to reduce spam
                Debug.Log($"<color=gray>Gyro magnitude {filteredGyro.magnitude:F4} below dead zone {deadZone:F3}</color>");
            return;
        }
        
        // Integrate angular velocity to get angular position
        // gyro is in degrees/second, so multiply by deltaTime to get degrees moved this frame
        Vector3 rotationDelta = filteredGyro * Time.deltaTime * gyroSensitivity;
        
        // Accumulate the rotation
        currentRotation += rotationDelta;
        
        // Apply the accumulated rotation to the transform
        // Note: You may need to swap/negate axes depending on your IMU orientation
        // Common IMU to Unity axis mappings (test and adjust as needed):
        Quaternion targetRotation = Quaternion.Euler(-currentRotation.x, -currentRotation.y, currentRotation.z);
        transform.rotation = targetRotation;
        
        if (debugValues && rotationDelta.magnitude > 0.01f)
        {
            Debug.Log($"<color=cyan>Delta: {rotationDelta.ToString("F2")}° | Total: {currentRotation.ToString("F1")}° | Gyro: {filteredGyro.ToString("F2")}°/s</color>");
        }
    }
    
    void ApplyAccelerometerRotation()
    {
        // Calculate tilt from gravity vector
        if (accel.magnitude < 0.5f) return; // Skip if accelerometer data seems invalid
        
        float pitch = Mathf.Atan2(-accel.x, Mathf.Sqrt(accel.y * accel.y + accel.z * accel.z)) * Mathf.Rad2Deg;
        float roll = Mathf.Atan2(accel.y, accel.z) * Mathf.Rad2Deg;
        
        Vector3 targetRotation = new Vector3(pitch, 0, roll);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(targetRotation), Time.deltaTime * 2f);
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
                Debug.Log("ISM330DHCX serial port closed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing serial port: {e.Message}");
            }
        }
        isConnected = false;
    }
    
    // Public methods
    public void CalibrateGyro()
    {
        if (isConnected)
        {
            gyroOffset = gyro; // Set current reading as zero point
            isCalibrated = true;
            Debug.Log($"<color=green>ISM330DHCX gyroscope calibrated. Offset: {gyroOffset.ToString("F4")}°/s</color>");
        }
    }
    
    public void ResetRotation()
    {
        transform.rotation = Quaternion.identity;
        currentRotation = Vector3.zero; // Reset accumulated rotation
        Debug.Log("Object rotation and accumulated rotation reset");
    }
    
    public Vector3 GetAcceleration() => accel;
    public Vector3 GetGyroscope() => gyro;
    public Vector3 GetCurrentRotation() => currentRotation; // Get accumulated rotation
    public bool IsConnected() => isConnected;
    public bool IsCalibrated() => isCalibrated;
    
    // Unity lifecycle
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