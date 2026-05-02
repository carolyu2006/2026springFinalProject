using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

public static class DebugIntro
{
    public static void Execute()
    {
        var go = GameObject.Find("GameManager");
        if (go == null) { Debug.Log("[INTRO] no GameManager"); return; }
        var mgr = go.GetComponent<InstructionSceneManager>();
        if (mgr == null) { Debug.Log("[INTRO] no manager"); return; }

        var t = typeof(InstructionSceneManager);
        var f = t.GetField("introCharacters", BindingFlags.Instance | BindingFlags.NonPublic);
        var list = f?.GetValue(mgr) as List<Transform>;
        Debug.Log($"[INTRO] introCharacters count={(list == null ? -1 : list.Count)}");
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                Debug.Log($"[INTRO]  [{i}] {(c == null ? "<null>" : c.name + " pos=" + c.position)}");
            }
        }

        var op = t.GetField("orangeProp", BindingFlags.Instance | BindingFlags.NonPublic);
        var propVal = op?.GetValue(mgr) as Transform;
        Debug.Log($"[INTRO] orangeProp = {(propVal == null ? "<null>" : propVal.name)}");

        var ll = t.GetField("lines", BindingFlags.Instance | BindingFlags.NonPublic);
        var lines = ll?.GetValue(mgr) as System.Collections.IList;
        Debug.Log($"[INTRO] lines count={(lines == null ? -1 : lines.Count)}");
    }
}
