using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeAttackManager : MonoBehaviour
{
    //How much movement force should be applied to the player DOWNWARD or HORIZONTALLY when a melee attack collides with a GameObject.
    public float defaultForce = 30f;
    //How much movement force should be applied to the player UPWARD when a melee attack collides with a GameObject with a downward strike attack.
    public float upwardsForce = 0f;
    //How long the player should move when the melee attack collides with a GameObject with an EnemyHealth script attached.
    public float attackMovementTime = 1f;
    //Receives input from Unity Input System
    private bool meleeAttackInput;
    //Just checks to see if an input is being pressed for a melee attack.
    private bool meleeAttack;
    //Is the direction the player is attacking.
    public Vector2 meleeAttackDir;
    //Reference to the animator component on the melee weapon itself.
    private Animator meleeAnimator;
    //The Animator Component on the player
    private Animator anim;
    //The CharacterControl script on the player; pulled to access the grounded state.
    private CharacterControl characterControl;

    public Vector2 raw;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        //references components
        anim = GetComponent<Animator>(); //The Animator component on the player.
        characterControl = GetComponent<CharacterControl>();//The main CharacterControl script on the player used for managing grounded state.
        meleeAnimator = GetComponentInChildren<MeleeWeapon>().gameObject.GetComponent<Animator>();//The animator component on the melee weapon
    }

    private void OnMeleeAttack()
    {
        meleeAttackInput = true;
    }


    private void TrySticky()
    {
        Debug.Log($"[AttackMgr] RawInput=({raw.x:F2},{raw.y:F2}) SnappedDir={meleeAttackDir}");
        var stick = GetComponent<AttackStickinessController>();
        if (stick != null)
            stick.TryStickToNearestEnemy(meleeAttackDir);

    }

    private void CheckInput() // Main method to handle melee input and execute corresponding attacks
    {
        // ------------------------------------------------------------------
        // 1. Gather raw input so that we know which way the player is aiming.
        // ------------------------------------------------------------------

        float x = Input.GetAxisRaw("Horizontal"); // Raw horizontal axis value (-1 to 1)
        float y = Input.GetAxisRaw("Vertical");    // Raw vertical axis value (-1 to 1)

        raw = new Vector2(x, y);                    // Cache for use by other systems

        // Define a small threshold to ignore tiny stick movements (joystick drift)
        const float deadZone = 0.2f;
        // If the stick’s overall tilt is less than the dead zone...
        if (raw.magnitude < deadZone)
        {
            meleeAttackDir = Vector2.zero;          // ...treat it as “no direction”
        }
        else
        {
            // When one axis is much stronger than the other, we snap to cardinal
            const float cardinalThreshold = 2.3f;
            float ax = Mathf.Abs(raw.x), ay = Mathf.Abs(raw.y); // absolute X and Y for comparison

            Vector2 dir;                               // will hold our snapped direction
                                                       // If horizontal input dominates by our threshold...
            if (ax > ay * cardinalThreshold)
                dir = new Vector2(Mathf.Sign(raw.x), 0);       // ...snap to pure left/right
                                                               // Else if vertical input dominates...
            else if (ay > ax * cardinalThreshold)
                dir = new Vector2(0, Mathf.Sign(raw.y));       // ...snap to pure up/down
            else
                // Otherwise, treat it as a diagonal using the sign of each axis
                dir = new Vector2(Mathf.Sign(raw.x), Mathf.Sign(raw.y));

            meleeAttackDir = dir;                      // store the discrete direction (±1,0) or (±1,±1)
        }

        // If the player didn’t actually press the attack button...
        if (!meleeAttackInput)
            return;                                    // exit early, no attack to perform

        meleeAttack = meleeAttackInput;                // copy the input flag for any other systems

        // This bool tracks whether an attack animation was played at all.
        bool performedAttack = false;

        // -------------------------------------------------------------
        // 2. Trigger the player’s swing animation based on the snapped
        //    direction.  The weapon’s swipe animation is handled later
        //    so that it can rotate freely.
        // -------------------------------------------------------------
        switch (meleeAttackDir)
        {
            case var d when d == new Vector2(0, 1):   // straight up
                anim.SetTrigger("UpwardMelee");       // trigger character animation
                TrySticky();
                performedAttack = true;
                break;

            case var d when d == new Vector2(0, -1)
                           && !characterControl.isGrounded:  // straight down in air
                anim.SetTrigger("DownwardMelee");
                performedAttack = true;
                break;

            case var d when (d.y == 0):              // forward on ground or no dir
                anim.SetTrigger("ForwardMelee");
                TrySticky();
                performedAttack = true;
                break;

            case var d when d == new Vector2(1, 1):   // up-right diagonal
                anim.SetTrigger("UpwardDiagonalMelee");
                TrySticky();
                performedAttack = true;
                break;

            case var d when d == new Vector2(1, -1)
                           && !characterControl.isGrounded: // down-right diagonal in air
                anim.SetTrigger("DownwardDiagonalMelee");
                TrySticky();
                performedAttack = true;
                break;

            case var d when d == new Vector2(-1, 1):  // up-left diagonal
                anim.SetTrigger("UpwardDiagonalMelee");
                TrySticky();
                performedAttack = true;
                break;

            case var d when d == new Vector2(-1, -1)
                           && !characterControl.isGrounded: // down-left diagonal in air
                anim.SetTrigger("DownwardDiagonalMelee");
                TrySticky();
                performedAttack = true;
                break;

            default:
                TrySticky();                          // grounded down-diagonal, do nothing
                break;
        }

        // -----------------------------------------------------------------
        // 3. If an attack was performed, rotate the swipe to face the raw
        //    input direction and trigger the omni-directional swipe effect.
        // -----------------------------------------------------------------
        if (performedAttack)
        {
            // Determine the direction the swipe should face.  If no stick
            // input is given, default to the character’s facing direction.
            Vector2 swipeDir = raw;
            if (swipeDir == Vector2.zero)
                swipeDir = characterControl.facingRight ? Vector2.right : Vector2.left;

            // Grounded downward attacks are disallowed; force horizontal.
            if (characterControl.isGrounded && swipeDir.y < 0)
                swipeDir.y = 0;

            PlayOmniSwipe(swipeDir);                  // rotate + trigger animation
            meleeAttackInput = false;                 // clear the input flag
        }
    }

    private void PlayOmniSwipe(Vector2 direction)
    {
        // Rotate the swipe animator so that its local X axis points along
        // the desired direction.  The swipe animation itself faces right
        // by default, so aligning transform.right works for any direction.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        meleeAnimator.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Trigger the universal swipe animation.
        meleeAnimator.SetTrigger("OmniMeleeSwipe");
    }




    // Update is called once per frame
    void Update()
    {
        //Method that checks if melee button is being pressed.
        CheckInput();
    }
}
