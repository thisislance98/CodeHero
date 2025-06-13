using UnityEngine;

public class CubeSpinner : MonoBehaviour
{
    public float spinSpeed = 90f; // Degrees per second
    public Vector3 spinAxis = Vector3.up; // Default to spinning around Y-axis
    
    void Update()
    {
        // Rotate the cube continuously
        transform.Rotate(spinAxis * spinSpeed * Time.deltaTime);
    }
}