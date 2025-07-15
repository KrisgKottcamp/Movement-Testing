using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class KnockbackController : MonoBehaviour
{
    [Header("Knockback Settings")]
    [Tooltip("Can this object be knocked back at all?")]
    [SerializeField] private bool canBeKnockedBack = true;

    [Tooltip("Total time the knockback takes (seconds).")]
    [SerializeField] private float duration = 0.25f;

    [Tooltip("Peak strength of the knockback.")]
    [SerializeField] private float strength = 8f;

    [Tooltip("Shape of the force over time: X=0 start, X=1 end, Y=0 no force, Y=1 full force.")]
    [SerializeField]
    private AnimationCurve forceCurve =
        AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Rigidbody2D rb;
    private Coroutine knockbackRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Call this to fire off a knockback. 0,0 dir is ignored.
    /// </summary>
    public void TriggerKnockback(Vector2 rawDir)
    {
        if (!canBeKnockedBack || rawDir == Vector2.zero)
            return;

        Vector2 dir = rawDir.normalized;

        if (knockbackRoutine != null)
            StopCoroutine(knockbackRoutine);

        knockbackRoutine = StartCoroutine(DoKnockback(dir));
    }

    private IEnumerator DoKnockback(Vector2 dir)
    {
        float timer = 0f;

        // Optional: optional tiny freeze-frame at the very start
        // yield return new WaitForSeconds(0.02f);

        while (timer < duration)
        {
            float t = timer / duration;
            float curveValue = forceCurve.Evaluate(t);

            // Directly set velocity for crisp control
            rb.linearVelocity = dir * (strength * curveValue);

            timer += Time.deltaTime;
            yield return null;
        }

        // End: zero it out so you don't keep sliding
        rb.linearVelocity = Vector2.zero;
        knockbackRoutine = null;
    }
}
