# CodeHero - Unity AI Assistant

An innovative Unity project that integrates Claude AI directly into the Unity Editor, providing developers with an intelligent chat-based assistant for Unity development tasks.

![Unity](https://img.shields.io/badge/Unity-2022.3+-000000?style=flat&logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=c-sharp&logoColor=white)
![AI](https://img.shields.io/badge/Claude%20AI-FF6B35?style=flat&logo=anthropic&logoColor=white)

## 🚀 Features

- **AI-Powered Development**: Chat with Claude AI directly in Unity Editor
- **Voice Recognition**: Windows Speech Recognition for hands-free coding
- **Automatic Script Generation**: Create C# scripts through natural language
- **GameObject Manipulation**: Create and modify Unity objects via AI commands
- **Error Detection & Fixing**: Real-time error analysis and automatic fixes
- **Console Integration**: Unity console logs integrated into chat interface

## 🎯 Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/CodeHero.git
   ```

2. **Open in Unity**
   - Open Unity Hub
   - Click "Add" and select the project folder
   - Open with Unity 2022.3 or later

3. **Configure AI**
   - Add your Claude AI API key in `Assets/Editor/ClaudeAIAgent.cs`
   - Replace `API_KEY` constant with your actual key

4. **Start Using**
   - Go to `Tools → Chat Window` in Unity
   - Start chatting with Claude AI!

## 💬 Example Commands

```
"Create a player movement script with WASD controls"
"Make a red cube at position (0, 5, 0)"
"Add a Rigidbody to the Player GameObject"
"Help me fix this compilation error"
"Create a rotating platform script"
```

## 🛠 Requirements

- **Unity 2022.3+**
- **Windows OS** (for speech recognition)
- **Claude AI API Key**
- **Newtonsoft.Json** (included via Package Manager)

## 📁 Project Structure

```
CodeHero/
├── Assets/
│   ├── Scripts/           # Runtime game scripts
│   ├── Editor/            # Unity Editor extensions and AI integration
│   └── Scenes/            # Unity scene files
├── ProjectSettings/       # Unity project configuration
└── Packages/             # Unity package dependencies
```

## 🎮 How It Works

1. **Chat Interface**: Custom Unity Editor window provides real-time chat with Claude AI
2. **AI Tools**: Claude has access to Unity-specific tools for creating scripts and GameObjects
3. **Voice Input**: Use speech recognition for hands-free interaction
4. **Error Handling**: Automatic detection and fixing of compilation errors
5. **Context Awareness**: AI maintains conversation history for complex tasks

## 🔧 AI Tools Available

- `create_script` - Generate new C# scripts
- `create_gameobject` - Create GameObjects and primitives
- `add_component` - Add components to GameObjects
- `set_transform` - Modify position, rotation, and scale
- `list_gameobjects` - List all scene GameObjects
- `delete_gameobject` - Remove GameObjects from scene
- `edit_script` - Modify existing script content
- `read_script` - Read script file contents

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Claude AI by Anthropic](https://www.anthropic.com/) for the powerful AI capabilities
- [Unity Technologies](https://unity.com/) for the amazing game engine
- [Newtonsoft.Json](https://www.newtonsoft.com/json) for JSON serialization

## 📞 Support

If you have any questions or need help:
- Open an issue on GitHub
- Check the [detailed project report](GITHUB_REPORT.md) for technical details

---

**Made with ❤️ for the Unity developer community** 