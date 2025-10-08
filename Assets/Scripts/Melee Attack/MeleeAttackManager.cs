using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Original flow + 3–hit combo wiring (compatible with MeleeWeapon).
/// - Keeps public fields: `raw` and `meleeAttackDir`
/// - Triggers player 5-direction states exactly like before
/// - Drives weapon swipe with ComboStage (1/2/3) + PlaySwipe
/// </summary>
public class MeleeAttackManager : MonoBehaviour
{
    // ------- ORIGINAL SURFACE (kept for compatibility) -------
    private bool meleeAttackInput;
    private bool meleeAttack;
    public Vector2 meleeAttackDir;  // (±1,0) or (±1,±1)
    public Vector2 raw;             // (-1..1 per axis), used by MeleeWeapon

    private Animator meleeAnimator; // weapon swipe animator
    private Animator anim;          // player animator
    private CharacterControl characterControl;

    [Header("Swipe Placement")]
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private float swipeReach = 1.0f;
    [SerializeField] private bool cancelParentFlip = true;

    [Header("Optional Stickiness Pull")]
    [SerializeField] private AttackStickinessController stickiness;

    // ---------------- NEW: Combo knobs ----------------
    [Header("Combo")]
    [Tooltip("Max seconds allowed between presses to continue the 3-hit chain.")]
    [SerializeField] private float comboWindow = 0.40f;
    [Tooltip("Short lockout after the third swing (or when the chain ends).")]
    [SerializeField] private float comboCooldown = 0.45f;

    private int swingIndex = 1;       // 1 -> 2 -> 3 -> 1
    private float comboUntil = -1f;     // Time.time deadline to continue chain
    private bool inComboCooldown;
    private float comboCooldownUntil;
    private int _latchedStage;        // stage for THIS swing

    // Animator param hash
    static readonly int ANIM_AttackLocked = Animator.StringToHash("AttackLocked");

    // Simple one-deep queue
    private bool queuedAttack = false;
    private int queuedStage = 1;
    private Vector2 queuedDir = Vector2.right;
    private string queuedTrigger = "ForwardMelee";


    // Animator params
    private static readonly int ANIM_ComboIndex = Animator.StringToHash("ComboIndex");   // Player (float)
    private static readonly int SWIPE_ComboStage = Animator.StringToHash("ComboStage");   // Weapon (int)
    private static readonly int SWIPE_PlaySwipe = Animator.StringToHash("PlaySwipe");    // Weapon (trigger)

    // Player direction triggers (unchanged)
    private const string TRIG_Up = "UpwardMelee";
    private const string TRIG_UpDiag = "UpwardDiagonalMelee";
    private const string TRIG_Forward = "ForwardMelee";
    private const string TRIG_DownDiag = "DownwardDiagonalMelee";
    private const string TRIG_Down = "DownwardMelee";

    private void Start()
    {
        // Auto-wire AttackStickinessController if not set in Inspector
        if (stickiness == null)
            stickiness = GetComponent<AttackStickinessController>();

        anim = GetComponent<Animator>();
        characterControl = GetComponent<CharacterControl>();

        var mw = GetComponentInChildren<MeleeWeapon>();
        if (mw != null)
        {
            meleeAnimator = mw.GetComponent<Animator>();
            if (meleeAnimator == null) meleeAnimator = mw.GetComponentInChildren<Animator>();
            if (weaponPivot == null && meleeAnimator != null) weaponPivot = meleeAnimator.transform;
        }
        if (weaponPivot == null && meleeAnimator != null)
            weaponPivot = meleeAnimator.transform;
    }

    private void Update()
    {
        CheckInput();
    }

    // Input System callback (kept)
    private void OnMeleeAttack() => meleeAttackInput = true;

    private void TrySticky()
    {
        if (stickiness != null)
            stickiness.TryStickToNearestEnemy(meleeAttackDir);
    }

