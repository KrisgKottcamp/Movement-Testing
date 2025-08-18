using UnityEngine;
using Unity.Cinemachine;

public class CameraDirectorCM3 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform player;
    [SerializeField] Rigidbody2D playerRb;
    [SerializeField] CharacterControl playerController; // reads .mover (-1..1)

    [Header("Base Camera (required)")]
    [SerializeField] CinemachineCamera baseCam;
    [SerializeField] CinemachinePositionComposer baseComposer;   // on baseCam
    [SerializeField] CinemachineCameraOffset baseOffset;         // on baseCam
    [Tooltip("Optional: if you use Cinemachine Follow instead of Position Composer")]
    [SerializeField] CinemachineFollow baseFollow;

    [Header("Vertical Bias (world units)")]
    [SerializeField] float idleY = 0.5f;
    [SerializeField] float risingY = 1.2f;
    [SerializeField] float fallingY = -1.2f;

    // ---------------- INPUT LOOK-AHEAD (with anti-jitter) ----------------
    [Header("Look-Ahead (Input)")]
    [SerializeField] bool useInputLookAhead = true;
    [Tooltip("World units at full input (|mover|=1) before dead zone mapping")]
    [SerializeField] float inputLookDistance = 1.2f;
    [Tooltip("Seconds to ~63% of target input magnitude")]
    [SerializeField] float inputMagTime = 0.08f;
    [Tooltip("Seconds to ~63% when blending sign (-1..+1) to avoid instant flips")]
    [SerializeField] float signBlendTime = 0.09f;
    [Tooltip("Ignore tiny input |x| < this (pre-smoothing)")]
    [SerializeField] float inputDeadZone = 0.12f;
    [Tooltip("Keep a little look in facing direction when idle")]
    [SerializeField] bool keepFacingBiasWhenIdle = true;
    [SerializeField] float idleFacingBias = 0.35f;   // world units

    // ---------------- DEAD ZONE (world units, with hysteresis) ----------------
    [Header("Look-Ahead Dead Zone (World Units)")]
    [Tooltip("Must exceed this to ENGAGE look-ahead; within this, look-ahead = 0")]
    [SerializeField] float lookDeadEnter = 0.70f;
    [Tooltip("Falls back to 0 only when below this; must be <= Enter (hysteresis)")]
    [SerializeField] float lookDeadExit = 0.55f;
    [Tooltip("Add dash/aim look even when inside dead zone")]
    [SerializeField] bool bonusesBypassDeadZone = true;

    // ---------------- Velocity look-ahead (fallback if you turn off input mode) ----------------
    [Header("Look-Ahead (Velocity Fallback)")]
    [SerializeField] float maxVelLook = 1.2f;
    [SerializeField] float velAtMaxLook = 12f;
    [SerializeField] float aimLook = 0.8f;
    [SerializeField] float dashLook = 1.0f;

    [Header("Zoom (orthographic)")]
    [SerializeField] float minOrtho = 6.5f;
    [SerializeField] float maxOrtho = 9.5f;

    [Header("Base Smoothing (time constants, seconds)")]
    [SerializeField] float xTime = 0.10f;   // final offset.x blend
    [SerializeField] float yTime = 0.10f;   // vertical bias blend
    [SerializeField] float zoomTime = 0.14f;

    // ---------------- Break Nudge ----------------
    [Header("Break Nudge")]
    [SerializeField] bool enableBreakNudge = true;
    [SerializeField] float breakNudgeDistance = 2.0f;
    [SerializeField] float breakNudgeHold = 0.35f;
    [SerializeField] bool breakNudgeAffectY = false;
    [SerializeField] bool breakZoom = true;
    [SerializeField] float breakZoomExtra = 1.0f;

    [Header("Break Nudge Smoothing (time constants)")]
    [SerializeField] float breakApproachTime = 0.08f;
    [SerializeField] float breakFadeTime = 0.20f;
    [SerializeField] float breakIdleReturnTime = 0.12f;

    // ---------------- Hit Nudge ----------------
    [Header("Hit Nudge")]
    [SerializeField] bool enableHitNudge = true;
    [SerializeField] float hitNudgeDistance = 0.6f;
    [SerializeField] float hitNudgeHold = 0.10f;
    [SerializeField] bool hitNudgeAffectY = false;
    [SerializeField] float hitStackCapMultiplier = 2.0f;

    [Header("Hit Nudge Smoothing (time constants)")]
    [SerializeField] float hitApproachTime = 0.06f;
    [SerializeField] float hitFadeTime = 0.16f;
    [SerializeField] float hitIdleReturnTime = 0.10f;
    [SerializeField] float hitTargetRelaxTime = 0.20f;

    // --- runtime ---
    float baseSize;
    bool dashBurst, attacking;

    // break nudge
    Vector2 nudgeTargetBreak, nudgeCurrentBreak;
    float breakHoldTimer = -1f, breakFadeTimer = -1f;

    // hit nudge
    Vector2 nudgeTargetHit, nudgeCurrentHit;
    float hitHoldTimer = -1f, hitFadeTimer = -1f;

    // input anti-jitter state
    float smoothedInputMag;   // 0..1
    float signBlend;          // -1..+1 smooth sign

    // dead-zone state
    bool lookEngaged = false; // whether we're currently outside the dead zone

    // ---------- API from gameplay ----------
    public void NotifyDashStart() => dashBurst = true;
    public void NotifyDashEnd() => dashBurst = false;
    public void NotifyAttackStart() => attacking = true;
    public void NotifyAttackEnd() => attacking = false;

    public void NotifyBruiseBreak(Transform enemy, Vector2 launchDir, float launchSpeed = 0f)
    {
        if (!enableBreakNudge) return;
        var dir = launchDir.sqrMagnitude > 0.0001f ? launchDir.normalized : Vector2.right;
        if (!breakNudgeAffectY) dir.y = 0f;
        nudgeTargetBreak = dir * breakNudgeDistance;
        breakHoldTimer = breakNudgeHold;
        breakFadeTimer = breakFadeTime;
    }

    public void NotifyHit(Vector2 hitDir, float intensity = 1f)
    {
        if (!enableHitNudge) return;
        var dir = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : Vector2.right;
        if (!hitNudgeAffectY) dir.y = 0f;

        var add = dir * (hitNudgeDistance * Mathf.Max(0.05f, intensity));
        float cap = hitNudgeDistance * Mathf.Max(1f, hitStackCapMultiplier);
        nudgeTargetHit = Vector2.ClampMagnitude(nudgeTargetHit + add, cap);

        hitHoldTimer = Mathf.Max(hitHoldTimer, hitNudgeHold);
        hitFadeTimer = hitFadeTime;
    }

    void Awake()
    {
        if (!baseComposer && baseCam) baseComposer = baseCam.GetComponent<CinemachinePositionComposer>();
        if (!baseOffset && baseCam) baseOffset = baseCam.GetComponent<CinemachineCameraOffset>();
        if (!baseFollow && baseCam) baseFollow = baseCam.GetComponent<CinemachineFollow>();

        if (!playerController && player)
            playerController = player.GetComponent<CharacterControl>();

        baseSize = minOrtho;
        if (baseCam) baseCam.Lens.OrthographicSize = baseSize;

        if (baseComposer) baseComposer.Damping = new Vector3(0.20f, 0.24f, 0f);
        if (baseFollow)
        {
            var t = baseFollow.TrackerSettings;
            t.PositionDamping = new Vector3(0.20f, 0.24f, t.PositionDamping.z);
            baseFollow.TrackerSettings = t;
        }

        // Ensure hysteresis is sane
        lookDeadExit = Mathf.Min(lookDeadExit, lookDeadEnter);
    }

    float Alpha(float timeConst, float dt) => timeConst <= 0f ? 1f : 1f - Mathf.Exp(-dt / timeConst);

    void Update()
    {
        if (!player) return;

        float dt = Time.unscaledDeltaTime;

        // ---- Vertical bias
        float ay = Alpha(yTime, dt);
        float vy = playerRb ? playerRb.linearVelocity.y : 0f;
        float targetY = vy > 0.5f ? risingY : (vy < -0.5f ? fallingY : idleY);

        if (baseComposer)
        {
            var off = baseComposer.TargetOffset;
            off.y = Mathf.Lerp(off.y, targetY, ay);
            baseComposer.TargetOffset = off;
        }
        else if (baseFollow)
        {
            var off = baseFollow.FollowOffset;
            off.y = Mathf.Lerp(off.y, targetY, ay);
            baseFollow.FollowOffset = off;
        }
        else if (baseOffset)
        {
            baseOffset.Offset = new Vector3(baseOffset.Offset.x, Mathf.Lerp(baseOffset.Offset.y, targetY, ay), 0f);
        }

        // ---- NUDGES (break + hits)
        // break
        if (breakHoldTimer > 0f)
        {
            breakHoldTimer -= dt;
            nudgeCurrentBreak = Vector2.Lerp(nudgeCurrentBreak, nudgeTargetBreak, Alpha(breakApproachTime, dt));
        }
        else if (breakFadeTimer > 0f)
        {
            breakFadeTimer -= dt;
            nudgeCurrentBreak = Vector2.Lerp(nudgeCurrentBreak, Vector2.zero, Alpha(breakFadeTime, dt));
        }
        else
        {
            nudgeCurrentBreak = Vector2.Lerp(nudgeCurrentBreak, Vector2.zero, Alpha(breakIdleReturnTime, dt));
        }

        // hits
        if (hitHoldTimer > 0f)
        {
            hitHoldTimer -= dt;
            nudgeCurrentHit = Vector2.Lerp(nudgeCurrentHit, nudgeTargetHit, Alpha(hitApproachTime, dt));
        }
        else if (hitFadeTimer > 0f)
        {
            hitFadeTimer -= dt;
            nudgeCurrentHit = Vector2.Lerp(nudgeCurrentHit, Vector2.zero, Alpha(hitFadeTime, dt));
            nudgeTargetHit = Vector2.Lerp(nudgeTargetHit, Vector2.zero, Alpha(hitTargetRelaxTime, dt));
        }
        else
        {
            nudgeCurrentHit = Vector2.Lerp(nudgeCurrentHit, Vector2.zero, Alpha(hitIdleReturnTime, dt));
            nudgeTargetHit = Vector2.Lerp(nudgeTargetHit, Vector2.zero, Alpha(hitTargetRelaxTime, dt));
        }

        Vector2 nudge = nudgeCurrentBreak + nudgeCurrentHit;

        // ---- INPUT-DRIVEN look-ahead with DEAD ZONE
        float targetXLook;

        if (useInputLookAhead)
        {
            // read raw input
            float raw = 0f;
            if (playerController != null) raw = Mathf.Clamp(playerController.mover, -1f, 1f);
            else raw = Input.GetAxisRaw("Horizontal");

            // pre-deadzone ignore of tiny noise
            if (Mathf.Abs(raw) < inputDeadZone) raw = 0f;

            // smooth sign (-1..+1)
            float desiredSign = 0f;
            if (raw != 0f) desiredSign = Mathf.Sign(raw);
            else desiredSign = keepFacingBiasWhenIdle ? Mathf.Sign(player.localScale.x == 0f ? 1f : player.localScale.x) : 0f;

            signBlend = Mathf.Lerp(signBlend, desiredSign, Alpha(signBlendTime, dt));

            // smooth magnitude 0..1
            float aimMag = Mathf.Abs(raw);
            smoothedInputMag = Mathf.Lerp(smoothedInputMag, aimMag, Alpha(inputMagTime, dt));

            // base look (world units) BEFORE dead-zone mapping
            float baseLookUnits = inputLookDistance * smoothedInputMag;

            // optional tiny facing bias (added after dead-zone mapping if inside)
            bool idle = raw == 0f;

            // Signed desired look from input
            float signedDesired = baseLookUnits * Mathf.Clamp(signBlend, -1f, 1f);

            // ----- DEAD-ZONE MAPPING (Schmitt trigger) -----
            float absDesired = Mathf.Abs(signedDesired);

            if (!lookEngaged)
            {
                // enter only if exceed Enter threshold
                if (absDesired >= lookDeadEnter)
                    lookEngaged = true;
            }
            else
            {
                // disengage only if below Exit threshold
                if (absDesired <= lookDeadExit)
                    lookEngaged = false;
            }

            float mappedLook = 0f;
            if (lookEngaged)
            {
                // Soft start: subtract the Enter threshold so it ramps from 0 smoothly
                float over = Mathf.Max(0f, absDesired - lookDeadEnter);
                mappedLook = Mathf.Sign(signedDesired) * over;
            }
            else
            {
                mappedLook = 0f; // completely free window
            }

            // idle facing peek if desired and still inside dead zone
            if (!lookEngaged && keepFacingBiasWhenIdle && idle)
                mappedLook += idleFacingBias * Mathf.Sign(player.localScale.x == 0f ? 1f : player.localScale.x);

            // dash/aim bonuses (optionally bypass dead zone)
            float dirForBonuses = (Mathf.Abs(signBlend) > 0.01f) ? Mathf.Sign(signBlend) : Mathf.Sign(player.localScale.x);
            if (bonusesBypassDeadZone || lookEngaged)
            {
                mappedLook += (dashBurst ? dashLook : 0f) * dirForBonuses;
                mappedLook += (attacking ? aimLook : 0f) * dirForBonuses;
            }

            targetXLook = mappedLook;
        }
        else
        {
            // velocity fallback
            float vxAbs = playerRb ? Mathf.Abs(playerRb.linearVelocity.x) : 0f;
            float lookMag = Mathf.Clamp01(vxAbs / Mathf.Max(0.01f, velAtMaxLook));
            targetXLook = maxVelLook * lookMag * Mathf.Sign(player.localScale.x);
            targetXLook += (dashBurst ? dashLook : 0f) * Mathf.Sign(player.localScale.x);
            targetXLook += (attacking ? aimLook : 0f) * Mathf.Sign(player.localScale.x);
        }

        // combine nudges
        targetXLook += nudge.x;

        // ---- Apply X offset
        float ax = Alpha(xTime, dt);
        if (baseOffset)
        {
            baseOffset.Offset = new Vector3(Mathf.Lerp(baseOffset.Offset.x, targetXLook, ax),
                                            baseOffset.Offset.y, 0f);
        }
        else if (baseComposer)
        {
            var off = baseComposer.TargetOffset;
            off.x = Mathf.Lerp(off.x, targetXLook, ax);
            baseComposer.TargetOffset = off;
        }
        else if (baseFollow)
        {
            var off = baseFollow.FollowOffset;
            off.x = Mathf.Lerp(off.x, targetXLook, ax);
            baseFollow.FollowOffset = off;
        }

        // ---- Zoom (extra during break hold)
        float az = Alpha(zoomTime, dt);
        float targetSize = minOrtho + ((breakZoom && breakHoldTimer > 0f) ? breakZoomExtra : 0f);
        targetSize = Mathf.Clamp(targetSize, minOrtho, maxOrtho);
        baseSize = Mathf.Lerp(baseSize, targetSize, az);
        if (baseCam) baseCam.Lens.OrthographicSize = baseSize;
    }
}
