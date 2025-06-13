using UnityEngine;

public class SpinCube : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 50f;
    public Vector3 rotationAxis = Vector3.up;
    
    void Update()
    {
        // Rotate the cube continuously
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
    }
}