using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Ensures Cinemachine brains update in sync with physics to prevent jitter
/// when using pixel-perfect movement.
/// </summary>
public static class CinemachineFixedUpdate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SetFixedUpdate()
    {
        foreach (var brain in Object.FindObjectsOfType<CinemachineBrain>())
        {
            brain.UpdateMethod = CinemachineBrain.UpdateMethod.FixedUpdate;
        }
    }
}
