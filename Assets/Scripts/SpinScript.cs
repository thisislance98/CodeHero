using UnityEngine;

public class SpinScript : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 90f; // degrees per second
    [SerializeField] private Vector3 spinAxis = Vector3.up; // default spin around Y-axis
    
    void Update()
    {
        // Rotate the object around the specified axis at the specified speed
        transform.Rotate(spinAxis * spinSpeed * Time.deltaTime);
    }
}