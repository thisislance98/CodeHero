using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyntaxErrorExample : MonoBehaviour
{
    public int health = 100;
    public string playerName = "Player";
    
    void Start()
    {
        Debug.Log("Starting game...");
        // Fixed: Added missing semicolon
        int score = 0;
        
        // Fixed: Added missing closing parenthesis
        if (health > 0)
        {
            Debug.Log("Player is alive!");
        }
        
        // String concatenation is correct
        Debug.Log("Welcome " + playerName);
    }
    
    void Update()
    {
        // Empty Update method
    }
}