# Unity Editor Scripts Documentation

## Overview

This Unity project contains a sophisticated AI-powered chat system built as custom Unity Editor tools. The system integrates with Claude AI to provide intelligent assistance for Unity development tasks, automatic error detection and fixing, and a comprehensive chat interface with unified streaming for all message types.

**Quick Access**: Press `Ctrl+Shift+D` to open the Chat Window from anywhere in Unity.

**Total Lines of Code**: 5,114 lines across 19 C# files

## 📁 File Structure

The editor scripts are organized into a modular structure under `Assets/Editor/ChatSystem/`:

```
Assets/Editor/ChatSystem/
├── Core/ (1,045 lines)                    # Main chat system core
│   ├── ChatWindow.cs (955 lines)         # Main controller & UI window
│   └── ChatData.cs (90 lines)            # Data models & streaming infrastructure
├── AI/ (738 lines)                       # Claude AI integration core
│   ├── ClaudeAIAgent.cs (416 lines)      # Core API communication & streaming
│   ├── ClaudeAPIModels.cs (151 lines)    # API data models & structures
│   ├── SystemPrompts.cs (122 lines)      # System prompts for Claude
│   ├── ClaudeStreamingModels.cs (57 lines) # Streaming event models
│   ├── ClaudeJSONSerializer.cs (51 lines) # Custom JSON serialization
│   └── Tools/ (1,989 lines)              # Claude AI tool implementations
│       ├── UnityTools.cs (968 lines)     # Unity tool implementations
│       ├── GameObjectTools.cs (459 lines) # GameObject manipulation tools
│       ├── TextEditorTools.cs (299 lines) # Text editing & file operations
│       ├── ScriptTools.cs (159 lines)    # Script creation & editing tools
│       └── FileSystemTools.cs (103 lines) # File system operations
├── UI/ (286 lines)                       # User interface components
│   ├── ChatMessageRenderer.cs (148 lines) # Message rendering & styling
│   └── ChatSuggestionSystem.cs (138 lines) # Quick actions & suggestions
├── Utilities/ (998 lines)                # Helper classes & utilities
│   ├── ChatWindowErrorHandler.cs (403 lines) # Automated error fixing
│   ├── ChatWindowCLI.cs (237 lines)      # Command line interface
│   ├── ChatConsoleCapture.cs (180 lines) # Console monitoring & error detection
│   ├── ChatClipboardManager.cs (104 lines) # Data export functionality
│   ├── ChatCommandHandler.cs (62 lines)  # User command processing
│   └── AssetDatabaseRefresh.cs (12 lines) # Asset database utilities
└── Configuration/ (1 line)               # Configuration files
    └── claude_config.txt (1 line)        # Claude AI API key
```

## Architecture Overview

The system follows a modular architecture with clear separation of concerns:

```
📁 Core/ (1,045 lines)
├── ChatWindow (Main Controller & Orchestration)  
└── ChatData (Data Models & Streaming Infrastructure)

📁 AI/ (738 lines - Core Integration)
├── ClaudeAIAgent (Core API Communication)
├── ClaudeAPIModels (API Data Structures)
├── ClaudeStreamingModels (Streaming Event Models)
├── ClaudeJSONSerializer (Custom Serialization)
├── SystemPrompts (AI System Prompts)
└── 📁 Tools/ (1,989 lines - Tool Implementations)
    ├── UnityTools (Unity Scene Manipulation)
    ├── GameObjectTools (GameObject Operations)
    ├── TextEditorTools (Text Editing & File Operations)
    ├── ScriptTools (Script Creation & Editing)
    └── FileSystemTools (File System Operations)

📁 UI/ (286 lines)
├── ChatMessageRenderer (Message Styling & Display)
└── ChatSuggestionSystem (Quick Actions & Context Awareness)

📁 Utilities/ (998 lines)
├── ChatWindowErrorHandler (Automated Error Fixing)
├── ChatConsoleCapture (Error Detection & Batching)
├── ChatCommandHandler (User Command Processing)
├── ChatClipboardManager (Data Export & Formatting)
├── ChatWindowCLI (Command Line Interface)
└── AssetDatabaseRefresh (Asset Database Utilities)

📁 Configuration/ (1 line)
└── claude_config.txt (API Key Storage)
```

