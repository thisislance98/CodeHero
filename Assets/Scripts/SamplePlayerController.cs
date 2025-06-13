using UnityEngine;

public class SamplePlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float mouseSensitivity = 2f;
    
    [Header("Ground Check")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayerMask = 1;
    
    [Header("Components")]
    private Rigidbody rb;
    private Camera playerCamera;
    private bool isGrounded;
    private float xRotation = 0f;
    
    void Start()
    {
        // Get required components
        rb = GetComponent<Rigidbody>();
        playerCamera = GetComponentInChildren<Camera>();
        
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        
        // Create ground check point if not assigned
        if (groundCheckPoint == null)
        {
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(transform);
            groundCheck.transform.localPosition = new Vector3(0, -1f, 0);
            groundCheckPoint = groundCheck.transform;
        }
    }

    void Update()
    {
        HandleMouseLook();
        HandleInput();
        CheckGrounded();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMouseLook()
    {
        if (playerCamera == null) return;
        
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player body left/right
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera up/down
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandleInput()
    {
        // Jump input
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
        }

        // Sample interaction input
        if (Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
        
        // Toggle cursor lock
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
    }

    void HandleMovement()
    {
        if (rb == null) return;
        
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calculate movement direction
        Vector3 direction = (transform.right * horizontal + transform.forward * vertical).normalized;

        // Apply movement
        Vector3 targetVelocity = direction * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; // Preserve vertical velocity
        
        rb.linearVelocity = targetVelocity;
    }

    void Jump()
    {
        if (rb != null && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            Debug.Log("Player jumped!");
        }
    }

    void CheckGrounded()
    {
        if (groundCheckPoint == null) return;
        
        isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayerMask);
    }

    void Interact()
    {
        // Sample interaction - check for objects in front of player
        RaycastHit hit;
        float interactRange = 3f;
        
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, interactRange))
        {
            Debug.Log($"Interacting with: {hit.collider.name}");
            
            // Example: If object has a specific component, do something
            var interactable = hit.collider.GetComponent<SampleInteractable>();
            if (interactable != null)
            {
                interactable.OnInteract();
            }
        }
    }

    void ToggleCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Visualize ground check in scene view
    void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}