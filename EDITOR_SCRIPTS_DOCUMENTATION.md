# Unity Editor Scripts Documentation

## Overview

This Unity project contains a sophisticated AI-powered chat system built as custom Unity Editor tools. The system integrates with Claude AI to provide intelligent assistance for Unity development tasks, automatic error detection and fixing, and a comprehensive chat interface.

**Quick Access**: Press `Ctrl+Shift+D` to open the Chat Window from anywhere in Unity.

## Architecture Overview

The system follows a modular architecture with clear separation of concerns:

```
ChatWindow (Main Controller)
├── ChatMessageRenderer (UI Rendering)
├── ChatConsoleCapture (Error Detection)
├── ChatCommandHandler (Command Processing)
├── ChatSuggestionSystem (Quick Actions)
├── ChatWindowErrorHandler (Automated Error Fixing)
├── ChatClipboardManager (Data Export)
├── ClaudeAIAgent (AI Integration)
└── ChatData (Data Models)
```

## File Analysis

### 1. ChatWindow.cs (571 lines) - Main Controller
**Status: ⚠️ NEEDS REFACTORING (exceeds 500 line limit)**

**Purpose**: The main EditorWindow that orchestrates the entire chat system.

**Hotkey**: `Ctrl+Shift+D` - Opens the Chat Window from anywhere in Unity

**Key Responsibilities**:
- Window lifecycle management and UI layout
- Message flow coordination between components
- AI interaction orchestration
- Compilation state tracking
- Component initialization and cleanup

**Key Components**:
```csharp
private ChatMessageRenderer messageRenderer;
private ChatConsoleCapture consoleCapture;
private ChatCommandHandler commandHandler;
private ChatSuggestionSystem suggestionSystem;
private ChatWindowErrorHandler errorHandler;
```

**Critical Features**:
- **Unified Compilation Tracking**: Monitors Unity's compilation state to provide feedback
- **Message Management**: Robust message addition/removal with ID-based tracking
- **AI State Management**: Tracks waiting states to prevent concurrent AI requests
- **Error Queue Processing**: Coordinates with error handler for automated fixes

**Event System**:
- Console capture events for error detection
- Command handler events for user commands
- Error handler events for fix completion
- Compilation events for build feedback

### 2. ClaudeAIAgent.cs (1020 lines) - AI Integration Core
**Status: ⚠️ NEEDS REFACTORING (exceeds 500 line limit)**

**Purpose**: Handles all Claude AI API communication and Unity tool integration.

**Key Features**:
- **API Communication**: Manages HTTP requests to Claude AI with proper authentication
- **Tool System**: Provides Claude with Unity-specific tools for direct manipulation
- **Message Serialization**: Handles complex JSON serialization for Claude's API format
- **Error Context**: Specialized error-fixing mode for automated debugging

**Available Tools for Claude**:
1. `create_script` - Generate new C# scripts
2. `create_gameobject` - Create GameObjects with components
3. `add_component` - Add components to existing GameObjects
4. `set_transform` - Modify object positions, rotations, scales
5. `list_gameobjects` - Query scene contents
6. `delete_gameobject` - Remove objects from scene
7. `edit_script` - Modify existing scripts
8. `read_script` - Examine script contents

**API Configuration**:
- Uses `claude_config.txt` or `CLAUDE_API_KEY` environment variable
- Supports latest Claude Sonnet 4 model
- Implements proper error handling and retry logic

### 3. ChatWindowErrorHandler.cs (387 lines) - Automated Error Fixing

**Purpose**: Provides intelligent, automated error detection and fixing capabilities.

**Key Features**:
- **Error Batching**: Groups related errors to avoid spam
- **Queue System**: Manages error processing when AI is busy
- **Retry Logic**: Attempts fixes up to 3 times with progressively better context
- **Fix Tracking**: Maintains summaries of applied fixes
- **Cycle Management**: Prevents infinite error-fixing loops

**Error Processing Workflow**:
1. **Detection**: Receives error batches from console capture
2. **Analysis**: Sends errors to Claude with context and instructions
3. **Implementation**: Claude uses tools to fix detected issues
4. **Verification**: Monitors compilation to confirm fixes
5. **Reporting**: Provides detailed success/failure feedback

