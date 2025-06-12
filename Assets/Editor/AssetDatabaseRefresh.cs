using UnityEditor;
using UnityEngine;

public class AssetDatabaseRefresh : EditorWindow
{
    [MenuItem("Tools/Refresh Asset Database")]
    public static void RefreshAssetDatabase()
    {
        Debug.Log("Refreshing Unity Asset Database...");
        AssetDatabase.Refresh();
        Debug.Log("Asset Database refresh complete!");
    }
} 