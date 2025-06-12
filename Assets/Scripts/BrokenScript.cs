using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrokenScript : MonoBehaviour
{
    public float speed = 5.0f;
    public int health = 100;
    
    void Start()
    {
        Debug.Log("Starting the broken script");
        // Fixed: Added missing semicolon
        transform.position = new Vector3(0, 0, 0);
    }
    
    void Update()
    {
        // Fixed: Added proper closing brace for the if statement
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
        
        // Fixed: Added semicolon to variable declaration
        string playerName;
        playerName = "Player1";
        
        // Fixed: Added missing parentheses in method call
        Debug.Log("Player health: " + health);
    }
}