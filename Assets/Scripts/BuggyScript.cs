using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuggyScript : MonoBehaviour
{
    public float speed = 5.0f;
    public string playerName;
    
    void Start()
    {
        Debug.Log("Starting buggy script");
        // Fixed: Added missing semicolon
        playerName = "Player1";
        
        // Another syntax error - missing closing brace for if statement
        if (speed > 0)
        {
            Debug.Log("Speed is positive");
        } // Fixed: Added missing closing brace for if statement
        
        // Fixed: Added variable type declaration
        int incorrectVariable = 10;
    }
    
    void Update()
    {
        // Fixed: Corrected method call with proper parentheses
        transform.Rotate(0, speed * Time.deltaTime, 0);
        
        // Fixed: Corrected method name typo
        Debug.Log("Update called");
    }
    
} // Fixed: Added missing closing brace for the class