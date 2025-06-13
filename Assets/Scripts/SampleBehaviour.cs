using UnityEngine;

/// <summary>
/// Sample Unity script demonstrating common functionality
/// This script can be attached to any GameObject to see basic Unity features in action
/// </summary>
public class SampleBehaviour : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 90f;
    
    [Header("Color Settings")]
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.red;
    [SerializeField] private float colorChangeSpeed = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private Renderer objectRenderer;
    private float colorTimer = 0f;
    private Vector3 originalPosition;
    
    void Start()
    {
        // Cache the renderer component
        objectRenderer = GetComponent<Renderer>();
        
        // Store original position
        originalPosition = transform.position;
        
        if (enableDebugLogs)
            Debug.Log($"SampleBehaviour started on {gameObject.name}");
    }
    
    void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleColorChange();
    }
    
    private void HandleMovement()
    {
        // Simple up and down movement using sine wave
        float verticalMovement = Mathf.Sin(Time.time * moveSpeed) * 2f;
        transform.position = originalPosition + Vector3.up * verticalMovement;
    }
    
    private void HandleRotation()
    {
        // Continuous rotation around Y axis
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
    
    private void HandleColorChange()
    {
        if (objectRenderer != null)
        {
            // Animate color change using PingPong
            colorTimer += Time.deltaTime * colorChangeSpeed;
            float lerpValue = Mathf.PingPong(colorTimer, 1f);
            Color currentColor = Color.Lerp(startColor, endColor, lerpValue);
            objectRenderer.material.color = currentColor;
        }
    }
    
    // Example of a public method that can be called from other scripts
    public void ResetPosition()
    {
        transform.position = originalPosition;
        if (enableDebugLogs)
            Debug.Log($"{gameObject.name} position reset!");
    }
    
    // Example of collision detection
    private void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs)
            Debug.Log($"{gameObject.name} triggered by {other.gameObject.name}");
    }
    
    // Example of mouse interaction
    private void OnMouseDown()
    {
        ResetPosition();
    }
    
    // Cleanup when object is destroyed
    private void OnDestroy()
    {
        if (enableDebugLogs)
            Debug.Log($"SampleBehaviour on {gameObject.name} destroyed");
    }
}