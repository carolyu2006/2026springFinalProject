using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SaveSceneToCorrectPath
{
    public static string Execute()
    {
        var scene = SceneManager.GetActiveScene();
        bool saved = EditorSceneManager.SaveScene(scene, "Assets/Scenes/ControlSelectScene.unity", false);
        return saved ? "Saved to Assets/Scenes/ControlSelectScene.unity" : "Save failed";
    }
}
