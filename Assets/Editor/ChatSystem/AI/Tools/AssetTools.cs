using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public static class AssetTools
{
    public static List<ClaudeTool> GetAssetTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "manage_asset",
                description = "Perform comprehensive asset operations in Unity including create, import, modify, delete, search, and get info on various asset types.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["action"] = new ClaudeToolProperty { type = "string", description = "Operation to perform: 'create', 'import', 'modify', 'delete', 'duplicate', 'move', 'rename', 'search', 'get_info', 'create_folder'" },
                        ["path"] = new ClaudeToolProperty { type = "string", description = "Asset path relative to Assets folder (e.g., 'Materials/MyMaterial.mat')" },
                        ["asset_type"] = new ClaudeToolProperty { type = "string", description = "Asset type for create operations: 'Material', 'Folder', 'Texture2D', 'AnimationClip', 'AudioClip', 'Mesh', 'Prefab'" },
                        ["properties"] = new ClaudeToolProperty { type = "object", description = "Dictionary of properties to set for create/modify operations" },
                        ["destination"] = new ClaudeToolProperty { type = "string", description = "Target path for duplicate/move operations" },
                        ["search_pattern"] = new ClaudeToolProperty { type = "string", description = "Search pattern for search operations (e.g., '*.prefab', '*.mat')" },
                        ["filter_type"] = new ClaudeToolProperty { type = "string", description = "Filter by asset type during search" }
                    },
                    required = new List<string> { "action", "path" }
                }
            },
            new ClaudeTool
            {
                name = "refresh_assets",
                description = "Refresh the Unity Asset Database to detect changes made outside Unity.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["import_options"] = new ClaudeToolProperty { type = "string", description = "Import options: 'default', 'force_update', 'force_synchronous'" }
                    },
                    required = new List<string>()
                }
            }
        };
    }
    
    public static string ExecuteAssetTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "manage_asset":
                return ManageAsset(inputDict);
            case "refresh_assets":
                return RefreshAssets(inputDict);
            default:
                return $"Unknown asset tool: {toolUse.name}";
        }
    }
    
    private static string ManageAsset(Dictionary<string, object> input)
    {
        try
        {
            var action = input["action"].ToString().ToLower();
            var path = input["path"].ToString();
            
            // Ensure path starts with Assets/
            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/" + path;
            }
            
            switch (action)
            {
                case "create":
                    return CreateAsset(path, input);
                case "import":
                    return ImportAsset(path, input);
                case "modify":
                    return ModifyAsset(path, input);
                case "delete":
                    return DeleteAsset(path);
                case "duplicate":
                    return DuplicateAsset(path, input);
                case "move":
                    return MoveAsset(path, input);
                case "rename":
                    return RenameAsset(path, input);
                case "search":
                    return SearchAssets(input);
                case "get_info":
                    return GetAssetInfo(path);
                case "create_folder":
                    return CreateFolder(path);
                default:
                    return $"Unknown asset action: {action}";
            }
        }
        catch (Exception ex)
        {
            return $"Asset operation failed: {ex.Message}";
        }
    }
    
    private static string CreateAsset(string path, Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("asset_type"))
            {
                return "Error: asset_type is required for create operation";
            }
            
            var assetType = input["asset_type"].ToString();
            var properties = input.ContainsKey("properties") ? 
                JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(input["properties"])) : 
                new Dictionary<string, object>();
            
            UnityEngine.Object asset = null;
            
            switch (assetType.ToLower())
            {
                case "material":
                    asset = new Material(Shader.Find("Standard"));
                    ApplyMaterialProperties(asset as Material, properties);
                    break;
                    
                case "folder":
                    return CreateFolder(path);
                    
                case "texture2d":
                    // Create a simple colored texture
                    var width = properties.ContainsKey("width") ? Convert.ToInt32(properties["width"]) : 256;
                    var height = properties.ContainsKey("height") ? Convert.ToInt32(properties["height"]) : 256;
                    var texture = new Texture2D(width, height);
                    
                    // Fill with color if specified
                    if (properties.ContainsKey("color"))
                    {
                        var colorArray = JsonConvert.DeserializeObject<float[]>(JsonConvert.SerializeObject(properties["color"]));
                        var color = new Color(colorArray[0], colorArray[1], colorArray[2], colorArray.Length > 3 ? colorArray[3] : 1f);
                        var colors = new Color[width * height];
                        for (int i = 0; i < colors.Length; i++) colors[i] = color;
                        texture.SetPixels(colors);
                        texture.Apply();
                    }
                    
                    asset = texture;
                    break;
                    
                case "prefab":
                    // Create empty GameObject and convert to prefab
                    var go = new GameObject(Path.GetFileNameWithoutExtension(path));
                    asset = PrefabUtility.SaveAsPrefabAsset(go, path);
                    UnityEngine.Object.DestroyImmediate(go);
                    break;
                    
                default:
                    return $"Unsupported asset type for creation: {assetType}";
            }
            
            if (asset != null)
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return $"Successfully created {assetType} asset at {path}";
            }
            
            return $"Failed to create {assetType} asset";
        }
        catch (Exception ex)
        {
            return $"Failed to create asset: {ex.Message}";
        }
    }
    
    private static void ApplyMaterialProperties(Material material, Dictionary<string, object> properties)
    {
        foreach (var prop in properties)
        {
            try
            {
                switch (prop.Key.ToLower())
                {
                    case "color":
                    case "maincolor":
                        var colorArray = JsonConvert.DeserializeObject<float[]>(JsonConvert.SerializeObject(prop.Value));
                        material.color = new Color(colorArray[0], colorArray[1], colorArray[2], colorArray.Length > 3 ? colorArray[3] : 1f);
                        break;
                    case "metallic":
                        material.SetFloat("_Metallic", Convert.ToSingle(prop.Value));
                        break;
                    case "smoothness":
                        material.SetFloat("_Glossiness", Convert.ToSingle(prop.Value));
                        break;
                    case "emission":
                        material.EnableKeyword("_EMISSION");
                        var emissionArray = JsonConvert.DeserializeObject<float[]>(JsonConvert.SerializeObject(prop.Value));
                        material.SetColor("_EmissionColor", new Color(emissionArray[0], emissionArray[1], emissionArray[2]));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to set material property {prop.Key}: {ex.Message}");
            }
        }
    }
    
    private static string CreateFolder(string path)
    {
        try
        {
            var folderPath = Path.GetDirectoryName(path);
            var folderName = Path.GetFileName(path);
            
            if (string.IsNullOrEmpty(folderPath) || folderPath == "Assets")
            {
                folderPath = "Assets";
            }
            
            var guid = AssetDatabase.CreateFolder(folderPath, folderName);
            if (!string.IsNullOrEmpty(guid))
            {
                return $"Successfully created folder at {path}";
            }
            return "Failed to create folder";
        }
        catch (Exception ex)
        {
            return $"Failed to create folder: {ex.Message}";
        }
    }
    
    private static string DeleteAsset(string path)
    {
        try
        {
            if (AssetDatabase.DeleteAsset(path))
            {
                AssetDatabase.Refresh();
                return $"Successfully deleted asset at {path}";
            }
            return $"Failed to delete asset at {path} (asset may not exist)";
        }
        catch (Exception ex)
        {
            return $"Failed to delete asset: {ex.Message}";
        }
    }
    
    private static string DuplicateAsset(string path, Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("destination"))
            {
                return "Error: destination is required for duplicate operation";
            }
            
            var destination = input["destination"].ToString();
            if (!destination.StartsWith("Assets/"))
            {
                destination = "Assets/" + destination;
            }
            
            if (AssetDatabase.CopyAsset(path, destination))
            {
                AssetDatabase.Refresh();
                return $"Successfully duplicated asset from {path} to {destination}";
            }
            return $"Failed to duplicate asset from {path} to {destination}";
        }
        catch (Exception ex)
        {
            return $"Failed to duplicate asset: {ex.Message}";
        }
    }
    
    private static string MoveAsset(string path, Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("destination"))
            {
                return "Error: destination is required for move operation";
            }
            
            var destination = input["destination"].ToString();
            if (!destination.StartsWith("Assets/"))
            {
                destination = "Assets/" + destination;
            }
            
            var error = AssetDatabase.MoveAsset(path, destination);
            if (string.IsNullOrEmpty(error))
            {
                AssetDatabase.Refresh();
                return $"Successfully moved asset from {path} to {destination}";
            }
            return $"Failed to move asset: {error}";
        }
        catch (Exception ex)
        {
            return $"Failed to move asset: {ex.Message}";
        }
    }
    
    private static string RenameAsset(string path, Dictionary<string, object> input)
    {
        try
        {
            var newName = Path.GetFileNameWithoutExtension(path);
            if (input.ContainsKey("name"))
            {
                newName = input["name"].ToString();
            }
            
            var error = AssetDatabase.RenameAsset(path, newName);
            if (string.IsNullOrEmpty(error))
            {
                AssetDatabase.Refresh();
                return $"Successfully renamed asset at {path} to {newName}";
            }
            return $"Failed to rename asset: {error}";
        }
        catch (Exception ex)
        {
            return $"Failed to rename asset: {ex.Message}";
        }
    }
    
    private static string SearchAssets(Dictionary<string, object> input)
    {
        try
        {
            var searchPattern = input.ContainsKey("search_pattern") ? input["search_pattern"].ToString() : "*";
            var filterType = input.ContainsKey("filter_type") ? input["filter_type"].ToString() : "";
            
            var searchFilter = searchPattern;
            if (!string.IsNullOrEmpty(filterType))
            {
                searchFilter += $" t:{filterType}";
            }
            
            var guids = AssetDatabase.FindAssets(searchFilter);
            
            if (guids.Length == 0)
            {
                return $"No assets found matching pattern '{searchPattern}'";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Found {guids.Length} asset(s) matching '{searchPattern}':");
            result.AppendLine();
            
            foreach (var guid in guids.Take(50)) // Limit to first 50 results
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                
                result.AppendLine($"📁 {assetPath}");
                result.AppendLine($"   Type: {asset?.GetType().Name ?? "Unknown"}");
                result.AppendLine($"   GUID: {guid}");
                result.AppendLine();
            }
            
            if (guids.Length > 50)
            {
                result.AppendLine($"... and {guids.Length - 50} more results");
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to search assets: {ex.Message}";
        }
    }
    
    private static string GetAssetInfo(string path)
    {
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
            {
                return $"Asset not found at {path}";
            }
            
            var guid = AssetDatabase.AssetPathToGUID(path);
            var importer = AssetImporter.GetAtPath(path);
            
            var result = new StringBuilder();
            result.AppendLine($"Asset Information for: {path}");
            result.AppendLine($"Name: {asset.name}");
            result.AppendLine($"Type: {asset.GetType().Name}");
            result.AppendLine($"GUID: {guid}");
            result.AppendLine($"Importer: {importer?.GetType().Name ?? "None"}");
            
            // Get file info if it exists
            var fullPath = Path.Combine(Application.dataPath, "..", path);
            if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                result.AppendLine($"File Size: {fileInfo.Length} bytes");
                result.AppendLine($"Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to get asset info: {ex.Message}";
        }
    }
    
    private static string ImportAsset(string path, Dictionary<string, object> input)
    {
        try
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            return $"Successfully imported asset at {path}";
        }
        catch (Exception ex)
        {
            return $"Failed to import asset: {ex.Message}";
        }
    }
    
    private static string ModifyAsset(string path, Dictionary<string, object> input)
    {
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
            {
                return $"Asset not found at {path}";
            }
            
            if (asset is Material material && input.ContainsKey("properties"))
            {
                var properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(input["properties"]));
                ApplyMaterialProperties(material, properties);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                return $"Successfully modified material at {path}";
            }
            
            return $"Modification not supported for asset type: {asset.GetType().Name}";
        }
        catch (Exception ex)
        {
            return $"Failed to modify asset: {ex.Message}";
        }
    }
    
    private static string RefreshAssets(Dictionary<string, object> input)
    {
        try
        {
            var importOptions = input.ContainsKey("import_options") ? input["import_options"].ToString() : "default";
            
            switch (importOptions.ToLower())
            {
                case "force_update":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    break;
                case "force_synchronous":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    break;
                default:
                    AssetDatabase.Refresh();
                    break;
            }
            
            return "Successfully refreshed Asset Database";
        }
        catch (Exception ex)
        {
            return $"Failed to refresh assets: {ex.Message}";
        }
    }
} 