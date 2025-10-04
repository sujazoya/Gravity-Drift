using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UltimatePointLightCookieDemo : MonoBehaviour
{
    [Header("Cookie Settings")]
    public List<Cubemap> cookies = new List<Cubemap>(); // Assign multiple cookies here
    private int currentCookieIndex = 0;

    [Header("Light Settings")]
    public float intensity = 2f;
    public float range = 15f;
    public LightShadows shadows = LightShadows.None;
    public float orbitSpeed = 20f;

    [Header("Bias Settings")]
    public float[] biasValues = { 0f, 0.002f, 0.01f, 0.05f };
    private int currentBiasIndex = 0;

    [Header("UI Elements")]
    public Text uiText; // Assign a UI Text element to show live values

    [Header("Camera Settings")]
    public Transform cameraTarget;
    public float mouseSensitivity = 2f;
    public float scrollSensitivity = 10f;
    private Vector3 lastMousePos;

    private Light pointLight;
    private GameObject room;
    private List<GameObject> floatingCubes = new List<GameObject>();

    void Start()
    {
        SetupRoom();
        SetupFloatingCubes();
        SetupPointLight();
        SetupCamera();
        UpdateUI();
    }

    void Update()
    {
        HandleInput();
        OrbitCamera();
        UpdateUI();
    }

    // -------------------------------
    private void SetupRoom()
    {
        room = GameObject.CreatePrimitive(PrimitiveType.Cube);
        room.transform.localScale = new Vector3(20, 20, 20);
        room.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        room.GetComponent<MeshRenderer>().material.color = Color.gray;
        room.GetComponent<MeshRenderer>().material.SetFloat("_Glossiness", 0f);

        // Invert normals
        Mesh mesh = room.GetComponent<MeshFilter>().mesh;
        mesh.triangles = InvertTriangles(mesh.triangles);
    }

    private void SetupFloatingCubes()
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f)
            );
            cube.transform.localScale = Vector3.one * Random.Range(1f, 2f);
            cube.GetComponent<MeshRenderer>().material.color = Color.Lerp(Color.white, Color.gray, Random.value);
            floatingCubes.Add(cube);
        }
    }

    private void SetupPointLight()
    {
        GameObject lightObj = new GameObject("PointLightWithCookie");
        pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.range = range;
        pointLight.intensity = intensity;
        pointLight.shadows = shadows;

        if (cookies.Count > 0)
            pointLight.cookie = cookies[currentCookieIndex];

       // pointLight.cookieBias = biasValues[currentBiasIndex];
        
    }

    private void SetupCamera()
    {
        if (Camera.main == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 0, -orbitSpeed);
            cam.transform.LookAt(Vector3.zero);
        }

        if (cameraTarget == null)
        {
            cameraTarget = new GameObject("CameraTarget").transform;
            cameraTarget.position = Vector3.zero;
        }
    }

    private void OrbitCamera()
    {
        if (Camera.main == null) return;

        // Automatic orbit
        Camera.main.transform.RotateAround(cameraTarget.position, Vector3.up, orbitSpeed * Time.deltaTime);

        // Manual orbit with right mouse
        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            Camera.main.transform.RotateAround(cameraTarget.position, Vector3.up, delta.x * mouseSensitivity * 0.02f);
            Camera.main.transform.RotateAround(cameraTarget.position, Camera.main.transform.right, -delta.y * mouseSensitivity * 0.02f);
        }

        // Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Camera.main.transform.position += Camera.main.transform.forward * scroll * scrollSensitivity;

        Camera.main.transform.LookAt(cameraTarget.position);
        lastMousePos = Input.mousePosition;
    }

    private void HandleInput()
    {
        // Cycle bias
        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentBiasIndex = (currentBiasIndex + 1) % biasValues.Length;
            // Send the bias value to the shader instead
            foreach (var cube in floatingCubes)
            {
                var mat = cube.GetComponent<Renderer>().material;
                if (mat != null && mat.HasProperty("_CookieBias"))
                    mat.SetFloat("_CookieBias", biasValues[currentBiasIndex]);
            }
        }

        // Cycle cookies
        if (Input.GetKeyDown(KeyCode.C) && cookies.Count > 0)
        {
            currentCookieIndex = (currentCookieIndex + 1) % cookies.Count;
            pointLight.cookie = cookies[currentCookieIndex];
        }

        // Adjust intensity
        if (Input.GetKey(KeyCode.I)) pointLight.intensity += Time.deltaTime * 1f;
        if (Input.GetKey(KeyCode.K)) pointLight.intensity = Mathf.Max(0f, pointLight.intensity - Time.deltaTime * 1f);

        // Adjust range
        if (Input.GetKey(KeyCode.O)) pointLight.range += Time.deltaTime * 2f;
        if (Input.GetKey(KeyCode.L)) pointLight.range = Mathf.Max(1f, pointLight.range - Time.deltaTime * 2f);

        // Adjust orbit speed
        if (Input.GetKey(KeyCode.U)) orbitSpeed += Time.deltaTime * 5f;
        if (Input.GetKey(KeyCode.J)) orbitSpeed = Mathf.Max(0f, orbitSpeed - Time.deltaTime * 5f);
    }

    private void UpdateUI()
    {
        if (uiText != null)
        {
            string cookieName = cookies.Count > 0 ? cookies[currentCookieIndex].name : "None";
            uiText.text =
                $"Cookie: {cookieName}\n" +
               $"Bias: {biasValues[currentBiasIndex]:F3}\n" +
                $"Intensity: {pointLight.intensity:F2}\n" +
                $"Range: {pointLight.range:F2}\n" +
                $"Orbit Speed: {orbitSpeed:F1}\n\n" +
                $"Controls:\nSPACE: Next Bias\nC: Next Cookie\nI/K: Intensity\nO/L: Range\nU/J: Orbit Speed\nRight Mouse: Orbit\nScroll: Zoom";
        }
    }

    // Helper to invert cube normals so camera is inside
    private int[] InvertTriangles(int[] tris)
    {
        for (int i = 0; i < tris.Length; i += 3)
        {
            int temp = tris[i];
            tris[i] = tris[i + 1];
            tris[i + 1] = temp;
        }
        return tris;
    }
}
