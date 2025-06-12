using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuggyScript : MonoBehaviour
{
    public int health = 100;
    public string playerName = "Player";
    
    void Start()
    {
        Debug.Log("Starting game for " + playerName);
        // Fixed: Added missing semicolon
        health = 50;
        
        if (health > 0)
        {
            Debug.Log("Player is alive with " + health + " health");
        } // Fixed: Added missing closing brace for if statement
    } // Fixed: Added missing closing brace for Start() method
    
    void Update()
    {
        transform.Rotate(0, 1, 0);
    }
}