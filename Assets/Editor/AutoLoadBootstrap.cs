#if UNITY_EDITOR
using FishNet.Component.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class AutoLoadBootstrap
{
    static AutoLoadBootstrap()
    {
        // Automatically switch to the scene when "Play" is clicked in the editor
        EditorApplication.playModeStateChanged += LoadDefaultScene;
    }

    private static void LoadDefaultScene(PlayModeStateChange state)
    {
        Debug.Log("weoh");
        if (state == PlayModeStateChange.EnteredPlayMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Bootstrap")
        {
            EditorSceneManager.LoadScene("Assets/Scenes/Bootstrap.unity");
        }
    }
}
#endif
