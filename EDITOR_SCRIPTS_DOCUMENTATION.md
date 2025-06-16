# Unity Editor Scripts Documentation

## Overview

This Unity project contains a sophisticated AI-powered chat system built as custom Unity Editor tools. The system integrates with Claude AI to provide intelligent assistance for Unity development tasks, automatic error detection and fixing, and a comprehensive chat interface with unified streaming for all message types.

**Quick Access**: Press `Ctrl+Shift+D` to open the Chat Window from anywhere in Unity.

**Auto Fix Toggle**: Use the "Auto Fix" button in the Chat Window header to enable/disable automatic compilation error fixing.

**Auto Compile Toggle**: Use the "Auto Compile" button in the Chat Window header to enable/disable Unity's automatic script compilation. When disabled, Unity will not automatically compile scripts when they are modified, allowing you to make multiple changes before manually triggering compilation.

**Total Lines of Code**: 7,630 lines across 28 C# files

## Tools and Compilation System Integration

The Unity Chat System features sophisticated integration between Claude AI tools and Unity's compilation system to ensure smooth operation and maintain context awareness during script operations.

### Core Integration Features

**Unified Compilation Tracking System**
- Centralized compilation state monitoring in `ChatWindow.cs`
- Automatic detection of compilation start/finish events
- Prevention of duplicate compilation result processing
- Integration with both error fixing cycles and normal tool operations

**Compilation Context Awareness with Automatic Response**
- **Automatic Claude Feedback**: When compilation completes, the system automatically sends results to Claude as user messages
- **Claude Auto-Response**: Claude automatically responds to compilation results with acknowledgment and contextual suggestions
- **Context Maintenance**: Ensures Claude stays informed about whether its script creation/modification actions succeeded
- **Next Steps Guidance**: Provides intelligent follow-up suggestions based on compilation outcomes

**Assembly Reload Control**
- **Auto Compile Toggle**: Users can disable Unity's automatic compilation via the chat window header
- **Assembly Locking**: When auto-compilation is disabled, uses `EditorApplication.LockReloadAssemblies()` to prevent automatic script compilation
- **Manual Control**: Allows making multiple script changes before triggering compilation manually

### Script Creation Workflow

The `ScriptTools.cs` class implements an optimized script creation workflow:

```csharp
// Key workflow steps:
1. Directory preparation and validation
2. File existence checking to prevent duplicates  
3. Script file writing with proper content
4. Unity AssetDatabase import for immediate recognition
5. Immediate feedback with file details (size, lines, location)
6. Optional compilation waiting with timeout handling
```

**Compilation Wait Mechanism**
```csharp
public static async Task WaitForCompilationToComplete()
{
    // Intelligent compilation detection:
    // - 100ms initial delay to detect compilation start
    // - 200ms check intervals for stable monitoring  
    // - 30-second timeout with graceful handling
    // - Status updates every 3 seconds during long compilations
    // - Additional 300ms safety delay after completion
}
```

### Error Fixing Integration

The `ChatWindowErrorHandler.cs` coordinates with the compilation system for automated error fixing:

**Error Fix Compilation Flow**
1. **Error Detection**: Console capture system detects and batches compilation errors
2. **AI Analysis**: Errors sent to Claude for analysis and fix generation
3. **Compilation Callback Registration**: Error handler registers custom success callback with main window
4. **Unified Tracking**: Main window's compilation system monitors fix attempts
5. **Result Processing**: Success/failure results automatically sent back to Claude
6. **Retry Logic**: Up to 3 fix attempts with intelligent retry decisions

**Compilation Success Callback System**
```csharp
// Error handler registers custom callback:
parentWindow.RegisterCompilationSuccessCallback(CreateErrorFixSuccessMessage);

// Main window processes compilation results:
private void CheckSuccessfulCompilation()
{
    // Check for recent errors
    bool success = !consoleCapture?.HasRecentErrors();
    
    // Use custom callback if registered, otherwise default message
    string message = customSuccessMessageProvider?.Invoke(success) ?? defaultMessage;
    
    // Send results to Claude automatically
    SendCompilationResultToClaude(message, success);
}
```

### Tool Coordination and Deduplication

The `UnityTools.cs` coordinator implements tool execution management:

