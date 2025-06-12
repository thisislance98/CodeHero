using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuggyScript : MonoBehaviour
{
    public int health = 100;
    public string playerName;
    
    void Start()
    {
        Debug.Log("Player health: " + health);
        // Fixed: Added missing semicolon
        playerName = "Hero";
        
        // Fixed: Commented out non-existent method call
        // DoSomethingThatDoesntExist();
        
        // Fixed: Corrected variable type assignment
        // health = "not a number"; // This would cause a type error
        health = 50; // Properly assigning an integer value
    }
    
    void Update()
    {
        // Fixed: Added missing closing brace for Update method
    }
}