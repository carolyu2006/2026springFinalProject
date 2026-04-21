using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

public static class SetupPhoneStartScene
{
    public static void Execute()
    {
        AssetDatabase.Refresh();

        const string scenePath = "Assets/Scenes/PhoneStartScene.unity";

        // 1. Create the scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Create GameObject + PhoneStartSceneManager
        var managerGO = new GameObject("PhoneStartSceneManager");
        var managerType = System.Type.GetType("PhoneStartSceneManager, Assembly-CSharp");
        if (managerType == null)
        {
            Debug.LogError("PhoneStartSceneManager type not found. Has it compiled?");
            return;
        }
        var manager = managerGO.AddComponent(managerType);

        // 3. Invoke the editor-only Build UI Hierarchy method via reflection
        var buildMethod = managerType.GetMethod("EditorBuildUI",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (buildMethod != null)
        {
            buildMethod.Invoke(manager, null);
        }
        else
        {
            Debug.LogError("EditorBuildUI method not found on PhoneStartSceneManager.");
        }

        // 4. Assign prefab references via SerializedObject
        var so = new SerializedObject(manager);

        AssignPrefab(so, "wsClientPrefab",
            "Assets/Prefab/WebSocketClient.prefab", "WebSocketClient");
        AssignPrefab(so, "phoneInputManagerPrefab",
            "Assets/Prefab/PhoneInputManager.prefab", "PhoneInputManager");
        AssignPrefab(so, "networkManagerPrefab",
            "Assets/Prefab/JoystickNetworkManager.prefab", "JoystickNetworkManager");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);

        // 5. Save the scene
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"Scene saved to {scenePath}");

        // 6. Add to Build Settings (append if not already present)
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Any(s => s.path == scenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"Added {scenePath} to Build Settings.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SetupPhoneStartScene] Done.");
    }

    private static void AssignPrefab(SerializedObject so, string propName, string prefabPath, string componentTypeName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab not found at {prefabPath}");
            return;
        }
        var componentType = System.Type.GetType($"{componentTypeName}, Assembly-CSharp");
        if (componentType == null)
        {
            Debug.LogWarning($"Type {componentTypeName} not found.");
            return;
        }
        var comp = prefab.GetComponent(componentType);
        if (comp == null)
        {
            Debug.LogWarning($"Component {componentTypeName} not found on {prefabPath}");
            return;
        }
        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            Debug.LogWarning($"Property {propName} not found on manager.");
            return;
        }
        prop.objectReferenceValue = comp;
        Debug.Log($"Assigned {propName} = {prefabPath}");
    }
}
