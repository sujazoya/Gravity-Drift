using UnityEngine;
using UnityEngine.VFX;

public class PlayButtonDissolve : MonoBehaviour
{
    public VisualEffect vfx;

    public void TriggerDissolve()
    {
        if(vfx != null)
            vfx.SendEvent("OnPlay");
    }
}
