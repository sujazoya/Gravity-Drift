using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Managing.Scened;

public class SceneEventsHandler : MonoBehaviour
{
    private readonly HashSet<string> _loadedScenes = new HashSet<string>();

    private void OnEnable()
    {
        InstanceFinder.SceneManager.OnLoadStart += OnSceneLoadStart;
        InstanceFinder.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        InstanceFinder.SceneManager.OnUnloadStart += OnSceneUnloadStart;
    }

    private void OnDisable()
    {
        if (InstanceFinder.SceneManager != null)
        {
            InstanceFinder.SceneManager.OnLoadStart -= OnSceneLoadStart;
            InstanceFinder.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
            InstanceFinder.SceneManager.OnUnloadStart -= OnSceneUnloadStart;
        }
    }

    private void OnSceneLoadStart(SceneLoadStartEventArgs args)
    {
        if (args.QueueData != null && args.QueueData.SceneLoadData != null)
        {
            foreach (var lookup in args.QueueData.SceneLoadData.SceneLookupDatas)
            {
                Debug.Log($"[SceneLoadStart] Preparing to load: {lookup.Name}");
                _loadedScenes.Add(lookup.Name);
            }
        }
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        if (args.LoadedScenes != null)
        {
            foreach (Scene scene in args.LoadedScenes)
            {
                if (scene.IsValid())
                    Debug.Log($"[SceneLoadEnd] Scene loaded: {scene.name}");
            }
        }
    }

    private void OnSceneUnloadStart(SceneUnloadStartEventArgs args)
    {
        if (args.QueueData != null && args.QueueData.SceneUnloadData != null)
        {
            foreach (var lookup in args.QueueData.SceneUnloadData.SceneLookupDatas)
            {
                Debug.Log($"[SceneUnloadStart] Unloading: {lookup.Name}");
                _loadedScenes.Remove(lookup.Name);
            }
        }
    }
}