**Tool Execution Safety**
- **Deduplication System**: Prevents concurrent execution of the same tool
- **Thread-Safe Operations**: Uses locking mechanisms for tool execution tracking
- **Specialized Delegation**: Routes tool calls to appropriate specialized handler classes
- **Error Boundary**: Comprehensive error handling and logging for tool operations

### Claude AI Tool Integration

**Available Tool Categories**:
- **Script & File Operations**: `create_script`, `str_replace_based_edit_tool`, `search_files`, `delete_file`, `search_codebase`
- **GameObject Management**: `create_gameobject`, `add_component`, `set_transform`, `list_gameobjects`, `delete_gameobject`
- **Asset Management**: `manage_asset`, `refresh_assets`
- **Scene Management**: `manage_scene` (load, save, create, hierarchy, build settings)
- **Editor Control**: `manage_editor`, `execute_menu_item`
- **Unity Logs & Console**: `read_unity_logs`, `check_compilation_status`, `clear_console`
- **Directory Operations**: `list_directory`
- **Terminal Operations**: `execute_terminal_command`

**Compilation Status Monitoring**
```csharp
case "check_compilation_status":
    // Returns comprehensive compilation state:
    // - Current compilation status (Yes/No)
    // - Recent error detection
    // - Contextual guidance for next steps
    // - Integration with console capture system
```

### Advanced Features

**Streaming Integration**
- Compilation messages stream with the same visual effects as other system messages
- Compilation results are inserted above currently streaming Claude responses
- Unified message queue prevents interference between compilation feedback and AI responses

**State Management**
- Robust cleanup mechanisms using try/finally blocks
- Prevention of stuck states during compilation
- Automatic reset of AI waiting states after compilation completion
- Thread-safe state transitions

**Timeout and Error Handling**
- 30-second compilation timeout with graceful degradation
- Comprehensive error logging for debugging
- Fallback mechanisms when compilation detection fails
- User feedback during long compilation operations

This integration ensures that Claude AI maintains full awareness of compilation states and outcomes, enabling intelligent follow-up actions and maintaining smooth user experience during complex development workflows.

## 📁 File Structure

The editor scripts are organized into a modular structure under `Assets/Editor/ChatSystem/`:

