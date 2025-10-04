using UnityEditor;
using UnityEngine;

public class MissingScriptFinder
{
    [MenuItem("Tools/Find Missing Scripts in Scene")]
    static void FindMissing()
    {
        GameObject[] gos = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;
        foreach (GameObject go in gos)
        {
            Component[] comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    Debug.LogWarning($"Missing script in: {go.name}", go);
                    count++;
                }
            }
        }
        Debug.Log($"Finished. Found {count} missing scripts.");
    }
}
