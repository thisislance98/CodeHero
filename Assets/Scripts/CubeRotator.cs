using UnityEngine;

public class CubeRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationSpeed = new Vector3(0, 50, 0); // Degrees per second
    
    void Update()
    {
        // Rotate the cube continuously
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}