## File Analysis

### Core/ChatWindow.cs (991 lines) - Main Controller

**Purpose**: The main EditorWindow that orchestrates the entire chat system with unified streaming for all message types.

**Hotkey**: `Ctrl+Shift+D` - Opens the Chat Window from anywhere in Unity

**Key Responsibilities**:
- Window lifecycle management and UI layout
- Unified message streaming for all message types
- AI interaction orchestration with proper state management
- Compilation state tracking with automatic result feedback to Claude
- Component initialization and cleanup

**New Feature**: **Compilation Context Awareness with Automatic Response**
- Automatically sends compilation success/failure results to Claude as user messages
- **Claude automatically responds** to compilation results with acknowledgment and suggestions
- Ensures Claude maintains awareness of whether its actions (script creation, error fixing) succeeded
- Integrates with both default compilation messages and custom error-fixing messages
- Provides contextual feedback and next steps after each compilation

**Key Components**:
```csharp
private ChatMessageRenderer messageRenderer;
private ChatConsoleCapture consoleCapture;
private ChatCommandHandler commandHandler;
private ChatSuggestionSystem suggestionSystem;
private ChatWindowErrorHandler errorHandler;
private ChatMessage currentlyStreamingMessage = null;
```

### AI/ClaudeAIAgent.cs (416 lines) - Core API Communication

**Purpose**: Handles Claude AI API communication and streaming coordination.

**Key Features**:
- HTTP communication with Claude AI API
- Streaming response processing and coordination
- Error handling and retry logic
- Cancellation token management
- Tool execution orchestration

**API Configuration**:
- Uses `Configuration/claude_config.txt` or `CLAUDE_API_KEY` environment variable
- Supports Claude Sonnet 4 model
- Implements proper error handling and retry logic

### AI/Tools/UnityTools.cs (968 lines) - Unity Tool Implementations

**Purpose**: Provides Claude with Unity-specific tools for direct scene manipulation.

**Available Tools for Claude**:
1. `create_script` - Generate new C# scripts
2. `create_gameobject` - Create GameObjects with components
3. `add_component` - Add components to existing GameObjects
4. `set_transform` - Modify object positions, rotations, scales
5. `list_gameobjects` - Query scene contents
6. `delete_gameobject` - Remove objects from scene
7. `text_editor_20250429` - Claude's built-in text editor for advanced script editing

### AI/Tools/GameObjectTools.cs (459 lines) - GameObject Operations

**Purpose**: Specialized tools for GameObject creation, manipulation, and querying.

**Key Features**:
- GameObject creation with primitive types
- Component attachment and management
- Transform manipulation (position, rotation, scale)
- Scene querying and object inspection

### AI/Tools/TextEditorTools.cs (299 lines) - Text Editing & File Operations

**Purpose**: Advanced text editing and file manipulation tools for Claude AI.

**Key Features**:
- Claude's built-in text editor integration
- File content viewing with line numbers
- Precise string replacement operations
- File creation and directory management
- Advanced editing capabilities for various file types

### AI/Tools/ScriptTools.cs (159 lines) - Script Creation & Editing

**Purpose**: Tools for creating and managing C# scripts in Unity projects.

**Key Features**:
- Script file creation with proper Unity formatting
- Compilation waiting and monitoring
- Asset database integration
- Script template management

### AI/Tools/FileSystemTools.cs (103 lines) - File System Operations

**Purpose**: File system operations for project file management.

**Key Features**:
- File searching and pattern matching
- Directory navigation and listing
- Path resolution and validation
- Asset path management

### AI/ClaudeAPIModels.cs (151 lines) - API Data Models

**Purpose**: Defines all data structures used for Claude AI API communication.

**Key Classes**:
```csharp
public class ClaudeMessage          // API message structure
public class ClaudeContentBlock     // Message content blocks
public class ClaudeRequest          // API request format
public class ClaudeResponse         // API response format
public class ClaudeTool             // Tool definitions
public class ClaudeToolInputSchema  // Tool parameter schemas
```

