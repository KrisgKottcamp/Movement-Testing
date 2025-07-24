using UnityEngine;
using DG.Tweening;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class AttackStickinessController : MonoBehaviour
{
    [Header("Stickiness Settings")]
    [Tooltip("How far to search for enemies")]
    public float stickRadius = 2f;

    [Tooltip("Which layers count as enemies")]
    public LayerMask enemyLayerMask;

    [Header("Stick Speed (units/sec)")]
    [Tooltip("Speed when enemy is very close")]
    public float maxStickSpeed = 8f;
    [Tooltip("Speed when enemy is at the edge of stickRadius")]
    public float minStickSpeed = 2f;

    [Header("Angle Settings")]
    [Tooltip("Max deviation from attackDir to still stick (deg)")]
    [Range(0, 180)] public float maxStickAngleDeg = 45f;
    [Tooltip("If attack is this close to straight-down (deg), skip stick so you can pogo")]
    [Range(0, 90)] public float downwardPogoSkipAngleDeg = 15f;

    [Header("Ease Curve")]
    public AnimationCurve stickCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isSticking;


    Rigidbody2D rb;
    CharacterControl cc;
    private Tween _activeTween;
    private Vector2 lastAttackDir = Vector2.up;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cc = GetComponent<CharacterControl>();
    }

    
    // Pull you toward the nearest valid enemy in attackDir.
 
    public void TryStickToNearestEnemy(Vector2 attackDir)
    {
        // remember your last non-zero attack dir
        if (attackDir.sqrMagnitude > 0.01f)
            lastAttackDir = attackDir.normalized;

        // skip pure downward-ish swings so pogo still works
        float angleFromDown = Vector2.Angle(lastAttackDir, Vector2.down);
        if (angleFromDown <= downwardPogoSkipAngleDeg) return;

        // collect enemies in radius
        var hits = Physics2D.OverlapCircleAll(rb.position, stickRadius, enemyLayerMask);
        Transform best = null;
        float bestAngle = float.MaxValue;

        
        // pick the one with the smallest angle to your attackDir
        foreach (var c in hits)
        {
            Vector2 toEnemy = (Vector2)c.transform.position - rb.position;
            if (Vector2.Dot(toEnemy, lastAttackDir) <= 0)
                continue; // behind you

            float angle = Vector2.Angle(lastAttackDir, toEnemy.normalized);
            if (angle <= maxStickAngleDeg && angle < bestAngle)
            {
                bestAngle = angle;
                best = c.transform;
            }
        }

        if (best == null) return;

        // compute speed based on how far along the ray the enemy sits
        float along = Vector2.Dot((best.position - (Vector3)rb.position), lastAttackDir);
        float t = Mathf.Clamp01(along / stickRadius);
        float speed = Mathf.Lerp(maxStickSpeed, minStickSpeed, t);

        // distance to target
        float distance = Vector2.Distance(rb.position, best.position);
        float duration = distance / speed;

        // lock out normal movement
        if (cc != null) cc.isMovementLocked = true;

        // kill any old pull and start a one-shot forward tween
        _activeTween?.Kill();
        _activeTween = rb.DOMove(best.position, duration)
                         .SetEase(stickCurve)
                         .SetLoops(1, LoopType.Restart)
                         .OnComplete(() =>
                         {
                             cc.isMovementLocked = false;
                             _activeTween = null;
                         });
    }

    void OnDrawGizmos()
    {
        // visualize radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stickRadius);

        // visualize enemies in range
        Gizmos.color = Color.yellow;
        foreach (var c in Physics2D.OverlapCircleAll(transform.position, stickRadius, enemyLayerMask))
            Gizmos.DrawWireSphere(c.transform.position, 0.1f);

        // visualize attackDir arrow
        Gizmos.color = Color.green;
        Vector3 dir = new Vector3(lastAttackDir.x, lastAttackDir.y, 0) * stickRadius;
        Gizmos.DrawLine(transform.position, transform.position + dir);

        // visualize cone
        float half = maxStickAngleDeg;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, 0, half) * dir);
        Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, 0, -half) * dir);
    }
}
