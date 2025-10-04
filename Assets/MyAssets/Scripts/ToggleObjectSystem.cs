using UnityEngine;

public class ToggleObjectSystem : MonoBehaviour
{
    [SerializeField] GameObject object1, object2;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    public void ToggleObjects()
    {
        if(object1 == null || object2 == null) return;
        object1.SetActive(!object1.activeSelf);
        object2.SetActive(!object2.activeSelf);
    }
}