```
Assets/Editor/ChatSystem/
├── Core/ (1,254 lines)                    # Main chat system core
│   ├── ChatWindow.cs (1,164 lines)       # Main controller & UI window
│   └── ChatData.cs (90 lines)            # Data models & streaming infrastructure
├── AI/ (1,286 lines)                     # Claude AI integration core
│   ├── ClaudeAIAgent.cs (451 lines)      # Core API communication & streaming
│   ├── ClaudeAPIModels.cs (151 lines)    # API data models & structures
│   ├── SystemPrompts.cs (84 lines)       # System prompts for Claude
│   ├── ClaudeStreamingModels.cs (57 lines) # Streaming event models
│   ├── ClaudeJSONSerializer.cs (51 lines) # Custom JSON serialization
│   └── Tools/ (4,803 lines)              # Claude AI tool implementations
│       ├── SceneTools.cs (510 lines)     # Scene management & hierarchy
│       ├── AssetTools.cs (492 lines)     # Asset management operations
│       ├── EditorTools.cs (474 lines)    # Editor control & menu execution
│       ├── GameObjectTools.cs (459 lines) # GameObject manipulation tools
│       ├── DirectoryTools.cs (448 lines) # Directory operations & listing
│       ├── AdvancedFileTools.cs (382 lines) # Advanced file manipulation
│       ├── CodebaseSearchTools.cs (325 lines) # Codebase search & pattern matching
│       ├── TextEditorTools.cs (310 lines) # Text editing & file operations
│       ├── UnityLogTools.cs (303 lines)  # Unity logs & console management
│       ├── TerminalTools.cs (171 lines)  # Terminal command execution
│       ├── ScriptTools.cs (171 lines)    # Script creation & editing tools
│       ├── UnityTools.cs (146 lines)     # Tool coordinator & dispatcher
│       └── FileSystemTools.cs (103 lines) # File system operations
├── UI/ (287 lines)                       # User interface components
│   ├── ChatMessageRenderer.cs (148 lines) # Message rendering & styling
│   └── ChatSuggestionSystem.cs (139 lines) # Quick actions & suggestions
├── Utilities/ (1,026 lines)              # Helper classes & utilities
│   ├── ChatWindowErrorHandler.cs (406 lines) # Automated error fixing
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
📁 Core/ (1,254 lines)
├── ChatWindow (Main Controller & Orchestration)  
└── ChatData (Data Models & Streaming Infrastructure)

📁 AI/ (1,286 lines - Core Integration)
├── ClaudeAIAgent (Core API Communication)
├── ClaudeAPIModels (API Data Structures)
├── ClaudeStreamingModels (Streaming Event Models)
├── ClaudeJSONSerializer (Custom Serialization)
├── SystemPrompts (AI System Prompts)
└── 📁 Tools/ (4,803 lines - Tool Implementations)
    ├── SceneTools (Scene Management & Build Settings)
    ├── AssetTools (Asset Management & Database Operations)
    ├── EditorTools (Editor Control & Menu Execution)
    ├── GameObjectTools (GameObject Operations)
    ├── DirectoryTools (Directory Operations & Listing)
    ├── AdvancedFileTools (Advanced File Manipulation)
    ├── CodebaseSearchTools (Codebase Search & Pattern Matching)
    ├── TextEditorTools (Text Editing & File Operations)
    ├── UnityLogTools (Unity Logs & Console Management)
    ├── TerminalTools (Terminal Command Execution)
    ├── ScriptTools (Script Creation & Editing)
    ├── UnityTools (Tool Coordinator & Dispatcher)
    └── FileSystemTools (File System Operations)

📁 UI/ (287 lines)
├── ChatMessageRenderer (Message Styling & Display)
└── ChatSuggestionSystem (Quick Actions & Context Awareness)

📁 Utilities/ (1,026 lines)
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

### Core/ChatWindow.cs (1,164 lines) - Main Controller

**Purpose**: The main EditorWindow that orchestrates the entire chat system with unified streaming for all message types.

**Hotkey**: `Ctrl+Shift+D` - Opens the Chat Window from anywhere in Unity

**Key Responsibilities**:
- Window lifecycle management and UI layout
- Unified message streaming for all message types
- AI interaction orchestration with proper state management
- Compilation state tracking with automatic result feedback to Claude
- Auto-fix toggle for enabling/disabling automatic error fixing
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
private bool autoCompilationEnabled = true; // Controls Unity's automatic compilation
```

### AI/ClaudeAIAgent.cs (451 lines) - Core API Communication

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

### AI/Tools/UnityTools.cs (146 lines) - Tool Coordinator & Dispatcher

**Purpose**: Acts as a central coordinator that delegates tool execution to specialized tool classes.

**Architecture**: 
- Provides unified `GetUnityTools()` method that aggregates tools from all specialized classes
- Routes tool execution to appropriate specialized handlers
- Implements tool deduplication and error handling
- Maintains clean separation of concerns between different tool types

**Available Tools for Claude** (delegated to specialized classes):

**Script & File Operations:**
1. `create_script` - Generate new C# scripts (ScriptTools)
2. `str_replace_based_edit_tool` - Advanced text editing (TextEditorTools)
3. `search_files` - File system operations (FileSystemTools)
4. `delete_file` - Delete files and directories (AdvancedFileTools)
5. `search_codebase` - Search code patterns across project (CodebaseSearchTools)

**GameObject Management:**
6. `create_gameobject` - Create GameObjects with components (GameObjectTools)
7. `add_component` - Add components to existing GameObjects (GameObjectTools)
8. `set_transform` - Modify object positions, rotations, scales (GameObjectTools)
9. `list_gameobjects` - Query scene contents (GameObjectTools)
10. `delete_gameobject` - Remove objects from scene (GameObjectTools)

**Asset Management:**
11. `manage_asset` - Comprehensive asset operations: create, import, modify, delete, search, get info (AssetTools)
12. `refresh_assets` - Refresh Unity Asset Database (AssetTools)

**Scene Management:**
13. `manage_scene` - Scene operations: load, save, create, get hierarchy, build settings (SceneTools)

