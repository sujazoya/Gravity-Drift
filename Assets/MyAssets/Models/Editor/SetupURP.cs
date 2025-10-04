using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SetupURP : EditorWindow
{
    [MenuItem("Tools/Setup URP")]
    public static void ShowWindow()
    {
        GetWindow<SetupURP>("URP Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Universal Render Pipeline Setup", EditorStyles.boldLabel);

        if (GUILayout.Button("Create and Assign URP Asset"))
        {
            CreateAndAssignURP();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "To upgrade built-in materials to URP, use:\nEdit → Render Pipeline → Universal Render Pipeline → Upgrade Project Materials to URP",
            MessageType.Info
        );
    }

    private static void CreateAndAssignURP()
    {
        // Create Forward Renderer
        string rendererPath = "Assets/ForwardRenderer.asset";
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(renderer, rendererPath);

        // Create URP Asset
        string urpPath = "Assets/UniversalRenderPipelineAsset.asset";
        var urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        AssetDatabase.CreateAsset(urpAsset, urpPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Assign URP Asset in Graphics & Quality
        GraphicsSettings.defaultRenderPipeline = urpAsset;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urpAsset;
        }

        Debug.Log("✅ URP asset created and assigned.\n👉 Now open the UniversalRenderPipelineAsset in Inspector and assign the ForwardRenderer manually.");
    }
}
