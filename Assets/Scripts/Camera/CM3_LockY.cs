using UnityEngine;
using Unity.Cinemachine; // CM3 namespace

/// Add to any CinemachineCamera to freeze its world-space Y.
[ExecuteAlways]
public class CM3_LockY : CinemachineExtension
{
    [Tooltip("Freeze the camera's Y position in world space.")]
    public bool lockY = true;

    [Tooltip("World-space Y to hold. If Capture On First Tick is true, this will be set automatically.")]
    public float fixedY = 0f;

    [Tooltip("Capture the current camera Y on the first pipeline tick.")]
    public bool captureOnFirstTick = true;

    bool _captured;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;

        if (captureOnFirstTick && !_captured)
        {
            fixedY = state.RawPosition.y;
            _captured = true;
        }

        if (!lockY) return;

        var p = state.RawPosition;
        p.y = fixedY;
        state.RawPosition = p;
    }
}
