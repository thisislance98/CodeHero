#!/bin/bash

# Unity Chat Window CLI Helper Script
# This script provides an easy way to send messages to the Unity ChatWindow from the command line

# Default Unity path (adjust as needed for your system)
UNITY_PATH="/Applications/Unity/Hub/Editor/6000.0.34f1/Unity.app/Contents/MacOS/Unity"

# Get the current directory as the project path
PROJECT_PATH=$(pwd)

# Function to display usage
show_usage() {
    echo "Unity ChatWindow CLI Helper"
    echo ""
    echo "Usage:"
    echo "  ./chat_cli.sh send \"Your message here\""
    echo "  ./chat_cli.sh response"
    echo "  ./chat_cli.sh clear"
    echo ""
    echo "Commands:"
    echo "  send <message>    Send a message to the ChatWindow"
    echo "  response          Get the last CLI response"
    echo "  clear             Clear any pending CLI messages"
    echo ""
    echo "Options:"
    echo "  -u, --unity-path  Specify Unity executable path"
    echo "  -p, --project     Specify project path (default: current directory)"
    echo "  -h, --help        Show this help message"
    echo ""
    echo "Examples:"
    echo "  ./chat_cli.sh send \"Create a player movement script\""
    echo "  ./chat_cli.sh send \"What scripts are in my project?\""
    echo "  ./chat_cli.sh -u \"/path/to/Unity\" send \"Hello Claude!\""
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -u|--unity-path)
            UNITY_PATH="$2"
            shift 2
            ;;
        -p|--project)
            PROJECT_PATH="$2"
            shift 2
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        send)
            COMMAND="send"
            MESSAGE="$2"
            shift 2
            ;;
        response)
            COMMAND="response"
            shift
            ;;
        clear)
            COMMAND="clear"
            shift
            ;;
        *)
            echo "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Check if Unity path exists
if [ ! -f "$UNITY_PATH" ]; then
    echo "Error: Unity not found at $UNITY_PATH"
    echo "Please specify the correct Unity path using -u option or edit the script."
    echo ""
    echo "Common Unity paths:"
    echo "  macOS: /Applications/Unity/Hub/Editor/[VERSION]/Unity.app/Contents/MacOS/Unity"
    echo "  Windows: C:/Program Files/Unity/Hub/Editor/[VERSION]/Editor/Unity.exe"
    echo "  Linux: /opt/Unity/Editor/Unity"
    exit 1
fi

# Check if project path exists
if [ ! -d "$PROJECT_PATH" ]; then
    echo "Error: Project path not found: $PROJECT_PATH"
    exit 1
fi

# Execute command
case $COMMAND in
    send)
        if [ -z "$MESSAGE" ]; then
            echo "Error: No message provided"
            echo "Usage: ./chat_cli.sh send \"Your message here\""
            exit 1
        fi
        
        echo "Sending message to ChatWindow: $MESSAGE"
        echo "Unity: $UNITY_PATH"
        echo "Project: $PROJECT_PATH"
        echo ""
        
        "$UNITY_PATH" -batchmode -quit \
            -projectPath "$PROJECT_PATH" \
            -executeMethod ChatWindowCLI.SendMessage \
            -message "$MESSAGE" \
            -logFile - 2>&1 | grep -E "(ChatWindowCLI|Error|Exception)"
        
        echo ""
        echo "Message sent! If Unity is running, the message should appear in the ChatWindow."
        ;;
        
    response)
        echo "Getting last CLI response..."
        "$UNITY_PATH" -batchmode -quit \
            -projectPath "$PROJECT_PATH" \
            -executeMethod ChatWindowCLI.GetLastResponse \
            -logFile - 2>&1 | grep -E "(ChatWindowCLI|Error|Exception)"
        ;;
        
    clear)
        echo "Clearing CLI messages..."
        "$UNITY_PATH" -batchmode -quit \
            -projectPath "$PROJECT_PATH" \
            -executeMethod ChatWindowCLI.ClearMessages \
            -logFile - 2>&1 | grep -E "(ChatWindowCLI|Error|Exception)"
        ;;
        
    *)
        echo "Error: No command specified"
        show_usage
        exit 1
        ;;
esac 