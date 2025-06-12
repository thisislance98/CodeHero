using UnityEngine;

public class ObjectRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationSpeed = new Vector3(0, 50, 0);
    public bool rotateInLocalSpace = true;
    
    [Header("Animation Settings")]
    public bool useRandomSpeed = false;
    public float minSpeed = 10f;
    public float maxSpeed = 100f;
    
    private Vector3 actualRotationSpeed;
    
    void Start()
    {
        // If random speed is enabled, randomize the rotation speed
        if (useRandomSpeed)
        {
            actualRotationSpeed = new Vector3(
                Random.Range(-maxSpeed, maxSpeed),
                Random.Range(-maxSpeed, maxSpeed),
                Random.Range(-maxSpeed, maxSpeed)
            );
        }
        else
        {
            actualRotationSpeed = rotationSpeed;
        }
    }
    
    void Update()
    {
        // Rotate the object based on the settings
        if (rotateInLocalSpace)
        {
            transform.Rotate(actualRotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.Rotate(actualRotationSpeed * Time.deltaTime, Space.World);
        }
    }
    
    // Method to change rotation speed at runtime
    public void SetRotationSpeed(Vector3 newSpeed)
    {
        actualRotationSpeed = newSpeed;
    }
    
    // Method to toggle rotation
    public void ToggleRotation()
    {
        enabled = !enabled;
    }
}