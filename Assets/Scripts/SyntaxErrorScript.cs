using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyntaxErrorScript : MonoBehaviour
{
    public int health = 100; // Fixed: Added missing semicolon
    public string playerName = "Player"; // Fixed: Added missing semicolon
    public float speed = 5.0f; // Fixed: Declared the speed variable
    
    void Start()
    {
        Debug.Log("Starting game"); // Fixed: Added missing semicolon
        if (health > 0) // Fixed: Added missing closing parenthesis
        {
            Debug.Log("Player is alive");
        }
        else // Fixed: Proper else statement structure
        {
            Debug.Log("Player is dead"); // Fixed: Added missing semicolon
        }
    }
    
    void Update()
    {
        // Fixed: Now using the properly declared speed variable
        transform.Translate(Vector3.forward * speed * Time.deltaTime); // Fixed: Added missing closing parenthesis and semicolon
    }
} // Fixed: Added missing closing brace for the class