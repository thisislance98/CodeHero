using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using UnityEditor.SceneManagement;

public static class GameObjectTools
{
    public static List<ClaudeTool> GetGameObjectTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "create_gameobject",
                description = "Create a new GameObject in the current scene. Can create empty GameObjects or primitive shapes like cubes, spheres, etc.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject (optional, will use primitive type or 'GameObject' as default)" },
                        ["primitive_type"] = new ClaudeToolProperty 
                        { 
                            type = "string", 
                            description = "Type of primitive to create. Must be one of: Cube, Sphere, Cylinder, Plane, Quad, Capsule. If not specified, creates an empty GameObject."
                        },
                        ["position"] = new ClaudeToolProperty { type = "string", description = "Position as 'x,y,z' (default: 0,0,0)" }
                    },
                    required = new List<string>()
                }
            },
            new ClaudeTool
            {
                name = "add_component",
                description = "Add a component to a GameObject",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject to add component to" },
                        ["component_type"] = new ClaudeToolProperty { type = "string", description = "Type of component to add (e.g., Rigidbody, BoxCollider)" }
                    },
                    required = new List<string> { "gameobject_name", "component_type" }
                }
            },
            new ClaudeTool
            {
                name = "set_transform",
                description = "Set the position, rotation, or scale of a GameObject",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject" },
                        ["position"] = new ClaudeToolProperty { type = "string", description = "Position as 'x,y,z'" },
                        ["rotation"] = new ClaudeToolProperty { type = "string", description = "Rotation as 'x,y,z' (euler angles)" },
                        ["scale"] = new ClaudeToolProperty { type = "string", description = "Scale as 'x,y,z'" }
                    },
                    required = new List<string> { "gameobject_name" }
                }
            },
            new ClaudeTool
            {
                name = "list_gameobjects",
                description = "List all GameObjects in the current scene",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>(),
                    required = new List<string>()
                }
            },
            new ClaudeTool
            {
                name = "delete_gameobject",
                description = "Delete a GameObject from the scene",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject to delete" }
                    },
                    required = new List<string> { "gameobject_name" }
                }
            },
            new ClaudeTool
            {
                name = "view_gameobject",
                description = "View detailed information about a GameObject including all its components, transform properties, and other details",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject to inspect" }
                    },
                    required = new List<string> { "gameobject_name" }
                }
            }
        };
    }
    
    public static string ExecuteGameObjectTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "create_gameobject":
                Debug.Log("[ClaudeAI] GameObjectTools: Calling CreateGameObject");
                return CreateGameObject(inputDict);
                
            case "add_component":
                Debug.Log("[ClaudeAI] GameObjectTools: Calling AddComponent");
                return AddComponent(inputDict);
                
            case "set_transform":
                Debug.Log("[ClaudeAI] GameObjectTools: Calling SetTransform");
                return SetTransform(inputDict);
                
            case "list_gameobjects":
                Debug.Log("[ClaudeAI] GameObjectTools: Calling ListGameObjects");
                return ListGameObjects();
                
            case "delete_gameobject":
                Debug.Log("[ClaudeAI] GameObjectTools: Calling DeleteGameObject");
                return DeleteGameObject(inputDict);
                
            case "view_gameobject":
                Debug.Log("[ClaudeAI] GameObjectTools: Calling ViewGameObject");
                return ViewGameObject(inputDict);
                
            default:
                return $"Unknown GameObject tool: {toolUse.name}";
        }
    }
    
    private static string CreateGameObject(Dictionary<string, object> input)
    {
        Debug.Log("[ClaudeAI] CreateGameObject called");
        
        try
        {
            if (input == null)
            {
                Debug.LogError("[ClaudeAI] CreateGameObject: input is null!");
                return "Error: Input dictionary is null";
            }
            
            Debug.Log($"[ClaudeAI] CreateGameObject input keys: {string.Join(", ", input.Keys)}");
            
            // Ensure we have a name, use default if not provided
            var name = input.ContainsKey("name") ? input["name"]?.ToString() : "GameObject";
            Debug.Log($"[ClaudeAI] CreateGameObject name: {name}");
            
            // If no name but primitive_type is specified, use primitive type as name
            if (name == "GameObject" && input.ContainsKey("primitive_type"))
            {
                name = input["primitive_type"]?.ToString();
                Debug.Log($"[ClaudeAI] CreateGameObject name updated to: {name}");
            }
            
            // Create GameObject directly - Unity editor scripts run on main thread
            GameObject go = null;
            
            if (input.ContainsKey("primitive_type"))
            {
                var primitiveTypeStr = input["primitive_type"]?.ToString();
                Debug.Log($"[ClaudeAI] Creating primitive: {primitiveTypeStr}");
                
                if (System.Enum.TryParse<PrimitiveType>(primitiveTypeStr, out PrimitiveType primitiveType))
                {
                    Debug.Log($"[ClaudeAI] Creating primitive type: {primitiveType}");
                    go = GameObject.CreatePrimitive(primitiveType);
                    Debug.Log($"[ClaudeAI] GameObject.CreatePrimitive completed: {go?.name ?? "NULL"}");
                }
                else
                {
                    Debug.LogError($"[ClaudeAI] Invalid primitive type: {primitiveTypeStr}");
                    return $"Invalid primitive type: {primitiveTypeStr}";
                }
            }
            else
            {
                Debug.Log("[ClaudeAI] Creating empty GameObject");
                go = new GameObject();
                Debug.Log($"[ClaudeAI] new GameObject() completed: {go?.name ?? "NULL"}");
            }
            
            if (go != null)
            {
                Debug.Log($"[ClaudeAI] Setting GameObject name to: {name}");
                go.name = name;
                
                if (input.ContainsKey("position"))
                {
                    Debug.Log("[ClaudeAI] Setting position");
                    var posStr = input["position"].ToString();
                    var parts = posStr.Split(',');
                    if (parts.Length == 3 && 
                        float.TryParse(parts[0], out float x) &&
                        float.TryParse(parts[1], out float y) &&
                        float.TryParse(parts[2], out float z))
                    {
                        go.transform.position = new Vector3(x, y, z);
                        Debug.Log($"[ClaudeAI] Position set to: {x}, {y}, {z}");
                    }
                }
                
                Debug.Log("[ClaudeAI] Marking scene dirty and setting selection");
                Selection.activeGameObject = go;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                var result = $"GameObject '{name}' created successfully";
                Debug.Log($"[ClaudeAI] Success result: {result}");
                return result;
            }
            else
            {
                Debug.LogError("[ClaudeAI] GameObject is null after creation");
                return $"Failed to create GameObject '{name}'";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Exception in CreateGameObject: {ex.Message}\nStack trace: {ex.StackTrace}");
            return $"Failed to create GameObject: {ex.Message}";
        }
    }
    
    private static string AddComponent(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var componentType = input["component_type"].ToString();
            
            var go = GameObject.Find(gameObjectName);
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            var type = Type.GetType($"UnityEngine.{componentType}, UnityEngine") ?? 
                      Type.GetType($"{componentType}, Assembly-CSharp");
                      
            if (type == null)
            {
                return $"Component type '{componentType}' not found";
            }
            
            go.AddComponent(type);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return $"Component '{componentType}' added to '{gameObjectName}'";
        }
        catch (Exception ex)
        {
            return $"Failed to add component: {ex.Message}";
        }
    }
    
    private static string SetTransform(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var go = GameObject.Find(gameObjectName);
            
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            var result = new StringBuilder();
            
            if (input.ContainsKey("position"))
            {
                var posStr = input["position"].ToString();
                var parts = posStr.Split(',');
                if (parts.Length == 3)
                {
                    var pos = new Vector3(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    );
                    go.transform.position = pos;
                    result.AppendLine($"Position set to {pos}");
                }
            }
            
            if (input.ContainsKey("rotation"))
            {
                var rotStr = input["rotation"].ToString();
                var parts = rotStr.Split(',');
                if (parts.Length == 3)
                {
                    var rot = new Vector3(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    );
                    go.transform.rotation = Quaternion.Euler(rot);
                    result.AppendLine($"Rotation set to {rot}");
                }
            }
            
            if (input.ContainsKey("scale"))
            {
                var scaleStr = input["scale"].ToString();
                var parts = scaleStr.Split(',');
                if (parts.Length == 3)
                {
                    var scale = new Vector3(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    );
                    go.transform.localScale = scale;
                    result.AppendLine($"Scale set to {scale}");
                }
            }
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to set transform: {ex.Message}";
        }
    }
    
    private static string ListGameObjects()
    {
        try
        {
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            var result = new StringBuilder("GameObjects in scene:\n");
            
            foreach (var go in allObjects)
            {
                result.AppendLine($"- {go.name} (Position: {go.transform.position})");
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to list GameObjects: {ex.Message}";
        }
    }
    
    private static string DeleteGameObject(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var go = GameObject.Find(gameObjectName);
            
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            GameObject.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return $"GameObject '{gameObjectName}' deleted successfully";
        }
        catch (Exception ex)
        {
            return $"Failed to delete GameObject: {ex.Message}";
        }
    }
    
    private static string ViewGameObject(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var go = GameObject.Find(gameObjectName);
            
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"GameObject: {go.name}");
            result.AppendLine($"Active: {go.activeInHierarchy}");
            result.AppendLine($"Tag: {go.tag}");
            result.AppendLine($"Layer: {LayerMask.LayerToName(go.layer)} ({go.layer})");
            result.AppendLine();
            
            // Transform information
            result.AppendLine("Transform:");
            result.AppendLine($"  Position: {go.transform.position}");
            result.AppendLine($"  Rotation: {go.transform.rotation.eulerAngles}");
            result.AppendLine($"  Scale: {go.transform.localScale}");
            result.AppendLine();
            
            // Components
            var components = go.GetComponents<Component>();
            result.AppendLine($"Components ({components.Length}):");
            
            foreach (var component in components)
            {
                if (component == null) continue;
                
                var componentType = component.GetType();
                result.AppendLine($"  • {componentType.Name}");
                
                // Add some specific details for common components
                if (component is Rigidbody rb)
                {
                    result.AppendLine($"    - Mass: {rb.mass}");
                    result.AppendLine($"    - Use Gravity: {rb.useGravity}");
                    result.AppendLine($"    - Is Kinematic: {rb.isKinematic}");
                }
                else if (component is Collider col)
                {
                    result.AppendLine($"    - Is Trigger: {col.isTrigger}");
                    result.AppendLine($"    - Material: {(col.material ? col.material.name : "None")}");
                }
                else if (component is Renderer rend)
                {
                    result.AppendLine($"    - Enabled: {rend.enabled}");
                    if (rend.material)
                        result.AppendLine($"    - Material: {rend.material.name}");
                }
                else if (component is MonoBehaviour mb && component.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                {
                    // This is a custom script
                    result.AppendLine($"    - Custom Script");
                    result.AppendLine($"    - Enabled: {mb.enabled}");
                }
            }
            
            // Child objects count
            if (go.transform.childCount > 0)
            {
                result.AppendLine();
                result.AppendLine($"Children ({go.transform.childCount}):");
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    result.AppendLine($"  • {go.transform.GetChild(i).name}");
                }
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to view GameObject: {ex.Message}";
        }
    }
} 