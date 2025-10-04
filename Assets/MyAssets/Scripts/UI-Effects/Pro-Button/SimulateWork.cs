using UnityEngine;
using System.Collections;

public class SimulateWork : MonoBehaviour
{
    public ProUIButton proButton;
    public float simulatedWorkTime = 2f;

    public void StartWork()
    {
        proButton.OnButtonPressed();
        StartCoroutine(DoSimulatedWork());
    }

    IEnumerator DoSimulatedWork()
    {
        yield return new WaitForSeconds(simulatedWorkTime);
        proButton.OnWorkDone();
    }
}
