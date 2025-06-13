using UnityEngine;

public class TestClaude : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Test script created successfully by Claude AI Agent!");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space key pressed - Claude AI Agent is working!");
        }
    }
}