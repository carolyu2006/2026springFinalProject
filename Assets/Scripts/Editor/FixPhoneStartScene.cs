using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class FixPhoneStartScene
{
    public static void Execute()
    {
        const string scenePath = "Assets/Scenes/PhoneStartScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        bool hasEventSystem = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<EventSystem>() != null)
            {
                hasEventSystem = true;
                break;
            }
        }

        if (!hasEventSystem)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("[FixPhoneStartScene] Added EventSystem.");
        }
        else
        {
            Debug.Log("[FixPhoneStartScene] EventSystem already present.");
        }

        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[FixPhoneStartScene] Done.");
    }
}
