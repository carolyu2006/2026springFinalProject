using UnityEditor;
using System.Linq;

public class VerifySetup
{
    public static string Execute()
    {
        var scenes = EditorBuildSettings.scenes;
        var lines = scenes.Select((s, i) => $"[{i}] {s.path} enabled={s.enabled}");
        return "Build Settings:\n" + string.Join("\n", lines);
    }
}
