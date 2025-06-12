using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyntaxErrorExample : MonoBehaviour
{
    public int playerHealth = 100;
    public string playerName = "Hero";
    
    void Start()
    {
        Debug.Log("Game Started!");
        
        // Fixed: Added missing semicolon
        int score = 0;
        
        // Fixed: Added missing closing parenthesis
        if (playerHealth > 50)
        {
            Debug.Log("Player is healthy");
        }
        
        // Fixed: Added missing closing brace for the Start method
    }
    
    void Update()
    {
        // Fixed: Changed = to == for comparison
        if (playerHealth == 0)
        {
            Debug.Log("Game Over!");
        }
        
        // Fixed: Added proper string concatenation with quotes
        Debug.Log("Player name is: " + playerName);
    }
}