### AI/ClaudeStreamingModels.cs (57 lines) - Streaming Event Models

**Purpose**: Data models for handling Claude AI streaming responses.

**Key Classes**:
```csharp
public class StreamEvent            // Base streaming event
public class ContentBlockStartEvent // Content block started
public class ContentBlockDeltaEvent // Content block delta updates
public class MessageDeltaEvent      // Message-level updates
public class StreamError            // Error events
```

### AI/ClaudeJSONSerializer.cs (51 lines) - Custom JSON Serialization

**Purpose**: Custom JSON contract resolver for proper Claude AI API serialization.

**Key Features**:
- Excludes null and empty values from serialization
- Handles built-in tool serialization correctly
- Optimizes API request payload size
- Ensures API compatibility

### AI/SystemPrompts.cs (122 lines) - AI System Prompts

**Purpose**: Contains the system prompts that define Claude's behavior as a Unity development agent.

**Key Features**:
- CodeHero system prompt with Unity-specific instructions
- Project path and context information
- Tool usage guidelines and workflows
- Error diagnosis and fixing instructions

### Utilities/ChatWindowErrorHandler.cs (403 lines) - Automated Error Fixing

**Purpose**: Provides intelligent, automated error detection and fixing capabilities.

**Key Features**:
- Error batching to group related errors
- Queue system for managing error processing when AI is busy
- Retry logic with up to 3 attempts
- Fix tracking with summaries of applied fixes
- Cycle management to prevent infinite error-fixing loops

**Error Processing Workflow**:
1. Detection: Receives error batches from console capture
2. Analysis: Sends errors to Claude with context and instructions
3. Implementation: Claude uses tools to fix detected issues
4. Verification: Monitors compilation to confirm fixes
5. Reporting: Provides detailed success/failure feedback

### Utilities/ChatConsoleCapture.cs (180 lines) - Error Detection System

**Purpose**: Monitors Unity's console for errors and manages error batching.

**Key Features**:
- Real-time monitoring of Unity console messages
- Error batching to group similar errors
- Deduplication with occurrence counting
- Recent error tracking for compilation success detection
- Memory management with limited log storage

**Data Structures**:
```csharp
public class ErrorBatch
{
    public string LogString;
    public string StackTrace;
    public LogType LogType;
    public DateTime Timestamp;
    public int Count;
}
```

### UI/ChatMessageRenderer.cs (148 lines) - UI Rendering

**Purpose**: Handles all visual rendering of chat messages with proper styling.

**Key Features**:
- Rich text support with Unity's formatting
- Message type styling with different colors/styles
- Responsive layout adapting to window sizes
- Scroll management for proper user experience

### UI/ChatSuggestionSystem.cs (138 lines) - Quick Actions

**Purpose**: Provides contextual quick-action buttons for common tasks.

**Key Features**:
- Context-aware suggestions based on recent messages
- Dynamic layout with responsive button positioning
- State management during AI operations
- Error-specific suggestions when errors occur

### Utilities/ChatCommandHandler.cs (62 lines) - Command Processing

**Purpose**: Processes user commands starting with "/" prefix.

**Available Commands**:
- `/clear` - Clear all messages
- `/copy` - Copy conversation to clipboard
- `/help` - Show available commands
- `/time` - Display current time
- `/warn [message]` - Send a warning message
- `/error [message]` - Send an error message

### Utilities/ChatClipboardManager.cs (104 lines) - Data Export

**Purpose**: Handles copying conversation data to clipboard with formatting.

**Key Features**:
- Formatted output with headers and sections
- Optional console log inclusion
- Message type indication with clear prefixes
- Timestamp preservation for temporal context

### Core/ChatData.cs (90 lines) - Data Models & Streaming Infrastructure

**Purpose**: Defines core data structures used throughout the system.

**Key Classes**:
```csharp
public class ChatMessage
{
    public string id;           // Unique identifier
    public string username;     // Message sender
    public string message;      // Message content
    public string timestamp;    // When sent
    public MessageType type;    // Message category
    public bool isStreaming;    // Currently being streamed
    public bool isComplete;     // Streaming finished
}

public enum MessageType
{
    Normal, System, Warning, Error
}

public class LogEntry
{
    public string timestamp;
    public string logString;
    public string stackTrace;
    public LogType type;
}
```