**Integration Points**:
- Works with `ChatConsoleCapture` for error detection
- Communicates with `ClaudeAIAgent` for AI-powered fixes
- Coordinates with `ChatWindow` for UI updates and state management

### 4. ChatConsoleCapture.cs (181 lines) - Error Detection System

**Purpose**: Monitors Unity's console for errors and manages error batching.

**Key Features**:
- **Real-time Monitoring**: Captures all Unity console messages
- **Error Batching**: Groups similar errors to reduce noise
- **Deduplication**: Counts occurrences of identical errors
- **Recent Error Tracking**: Determines if compilation was successful
- **Memory Management**: Limits stored logs to prevent memory issues

**Error Batching Logic**:
- 1-second delay to collect related errors
- Deduplication by message content and stack trace
- Automatic processing when batch window expires

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

### 5. ChatMessageRenderer.cs (121 lines) - UI Rendering

**Purpose**: Handles all visual rendering of chat messages with proper styling.

**Key Features**:
- **Rich Text Support**: Supports Unity's rich text formatting
- **Message Type Styling**: Different colors/styles for system, error, warning messages
- **Responsive Layout**: Adapts to different window sizes
- **Scroll Management**: Maintains proper scrolling behavior

**Styling System**:
- Message-specific styles for different types
- Username styling with bold formatting
- Timestamp display with consistent formatting
- Dark theme background for message area

### 6. ChatSuggestionSystem.cs (139 lines) - Quick Actions

**Purpose**: Provides contextual quick-action buttons for common tasks.

**Key Features**:
- **Context Awareness**: Suggestions adapt based on recent messages
- **Dynamic Layout**: Responsive button layout based on window width
- **State Management**: Proper enabling/disabling during AI operations
- **Error Context**: Special suggestions when errors occur

**Suggestion Categories**:
- **Basic Actions**: Create objects, list GameObjects, get help
- **Context-Sensitive**: Follow-up actions based on recent messages
- **Error Response**: Suggestions when errors are detected
- **Utility Commands**: Quick access to common commands

### 7. ChatCommandHandler.cs (63 lines) - Command Processing

**Purpose**: Processes user commands starting with "/" prefix.

**Available Commands**:
- `/clear` - Clear all messages
- `/copy` - Copy conversation to clipboard
- `/help` - Show available commands
- `/time` - Display current time
- `/warn [message]` - Send a warning message
- `/error [message]` - Send an error message

**Architecture**:
- Event-based communication with main window
- Simple string parsing with space-separated arguments
- Extensible design for adding new commands

### 8. ChatClipboardManager.cs (105 lines) - Data Export

**Purpose**: Handles copying conversation data to clipboard with formatting.

**Key Features**:
- **Formatted Output**: Professional formatting with headers and sections
- **Log Integration**: Optional inclusion of console logs
- **Message Type Indication**: Clear prefixes for different message types
- **Timestamp Preservation**: Maintains temporal context

**Export Format**:
```
=== Unity Chat Window Conversation ===
Exported on: 2024-01-01 12:00:00

12:00:01 - User: Create a cube
12:00:02 - Claude: I'll create a cube for you...
=== End of Conversation ===
```

### 9. ChatData.cs (49 lines) - Data Models

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

### 10. AssetDatabaseRefresh.cs (13 lines) - Utility

**Purpose**: Simple utility for manually refreshing Unity's Asset Database.

**Usage**: Provides menu item `Tools/Refresh Asset Database` for manual database refresh.

### 11. claude_config.txt (1 line) - Configuration

**Purpose**: Stores Claude AI API key for authentication.

**Security Note**: This file should be added to .gitignore to prevent API key exposure.

## System Integration Flow

### Normal Chat Flow
1. User types message in `ChatWindow`
2. `ChatWindow` adds message to conversation
3. `ClaudeAIAgent` processes message with available tools
4. AI response is displayed via `ChatMessageRenderer`
5. `ChatSuggestionSystem` updates contextual suggestions

