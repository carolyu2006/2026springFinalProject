using UnityEngine;
using UnityEditor;

public class SetInactive
{
    public static string Execute()
    {
        var go = GameObject.Find("Canvas/PhonePanel");
        if (go != null)
        {
            go.SetActive(false);
            EditorUtility.SetDirty(go);
            return "PhonePanel set to inactive";
        }
        return "PhonePanel not found";
    }
}
