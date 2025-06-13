using UnityEngine;

public class SpinningCube : MonoBehaviour
{
    [Header("Spin Settings")]
    public Vector3 spinSpeed = new Vector3(0, 90, 0); // Degrees per second
    public bool useLocalSpace = true;
    
    void Update()
    {
        // Rotate the cube based on the spin speed
        if (useLocalSpace)
        {
            transform.Rotate(spinSpeed * Time.deltaTime);
        }
        else
        {
            transform.Rotate(spinSpeed * Time.deltaTime, Space.World);
        }
    }
}