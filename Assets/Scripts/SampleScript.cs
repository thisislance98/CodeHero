using UnityEngine;

public class SampleScript : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 90f;
    
    [Header("Color Settings")]
    public Color targetColor = Color.red;
    
    private Renderer objectRenderer;
    private Vector3 startPosition;
    
    void Start()
    {
        // Store the starting position
        startPosition = transform.position;
        
        // Get the renderer component for color changes
        objectRenderer = GetComponent<Renderer>();
        
        Debug.Log("SampleScript started on " + gameObject.name);
    }
    
    void Update()
    {
        // Move the object up and down using a sine wave
        float newY = startPosition.y + Mathf.Sin(Time.time * moveSpeed) * 2f;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        // Rotate the object continuously
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
        
        // Change color over time
        if (objectRenderer != null)
        {
            float colorLerp = (Mathf.Sin(Time.time * 2f) + 1f) / 2f;
            objectRenderer.material.color = Color.Lerp(Color.white, targetColor, colorLerp);
        }
        
        // Simple input handling
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetPosition();
        }
    }
    
    public void ResetPosition()
    {
        transform.position = startPosition;
        Debug.Log("Position reset for " + gameObject.name);
    }
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log(gameObject.name + " triggered by " + other.gameObject.name);
    }
}