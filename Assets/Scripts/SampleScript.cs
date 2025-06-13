using UnityEngine;

public class SampleScript : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 100f;
    
    [Header("Physics Settings")]
    public bool useGravity = true;
    public float jumpForce = 10f;
    
    [Header("Visual Settings")]
    public Color highlightColor = Color.yellow;
    public Material originalMaterial;
    
    private Rigidbody rb;
    private Renderer objectRenderer;
    private bool isGrounded = true;
    
    void Start()
    {
        // Get components
        rb = GetComponent<Rigidbody>();
        objectRenderer = GetComponent<Renderer>();
        
        // Store original material
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
        }
        
        // Configure rigidbody
        if (rb != null)
        {
            rb.useGravity = useGravity;
        }
        
        Debug.Log("SampleScript initialized on " + gameObject.name);
    }
    
    void Update()
    {
        HandleInput();
        HandleMovement();
    }
    
    void HandleInput()
    {
        // Jump input
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && rb != null)
        {
            Jump();
        }
        
        // Highlight toggle
        if (Input.GetKeyDown(KeyCode.H))
        {
            ToggleHighlight();
        }
        
        // Reset position
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPosition();
        }
    }
    
    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Movement
        Vector3 movement = new Vector3(horizontal, 0, vertical) * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.World);
        
        // Rotation
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(0, -rotationSpeed * Time.deltaTime, 0);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        }
    }
    
    void Jump()
    {
        if (rb != null && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            Debug.Log(gameObject.name + " jumped!");
        }
    }
    
    void ToggleHighlight()
    {
        if (objectRenderer != null)
        {
            if (objectRenderer.material.color == highlightColor)
            {
                objectRenderer.material = originalMaterial;
            }
            else
            {
                objectRenderer.material.color = highlightColor;
            }
        }
    }
    
    void ResetPosition()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        Debug.Log(gameObject.name + " position reset!");
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
        
        Debug.Log(gameObject.name + " collided with " + collision.gameObject.name);
    }
    
    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log(gameObject.name + " triggered by " + other.gameObject.name);
    }
    
    // Public method that can be called from other scripts
    public void CustomAction(string message)
    {
        Debug.Log(gameObject.name + " received message: " + message);
        
        // Example: Scale pulse effect
        StartCoroutine(ScalePulse());
    }
    
    System.Collections.IEnumerator ScalePulse()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;
        
        // Scale up
        float elapsedTime = 0;
        while (elapsedTime < 0.2f)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsedTime / 0.2f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Scale down
        elapsedTime = 0;
        while (elapsedTime < 0.2f)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsedTime / 0.2f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = originalScale;
    }
}