### Error Handling Flow
1. `ChatConsoleCapture` detects Unity errors
2. Errors are batched and sent to `ChatWindowErrorHandler`
3. Error handler analyzes errors with `ClaudeAIAgent`
4. Claude uses tools to fix detected issues
5. `ChatWindow` monitors compilation for success confirmation
6. Results are reported back to user

### Command Processing Flow
1. User enters command starting with "/"
2. `ChatCommandHandler` parses and executes command
3. Results are communicated back via events
4. `ChatWindow` updates UI accordingly

## Key Design Patterns

### 1. Event-Driven Architecture
- Components communicate through events rather than direct references
- Loose coupling allows for easy testing and modification
- Clear separation of concerns

### 2. Component-Based Design
- Each major functionality is encapsulated in its own class
- Main controller orchestrates but doesn't implement business logic
- Easy to extend and maintain

### 3. State Management
- Robust tracking of AI operation states
- Proper cleanup and reset mechanisms
- Prevention of concurrent operations

### 4. Error Recovery
- Graceful handling of API failures
- Automatic retry logic for transient errors
- User feedback for all error conditions

## Performance Considerations

### Memory Management
- Limited log storage (500 entries max)
- Proper disposal of HTTP clients
- Event subscription cleanup

### API Efficiency
- Batched error processing to reduce API calls
- Conversation history management
- Tool-based responses for direct Unity manipulation

### UI Responsiveness
- Async API calls to prevent blocking
- Progressive UI updates during long operations
- Proper scroll management for large conversations

## Security Considerations

### API Key Management
- Environment variable support
- Config file fallback
- Clear error messages for missing keys

### Input Validation
- Command parsing with bounds checking
- Safe tool parameter validation
- Proper error handling for malformed inputs

## Extensibility Points

### Adding New Commands
Extend `ChatCommandHandler.HandleCommand()` with new cases.

### Adding New Tools for Claude
1. Add tool definition in `ClaudeAIAgent.GetUnityTools()`
2. Implement execution logic in `ExecuteToolAsync()`
3. Update documentation

### Adding New Message Types
1. Extend `MessageType` enum in `ChatData.cs`
2. Update rendering logic in `ChatMessageRenderer`
3. Add appropriate styling

## Recommended Refactoring

### Priority 1: ChatWindow.cs (571 lines → multiple files)
**Suggested breakdown**:
- `ChatWindow.cs` (core window logic, ~200 lines)
- `ChatWindowState.cs` (state management, ~150 lines)
- `ChatWindowEvents.cs` (event handling, ~100 lines)
- `ChatWindowUI.cs` (UI drawing methods, ~121 lines)

### Priority 2: ClaudeAIAgent.cs (1020 lines → multiple files)
**Suggested breakdown**:
- `ClaudeAIAgent.cs` (core API logic, ~300 lines)
- `ClaudeAPIModels.cs` (data structures, ~200 lines)
- `ClaudeUnityTools.cs` (Unity tool implementations, ~520 lines)

## Dependencies

### External Packages
- **Newtonsoft.Json**: For Claude API serialization
- **System.Net.Http**: For HTTP communication

### Unity Dependencies
- **UnityEditor**: All editor functionality
- **UnityEngine**: Core Unity API access

## Testing Strategy

### Unit Testing Opportunities
- Command parsing logic
- Error batching algorithms
- Message formatting
- Tool parameter validation

### Integration Testing
- API communication with mocked responses
- Error handling workflows
- UI state management

### Manual Testing Scenarios
- Error fixing with various error types
- Long conversation management
- API key configuration scenarios
- Tool execution verification

## Conclusion

This editor script system represents a sophisticated integration of AI capabilities into Unity's development environment. The modular architecture provides a solid foundation for further development while maintaining clear separation of concerns and robust error handling.

The system successfully demonstrates:
- Real-time AI integration for development assistance
- Automated error detection and fixing
- Professional-grade Unity Editor tool development
- Scalable architecture for future enhancements

**Next Steps**: Implement the recommended refactoring to improve maintainability and adhere to the 500-line file size limit. 