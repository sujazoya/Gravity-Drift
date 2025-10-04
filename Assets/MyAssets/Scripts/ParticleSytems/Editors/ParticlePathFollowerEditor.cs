// Save as Assets/Editor/ParticlePathFollowerEditor.cs
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ParticlePathFollower))]
public class ParticlePathFollowerEditor : Editor
{
    // --- Serialized Properties ---
    private SerializedProperty pathType, pathPoints, loop;
    private SerializedProperty travelMode, fixedTravelTime, travelSpeed, alignToPath, speedCurve;
    private SerializedProperty distributeAlongPath, distributionSpread, randomPhase;
    private SerializedProperty shapeMode, shapeRadius, spiralTurns, curveY;
    private SerializedProperty useNoise, noiseAmplitude, noiseFrequency, noiseSpeed;
    private SerializedProperty spreadAngle, spreadRadius;

    private void OnEnable()
    {
        // Path
        pathType = serializedObject.FindProperty("pathType");
        pathPoints = serializedObject.FindProperty("pathPoints");
        loop = serializedObject.FindProperty("loop");

        // Movement
        travelMode = serializedObject.FindProperty("travelMode");
        fixedTravelTime = serializedObject.FindProperty("fixedTravelTime");
        travelSpeed = serializedObject.FindProperty("travelSpeed");
        alignToPath = serializedObject.FindProperty("alignToPath");
        speedCurve = serializedObject.FindProperty("speedCurve");

        // Distribution
        distributeAlongPath = serializedObject.FindProperty("distributeAlongPath");
        distributionSpread = serializedObject.FindProperty("distributionSpread");
        randomPhase = serializedObject.FindProperty("randomPhase");

        // Shape
        shapeMode = serializedObject.FindProperty("shapeMode");
        shapeRadius = serializedObject.FindProperty("shapeRadius");
        spiralTurns = serializedObject.FindProperty("spiralTurns");
        curveY = serializedObject.FindProperty("curveY");

        // Noise
        useNoise = serializedObject.FindProperty("useNoise");
        noiseAmplitude = serializedObject.FindProperty("noiseAmplitude");
        noiseFrequency = serializedObject.FindProperty("noiseFrequency");
        noiseSpeed = serializedObject.FindProperty("noiseSpeed");

        // Spread
        spreadAngle = serializedObject.FindProperty("spreadAngle");
        spreadRadius = serializedObject.FindProperty("spreadRadius");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Path ---
        EditorGUILayout.LabelField("Path Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pathType);
        EditorGUILayout.PropertyField(pathPoints, true);
        EditorGUILayout.PropertyField(loop);

        EditorGUILayout.Space();

        // --- Movement ---
        EditorGUILayout.LabelField("Movement Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(travelMode);
        if ((ParticlePathFollower.TravelMode)travelMode.enumValueIndex != ParticlePathFollower.TravelMode.Lifetime)
            EditorGUILayout.PropertyField(fixedTravelTime);
        EditorGUILayout.PropertyField(travelSpeed);
        EditorGUILayout.PropertyField(alignToPath);
        EditorGUILayout.PropertyField(speedCurve);

        EditorGUILayout.Space();

        // --- Distribution ---
        EditorGUILayout.LabelField("Distribution Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(distributeAlongPath);
        EditorGUILayout.Slider(randomPhase, 0f, 1f, new GUIContent("Random Phase"));
        EditorGUILayout.Slider(distributionSpread, 0f, 1f, new GUIContent("Distribution Spread"));

        EditorGUILayout.Space();

        // --- Shape ---
        EditorGUILayout.LabelField("Shape Adjustment", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shapeMode);
        if ((ParticlePathFollower.ShapeMode)shapeMode.enumValueIndex != ParticlePathFollower.ShapeMode.None)
        {
            EditorGUILayout.PropertyField(shapeRadius);
            if ((ParticlePathFollower.ShapeMode)shapeMode.enumValueIndex == ParticlePathFollower.ShapeMode.Spiral)
                EditorGUILayout.PropertyField(spiralTurns);
            if ((ParticlePathFollower.ShapeMode)shapeMode.enumValueIndex == ParticlePathFollower.ShapeMode.Curve)
                EditorGUILayout.PropertyField(curveY);
        }

        EditorGUILayout.Space();

        // --- Noise ---
        EditorGUILayout.LabelField("Noise & Turbulence", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useNoise);
        if (useNoise.boolValue)
        {
            EditorGUILayout.PropertyField(noiseAmplitude);
            EditorGUILayout.PropertyField(noiseFrequency);
            EditorGUILayout.PropertyField(noiseSpeed);
        }

        EditorGUILayout.Space();

        // --- Spread ---
        EditorGUILayout.LabelField("Spread Settings", EditorStyles.boldLabel);
        EditorGUILayout.Slider(spreadAngle, 0f, 180f, new GUIContent("Spread Angle"));
        EditorGUILayout.PropertyField(spreadRadius, new GUIContent("Spread Radius"));

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        ParticlePathFollower follower = (ParticlePathFollower)target;
        if (follower.pathPoints == null || follower.pathPoints.Count < 2)
            return;

        // --- Path with draggable handles ---
        Handles.color = Color.cyan;
        for (int i = 0; i < follower.pathPoints.Count; i++)
        {
            if (follower.pathPoints[i] == null) continue;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(follower.pathPoints[i].position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(follower.pathPoints[i], "Move Path Point");
                follower.pathPoints[i].position = newPos;
            }

            GUIStyle labelStyle = new GUIStyle { normal = { textColor = Color.white } };
            Handles.Label(follower.pathPoints[i].position + Vector3.up * 0.2f, $"P{i}", labelStyle);

            if (i < follower.pathPoints.Count - 1)
                Handles.DrawLine(follower.pathPoints[i].position, follower.pathPoints[i + 1].position);
        }
        if (follower.loop && follower.pathPoints.Count > 1)
            Handles.DrawLine(follower.pathPoints[^1].position, follower.pathPoints[0].position);

        // --- Distribution Preview ---
        if (follower.distributeAlongPath)
        {
            Handles.color = Color.yellow;
            int previewCount = 20;
            for (int i = 0; i < previewCount; i++)
            {
                float t = i / (float)(previewCount - 1);
                float spreadT = t * follower.distributionSpread;

                if (follower.randomPhase > 0f)
                {
                    float offset = Mathf.Sin(i * 13.37f) * 0.5f + 0.5f; // deterministic pseudo-random
                    spreadT = Mathf.Repeat(spreadT + offset / previewCount, 1f);
                }
                else
                {
                    spreadT = Mathf.Repeat(spreadT, 1f);
                }

                Vector3 pos = follower.GetPathPositionEditor(spreadT);
                Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.05f, EventType.Repaint);
            }
        }

        // --- Shape Previews ---
        Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
        int previewSteps = 16;

        for (int i = 0; i <= previewSteps; i++)
        {
            float t = i / (float)previewSteps;
            Vector3 pathPos = follower.GetPathPositionEditor(t);

            switch (follower.shapeMode)
            {
                case ParticlePathFollower.ShapeMode.Circle:
                    Handles.DrawWireDisc(pathPos, Vector3.up, follower.shapeRadius);
                    break;

                case ParticlePathFollower.ShapeMode.Spiral:
                    float angle = t * follower.spiralTurns * Mathf.PI * 2f;
                    Vector3 spiralOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * follower.shapeRadius * t;
                    Handles.DrawWireDisc(pathPos + spiralOffset, Vector3.up, 0.05f);
                    break;

                case ParticlePathFollower.ShapeMode.Curve:
                    float y = follower.curveY.Evaluate(t) * follower.shapeRadius;
                    Vector3 curveOffset = new Vector3(0, y, 0);
                    Handles.DrawWireDisc(pathPos + curveOffset, Vector3.up, 0.05f);
                    break;
            }
        }

       // --- Spread Preview (3D Cone) ---
        if (follower.spreadAngle > 0f && follower.spreadRadius > 0f)
        {
            Handles.color = new Color(0f, 1f, 0f, 0.25f);

            Vector3 origin = follower.transform.position;
            Vector3 forward = follower.transform.forward;

            // Draw central forward line
            Handles.DrawLine(origin, origin + forward * follower.spreadRadius);

            // Draw cone surface approximation
            int coneSteps = 24;
            for (int i = 0; i < coneSteps; i++)
            {
                float angleA = (i / (float)coneSteps) * 360f;
                float angleB = ((i + 1) / (float)coneSteps) * 360f;

                Vector3 dirA = Quaternion.AngleAxis(follower.spreadAngle, follower.transform.up) *
                            (Quaternion.AngleAxis(angleA, forward) * forward);
                Vector3 dirB = Quaternion.AngleAxis(follower.spreadAngle, follower.transform.up) *
                            (Quaternion.AngleAxis(angleB, forward) * forward);

                Vector3 posA = origin + dirA * follower.spreadRadius;
                Vector3 posB = origin + dirB * follower.spreadRadius;

                Handles.DrawLine(origin, posA);
                Handles.DrawLine(posA, posB);
            }

            // Draw wire disc at spread radius
            Handles.DrawWireDisc(origin + forward * follower.spreadRadius, forward, Mathf.Tan(follower.spreadAngle * Mathf.Deg2Rad) * follower.spreadRadius);
        }

    }
}
