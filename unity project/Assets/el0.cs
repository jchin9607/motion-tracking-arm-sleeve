using UnityEngine;

public class el0 : MonoBehaviour
{
    [Header("IMU Configuration")]
    public string imuPrefix; // Like "IMU1:"
    
    [Header("Axis Inversion")]
    public bool invertX, invertY, invertZ;
    
    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float smoothing = 0.1f;
    
    [Header("Calibration")]
    public bool calibrated = false;
    public bool autoCalibrate = true;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    private Quaternion initial = Quaternion.identity;
    private Quaternion current = Quaternion.identity;
    private Quaternion target = Quaternion.identity;
    private Vector4 lastRawData;
    
    void Start()
    {
        // Initialize current rotation to match transform
        current = transform.rotation;
    }
    
    public void UpdateRotation(Vector4 qRaw)
    {
        lastRawData = qRaw;
        
        // Check if quaternion data is valid (non-zero and normalized-ish)
        float magnitude = Mathf.Sqrt(qRaw.x * qRaw.x + qRaw.y * qRaw.y + qRaw.z * qRaw.z + qRaw.w * qRaw.w);
        if (magnitude < 0.1f || magnitude > 2.0f)
        {
            if (showDebugInfo)
                Debug.LogWarning($"{gameObject.name}: Invalid quaternion magnitude: {magnitude}");
            return;
        }
        
        // Create quaternion - try different orderings if this doesn't work
        // Common orderings: (w,x,y,z), (x,y,z,w), or your current (y,z,w,x)
        Quaternion q = new Quaternion(qRaw.y, qRaw.z, qRaw.w, qRaw.x);
        
        // Alternative orderings to try if above doesn't work:
        // Quaternion q = new Quaternion(qRaw.x, qRaw.y, qRaw.z, qRaw.w); // Standard w,x,y,z
        // Quaternion q = new Quaternion(qRaw.w, qRaw.x, qRaw.y, qRaw.z); // x,y,z,w input
        
        // Normalize the quaternion
        q = q.normalized;
        
        // Apply axis inversions
        if (invertX) q.x *= -1;
        if (invertY) q.y *= -1;
        if (invertZ) q.z *= -1;
        
        // Auto-calibration or manual calibration
        if (!calibrated && autoCalibrate)
        {
            initial = q;
            calibrated = true;
            if (showDebugInfo)
                Debug.Log($"{gameObject.name}: Calibrated with initial rotation: {initial}");
        }
        
        if (calibrated)
        {
            // Calculate relative rotation from initial position
            target = Quaternion.Inverse(initial) * q;
            
            // Apply smoothing
            if (smoothing > 0f)
            {
                current = Quaternion.Slerp(current, target, Time.deltaTime / smoothing);
            }
            else
            {
                current = target;
            }
            
            // Apply rotation to transform
            transform.rotation = current;
            
            if (showDebugInfo && Time.frameCount % 60 == 0) // Debug every 60 frames
            {
                Debug.Log($"{gameObject.name}: Raw: {qRaw}, Processed: {q}, Target: {target}, Current: {current}");
            }
        }
    }
    
    public void ResetCalibration()
    {
        calibrated = false;
        current = Quaternion.identity;
        target = Quaternion.identity;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name}: Calibration reset");
    }
    
    public void CalibrateNow()
    {
        calibrated = false; // This will trigger recalibration on next update
    }
    
    // Method to test different quaternion orderings
    public void TestQuaternionOrdering(int orderingType)
    {
        if (lastRawData == Vector4.zero) return;
        
        Quaternion q;
        switch (orderingType)
        {
            case 0: // w,x,y,z
                q = new Quaternion(lastRawData.x, lastRawData.y, lastRawData.z, lastRawData.w);
                break;
            case 1: // x,y,z,w  
                q = new Quaternion(lastRawData.w, lastRawData.x, lastRawData.y, lastRawData.z);
                break;
            case 2: // Your current: y,z,w,x
                q = new Quaternion(lastRawData.y, lastRawData.z, lastRawData.w, lastRawData.x);
                break;
            default:
                return;
        }
        
        Debug.Log($"Ordering {orderingType}: {q}");
    }
    
    void OnValidate()
    {
        // Clamp smoothing value
        smoothing = Mathf.Clamp01(smoothing);
    }
}