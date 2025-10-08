using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class AttackHover : MonoBehaviour
{
    [Header("Hover Tuning")]
    [Tooltip("Default hover time for BeginHoverDefault().")]
    [Min(0f)][SerializeField] private float defaultHoverDuration = 0.20f;

    [Tooltip("Zero only the vertical velocity at start so you stop falling immediately.")]
    [SerializeField] private bool zeroVerticalVelocityOnStart = true;

    [Tooltip("Optional temporary drag while hovering. <= 0 = leave unchanged.")]
    [SerializeField] private float horizontalDragDuringHover = 0f;

    [Tooltip("Freeze rotation during hover.")]
    [SerializeField] private bool freezeRotationDuringHover = false;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Rigidbody2D rb;
    private float baseGravityScale;
    private float baseDrag;
    private bool baseFreezeRotation;

    private bool hovering;
    private float hoverEndUnscaledTime = -1f;  // runs through hit-stop
    private Coroutine hoverRoutine;

    public bool IsHovering => hovering;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnDisable()
    {
        if (hovering) RestorePhysics();
        hovering = false;
        hoverRoutine = null;
    }

    public void SetDefaultHoverDuration(float seconds) => defaultHoverDuration = Mathf.Max(0f, seconds);
    public void BeginHoverDefault() => BeginHover(defaultHoverDuration);

    // Backward-compatible signature used by your MeleeWeapon:
    // BeginHover(duration, zeroYOnStart:true, overrideHorizontalDrag:null)
    public void BeginHover(float duration, bool? zeroYOnStart, float? overrideHorizontalDrag)
    {
        // apply per-call overrides, then run core
        bool prevZero = zeroVerticalVelocityOnStart;
        float prevDrag = horizontalDragDuringHover;

        if (zeroYOnStart.HasValue) zeroVerticalVelocityOnStart = zeroYOnStart.Value;
        if (overrideHorizontalDrag.HasValue) horizontalDragDuringHover = overrideHorizontalDrag.Value;

        BeginHover(duration);  // core path below

        // restore tunables so next call uses component defaults again
        zeroVerticalVelocityOnStart = prevZero;
        horizontalDragDuringHover = prevDrag;
    }

    // Core path (also used by BeginHoverDefault)
    public void BeginHover(float duration)
    {
        if (!rb || duration <= 0f) return;

        // First entry -> capture & apply hover physics
        if (!hovering)
        {
            baseGravityScale = rb.gravityScale;
            baseDrag = rb.linearDamping;
            baseFreezeRotation = rb.freezeRotation;

            rb.gravityScale = 0f;

            if (zeroVerticalVelocityOnStart)
            {
                var v = rb.linearVelocity;
                v.y = 0f;
                rb.linearVelocity = v;
            }

            if (horizontalDragDuringHover > 0f)
                rb.linearDamping = horizontalDragDuringHover;

            if (freezeRotationDuringHover)
                rb.freezeRotation = true;

            hovering = true;
            if (debugLog) Debug.Log($"[AttackHover] START at {Time.unscaledTime:F3} for {duration:F3}s");
        }

        // Extend end time (unscaled so hit-stop doesn�ft shorten it)
        float newEnd = Time.unscaledTime + duration;
        if (newEnd > hoverEndUnscaledTime)
            hoverEndUnscaledTime = newEnd;

        if (hoverRoutine == null)
            hoverRoutine = StartCoroutine(HoverLoop());
    }

    public void EndHoverNow()
    {
        if (!hovering) return;
        hoverEndUnscaledTime = Time.unscaledTime; // exit next frame
    }

    private IEnumerator HoverLoop()
    {
        while (Time.unscaledTime < hoverEndUnscaledTime)
            yield return null;

        if (debugLog) Debug.Log($"[AttackHover] END at {Time.unscaledTime:F3}");
        RestorePhysics();
        hovering = false;
        hoverRoutine = null;
    }

    private void RestorePhysics()
    {
        rb.gravityScale = baseGravityScale;
        rb.linearDamping = baseDrag;
        rb.freezeRotation = baseFreezeRotation;
    }
}
