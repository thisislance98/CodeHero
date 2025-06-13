using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float mouseSensitivity = 2f;
    
    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    [Header("Components")]
    private Rigidbody rb;
    private Camera playerCamera;
    
    [Header("State")]
    private bool isGrounded;
    private float xRotation = 0f;
    private Vector3 velocity;
    
    void Start()
    {
        // Get required components
        rb = GetComponent<Rigidbody>();
        playerCamera = GetComponentInChildren<Camera>();
        
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        
        // Create ground check if it doesn't exist
        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = new Vector3(0, -1f, 0);
            groundCheck = groundCheckObj.transform;
        }
    }
    
    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleJump();
        CheckGrounded();
    }
    
    void HandleMouseLook()
    {
        if (playerCamera == null) return;
        
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate the player body left and right
        transform.Rotate(Vector3.up * mouseX);
        
        // Rotate the camera up and down
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
    
    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 direction = transform.right * horizontal + transform.forward * vertical;
        Vector3 moveVelocity = direction * moveSpeed;
        
        // Apply movement while preserving Y velocity for gravity/jumping
        velocity.x = moveVelocity.x;
        velocity.z = moveVelocity.z;
        
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
        else
        {
            // Fallback for non-rigidbody movement
            transform.Translate(direction * moveSpeed * Time.deltaTime, Space.Self);
        }
    }
    
    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded && rb != null)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }
    
    void CheckGrounded()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw ground detection sphere in scene view
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
    
    // Public methods for external scripts
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
    
    public void SetJumpForce(float newForce)
    {
        jumpForce = newForce;
    }
    
    public bool IsGrounded()
    {
        return isGrounded;
    }
    
    public Vector3 GetVelocity()
    {
        return rb != null ? rb.linearVelocity : velocity;
    }
}