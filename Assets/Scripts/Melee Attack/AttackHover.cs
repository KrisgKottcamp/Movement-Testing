using System.Collections;
using UnityEngine;

public class AttackHover : MonoBehaviour
{
    [Header("Defaults (can be overridden per call)")]
    [Tooltip("If true, we zero only the Y velocity on hover start (keep X drift).")]
    [SerializeField] private bool zeroVerticalVelocityOnStart = true;
    [Tooltip("Optional horizontal drag during hover (<=0 means keep current).")]
    [SerializeField] private float horizontalDragDuringHover = 0f;
    [Tooltip("Freeze rotation during hover (useful for ragdolly enemies).")]
    [SerializeField] private bool freezeRotationDuringHover = false;

    private Rigidbody2D rb;
    private float baseGravity;
    private float baseDrag;
    private bool hadFrozenRotation;
    private float hoverUntilTime = -1f;
    private bool hovering;
    private Coroutine hoverCo;
    public bool IsHovering => hovering;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!rb) Debug.LogError($"{name}: AttackHover requires a Rigidbody2D.");
    }

    /// <summary>
    /// Start/extend a hover. If called again while hovering, it extends the end time.
    /// </summary>
    /// <param name="duration">Seconds to hover.</param>
    /// <param name="zeroYOnStart">Override for zeroing Y velocity at start.</param>
    /// <param name="overrideHorizontalDrag">If set, applies this drag during hover.</param>
    public void BeginHover(float duration, bool? zeroYOnStart = null, float? overrideHorizontalDrag = null)
    {
        if (!rb || duration <= 0f) return;

        // On first entry, capture base values
        if (!hovering)
        {
            baseGravity = rb.gravityScale;
            baseDrag = rb.linearDamping;

            if (freezeRotationDuringHover)
            {
                hadFrozenRotation = (rb.constraints & RigidbodyConstraints2D.FreezeRotation) != 0;
                rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
            }

            rb.gravityScale = 0f;

            bool zeroY = zeroYOnStart ?? zeroVerticalVelocityOnStart;
            if (zeroY)
            {
                var v = rb.linearVelocity; // if your code uses rb.linearVelocity, swap it in here
                v.y = 0f;
                rb.linearVelocity = v;
            }

            float drag = overrideHorizontalDrag.HasValue ? overrideHorizontalDrag.Value : horizontalDragDuringHover;
            if (drag > 0f) rb.linearDamping = drag;

            hovering = true;
        }

        // Extend the hover window
        float newUntil = Time.time + duration;
        if (newUntil > hoverUntilTime) hoverUntilTime = newUntil;

        // Single keeper coroutine that waits until hover window elapses
        if (hoverCo == null) hoverCo = StartCoroutine(HoverRoutine());
    }

    private IEnumerator HoverRoutine()
    {
        while (Time.time < hoverUntilTime) yield return null;
        // Restore original physics
        rb.gravityScale = baseGravity;
        rb.linearDamping = baseDrag;

        if (freezeRotationDuringHover && !hadFrozenRotation)
            rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;

        hovering = false;
        hoverCo = null;
    }
}