**Editor Control:**
14. `manage_editor` - Editor state control: play/pause/stop, tools, tags, layers, project settings (EditorTools)
15. `execute_menu_item` - Execute Unity menu items by path (EditorTools)

**Unity Logs & Console:**
16. `read_unity_logs` - Read Unity console logs (UnityLogTools)
17. `check_compilation_status` - Check compilation status (UnityLogTools)
18. `clear_console` - Clear Unity console (UnityLogTools)

**Directory Operations:**
19. `list_directory` - List directory contents with detailed information (DirectoryTools)

**Terminal Operations:**
20. `execute_terminal_command` - Execute terminal/command line commands (TerminalTools)

### AI/Tools/SceneTools.cs (510 lines) - Scene Management & Hierarchy

**Purpose**: Complete scene management including loading, saving, creation, and build settings.

**Key Features**:
- Scene lifecycle management (load, save, create)
- Scene hierarchy visualization with component details
- Build settings management (add/remove scenes)
- Multi-scene support and active scene tracking
- Scene state monitoring (dirty, loaded status)

**Available Operations**:
- `load` - Load scenes by name or build index
- `save` - Save current scene changes
- `save_as` - Save scene with new name/location
- `create` - Create new scenes with default setup
- `get_hierarchy` - Get detailed scene hierarchy
- `get_active` - Get active scene information
- `add_to_build` - Add scenes to build settings
- `remove_from_build` - Remove scenes from build settings
- `get_build_settings` - List all scenes in build settings

### AI/Tools/AssetTools.cs (492 lines) - Asset Management Operations

**Purpose**: Comprehensive asset management operations including creation, import, modification, and search.

**Key Features**:
- Asset creation: Materials, Textures, Prefabs, Folders
- Asset operations: Import, delete, duplicate, move, rename
- Asset search with filtering and pattern matching
- Material property management (color, metallic, smoothness, emission)
- Asset Database refresh and synchronization

**Available Operations**:
- `create` - Create new assets with properties
- `import` - Import external assets
- `modify` - Modify existing asset properties
- `delete` - Remove assets from project
- `duplicate` - Copy assets to new locations
- `move` - Move assets between directories
- `rename` - Rename existing assets
- `search` - Search assets with patterns and filters
- `get_info` - Get detailed asset information
- `create_folder` - Create new project folders

### AI/Tools/EditorTools.cs (474 lines) - Editor Control & Menu Execution

**Purpose**: Unity Editor state control, play mode management, and menu item execution.

**Key Features**:
- Play mode control (play, pause, stop)
- Editor state monitoring and reporting
- Active tool selection (Move, Rotate, Scale, etc.)
- Tag and Layer management
- Project settings access
- Menu item execution by path

**Available Operations**:
- `play` - Enter play mode
- `pause` - Pause/resume play mode
- `stop` - Exit play mode
- `get_state` - Get comprehensive editor state
- `set_active_tool` - Change active editor tool
- `add_tag` - Add new tags to project
- `add_layer` - Add new layers to project
- `get_tags` - List all project tags
- `get_layers` - List all project layers
- `get_project_settings` - Get project configuration
- `execute_menu_item` - Execute Unity menu commands

### AI/Tools/GameObjectTools.cs (459 lines) - GameObject Operations

**Purpose**: Specialized tools for GameObject creation, manipulation, and querying.

**Key Features**:
- GameObject creation with primitive types
- Component attachment and management
- Transform manipulation (position, rotation, scale)
- Scene querying and object inspection

### AI/Tools/DirectoryTools.cs (448 lines) - Directory Operations & Listing

**Purpose**: Advanced directory operations and file system navigation.

**Key Features**:
- Directory content listing with detailed information
- File size and modification date reporting
- File type detection and categorization
- Recursive directory traversal
- Project structure analysis

### AI/Tools/AdvancedFileTools.cs (382 lines) - Advanced File Manipulation

**Purpose**: Advanced file operations including deletion and complex file management.

**Key Features**:
- Safe file and directory deletion
- File existence checking
- Path validation and sanitization
- Backup creation before destructive operations
- Error handling for file system operations

### AI/Tools/CodebaseSearchTools.cs (325 lines) - Codebase Search & Pattern Matching

**Purpose**: Comprehensive codebase search and analysis tools.