### Utilities/AssetDatabaseRefresh.cs (12 lines) - Utility

**Purpose**: Simple utility for manually refreshing Unity's Asset Database.

**Usage**: Provides menu item `Tools/Refresh Asset Database` for manual database refresh.

### Configuration/claude_config.txt (1 line) - Configuration

**Purpose**: Stores Claude AI API key for authentication.

**Security Note**: This file should be added to .gitignore to prevent API key exposure.

## System Integration Flow

### Unified Streaming Message Flow
1. User types message in `ChatWindow`
2. `ChatWindow` adds user message to conversation
3. `ClaudeAIAgent` processes message with available tools
4. AI response streams character-by-character via streaming callbacks
5. `ChatMessageRenderer` displays streaming messages with consistent styling
6. `ChatSuggestionSystem` updates contextual suggestions
7. State cleanup with try/finally blocks
8. **Compilation results trigger automatic Claude responses with feedback and suggestions**

### Error Handling Flow
1. `ChatConsoleCapture` detects Unity errors
2. Errors are batched and sent to `ChatWindowErrorHandler`
3. Error handler analyzes errors with `ClaudeAIAgent`
4. Claude uses tools to fix detected issues
5. Error messages stream above currently streaming messages
6. `ChatWindow` monitors compilation for success confirmation
7. Results are reported back to user with streaming effects

### Tool Execution Flow
1. User sends request that requires tool usage
2. Claude's text response streams
3. Tool detection and parameter generation
4. Tool execution with contextual feedback
5. Tool result streaming
6. Claude's analysis and follow-up response streams
7. **Compilation results trigger automatic Claude responses with contextual feedback**

## Key Design Patterns

### Unified Streaming Architecture
- All message types stream consistently with character-by-character effects
- Smart insertion logic for system messages above streaming content
- Fire-and-forget streaming for system messages to avoid state interference

### Event-Driven Architecture
- Components communicate through events rather than direct references
- Loose coupling allows for easy testing and modification
- Clear separation of concerns

### Component-Based Design
- Each major functionality is encapsulated in its own class
- Main controller orchestrates but doesn't implement business logic
- Easy to extend and maintain

### State Management
- Robust tracking of AI operation states with guaranteed cleanup
- Proper cleanup and reset mechanisms using try/finally blocks
- Prevention of concurrent operations without breaking UI state

## Dependencies

### External Packages
- **Newtonsoft.Json**: For Claude API serialization
- **System.Net.Http**: For HTTP communication

### Unity Dependencies
- **UnityEditor**: All editor functionality
- **UnityEngine**: Core Unity API access

## System Statistics

### Code Distribution by Folder
- **AI Folder**: 2,727 lines (56.6%) - Claude integration and tools
  - **AI Core**: 738 lines (15.3%) - Core API communication and models
  - **AI Tools**: 1,989 lines (41.3%) - Tool implementations
- **Core Folder**: 1,045 lines (21.7%) - Main chat system
- **Utilities Folder**: 998 lines (20.7%) - Helper functionality
- **UI Folder**: 286 lines (5.9%) - User interface components
- **Configuration**: 1 line (0.02%) - API key storage

### Largest Files
1. `UnityTools.cs` - 968 lines (Unity tool implementations) - *AI/Tools/*
2. `ChatWindow.cs` - 955 lines (Main controller) - *Core/*
3. `GameObjectTools.cs` - 459 lines (GameObject operations) - *AI/Tools/*
4. `ClaudeAIAgent.cs` - 416 lines (API communication) - *AI/*
5. `ChatWindowErrorHandler.cs` - 403 lines (Error handling) - *Utilities/*
6. `TextEditorTools.cs` - 299 lines (Text editing operations) - *AI/Tools/*

### Architecture Benefits
- **Maintainable**: No file exceeds 1000 lines, most under 500
- **Modular**: Clear separation of concerns across folders
- **Scalable**: Easy to add new features in appropriate locations
- **Readable**: Well-organized code with consistent patterns
- **Testable**: Isolated components with clear interfaces 