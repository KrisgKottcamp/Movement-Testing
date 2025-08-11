using System;
using System.Collections;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MeleeWeapon : MonoBehaviour
{
    // let other systems know when a pogo actually happened
    public event Action OnPogoPerformed;

    [Header("Damage Settings")]
    [SerializeField] private int damageAmount = 20;
    [SerializeField] private int bruiseDamageAmount = 20;
    [SerializeField] private float attackCooldown = 0.2f;         // Time before next pogo

    [Header("Hit‑Stop Settings")]
    [Tooltip("Seconds of frozen time on each hit")]
    [SerializeField] private float hitStopDuration = 0.05f;
    private CinemachineImpulseSource impulseSource;

    // internal trackers
    private float _prevTimeScale, _prevFixedDeltaTime;

    [Header("Bounce Settings")]
    [SerializeField] private float pogoForce = 12f;          // Upward bounce strength
    [SerializeField] private float bounceBackForce = 5f;          // Backwards bounce strength
    [SerializeField] private float pogoDownDotThreshold = 0.6f; // 0.6 ≈ within ~53° cone below
    [SerializeField] public Vector2 hitboxSize = new Vector2(1f, 0.25f);   // Width x Height
    [SerializeField] public Vector2 hitboxOffset = new Vector2(0f, -0.5f);   // Local space offset
    [SerializeField] private LayerMask enemyLayer;                    // Layer mask for enemies

    [Header("Attack Hover Settings")]
    [SerializeField] private float playerHoverDurationAir = 0.10f;
    [SerializeField] private float playerHoverDurationGround = 0.06f;
    [SerializeField] private float enemyHoverDuration = 0.10f;
    [SerializeField] private bool enableHoverOnPogo = false;
    [SerializeField][Range(0f, 90f)] private float downwardAsPogoAngle = 35f;
    // Optional: how floaty enemies feel while hovering (0 = keep current)
    [SerializeField] private float enemyHoverHorizontalDrag = 8f;


    [SerializeField] private CharacterControl characterControl;
    private MeleeAttackManager meleeAttackManager;
    public EnemyHealth enemyHealth;
    private bool hasAttackedThisSwing = false;
    private Coroutine cooldownCoroutine;
    private AttackHover playerHover;
   

    static readonly Collider2D[] _hitBuffer = new Collider2D[4];

    private void Awake()
    {
        characterControl = GetComponentInParent<CharacterControl>();
        meleeAttackManager = GetComponentInParent<MeleeAttackManager>();
        playerHover = GetComponentInParent<AttackHover>();
    }



    public void PerformPogo()
    {
        if (hasAttackedThisSwing) return;

        Vector2 origin = (Vector2)transform.position + hitboxOffset; // your current box below player
        int count = Physics2D.OverlapBoxNonAlloc(origin, hitboxSize, 0f, _hitBuffer, enemyLayer);
        for (int i = 0; i < count; i++)
        {
            var enemy = _hitBuffer[i].GetComponentInParent<EnemyHealth>();
            if (enemy != null && TryPogo(enemy)) break;   // stop after first pogo
        }
    }



    private void OnTriggerEnter2D(Collider2D col)
    {
        if (hasAttackedThisSwing) return;

        var enemy = col.GetComponentInParent<EnemyHealth>();
        if (!enemy) return;

        // Try pogo first; if it triggers, we're done
        if (TryPogo(enemy)) return;

        // Otherwise do the normal side-hit
        HandleCollision(enemy);
    }

    // shared pogo path (used by trigger and by your animation event)
    private bool TryPogo(EnemyHealth enemy)
    {
        // Same aim/didPogo code as in PerformPogo()
        Vector2 aim = meleeAttackManager.meleeAttackDir.sqrMagnitude > 0.0001f
            ? meleeAttackManager.meleeAttackDir.normalized
            : (characterControl.facingRight ? Vector2.right : Vector2.left);

        bool isDownward = Vector2.Dot(aim, Vector2.down) >= pogoDownDotThreshold;

        if (!(enemy.giveUpwardForce && !characterControl.isGrounded && isDownward))
            return false;

        StartCoroutine(HitStop());
        enemy.Damage(damageAmount, bruiseDamageAmount, Vector2.down);

        // The bounce you already use
        characterControl.ApplyPogoForce(Vector2.up * pogoForce);
        OnPogoPerformed?.Invoke();

        hasAttackedThisSwing = true;
        if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(ResetAttack());
        return true;
    }

    private void HandleCollision(EnemyHealth enemy)
    {
        // 1) guard so we only ever do this once per attack swing
        if (hasAttackedThisSwing)
            return;
        hasAttackedThisSwing = true;

        // 2) Always apply both health and bruise damage:
        Vector2 hitDir = meleeAttackManager.raw;
        if (hitDir == Vector2.zero) // If I am not holding a stick direction when attacking, Hit direction defaults to forward.
        {
            hitDir = characterControl.facingRight ? Vector2.right : Vector2.left;
        }

        StartCoroutine(HitStop());

        // 3) Handles Enemy Damage
        enemy.Damage(damageAmount, bruiseDamageAmount, hitDir);

        // If this hit broke the gauge, grant a brief mid-air jump
        if (enemy.JustBrokeBruise)
        {
            if (characterControl != null)
                characterControl.GrantBruiseBreakAirJump(1f); // tweak in inspector if you like
        }

        // bounce the player *away* from the enemy  
        Vector2 bounceDir = -hitDir;    // (hitDir points TOWARD the enemy, so invert it)
        characterControl.ApplyBounceBackForce(bounceDir * bounceBackForce);



        // --- Attack Hover (Player + Enemy) ---
        Vector2 attackDir =
            (meleeAttackManager != null && meleeAttackManager.meleeAttackDir.sqrMagnitude > 0.0001f)
                ? meleeAttackManager.meleeAttackDir.normalized
                : (characterControl.facingRight ? Vector2.right : Vector2.left);

        bool isDownish = Vector2.Angle(attackDir, Vector2.down) <= downwardAsPogoAngle;

        // Enemy hover: always a short suspend
        var enemyHover = enemy.GetComponent<AttackHover>();
        if (enemyHover != null)
            enemyHover.BeginHover(enemyHoverDuration, true, enemyHoverHorizontalDrag);

        // Player hover: skip down-slash unless explicitly enabled
        if (playerHover != null && (enableHoverOnPogo || !isDownish))
        {
            float dur = (characterControl != null && characterControl.isGrounded) ? playerHoverDurationGround : playerHoverDurationAir;
            playerHover.BeginHover(dur, true, null);
        }



        if (cooldownCoroutine != null) 
            StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(ResetAttack());


    }

    private IEnumerator HitStop()
    {

        // store current time settings
        _prevTimeScale = Time.timeScale;
        _prevFixedDeltaTime = Time.fixedDeltaTime;

        // freeze everything
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        // wait in REAL time (unaffected by timeScale)
        yield return new WaitForSecondsRealtime(hitStopDuration);

        // restore normal time
        Time.timeScale = _prevTimeScale;
        Time.fixedDeltaTime = _prevFixedDeltaTime;
    }


    private IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(attackCooldown);
        hasAttackedThisSwing = false;
        
    }

    private void OnDrawGizmosSelected()
    {
        // Draws box for tuning
        Gizmos.color = Color.red;

        Vector2 origin = (Vector2)transform.position + hitboxOffset;
        Gizmos.DrawWireCube(origin, hitboxSize);
    }
}