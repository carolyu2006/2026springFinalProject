using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class SetupBuildSettings
{
    [MenuItem("Tools/Setup Build Settings")]
    public static void Run()
    {
        UnityEngine.Debug.Log(Execute());
    }

    public static string Execute()
    {
        // Desired scene order — ControlSelectScene first
        string[] desiredOrder = new[]
        {
            "Assets/Scenes/ControlSelectScene.unity",
            "Assets/Scenes/StartScene.unity",
            "Assets/Scenes/InstructionScene.unity",
            "Assets/Scenes/EnglishInstructionScene.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/EndScene.unity",
            "Assets/Scenes/EnglishEndScene.unity",
            "Assets/Scenes/Scene1.unity"
        };

        var result = new List<EditorBuildSettingsScene>();
        var existing = EditorBuildSettings.scenes.ToList();

        foreach (var path in desiredOrder)
        {
            // Check if scene file exists
            if (System.IO.File.Exists(path))
            {
                result.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        // Add any other scenes that weren't in our list
        foreach (var scene in existing)
        {
            if (!result.Any(s => s.path == scene.path))
                result.Add(scene);
        }

        EditorBuildSettings.scenes = result.ToArray();

        var summary = string.Join("\n", result.Select((s, i) => $"  [{i}] {s.path} (enabled={s.enabled})"));
        return $"Build settings updated:\n{summary}";
    }
}
