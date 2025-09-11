using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SceneManagerController : MonoBehaviour
{
    private FishNet.Managing.Scened.SceneManager _sceneManager;
    private string _currentScene;
    private List<string> _loadedScenes = new List<string>();

    public void Initialize(FishNet.Managing.Scened.SceneManager sceneManager)
    {
        _sceneManager = sceneManager;
        SetupSceneEvents();
    }

    private void SetupSceneEvents()
    {
        _sceneManager.OnLoadStart += OnSceneLoadStart;
        _sceneManager.OnLoadEnd += OnSceneLoadEnd;
        _sceneManager.OnUnloadStart += OnSceneUnloadStart;
        _sceneManager.OnUnloadEnd += OnSceneUnloadEnd;
    }

    private void OnSceneLoadStart(SceneLoadStartEventArgs args)
    {
        Debug.Log("[SceneManager] Scene load started.");
        // No direct scene names available in args
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        Debug.Log("[SceneManager] Scene load ended.");
        // You might update current scene manually elsewhere
    }

    private void OnSceneUnloadStart(SceneUnloadStartEventArgs args)
    {
        Debug.Log("[SceneManager] Scene unload started.");
    }

    private void OnSceneUnloadEnd(SceneUnloadEndEventArgs args)
    {
        Debug.Log("[SceneManager] Scene unload ended.");
    }

    // Optional: To track the active scene manually
    public void SetCurrentSceneName(string sceneName)
    {
        _currentScene = sceneName;
        if (!_loadedScenes.Contains(sceneName))
            _loadedScenes.Add(sceneName);
    }

    public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        SceneLoadData data = new SceneLoadData(sceneName)
        {
            ReplaceScenes = (mode == LoadSceneMode.Single) ? ReplaceOption.All : ReplaceOption.None
        };
        _sceneManager.LoadGlobalScenes(data);
        SetCurrentSceneName(sceneName); // Track manually
    }

    public void UnloadScene(string sceneName)
    {
        if (_loadedScenes.Contains(sceneName))
        {
            SceneUnloadData data = new SceneUnloadData(sceneName);
            _sceneManager.UnloadGlobalScenes(data);
            _loadedScenes.Remove(sceneName);
        }
    }
}
