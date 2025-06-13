using UnityEngine;

public class CubeSpin : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 90f; // Degrees per second
    public Vector3 rotationAxis = Vector3.up; // Default rotation around Y axis
    
    void Update()
    {
        // Rotate the cube around the specified axis
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
    }
}