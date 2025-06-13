using UnityEngine;

public class SampleInteractable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public string interactionMessage = "Press E to interact";
    public bool canInteract = true;
    public float cooldownTime = 1f;
    
    [Header("Visual Feedback")]
    public Color highlightColor = Color.yellow;
    public float highlightIntensity = 1.5f;
    
    private Renderer objectRenderer;
    private Color originalColor;
    private Material originalMaterial;
    private bool isHighlighted = false;
    private float lastInteractionTime;
    
    void Start()
    {
        // Get renderer component for visual feedback
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            originalColor = originalMaterial.color;
        }
    }

    public void OnInteract()
    {
        // Check if interaction is allowed
        if (!canInteract || Time.time < lastInteractionTime + cooldownTime)
        {
            Debug.Log("Cannot interact right now.");
            return;
        }

        // Perform interaction
        Debug.Log($"Interacted with {gameObject.name}: {interactionMessage}");
        
        // Example interaction behaviors
        PerformInteraction();
        
        // Update last interaction time
        lastInteractionTime = Time.time;
    }

    void PerformInteraction()
    {
        // Example: Change color temporarily
        StartCoroutine(FlashColor());
        
        // Example: Rotate object
        transform.Rotate(0, 45f, 0);
        
        // Example: Scale effect
        StartCoroutine(ScalePulse());
        
        // Add your custom interaction logic here
    }

    System.Collections.IEnumerator FlashColor()
    {
        if (objectRenderer != null)
        {
            objectRenderer.material.color = highlightColor;
            yield return new WaitForSeconds(0.2f);
            objectRenderer.material.color = originalColor;
        }
    }

    System.Collections.IEnumerator ScalePulse()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;
        
        // Scale up
        float duration = 0.1f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Scale back down
        elapsed = 0f;
        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = originalScale;
    }

    public void SetInteractable(bool state)
    {
        canInteract = state;
    }

    public void Highlight(bool highlight)
    {
        if (objectRenderer == null) return;
        
        if (highlight && !isHighlighted)
        {
            objectRenderer.material.color = originalColor * highlightIntensity;
            isHighlighted = true;
        }
        else if (!highlight && isHighlighted)
        {
            objectRenderer.material.color = originalColor;
            isHighlighted = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Highlight(true);
            Debug.Log(interactionMessage);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Highlight(false);
        }
    }

    // Show interaction range in scene view
    void OnDrawGizmos()
    {
        Gizmos.color = canInteract ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }
}