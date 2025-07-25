using System.Collections;
using System.Runtime.ConstrainedExecution;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using Unity.Cinemachine;


public class CharacterControl : MonoBehaviour
{


    #region Movement Settings

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    private PlayerControls playercontrols;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] public LayerMask layersToHit;


    [Header("Jump Settings")]
    [SerializeField] private float jumpforce;
    [SerializeField] private float fallMult;
    private float jumpTimeCounter;
    [SerializeField] private float jumpTime;
    private bool isJumping;
    [SerializeField] private float gravforce;
    [SerializeField] private float raycastDistance;
    public bool isGrounded = true;


    [Header("Coyote & Buffer")]
    private float coyoteTime = 0.13f;
    private float coyoteTimeCounter;
    private float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;
    [SerializeField] private float missCompensation;

    [Header("Walk Settings")]
    public float mover;
    public float moveSpeed;
    public bool facingRight = true;

    #endregion

    #region Movement Settings
    [Header("Wall Slide/Grab")]
    public bool isWallSliding;
    public float wallSlidingSpeed = 2f;



    [Header("Wall Jump")]
    public bool isWallJumping;
    private float wallJumpingDirection;
    public float wallJumpingTime = .2f;
    public float wallJumpingCounter;
    private float wallJumpingDuration = 0.4f;
    public Vector2 wallJumpingPower = new Vector2(-8f, 16f);

    public float airInterpolant = 1f;
    public float airInterpolantchange;

    public bool isWallGrabbing;
    public float wallClimbSpeed;
    public float wallMover;
    #endregion


    #region Dash Settings

    [Header("Dash")]
    [SerializeField] bool dashInput;
    public float dashingSpeed;
    public float dashingTime;
    public Vector2 dashingDir;
    public bool isDashing;
    private bool canDash = true;
    private CinemachineImpulseSource impulseSource;
    #endregion

    [Header("Bounce-Back (for attack swing)")]
    [SerializeField]
    public float bounceBackLerpTime = 0.2f;

    [HideInInspector] public bool isMovementLocked = false;

    


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playercontrols = new PlayerControls();
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isMovementLocked) return;   // ← bail out here when sticking
        WallSlide();
        WallJump();
        JumpController();

        bool isAttacking = animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack");
        if (isAttacking)
        {
            moveSpeed = 0;
        } else { moveSpeed = 12f; }

        //AIR INTERPOLANT
        airInterpolant = Mathf.Clamp01(airInterpolant + Time.deltaTime * airInterpolantchange);
    }

    private void FixedUpdate()
    {
        if (isPogoBouncing)
            return;

        if (isMovementLocked) return;   // ← also skip any physics override
        MovementController();
        Dashing();
    }

    private void MovementController()
    {

        //MOVEMENT CONTROLLER

        if (!isWallJumping)
        {
            mover = (Input.GetAxisRaw("Horizontal"));
            wallMover = (Input.GetAxisRaw("Vertical"));
            Vector2 direction = new Vector2(mover, wallMover);

            Walk(direction);
            //wallGrab(direction);


            if (mover > 0f && !facingRight)
            {
                Flip();
            }

            if (mover < 0f && facingRight)
            {
                Flip();
            }
        }

    }


    //JUMP CONTROLLER
    public void JumpController()
    {


        #region Inputs;     //Stores inputs as Vars.
        var jumpInput = (Input.GetButton("Jump"));
        var jumpInputDown = (Input.GetButtonDown("Jump"));
        var jumpInputUp = (Input.GetButtonUp("Jump"));
        #endregion
        if (Physics2D.OverlapCircle(groundCheck.position, 0.2f, wallLayer))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }


        // BASIC JUMP
        #region BasicJump; //Calls inputs to allow for a basic jump.
        if (jumpInputDown && isGrounded == true)
        {
            isJumping = true;
            jumpTimeCounter = jumpTime;
        }
        if (jumpInput && isJumping == true)
        {
            if (jumpTimeCounter > 0)
            {
                rb.linearVelocity = Vector2.up * jumpforce;
                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                isJumping = false;
            }
        }
        if (jumpInputUp)
        {
            isJumping = false;
        }
        #endregion


        // COYOTE TIME
        #region CoyoteTime;
        if (isGrounded == true)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
        #endregion

        // JUMP BUFFER
        #region BufferTime;
        if (jumpInputDown)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
        #endregion


        // CT & JB ACTIVATION
        #region CT/JBActivation;
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = Vector2.up * jumpforce * missCompensation;
            jumpBufferCounter = 0f;
        }
        if (jumpInput)
        {
            coyoteTimeCounter = 0f;
        }
        #endregion


        //FAST FALLING
        #region FastFalling; // causes player to fall faster than when they go up when jumping.
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMult - 1) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0 && jumpBufferCounter < 0f && coyoteTimeCounter < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * Time.deltaTime;
        }
        #endregion

        //HEAD HIT RAYCAST
        #region HeadHit; // Shoots a ray above the player's head when jumping to detect if they hit an object above them or not.
        var headHit = Physics2D.Raycast(transform.position, Vector2.up, raycastDistance, layersToHit);

        if (headHit.collider && isJumping)
        {
            Debug.Log("Something Was Hit");
            rb.linearVelocityY = -7f;
            isJumping = false;
        }
        #endregion

    }




    //WallSlide
    public void Walk(Vector2 direction)
    {
        var currentLinearVelocity = rb.linearVelocity;
        var desiredVelocity = (new Vector2(direction.x * moveSpeed, rb.linearVelocity.y));

        var newVelocity = Vector2.Lerp(currentLinearVelocity, desiredVelocity, airInterpolant);
        rb.linearVelocity = newVelocity;
    }

    //Draws Circle Collider
    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(wallCheck.position, .9f);
    }
    public bool isWalled()
    {
        return Physics2D.OverlapCircle(wallCheck.position, .9f, wallLayer);
    }

    private void WallSlide()
    {
        if (isWalled() && rb.linearVelocityY < 0 && mover != 0)
        {
            isWallSliding = true;
            rb.linearVelocityY -= (rb.linearVelocityY - wallSlidingSpeed);
        }
        else
        {
            isWallSliding = false;
        }
    }


    //WallGrab

    public void wallGrab(Vector2 direction)
    {

        if (isWalled() && Input.GetKey(KeyCode.RightShift))
        {
            isWallGrabbing = true;
        }
        else { isWallGrabbing = false; }

        if (isWallGrabbing)
        {

            rb.linearVelocity *= 0f;
            rb.gravityScale = 3;

            if (wallMover != 0)
            {
                rb.linearVelocityY = rb.linearVelocityY = direction.y * wallClimbSpeed;
            }

        }
        else { rb.gravityScale = gravforce; }

        if (isWallGrabbing && Input.GetButton("Jump"))
        {
            mover = 0;
            isWallGrabbing = false;
            isWallJumping = true;

            jumpTimeCounter = jumpTime;
        }

    }



    //WallJump

    private void WallJump()
    {
        if (isWalled() && !isGrounded && mover != 0)
        {
            isWallJumping = false;
            isWallGrabbing = false;
            wallJumpingDirection = -transform.localScale.x;
            wallJumpingCounter = wallJumpingTime;

            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpingCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && wallJumpingCounter > 0f && isWalled())
        {
            isWallJumping = true;

            jumpTimeCounter = jumpTime;

        }

        if (Input.GetButton("Jump") && isWallJumping == true)
        {
            if (jumpTimeCounter > 0f)
            {
                airInterpolant = 0f;
                rb.linearVelocity = new Vector2(wallJumpingDirection * wallJumpingPower.x, wallJumpingPower.y);
                wallJumpingCounter = 0f;
                Invoke(nameof(StopWallJumping), wallJumpingCounter);

                jumpTimeCounter -= Time.deltaTime;
                Debug.Log("Wall Jump Triggered");

            }
        }

        if (Input.GetButtonUp("Jump"))
        {
            isWallJumping = false;
        }

        if (isGrounded == true)
        {
            airInterpolant = 1;
            wallJumpingCounter = wallJumpingTime;
        }


    }


    private void StopWallJumping()
    {
        isWallJumping = false;
    }


    public void Flip()
    {
        Vector3 currentScale = gameObject.transform.localScale;
        currentScale.x *= -1;
        gameObject.transform.localScale = currentScale;

        facingRight = !facingRight;
    }


    private void OnDash()
    {
        dashInput = true;
    }


    private void Dashing()
    {


        if (dashInput && canDash)
        {
            CameraShakeManager.instance.CameraShake(impulseSource);
            isDashing = true;
            canDash = false;
            dashingDir = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).normalized;

            //If the player is not moving, the dash direction defaults to forward.
            if (dashingDir == Vector2.zero)
            {
                dashingDir = new Vector2(transform.localScale.x, 0);
            }
            StartCoroutine(StopDashing());

        }


        if (isDashing)
        {

            rb.linearVelocity = dashingDir.normalized * dashingSpeed;
            rb.gravityScale = 0;
            airInterpolant = .5f;
            dashInput = false;
            return;
        }
        if (isGrounded)
        {
            canDash = true;
            rb.gravityScale = gravforce;
        }

        if (!canDash && Physics2D.OverlapCircle(wallCheck.position, 0.2f, wallLayer))
        {

            StopDashing();

        }
    }

    private IEnumerator StopDashing()
    {
        yield return new WaitForSeconds(dashingTime);
        isDashing = false;
        rb.linearVelocity *= 0f;
        rb.gravityScale = 8f;

    }
    [SerializeField]
    public bool isPogoBouncing = false;
    public void ApplyPogoForce(Vector2 force)
    {
        rb.gravityScale = gravforce;
        // 1) reset any vertical speed so every bounce starts from zero
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        // 2) apply the �gpogo�h as an instantaneous impulse
        rb.AddForce(force, ForceMode2D.Impulse);

    }
    public void ApplyBounceBackForce(Vector2 force)
    {
        Debug.Log("Bounce Back!");
        // stop any existing horizontal momentum
        rb.linearVelocity = new Vector2(0f, 0f);
        // then impulse in the given direction
        rb.AddForce(force, ForceMode2D.Impulse);

        // restart LERP coroutine so control returns smoothly
        ;
        StartCoroutine(nameof(BounceBackRoutine));
    }

    private IEnumerator BounceBackRoutine()
    {
        float elapsed = 0f;
        // start with zero input influence
        airInterpolant = 0f;

        while (elapsed < bounceBackLerpTime)
        {
            elapsed += Time.deltaTime;
            // ramp airInterpolant from 0 → 1 over bounceBackLerpTime
            airInterpolant = Mathf.Clamp01(elapsed / bounceBackLerpTime);
            yield return null;
        }

        // ensure full control at the end
        airInterpolant = 1f;
    }

}