**Key Features**:
- Pattern-based code searching with regex support
- Function and class definition finding
- Cross-file reference analysis
- File type filtering for targeted searches
- Search result ranking and relevance scoring

### AI/Tools/TextEditorTools.cs (310 lines) - Text Editing & File Operations

**Purpose**: Advanced text editing and file manipulation tools for Claude AI.

**Key Features**:
- Claude's built-in text editor integration
- File content viewing with line numbers
- Precise string replacement operations
- File creation and directory management
- Advanced editing capabilities for various file types

### AI/Tools/UnityLogTools.cs (303 lines) - Unity Logs & Console Management

**Purpose**: Unity console and logging system integration.

**Key Features**:
- Unity console log reading and filtering
- Compilation status monitoring
- Error detection and categorization
- Log level filtering (Error, Warning, Info)
- Console clearing and management

### AI/Tools/TerminalTools.cs (171 lines) - Terminal Command Execution

**Purpose**: Terminal and command-line interface integration.

**Key Features**:
- Cross-platform terminal command execution
- Git operations support
- Package management integration
- Build script execution
- System-level task automation

### AI/Tools/ScriptTools.cs (171 lines) - Script Creation & Editing

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

### AI/SystemPrompts.cs (84 lines) - AI System Prompts

**Purpose**: Contains the system prompts that define Claude's behavior as a Unity development agent.

**Key Features**:
- CodeHero system prompt with Unity-specific instructions
- Project path and context information
- Tool usage guidelines and workflows
- Error diagnosis and fixing instructions

### Utilities/ChatWindowErrorHandler.cs (406 lines) - Automated Error Fixing

**Purpose**: Provides intelligent, automated error detection and fixing capabilities.

**Key Features**:
- Error batching to group related errors
- Queue system for managing error processing when AI is busy
- Retry logic with up to 3 attempts
- Fix tracking with summaries of applied fixes
- Cycle management to prevent infinite error-fixing loops
- Respects auto-fix toggle setting from main chat window

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

### UI/ChatSuggestionSystem.cs (139 lines) - Quick Actions

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

### Utilities/ChatWindowCLI.cs (237 lines) - Command Line Interface

**Purpose**: Provides a command-line interface for advanced chat operations.

**Key Features**:
- Command parsing and execution
- Multi-line command support
- Command history and auto-completion
- Integration with chat system for seamless operation

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
- **AI Folder**: 6,089 lines (79.8%) - Claude integration and tools
  - **AI Core**: 1,286 lines (16.9%) - Core API communication and models
  - **AI Tools**: 4,803 lines (62.9%) - Tool implementations
- **Core Folder**: 1,254 lines (16.4%) - Main chat system
- **Utilities Folder**: 1,026 lines (13.4%) - Helper functionality
- **UI Folder**: 287 lines (3.8%) - User interface components
- **Configuration**: 1 line (0.01%) - API key storage

### Largest Files
1. `ChatWindow.cs` - 1,164 lines (Main controller) - *Core/*
2. `SceneTools.cs` - 510 lines (Scene management) - *AI/Tools/*
3. `AssetTools.cs` - 492 lines (Asset operations) - *AI/Tools/*
4. `EditorTools.cs` - 474 lines (Editor control) - *AI/Tools/*
5. `GameObjectTools.cs` - 459 lines (GameObject operations) - *AI/Tools/*
6. `ClaudeAIAgent.cs` - 451 lines (API communication) - *AI/*
7. `DirectoryTools.cs` - 448 lines (Directory operations) - *AI/Tools/*
8. `ChatWindowErrorHandler.cs` - 406 lines (Error handling) - *Utilities/*
9. `AdvancedFileTools.cs` - 382 lines (Advanced file operations) - *AI/Tools/*
10. `CodebaseSearchTools.cs` - 325 lines (Codebase search) - *AI/Tools/*

### Architecture Benefits
- **Maintainable**: Largest file is 1,164 lines, most under 500
- **Modular**: Clear separation of concerns across folders
- **Scalable**: Easy to add new features in appropriate locations
- **Readable**: Well-organized code with consistent patterns
- **Testable**: Isolated components with clear interfaces
- **Comprehensive**: 20 distinct AI tools covering all Unity development aspects 