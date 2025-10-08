using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class KnockbackController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D[] colliders; // auto-filled if empty
    [SerializeField, Tooltip("Primary collider used for distance/normal vs walls (optional).")]
    private Collider2D primaryCollider;              // auto-picked if null

    [Header("General")]
    [Tooltip("Freeze Z rotation so enemies never spin.")]
    [SerializeField] private bool lockZRotation = true;

    [Header("Materials (optional)")]
    [Tooltip("Applied to colliders ONLY while knocking back (Friction=0, Bounciness=0).")]
    [SerializeField] private PhysicsMaterial2D noFrictionMaterial;

    // ────────────────────────────── Curve-driven knockback (original feel)
    [Header("Curve Knockback (Original Feel)")]
    [SerializeField] private bool canBeKnockedBack = true;
    [Tooltip("Total knockback time (seconds).")]
    [SerializeField] private float duration = 0.25f;
    [Tooltip("Peak speed along 'dir' when curve = 1.")]
    [SerializeField] private float strength = 8f;
    [Tooltip("X = normalized time (0..1), Y = scale (0..1).")]
    [SerializeField] private AnimationCurve forceCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [Tooltip("If true, drive ONLY X; keep current Y velocity intact.")]
    [SerializeField] private bool preserveY = true;

    // ────────────────────────────── Finisher throw
    [Header("Finisher Throw")]
    [Tooltip("Launch speed for finisher throws.")]
    [SerializeField] private float finisherSpeed = 14f;
    [Tooltip("Ensure at least this vertical lift on finisher.")]
    [SerializeField] private float finisherMinLiftY = 7f;
    [Tooltip("Uncontrolled time after finisher launch.")]
    [SerializeField] private float finisherDuration = 0.45f;

    // ────────────────────────────── Early-flight stabilizer
    [Header("Stabilizers")]
    [Tooltip("Clamp horizontal sign briefly so nothing flips X in mid-air.")]
    [SerializeField] private bool clampHorizontalSign = true;
    [SerializeField] private float signLockDuration = 0.25f;

    // ────────────────────────────── OverlapBox wall pop-up (no collision callbacks)
    [Header("Wall Pop-Up (Overlap Probe)")]
    [Tooltip("Enable upward pop when a wall is detected ahead during knockback.")]
    [SerializeField] private bool bounceUpOnWall = true;

    [SerializeField, Tooltip("Layers considered 'wall/ground' for the probe.")]
    private LayerMask wallMask = ~0;

    [SerializeField, Tooltip("Size of the probe box in world units (W x H).")]
    private Vector2 wallProbeSize = new Vector2(0.25f, 1.0f);

    [SerializeField, Tooltip("Forward distance from RB position to center the probe box.")]
    private float wallProbeForward = 0.25f;

    [SerializeField, Tooltip("Vertical offset for the probe box center (keeps probe around mid-body).")]
    private float wallProbeYOffset = 0.0f;

    [SerializeField, Tooltip("Auto-height the probe using primary collider bounds.")]
    private bool probeAutoHeightFromCollider = true;

    [SerializeField, Tooltip("Multiplier for collider height when auto-height is enabled.")]
    private float probeHeightMultiplier = 0.7f;

    [SerializeField, Tooltip("How many upward bounces allowed per launch.")]
    private int maxWallBouncesPerLaunch = 1;

    [SerializeField, Tooltip("|normal.x| must be >= this to count as a wall (approx vertical).")]
    private float wallNormalMinX = 0.75f;

    [SerializeField, Tooltip("Min approach speed into the wall to trigger a pop.")]
    private float minImpactSpeed = 3.0f;

    [SerializeField, Tooltip("Base upward velocity after the pop.")]
    private float upwardBaseVelocity = 7.5f;

    [SerializeField, Tooltip("How much of horizontal speed becomes upward velocity.")]
    private float upwardFromHorizFactor = 0.60f;

    [SerializeField, Tooltip("Dampen X after the pop so we don't re-hit the wall (0..1).")]
    private float xDampenAfterBounce = 0.15f;

    [SerializeField, Tooltip("Small push out of the wall to avoid sticking.")]
    private float lateralOutPush = 0.04f;

    [SerializeField, Tooltip("Cooldown so multiple probes don't re-trigger immediately.")]
    private float wallBounceCooldown = 0.08f;

    [SerializeField, Tooltip("If true, only allow pops early in knockback.")]
    private bool limitPopToEarlyWindow = false;

    [SerializeField, Tooltip("Early window seconds (used if limitPopToEarlyWindow = true).")]
    private float popEarlyWindow = 0.25f;

    // Corner-case support: pop even if speed is ~0 right after the hit
    [SerializeField, Tooltip("Allow a pop even if speed is ~0 right after the hit.")]
    private bool allowEarlyPopNoSpeed = true;

    [SerializeField, Tooltip("Seconds after knockback start to allow speed-agnostic pop.")]
    private float earlyPopWindow = 0.12f;

    [SerializeField, Tooltip("If overlapped, require at least this separation depth to count as 'against the wall'.")]
    private float minOverlapDepth = 0.01f;

    // After a pop, let the pop own X so the curve can’t overwrite immediately
    [SerializeField, Tooltip("Time after a wall pop during which curve X updates are suppressed.")]
    private float suppressCurveXAfterPop = 0.15f;

    // ────────────────────────────── Runtime
    public bool IsKnockingBack { get; private set; }

    private Coroutine knockbackRoutine;
    private PhysicsMaterial2D[] _prevMats;
    private Vector2 _prevVel;
    private float _knockStartTime;
    private Vector2 _lastLaunchDir = Vector2.zero;

    // wall bounce state
    private int _wallBouncesLeft;
    private float _nextWallBounceTime;

    // pop/cancel state
    private float _suppressCurveXUntil = -1f;
    private bool _cancelSignLock = false;

    // overlap buffer (no alloc)
    private readonly Collider2D[] _overlapBuf = new Collider2D[8];

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider2D>(includeInactive: false);
        if (!primaryCollider && colliders != null && colliders.Length > 0)
            primaryCollider = colliders[0];

        if (lockZRotation && rb != null) rb.freezeRotation = true;
    }

    private void OnDisable()
    {
        RestoreMaterials();
        IsKnockingBack = false;
        knockbackRoutine = null;
    }

    private void FixedUpdate()
    {
        _prevVel = rb.linearVelocity;

        if (bounceUpOnWall && IsKnockingBack)
            ProbeAndTryWallPop(); // OverlapBox-based wall detection & pop
    }

    // ======================================================================
    // PUBLIC API  (new + legacy wrappers for compatibility)
    // ======================================================================

    /// <summary>Curve-driven knockback (original feel). strengthMul multiplies 'strength'.</summary>
    public void DoImpulse(Vector2 dir, float strengthMul = 1f)
    {
        if (!canBeKnockedBack) return;
        dir = dir == Vector2.zero ? Vector2.right : dir.normalized;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(CoCurveKnockback(dir, Mathf.Max(0.01f, strengthMul)));
    }

    /// <summary>Alias (matches older naming elsewhere).</summary>
    public void DoLinearKnockback(Vector2 dir, float strengthMul = 1f) => DoImpulse(dir, strengthMul);

    /// <summary>Finisher-style throw with minimum vertical lift.</summary>
    public void DoFinisherThrow(Vector2 dir, float strengthMul = 1f)
    {
        dir = dir == Vector2.zero ? Vector2.right : dir.normalized;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(CoFinisherThrow(dir, Mathf.Max(0.01f, strengthMul)));
    }

    // Legacy/compat wrappers (so existing code compiles unchanged)
    public void TriggerKnockback(Vector2 dir) => DoImpulse(dir, 1f);
    public void TriggerKnockback(Vector2 dir, float s) => DoImpulse(dir, s);
    public void TriggerFlyback(Vector2 dir, float s) => DoImpulse(dir, s);
    public void TriggerFinisher(Vector2 dir) => DoFinisherThrow(dir, 1f);
    public void TriggerFinisher(Vector2 dir, float s) => DoFinisherThrow(dir, s);
    public void TriggerFinisherThrow(Vector2 dir, float s = 1f) => DoFinisherThrow(dir, s);
    public void TriggerBruiseBreakLaunch(Vector2 dir, float s = 1f) => DoFinisherThrow(dir, s);
    public void DoFinishThrow(Vector2 dir, float s = 1f) => DoFinisherThrow(dir, s);
    public void DoFInishThrow(Vector2 dir, float s = 1f) => DoFinisherThrow(dir, s);

    // ======================================================================
    // IMPLEMENTATION
    // ======================================================================

    // Curve-driven knockback (original feel)
    private IEnumerator CoCurveKnockback(Vector2 dir, float strengthMul)
    {
        _knockStartTime = Time.time;
        _lastLaunchDir = dir;
        _cancelSignLock = false;
        _suppressCurveXUntil = -1f;

        IsKnockingBack = true;
        ApplyNoFriction();
        ResetWallBounceState();

        if (clampHorizontalSign) StartCoroutine(LockHorizontalSign(signLockDuration));

        float elapsed = 0f;
        float total = Mathf.Max(0.01f, duration);
        Vector2 peak = dir * (strength * strengthMul);

        while (elapsed < total)
        {
            float t01 = elapsed / total;
            float scale = Mathf.Clamp01(forceCurve.Evaluate(t01)); // 0..1
            Vector2 v = peak * scale;

            // During the suppress window, keep the pop's X (don't overwrite with curve).
            float xToApply = (Time.time < _suppressCurveXUntil) ? rb.linearVelocity.x : v.x;

            if (preserveY)
            {
                rb.linearVelocity = new Vector2(xToApply, rb.linearVelocity.y);
            }
            else
            {
                float yToApply = (Time.time < _suppressCurveXUntil) ? rb.linearVelocity.y : v.y;
                rb.linearVelocity = new Vector2(xToApply, yToApply);
            }

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        // Stop residual slide like the original
        Vector2 stop = rb.linearVelocity;
        if (preserveY) stop.x = 0f; else stop = Vector2.zero;
        rb.linearVelocity = stop;

        IsKnockingBack = false;
        RestoreMaterials();
        knockbackRoutine = null;
    }

    // Finisher throw
    private IEnumerator CoFinisherThrow(Vector2 dir, float strengthMul)
    {
        _knockStartTime = Time.time;
        _lastLaunchDir = dir;
        _cancelSignLock = false;
        _suppressCurveXUntil = -1f;

        IsKnockingBack = true;
        ApplyNoFriction();
        ResetWallBounceState();

        Vector2 v = dir * (finisherSpeed * strengthMul);
        v.y = Mathf.Max(v.y, finisherMinLiftY);
        rb.linearVelocity = v;

        if (clampHorizontalSign) StartCoroutine(LockHorizontalSign(signLockDuration));

        float elapsed = 0f;
        while (elapsed < finisherDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        IsKnockingBack = false;
        RestoreMaterials();
        knockbackRoutine = null;
    }

    // ────────────────────────────── Early-flight stabilizer
    private IEnumerator LockHorizontalSign(float seconds)
    {
        if (seconds <= 0f) yield break;
        float latchedSign = Mathf.Sign(rb.linearVelocity.x);
        float end = Time.time + seconds;

        while (Time.time < end)
        {
            if (_cancelSignLock) yield break;  // wall pop took over

            var v = rb.linearVelocity;

            // Lazy-latch: wait until X has a sign, then hold it
            if (latchedSign == 0f && Mathf.Abs(v.x) > 0.001f)
                latchedSign = Mathf.Sign(v.x);

            if (latchedSign != 0f && v.x != 0f && Mathf.Sign(v.x) != latchedSign)
            {
                v.x = Mathf.Abs(v.x) * latchedSign; // restore sign, keep magnitude
                rb.linearVelocity = v;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    // ────────────────────────────── OverlapBox wall pop-up (no collision callbacks)
    private void ResetWallBounceState()
    {
        _wallBouncesLeft = maxWallBouncesPerLaunch;
        _nextWallBounceTime = 0f;
    }

    private void ProbeAndTryWallPop()
    {
        if (_wallBouncesLeft <= 0) return;
        if (Time.time < _nextWallBounceTime) return;
        if (limitPopToEarlyWindow && (Time.time - _knockStartTime) > popEarlyWindow) return;

        // 1) Decide probe direction: prefer current velocity, else previous, else the intended launch dir.
        float vx = Mathf.Abs(rb.linearVelocity.x) > 0.001f ? rb.linearVelocity.x :
                   (Mathf.Abs(_prevVel.x) > 0.001f ? _prevVel.x : _lastLaunchDir.x);

        if (Mathf.Abs(vx) < 0.001f) return; // no idea which side → bail
        float sign = Mathf.Sign(vx);
        Vector2 fwd = new Vector2(sign, 0f);

        // 2) Compute probe box
        Vector2 size = wallProbeSize;
        if (probeAutoHeightFromCollider && primaryCollider != null)
        {
            float h = primaryCollider.bounds.size.y * Mathf.Clamp(probeHeightMultiplier, 0.1f, 2f);
            size = new Vector2(wallProbeSize.x, h);
        }
        Vector2 center = rb.position + fwd * wallProbeForward + new Vector2(0f, wallProbeYOffset);

        // 3) Probe walls
        int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, _overlapBuf, wallMask);
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            var other = _overlapBuf[i];
            if (!other) continue;
            if (other.attachedRigidbody == rb) continue;
            bool isOwn = false;
            if (colliders != null)
            {
                for (int k = 0; k < colliders.Length; k++)
                    if (colliders[k] == other) { isOwn = true; break; }
            }
            if (isOwn) continue;

            // 4) Get a reasonable wall normal pointing out of the wall toward us
            Vector2 wallNormal = -fwd; // fallback
            float overlapDepth = 0f;

            if (primaryCollider != null)
            {
                ColliderDistance2D cd = primaryCollider.Distance(other);
                wallNormal = -cd.normal.normalized;             // outward from wall toward us
                overlapDepth = Mathf.Max(0f, -cd.distance);     // positive when overlapped
            }

            // Must be a vertical-ish surface
            if (Mathf.Abs(wallNormal.x) < wallNormalMinX) continue;

            // 5) Are we moving INTO the wall?
            float approach = Vector2.Dot(rb.linearVelocity, wallNormal); // negative = into wall

            bool inEarlyWindow = allowEarlyPopNoSpeed && ((Time.time - _knockStartTime) <= earlyPopWindow);
            bool launchedTowardWall = Vector2.Dot(_lastLaunchDir, wallNormal) > 0f;

            // Condition: either (A) moving into wall fast enough, OR
            // (B) early window, overlapped enough, and we launched toward that wall.
            bool okBySpeed = (approach <= -minImpactSpeed);
            bool okByEarly = inEarlyWindow && (overlapDepth >= minOverlapDepth) && launchedTowardWall;

            if (!okBySpeed && !okByEarly) continue;

            // 6) Pop: convert horizontal momentum → upward, damp X away from wall
            float horizSpeed = Mathf.Abs(rb.linearVelocity.x);
            if (horizSpeed < 0.01f) horizSpeed = Mathf.Abs(_prevVel.x);
            if (horizSpeed < 0.01f) horizSpeed = Mathf.Abs(_lastLaunchDir.x) * strength; // last resort

            float newVy = Mathf.Max(upwardBaseVelocity, horizSpeed * upwardFromHorizFactor);
            float newVx = -Mathf.Sign(vx) * horizSpeed * xDampenAfterBounce; // away from wall

            // Nudge out of the wall so we don't immediately re-contact
            rb.position = rb.position + wallNormal * lateralOutPush;

            rb.linearVelocity = new Vector2(newVx, newVy);

            // prevent curve and sign-lock from immediately undoing the pop
            _cancelSignLock = true;
            _suppressCurveXUntil = Time.time + suppressCurveXAfterPop;

            _wallBouncesLeft--;
            _nextWallBounceTime = Time.time + wallBounceCooldown;
            break; // one pop per probe tick
        }
    }

    // ────────────────────────────── Materials (apply/restore cleanly)
    private void ApplyNoFriction()
    {
        if (noFrictionMaterial == null || colliders == null) return;

        if (_prevMats == null || _prevMats.Length != colliders.Length)
            _prevMats = new PhysicsMaterial2D[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i]) continue;
            _prevMats[i] = colliders[i].sharedMaterial;
            colliders[i].sharedMaterial = noFrictionMaterial;
        }
    }

    private void RestoreMaterials()
    {
        if (_prevMats == null || colliders == null) return;

        for (int i = 0; i < colliders.Length && i < _prevMats.Length; i++)
        {
            if (!colliders[i]) continue;
            colliders[i].sharedMaterial = _prevMats[i];
        }
    }

    // ────────────────────────────── Gizmos (visualize the probe in the editor)
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (rb == null) return;

        // forward = current vx -> prev -> last launch
        float vx = rb ? rb.linearVelocity.x : 0f;
        if (Mathf.Abs(vx) < 0.001f && Mathf.Abs(_prevVel.x) >= 0.001f) vx = _prevVel.x;
        if (Mathf.Abs(vx) < 0.001f && Mathf.Abs(_lastLaunchDir.x) >= 0.001f) vx = _lastLaunchDir.x;

        float sign = Mathf.Sign(Mathf.Abs(vx) > 0.001f ? vx : 1f);
        Vector2 fwd = new Vector2(sign, 0f);

        Vector2 size = wallProbeSize;
        if (probeAutoHeightFromCollider && primaryCollider != null)
            size = new Vector2(wallProbeSize.x, primaryCollider.bounds.size.y * Mathf.Clamp(probeHeightMultiplier, 0.1f, 2f));

        Vector2 center = (Vector2)transform.position + fwd * wallProbeForward + new Vector2(0f, wallProbeYOffset);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
