using UnityEngine;

public class SampleScript : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 90f;
    
    [Header("Color Settings")]
    public Color startColor = Color.white;
    public Color endColor = Color.red;
    public float colorChangeSpeed = 1f;
    
    private Renderer objectRenderer;
    private float colorTimer = 0f;
    
    void Start()
    {
        // Get the renderer component to change colors
        objectRenderer = GetComponent<Renderer>();
        
        // Set initial color
        if (objectRenderer != null)
        {
            objectRenderer.material.color = startColor;
        }
        
        Debug.Log("SampleScript started on " + gameObject.name);
    }
    
    void Update()
    {
        // Handle input for movement
        HandleMovement();
        
        // Handle rotation
        HandleRotation();
        
        // Handle color changing
        HandleColorChange();
        
        // Example of key press detection
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space key pressed! Object position: " + transform.position);
        }
    }
    
    void HandleMovement()
    {
        // Get input from arrow keys or WASD
        float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right arrows
        float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down arrows
        
        // Create movement vector
        Vector3 movement = new Vector3(horizontal, 0, vertical) * moveSpeed * Time.deltaTime;
        
        // Apply movement
        transform.Translate(movement);
    }
    
    void HandleRotation()
    {
        // Rotate the object continuously around Y-axis
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
    }
    
    void HandleColorChange()
    {
        if (objectRenderer != null)
        {
            // Update timer
            colorTimer += colorChangeSpeed * Time.deltaTime;
            
            // Use sine wave to smoothly transition between colors
            float lerpValue = (Mathf.Sin(colorTimer) + 1f) / 2f;
            
            // Interpolate between start and end colors
            Color currentColor = Color.Lerp(startColor, endColor, lerpValue);
            objectRenderer.material.color = currentColor;
        }
    }
    
    // Example of collision detection
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log(gameObject.name + " collided with " + collision.gameObject.name);
    }
    
    // Example of trigger detection
    void OnTriggerEnter(Collider other)
    {
        Debug.Log(gameObject.name + " trigger entered by " + other.gameObject.name);
    }
    
    // Public method that can be called from other scripts
    public void ResetPosition()
    {
        transform.position = Vector3.zero;
        Debug.Log("Position reset to origin");
    }
    
    // Example of custom method with parameters
    public void ChangeSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
        Debug.Log("Move speed changed to: " + newSpeed);
    }
}