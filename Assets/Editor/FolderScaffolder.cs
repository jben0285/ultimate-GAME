using UnityEditor;
using UnityEngine;
using System.IO;

public class FolderScaffolder : Editor
{
    [MenuItem("Tools/Scaffold Folders")]
    public static void ScaffoldFolders()
    {
        // Define the desired folder structure
        string[] folders = {
            "Assets/Audio",
            "Assets/Materials",
            "Assets/Resources",
            "Assets/Resources/Prefabs", // Prefabs inside Resources
            "Assets/Scripts",
            "Assets/Shaders",
            "Assets/Textures",
            "Assets/UI",
            "Assets/Scenes"
        };

        // Create the folders
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                Debug.Log($"Created folder: {folder}");
            }
            else
            {
                Debug.Log($"Folder already exists: {folder}");
            }
        }

        // Refresh the AssetDatabase to reflect changes
        AssetDatabase.Refresh();
    }
}
