using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MeleeWeapon : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private int bruiseDamageAmount = 1;

    [Header("Timing")]
    [SerializeField, Tooltip("How long before this weapon can hit again after a swing.")]
    private float attackCooldown = 0.12f;

    // In MeleeWeapon.cs (fields)
    [Header("Hit-Stop")]
    [SerializeField] private float hitStopDuration = 0.08f;          // your existing base freeze
    [SerializeField] private float extraHitStopOnBruiseBreak = 0.12f; 
    [SerializeField, Tooltip("Extra freeze when the swing is the 3rd combo finisher.")]
    private float extraHitStopOnFinisher = 0.08f;


    [Header("Pogo")]
    [SerializeField] private float pogoForce = 14f;
    [SerializeField, Tooltip("Dot threshold against Vector2.down to count as a downward aim (-1..1).")]
    private float pogoDownDotThreshold = 0.55f;

    [Header("Combo Finisher Tracking")]
    [SerializeField, Tooltip("Window in seconds for counting 3 hits on the SAME enemy.")]
    private float comboTargetWindow = 0.40f;

    [Header("Finisher Damage Bonus")]
    [SerializeField, Tooltip("Health damage multiplier applied on the 3rd combo hit.")]
    private float finisherHealthMultiplier = 1.5f;

    [SerializeField, Tooltip("Bruise damage multiplier applied on the 3rd combo hit.")]
    private float finisherBruiseMultiplier = 1.5f;

    // (Optional, keep old getter working)
    public int BruiseDamagePerHit => bruiseDamageAmount;
    // (Optional helper if other systems want to query finisher bruise)
    public int BruiseDamagePerHitFinisher => Mathf.Max(0, Mathf.RoundToInt(bruiseDamageAmount * finisherBruiseMultiplier));



    [Header("Player Hover on Hit")]
    [SerializeField, Tooltip("AttackHover on the player (auto-wires if left empty).")]
    private AttackHover playerHover;
    [SerializeField, Tooltip("Air hover time after a side/up/diag hit.")]
    private float playerHoverDurationAir = 0.22f;
    [SerializeField, Tooltip("Ground hover time after a side/up/diag hit.")]
    private float playerHoverDurationGround = 0.14f;
    [SerializeField, Tooltip("Also hover on pogo hits (downward). Off by default.")]
    private bool enableHoverOnPogo = false;



    // --- References (wired at runtime if not set) ---
    [SerializeField] private CharacterControl characterControl;
    [SerializeField] private MeleeAttackManager meleeAttackManager;

    // --- Runtime state ---
    private bool hasAttackedThisSwing = false;
    private Coroutine cooldownCoroutine;

    // Hit-stop stack safety
    private int _hitStopDepth = 0;
    private float _prevTimeScale = 1f;
    private float _prevFixedDeltaTime = 0.02f;

    // Per-target 3-hit tracking
    private EnemyHealth currentComboTarget = null;
    private int currentTargetHits = 0;
    private float comboTargetUntil = -1f;

    // Events
    public event Action OnPogoPerformed;

    private void Awake()
    {
        if (!characterControl) characterControl = GetComponentInParent<CharacterControl>();
        if (!meleeAttackManager) meleeAttackManager = GetComponentInParent<MeleeAttackManager>();
        if (!playerHover) playerHover = GetComponentInParent<AttackHover>();
    }

    private void OnEnable()
    {
        // re-arm on enable
        hasAttackedThisSwing = false;
    }

    // ------------------------------------------------------------
    // Collision
    // ------------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasAttackedThisSwing) return;

        // Be generous about where EnemyHealth lives
        var enemy = other.GetComponent<EnemyHealth>() ??
                    other.GetComponentInParent<EnemyHealth>() ??
                    other.GetComponentInChildren<EnemyHealth>();

        if (enemy == null || enemy.currentHealth <= 0) return;

        // First, give POGO a chance (air + down-aim + enemy allows pogo)
        if (TryPogo(enemy)) return;

        // Otherwise, handle a normal side/up/diag hit
        HandleCollision(enemy);
    }

    // ------------------------------------------------------------
    // Pogo path — single entry, single hit-stop, no freeze
    // ------------------------------------------------------------
    private bool TryPogo(EnemyHealth enemy)
    {
        // Determine the player's current attack aim
        Vector2 aim = (meleeAttackManager != null && meleeAttackManager.meleeAttackDir.sqrMagnitude > 0.0001f)
                        ? meleeAttackManager.meleeAttackDir.normalized
                        : (characterControl != null && characterControl.facingRight ? Vector2.right : Vector2.left);

        bool isDownward = Vector2.Dot(aim, Vector2.down) >= pogoDownDotThreshold;

        // Must be airborne + downward aim + enemy allows pogo
        if (!(enemy.giveUpwardForce && characterControl != null && !characterControl.isGrounded && isDownward))
            return false;

        // Prevent double entry BEFORE we start hit-stop
        if (hasAttackedThisSwing) return false;
        hasAttackedThisSwing = true;

        // Count this toward the 3-hit chain (finisher not applied on pogo)
        UpdateComboTarget(enemy);

        // Will this single hit break bruise?
        bool willBreak = (enemy.CurrentBruise + bruiseDamageAmount) >= enemy.MaxBruise
                         && !enemy.GaugeIsBroken;

        // One combined freeze request (base + extra if breaking)
        float freeze = hitStopDuration + (willBreak ? extraHitStopOnBruiseBreak : 0f);
        HitStopManager.Request(freeze);


        enemy.Damage(damageAmount, bruiseDamageAmount, Vector2.down);

        // Bounce the player upward
        if (characterControl != null)
        {
            characterControl.ApplyPogoForce(Vector2.up * pogoForce);
        }

        // Optional hover on pogo
        if (playerHover != null && enableHoverOnPogo)
        {
            // use air duration for pogo, zero vertical vel at start
            playerHover.BeginHover(playerHoverDurationAir, true, null);
        }

        OnPogoPerformed?.Invoke();

        // Rearm after cooldown
        if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(ResetAttack());

        return true;
    }

    // ------------------------------------------------------------
    // Side / Up / UpDiag / DownDiag hits
    // ------------------------------------------------------------
    private void HandleCollision(EnemyHealth enemy)
    {
        if (hasAttackedThisSwing) return; // guard

        hasAttackedThisSwing = true;


        //HITSTOP
        // Use the ACTUAL attack aim so finishers follow your stick/dir
        Vector2 hitDir = GetAttackAimDir(enemy);

        // Count toward the same-target chain and detect finisher
        bool finisherNow = UpdateComboTarget(enemy);

        // ---- compute this strike's actual damage numbers ----
        int hpThisHit = damageAmount;
        int bruiseThisHit = bruiseDamageAmount;

        if (finisherNow)
        {
            hpThisHit = Mathf.Max(0, Mathf.RoundToInt(damageAmount * finisherHealthMultiplier));
            bruiseThisHit = Mathf.Max(0, Mathf.RoundToInt(bruiseDamageAmount * finisherBruiseMultiplier));
        }

        // ---- ONE combined freeze (base + bruise-break extra + finisher extra) ----
        bool willBreak = (enemy.CurrentBruise + bruiseThisHit) >= enemy.MaxBruise && !enemy.GaugeIsBroken;

        float freeze = hitStopDuration
                     + (willBreak ? extraHitStopOnBruiseBreak : 0f)
                     + (finisherNow ? extraHitStopOnFinisher : 0f);

        HitStopManager.Request(freeze);

        // ---- apply damage & knockback ----
        if (finisherNow)
        {
            // Finisher: more damage, more bruise, directional throw
            enemy.Damage(hpThisHit, bruiseThisHit, hitDir, 1f, finisherThrow: true);
        }
        else
        {
            // Normal: base damage + bruise, linear knockback
            enemy.Damage(hpThisHit, bruiseThisHit, hitDir, 1f);
        }


        // Apply damage & route knockback
        if (finisherNow)
        {
            // Finisher: request the "throw" style in EnemyHealth/KnockbackController
            enemy.Damage(damageAmount, bruiseDamageAmount, hitDir, 1f, finisherThrow: true);
        }
        else
        {
            // Normal: linear knockback (mul=1 here; keep your own mul if desired)
            enemy.Damage(damageAmount, bruiseDamageAmount, hitDir, 1f);
        }

        // Trigger player hover on side/up/diag (skip downward unless you want it)
        if (playerHover != null)
        {
            bool isDownish = Vector2.Dot(hitDir.normalized, Vector2.down) >= pogoDownDotThreshold;
            if (!isDownish || enableHoverOnPogo)
            {
                float dur = (characterControl != null && characterControl.isGrounded)
                            ? playerHoverDurationGround
                            : playerHoverDurationAir;

                playerHover.BeginHover(dur, true, null); // zero Y, unscaled-time hover
            }
        }

        // (optional: your extra effects here — hover, player bounce-back, VFX, SFX, camera nudge, etc.)

        // Rearm after per-swing cooldown
        if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(ResetAttack());
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    // Use the snapped 5-way aim from the manager; fallbacks preserve directionality.
    private Vector2 GetAttackAimDir(EnemyHealth enemy)
    {
        if (meleeAttackManager != null && meleeAttackManager.meleeAttackDir.sqrMagnitude > 0.0001f)
            return meleeAttackManager.meleeAttackDir.normalized;

        if (characterControl != null)
        {
            Vector2 toEnemy = (Vector2)enemy.transform.position - (Vector2)characterControl.transform.position;
            if (toEnemy.sqrMagnitude > 0.0001f) return toEnemy.normalized;
            return characterControl.facingRight ? Vector2.right : Vector2.left;
        }

        return Vector2.right;
    }

    // Update per-target counters; returns true on 3rd (or more) connect within the window
    private bool UpdateComboTarget(EnemyHealth enemy)
    {
        if (Time.time > comboTargetUntil)
        {
            currentComboTarget = null;
            currentTargetHits = 0;
        }

        if (currentComboTarget == null || currentComboTarget != enemy)
        {
            currentComboTarget = enemy;
            currentTargetHits = 1;
        }
        else
        {
            currentTargetHits++;
        }

        comboTargetUntil = Time.time + comboTargetWindow;
        return currentTargetHits >= 3;
    }

    private IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(attackCooldown);
        hasAttackedThisSwing = false;
    }

    // Re-entrant-safe hit stop that restores timeScale correctly even on overlap
    private IEnumerator HitStop()
    {
        if (_hitStopDepth == 0)
        {
            _prevTimeScale = Time.timeScale;
            _prevFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
        }

        _hitStopDepth++;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        _hitStopDepth--;
        if (_hitStopDepth == 0)
        {
            Time.timeScale = _prevTimeScale;
            Time.fixedDeltaTime = _prevFixedDeltaTime;
        }
    }
}
