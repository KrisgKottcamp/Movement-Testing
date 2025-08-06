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
    private Transform swipePivot;

    public Vector2 raw;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        //references components
        anim = GetComponent<Animator>(); //The Animator component on the player.
        characterControl = GetComponent<CharacterControl>();//The main CharacterControl script on the player used for managing grounded state.
        var weapon = GetComponentInChildren<MeleeWeapon>();
        meleeAnimator = weapon.GetComponent<Animator>();//The animator component on the melee weapon
        swipePivot = weapon.transform.parent;
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

    private void PlayOmniSwipe(Quaternion rotation)
    {
        if (swipePivot != null)
            swipePivot.rotation = rotation;
    }

    private void CheckInput() // Main method to handle melee input and execute corresponding attacks
    {
        // Read raw stick input without Unity’s smoothing

        float x = Input.GetAxisRaw("Horizontal"); // Raw horizontal axis value (-1 to 1)
        float y = Input.GetAxisRaw("Vertical");    // Raw vertical axis value (-1 to 1)

        raw = new Vector2(x, y);

        // Define a small threshold to ignore tiny stick movements (joystick drift)
        const float deadZone = 0.2f;
        // If the stick’s overall tilt is less than the dead zone...
        if (raw.magnitude < deadZone)
        {
            meleeAttackDir = Vector2.zero; // ...treat it as “no direction”
        }
        else
        {
            // When one axis is much stronger than the other, we snap to cardinal
            const float cardinalThreshold = 2.3f;
            float ax = Mathf.Abs(raw.x), ay = Mathf.Abs(raw.y); // absolute X and Y for comparison

            Vector2 dir; // will hold our snapped direction
                         // If horizontal input dominates by our threshold...
            if (ax > ay * cardinalThreshold)
                dir = new Vector2(Mathf.Sign(raw.x), 0);       // ...snap to pure left/right
                                                               // Else if vertical input dominates...
            else if (ay > ax * cardinalThreshold)
                dir = new Vector2(0, Mathf.Sign(raw.y));       // ...snap to pure up/down
            else
                // Otherwise, treat it as a diagonal using the sign of each axis
                dir = new Vector2(Mathf.Sign(raw.x), Mathf.Sign(raw.y));

            meleeAttackDir = dir; // store the discrete direction (±1,0) or (±1,±1)
        }

        // If the player didn’t actually press the attack button...
        if (!meleeAttackInput)
            return;                        // exit early, no attack to perform

        meleeAttack = meleeAttackInput;    // copy the input flag for any other systems


        // Use the snapped direction to pick which attack animation to play
        switch (meleeAttackDir)
        {
            case var d when d == new Vector2(0, 1):   // straight up
                anim.SetTrigger("UpwardMelee");          // trigger character animation
                meleeAnimator.SetTrigger("UpwardMeleeSwipe"); // trigger swipe VFX
                meleeAttackInput = false;                // clear the input flag
                TrySticky();
                break;

            case var d when d == new Vector2(0, -1)
                           && !characterControl.isGrounded:  // straight down in air
                anim.SetTrigger("DownwardMelee");
                meleeAnimator.SetTrigger("DownwardMeleeSwipe");
                meleeAttackInput = false;
                break;

            case var d when (d.y == 0): // forward on ground or no dir
                anim.SetTrigger("ForwardMelee");
                meleeAnimator.SetTrigger("ForwardMeleeSwipe");
                meleeAttackInput = false;
                TrySticky();
                break;

            case var d when d == new Vector2(1, 1):   // up‐right diagonal
                anim.SetTrigger("UpwardDiagonalMelee");
                meleeAnimator.SetTrigger("UpwardDiagonalMeleeSwipe");
                meleeAttackInput = false;
                TrySticky();
                break;

            case var d when d == new Vector2(1, -1)
                           && !characterControl.isGrounded: // down‐right diagonal in air
                anim.SetTrigger("DownwardDiagonalMelee");
                meleeAnimator.SetTrigger("DownwardDiagonalMeleeSwipe");
                meleeAttackInput = false;
                TrySticky();
                break;

            case var d when d == new Vector2(-1, 1):   // up‐left diagonal
                anim.SetTrigger("UpwardDiagonalMelee");
                meleeAnimator.SetTrigger("UpwardDiagonalMeleeSwipe");
                meleeAttackInput = false;
                TrySticky();
                break;

            case var d when d == new Vector2(-1, -1)
                           && !characterControl.isGrounded: // down‐left diagonal in air
                anim.SetTrigger("DownwardDiagonalMelee");
                meleeAnimator.SetTrigger("DownwardDiagonalMeleeSwipe");
                meleeAttackInput = false;
                TrySticky();
                break;

            default:
                TrySticky();
                // Any other case (e.g. grounded down-diagonal) — do nothing
                break;
        }
    }




    // Update is called once per frame
    void Update()
    {
        //Method that checks if melee button is being pressed.
        CheckInput();
    }
}
