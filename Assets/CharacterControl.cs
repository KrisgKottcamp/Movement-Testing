using System.Collections;
using System.Runtime.ConstrainedExecution;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;


public class CharacterControl : MonoBehaviour
{
    private Rigidbody2D rb;

    public float jumpforce;
    public float fallMult;

    private float jumpTimeCounter;
    [SerializeField] private float jumpTime;
    private bool isJumping;


    private float coyoteTime = 0.13f;
    private float coyoteTimeCounter;
    private float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;
    public float missCompensation;

    public float mover;
    public float moveSpeed;
    public bool facingRight = true;

    public LayerMask layersToHit;

    public bool isWallSliding;
    public float wallSlidingSpeed = 2f;

    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask wallLayer;

    [SerializeField] private Transform groundCheck;

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


    //Dashing
    public float dashingSpeed;
    public float dashingTime;
    private Vector2 dashingDir;
    public bool isDashing;
    private bool canDash = true;


    //public Facing facing;
    //public Facing desiredFacing;

    //public enum Facing
    //{
    //    Left = 0,
    //    Right = 1,
    //}


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();


    }

    // Update is called once per frame
    void Update()
    {

        airInterpolant = Mathf.Clamp01(airInterpolant + Time.deltaTime * airInterpolantchange);

        //JUMPING




        //jump input
        if (Input.GetButtonDown("Jump") && isGrounded() == true)
        {
            isJumping = true;
            jumpTimeCounter = jumpTime;


        }
        if (Input.GetButton("Jump") && isJumping == true)
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
        if (Input.GetButtonUp("Jump"))
        {
            isJumping = false;
        }



        // coyote time
        if (isGrounded() == true)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // jump buffer
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }


        //jump activation
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = Vector2.up * jumpforce * missCompensation;
            jumpBufferCounter = 0f;
        }
        if (Input.GetButton("Jump"))
        {
            coyoteTimeCounter = 0f;
        }


        //fast falling
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMult - 1) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0 && jumpBufferCounter < 0f && coyoteTimeCounter < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * Time.deltaTime;
        }




        //MOVEMENT

        if (!isWallJumping)
        {
            mover = Input.GetAxisRaw("Horizontal");
            wallMover = Input.GetAxisRaw("Vertical");
            Vector2 direction = new Vector2(mover, wallMover);

            Walk(direction);
            wallGrab(direction);


            if (mover > 0f && !facingRight)
            {
                Flip();
            }

            if (mover < 0f && facingRight)
            {
                Flip();
            }
        }



        //Head Hit Raycast
        var headHit = Physics2D.Raycast(transform.position, Vector2.up, 0.6f, layersToHit);

        if (headHit.collider && rb.linearVelocityY > 0)
        {
            Debug.Log("Something Was Hit");
            rb.linearVelocityY = -7f;
            isJumping = false;

        }

        WallSlide();
        WallJump();
        Dashing();



    }



    //GroudCheck

    public bool isGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.2f, wallLayer);
    }





    //WallSlide
    public void Walk(Vector2 direction)
    {
        var currentLinearVelocity = rb.linearVelocity;
        var desiredVelocity = (new Vector2(direction.x * moveSpeed, rb.linearVelocity.y));

        var newVelocity = Vector2.Lerp(currentLinearVelocity, desiredVelocity, airInterpolant);
        rb.linearVelocity = newVelocity;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(wallCheck.position, 0.2f);
    }
    public bool isWalled()
    {
        return Physics2D.OverlapCircle(wallCheck.position, 0.2f, wallLayer);
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
        else { rb.gravityScale = 5; }

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
        if (isWalled() && !isGrounded() && mover != 0)
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

        if (isGrounded() == true)
        {
            airInterpolant = 1;
            wallJumpingCounter = wallJumpingTime;
        }


    }


    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    //Original Flip
    public void Flip()
    {
        Vector3 currentScale = gameObject.transform.localScale;
        currentScale.x *= -1;
        gameObject.transform.localScale = currentScale;

        facingRight = !facingRight;
    }


    private void Dashing()
    {
        var dashInput = Input.GetButtonDown("Dash");

        if (dashInput && canDash)
        {
            isDashing = true;
            canDash = false;
            dashingDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

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
            airInterpolant = 0f;
            return;
            
        }
        if (isGrounded())
        {
            canDash = true;
            rb.gravityScale = 5;
        }
    }

    private IEnumerator StopDashing()
    {
        yield return new WaitForSeconds(dashingTime);
        isDashing = false;
        rb.linearVelocity *= 0f;
        
    }

}