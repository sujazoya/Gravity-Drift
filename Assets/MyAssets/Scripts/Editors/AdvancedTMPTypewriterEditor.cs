using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AdvancedTMPTypewriter))]
public class AdvancedTMPTypewriterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null) return; // Safety check

        AdvancedTMPTypewriter typewriter = target as AdvancedTMPTypewriter;
        if (typewriter == null) return;

        serializedObject.Update();

        // Draw all serialized fields safely
        DrawPropertiesExcluding(serializedObject, "m_Script");

        serializedObject.ApplyModifiedProperties();

        // --- Runtime Controls ---
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Status: {(typewriter.IsTyping ? (typewriter.IsPaused ? "Paused" : "Typing") : "Idle")}");

            // Safe Progress display
            float progress = 0f;
            try
            {
                progress = typewriter.Progress;
            }
            catch
            {
                progress = 0f;
            }
            EditorGUILayout.LabelField($"Progress: {progress:P0}");

            EditorGUILayout.LabelField($"Page: {typewriter.CurrentPage}/{typewriter.TotalPages}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Typing")) typewriter.StartTyping();
            if (GUILayout.Button("Stop Typing")) typewriter.StopTyping();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (typewriter.IsTyping)
            {
                if (typewriter.IsPaused)
                {
                    if (GUILayout.Button("Resume")) typewriter.ResumeTyping();
                }
                else
                {
                    if (GUILayout.Button("Pause")) typewriter.PauseTyping();
                }

                if (GUILayout.Button("Skip Page")) typewriter.SkipCurrentPage();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