    private void CheckInput()
    {
        // --- Combo housekeeping ---
        if (inComboCooldown && Time.time >= comboCooldownUntil) inComboCooldown = false;
        if (inComboCooldown) { meleeAttackInput = false; return; }
        if (comboUntil > 0f && Time.time > comboUntil) swingIndex = 1;

        // --- 1) Gather raw input (kept) ---
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        raw = new Vector2(x, y);

        if (raw.sqrMagnitude <= 0.0001f)
        {
            meleeAttackDir = Vector2.zero; // still allows default forward later
        }
        else
        {
            // Snap to 5 directions (same style as original)
            const float cardinalThreshold = 2.3f;
            float ax = Mathf.Abs(raw.x), ay = Mathf.Abs(raw.y);

            Vector2 dir;
            if (ax > ay * cardinalThreshold) dir = new Vector2(Mathf.Sign(raw.x), 0f);
            else if (ay > ax * cardinalThreshold) dir = new Vector2(0f, Mathf.Sign(raw.y));
            else dir = new Vector2(Mathf.Sign(raw.x), Mathf.Sign(raw.y));

            meleeAttackDir = dir;
        }


        if (!meleeAttackInput) return; // no press this frame

        meleeAttack = meleeAttackInput; // kept
        bool performedAttack = false;



        // --- Latch stage & set player's ComboIndex once ---
        _latchedStage = swingIndex;
        if (anim != null) anim.SetFloat(ANIM_ComboIndex, _latchedStage);

        // --- 2) Fire the correct player swing trigger (unchanged behavior) ---
        if (meleeAttackDir == new Vector2(0, 1)) { anim.SetTrigger(TRIG_Up); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(1, 1)) { anim.SetTrigger(TRIG_UpDiag); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(1, 0)) { anim.SetTrigger(TRIG_Forward); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(1, -1) && !characterControl.isGrounded)
        { anim.SetTrigger(TRIG_DownDiag); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(0, -1) && !characterControl.isGrounded)
        { anim.SetTrigger(TRIG_Down); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(-1, 0)) { anim.SetTrigger(TRIG_Forward); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(-1, 1)) { anim.SetTrigger(TRIG_UpDiag); TrySticky(); performedAttack = true; }
        else if (meleeAttackDir == new Vector2(-1, -1) && !characterControl.isGrounded)
        { anim.SetTrigger(TRIG_DownDiag); TrySticky(); performedAttack = true; }
        else { anim.SetTrigger(TRIG_Forward); TrySticky(); performedAttack = true; }

        // --- 3) Aim & play the swipe (unchanged signature) ---
        if (performedAttack)
        {
            Vector2 swipeDir = raw;
            if (swipeDir == Vector2.zero)
                swipeDir = characterControl.facingRight ? Vector2.right : Vector2.left;

            if (characterControl.isGrounded && swipeDir.y < 0f)
                swipeDir.y = 0f; // grounded downward becomes horizontal

            PlayOmniSwipe(swipeDir);
        }

        // --- 4) Advance combo + timers ---
        comboUntil = Time.time + comboWindow;
        if (_latchedStage >= 3)
        {
            inComboCooldown = true;
            comboCooldownUntil = Time.time + comboCooldown;
            swingIndex = 1;
        }
        else swingIndex = _latchedStage + 1;

        meleeAttackInput = false; // clear one-shot
    }

    // Signature unchanged for compatibility with original code.
    private void PlayOmniSwipe(Vector2 direction)
    {
        if (weaponPivot == null)
        {
            if (meleeAnimator != null) weaponPivot = meleeAnimator.transform;
            if (weaponPivot == null) return;
        }

        // A) make local scale positive so parent flip doesn’t invert collider
        if (cancelParentFlip)
        {
            var ls = weaponPivot.localScale;
            ls.x = Mathf.Abs(ls.x);
            ls.y = Mathf.Abs(ls.y);
            weaponPivot.localScale = ls;
        }

        // B) compute rotation/position with parent-mirror compensation
        var parent = weaponPivot.parent;
        bool parentMirrored = parent && parent.lossyScale.x < 0f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (parentMirrored) angle += 180f;

        Vector3 worldPos = transform.position + (Vector3)(direction.normalized * swipeReach);
        weaponPivot.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, 0f, angle));

        // C) trigger the weapon swipe for the CURRENT stage (1/2/3)
        if (meleeAnimator != null)
        {
            meleeAnimator.ResetTrigger(SWIPE_PlaySwipe);
            meleeAnimator.SetInteger(SWIPE_ComboStage, _latchedStage);
            meleeAnimator.SetTrigger(SWIPE_PlaySwipe);

            // (optional, if you still have the legacy single-state around)
            // meleeAnimator.SetTrigger("OmniMeleeSwipe");
        }
    }
}
