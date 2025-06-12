# Project Structure Documentation

## Overview
This is a Unity project that integrates Claude AI for chat-based assistance with Unity development. The project features a custom Unity Editor window that allows developers to interact with Claude AI to create scripts, GameObjects, and get help with Unity tasks through both text and voice input.

## Root Directory Structure

### Core Unity Files
- **`My project (1).sln`** - Visual Studio solution file containing the project references
- **`Assembly-CSharp.csproj`** - Main runtime assembly project file (66KB, 1089 lines)
- **`Assembly-CSharp-Editor.csproj`** - Editor assembly project file (71KB, 1168 lines)

### Unity Standard Directories

#### `/Assets/` - Main Project Assets
Contains all the custom scripts, scenes, and assets for the project.

##### `/Assets/Scripts/`
Runtime scripts that run in the actual game/application.

- **`SphereMovement.cs`** - Simple movement controller for a sphere GameObject
  - Handles WASD/Arrow key input for horizontal and vertical movement
  - Applies movement using Unity's transform system
  - Configurable speed parameter

##### `/Assets/Editor/`
Editor-only scripts that extend Unity's development environment with Claude AI integration.

**Core AI Integration:**
- **`ClaudeAIAgent.cs`** (802 lines) - Main AI integration class
  - Handles API communication with Claude AI (claude-sonnet-4-20250514)
  - Defines Unity-specific tools for AI to manipulate Unity objects
  - Tools include: create_script, create_gameobject, add_component, set_transform, list_gameobjects, delete_gameobject, edit_script, read_script
  - Manages conversation history and tool execution

**Chat Interface:**
- **`ChatWindow.cs`** (467 lines) - Main Unity Editor window for AI chat
  - Custom Unity Editor window accessible via Tools → Chat Window
  - Manages chat interface, message display, and user input
  - Integrates speech recognition, console capture, and command handling
  - Handles AI responses and tool execution visualization

**Data Models:**
- **`ChatData.cs`** (47 lines) - Data structures for chat functionality
  - `ChatMessage` class for storing chat messages with timestamps
  - `MessageType` enum (Normal, System, Warning, Error)
  - `LogEntry` class for console log capture

**Component Managers:**
- **`ChatMessageRenderer.cs`** (121 lines) - Handles rendering of chat messages in the editor window
- **`ChatConsoleCapture.cs`** (48 lines) - Captures Unity console logs and integrates them into chat
- **`ChatCommandHandler.cs`** (63 lines) - Processes special chat commands (like /help, /clear)
- **`ChatSuggestionSystem.cs`** (139 lines) - Provides intelligent suggestions based on context
- **`ChatSpeechManager.cs`** (98 lines) - Manages speech recognition functionality
- **`ChatClipboardManager.cs`** (105 lines) - Handles clipboard operations for copying conversations

**Speech Recognition:**
- **`SpeechRecognition.cs`** (275 lines) - Windows-based speech recognition implementation
  - Uses Unity's Windows Speech API for dictation and keyword recognition
  - Supports both free-form dictation and specific command recognition
  - Platform-specific implementation (Windows only)

**Utilities:**
- **`AssetDatabaseRefresh.cs`** (13 lines) - Simple utility for refreshing Unity's asset database

##### `/Assets/Scenes/`
- **`SampleScene.unity`** - Main Unity scene file containing the basic scene setup

#### `/ProjectSettings/` - Unity Project Configuration
Contains all Unity project settings and configurations.

**Core Settings:**
- **`ProjectSettings.asset`** - Main project configuration
- **`ProjectVersion.txt`** - Unity version information
- **`EditorSettings.asset`** - Unity Editor preferences
- **`EditorBuildSettings.asset`** - Build configuration settings

**System Managers:**
- **`AudioManager.asset`** - Audio system configuration
- **`InputManager.asset`** - Input system settings (keyboard, mouse, gamepad mapping)
- **`Physics2DSettings.asset`** - 2D physics configuration
- **`QualitySettings.asset`** - Graphics quality settings and levels
- **`TagManager.asset`** - Unity tags and layers configuration
- **`TimeManager.asset`** - Time and frame rate settings

**Graphics & Rendering:**
- **`GraphicsSettings.asset`** - Rendering pipeline and graphics settings
- **`MemorySettings.asset`** - Memory management configuration
- **`VFXManager.asset`** - Visual effects system settings

**Specialized Managers:**
- **`DynamicsManager.asset`** - Physics simulation settings
- **`NavMeshAreas.asset`** - AI navigation system configuration
- **`NetworkManager.asset`** - Network system settings
- **`ClusterInputManager.asset`** - Multi-display input management
- **`UnityConnectSettings.asset`** - Unity Cloud services configuration
- **`PackageManagerSettings.asset`** - Package Manager preferences
- **`PresetManager.asset`** - Asset preset management
- **`VersionControlSettings.asset`** - Version control integration settings
- **`XRSettings.asset`** - Extended Reality (VR/AR) settings
- **`SceneTemplateSettings.json`** - Scene template system configuration

#### `/Packages/` - Package Management
- **`manifest.json`** - Lists all Unity packages and dependencies
  - Key dependencies: Newtonsoft JSON for AI API communication
  - Standard Unity packages for 2D, UI, Visual Scripting, etc.
- **`packages-lock.json`** - Locks specific package versions for consistency

#### Unity Generated Directories
- **`/Library/`** - Unity's cache and build artifacts (auto-generated)
- **`/Temp/`** - Temporary build files (auto-generated)
- **`/Logs/`** - Unity Editor and build logs
- **`/UserSettings/`** - User-specific editor preferences
- **`/.vscode/`** - Visual Studio Code configuration files

## Key Features

### AI Integration
- Direct integration with Claude AI API
- Unity-specific tools for AI to manipulate Unity objects
- Automatic error detection and fixing suggestions
- Voice-to-text input for hands-free interaction

### Chat Interface
- Real-time chat with Claude AI
- Console log integration for error handling
- Command system for quick actions
- Message history and clipboard support

### Speech Recognition
- Windows Speech Recognition support
- Both dictation and keyword recognition modes
- Voice commands for common Unity tasks

### Development Tools
- Automatic script creation and editing
- GameObject manipulation through AI commands
- Project structure analysis and suggestions
- Error detection and fixing assistance

## Dependencies
- **Unity 2022.3+** - Main game engine
- **Newtonsoft.Json** - JSON serialization for AI API
- **Windows Speech API** - Speech recognition (Windows only)
- **Claude AI API** - AI assistant functionality

## Usage
1. Open Unity project
2. Navigate to Tools → Chat Window
3. Interact with Claude AI through text or voice
4. Use commands like "Create a player movement script" or "Create a red cube"
5. AI will automatically create scripts, GameObjects, and provide assistance

This project demonstrates advanced Unity Editor customization and AI integration for enhanced development workflows. 