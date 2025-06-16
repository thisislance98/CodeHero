using UnityEngine;

/// <summary>
/// Sample Unity script demonstrating common Unity patterns including:
/// - Input handling
/// - Transform manipulation
/// - Component references
/// - Unity lifecycle methods
/// - Public/serialized fields for Inspector
/// </summary>
public class SamplePlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float rotationSpeed = 100f;
    
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer = 1;
    
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Renderer playerRenderer;
    
    // Private variables
    private Vector3 moveDirection;
    private bool isGrounded;
    private Color originalColor;
    
    /// <summary>
    /// Unity's Start method - called once when the GameObject becomes active
    /// </summary>
    void Start()
    {
        // Get components if not assigned in inspector
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        
        if (playerRenderer == null)
            playerRenderer = GetComponent<Renderer>();
        
        // Store original color
        if (playerRenderer != null)
            originalColor = playerRenderer.material.color;
        
        // Create ground check point if not assigned
        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = Vector3.down * 0.5f;
            groundCheck = groundCheckObj.transform;
        }
        
        Debug.Log($"SamplePlayerController initialized for {gameObject.name}");
    }
    
    /// <summary>
    /// Unity's Update method - called once per frame
    /// Handle input and non-physics updates here
    /// </summary>
    void Update()
    {
        HandleInput();
        CheckGrounded();
        HandleColorChange();
    }
    
    /// <summary>
    /// Unity's FixedUpdate method - called at fixed intervals
    /// Handle physics-related updates here
    /// </summary>
    void FixedUpdate()
    {
        MovePlayer();
    }
    
    /// <summary>
    /// Handle player input
    /// </summary>
    private void HandleInput()
    {
        // Get input axes (WASD or arrow keys)
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Create movement direction
        moveDirection = new Vector3(horizontal, 0, vertical).normalized;
        
        // Handle jumping
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
        }
        
        // Handle rotation with Q and E keys
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Move the player based on input
    /// </summary>
    private void MovePlayer()
    {
        if (rb != null && moveDirection != Vector3.zero)
        {
            // Move relative to player's current rotation
            Vector3 moveVector = transform.TransformDirection(moveDirection) * moveSpeed;
            moveVector.y = rb.velocity.y; // Preserve current Y velocity
            
            rb.velocity = moveVector;
        }
    }
    
    /// <summary>
    /// Make the player jump
    /// </summary>
    private void Jump()
    {
        if (rb != null)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            Debug.Log("Player jumped!");
        }
    }
    
    /// <summary>
    /// Check if player is on the ground
    /// </summary>
    private void CheckGrounded()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        }
    }
    
    /// <summary>
    /// Change color when moving (visual feedback)
    /// </summary>
    private void HandleColorChange()
    {
        if (playerRenderer != null)
        {
            if (moveDirection != Vector3.zero)
            {
                // Change to green when moving
                playerRenderer.material.color = Color.green;
            }
            else
            {
                // Return to original color when not moving
                playerRenderer.material.color = originalColor;
            }
        }
    }
    
    /// <summary>
    /// Unity's OnDrawGizmosSelected - draws debug information in Scene view
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // Draw ground check sphere
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        
        // Draw movement direction
        if (moveDirection != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(moveDirection) * 2f);
        }
    }
    
    /// <summary>
    /// Example of a public method that can be called from other scripts
    /// </summary>
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0, newSpeed);
        Debug.Log($"Move speed set to: {moveSpeed}");
    }
    
    /// <summary>
    /// Example of a property getter
    /// </summary>
    public bool IsGrounded => isGrounded;
    
    /// <summary>
    /// Example of handling collisions
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Hit an enemy!");
            // Handle enemy collision logic here
        }
    }
    
    /// <summary>
    /// Example of handling trigger events
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Collectible"))
        {
            Debug.Log("Collected an item!");
            // Handle collectible logic here
            Destroy(other.gameObject);
        }
    }
}