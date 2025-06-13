using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyntaxErrorScript : MonoBehaviour
{
    public float speed = 5.0f;
    public string playerName;
    private int definedVariable; // Fixed: defined the variable properly
    
    void Start()
    {
        Debug.Log("Starting script with syntax errors");
        // Fixed: Added semicolon
        playerName = "Player1";
        
        // Fixed: Added closing brace for if statement
        if (speed > 0)
        {
            Debug.Log("Speed is positive"); // Fixed: Added semicolon
        } // Fixed: Added missing closing brace
        
        // Fixed: Use properly defined variable
        definedVariable = 10;
    } // Fixed: Proper closing brace for Start method
    
    // Fixed: Moved Update method outside of Start and corrected spelling
    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }
} // Fixed: Added missing closing brace for the class