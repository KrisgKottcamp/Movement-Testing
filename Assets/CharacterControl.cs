using System.Runtime.ConstrainedExecution;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;


public class CharacterControl : MonoBehaviour
{
    private Rigidbody2D rb;

    public float jumpforce;
    public float fallMult;

    private float jumpTimeCounter;
    [SerializeField] private float jumpTime;
    private bool isJumping;
    private bool isGrounded;


    private float coyoteTime = 0.2f;
    private float coyoteTimeCounter;
    private float jumpBufferTime = 0.2f;
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


    private bool isWallJumping;
    private float wallJumpingDirection;
    private float wallJumpingTime = 0.2f;
    private float wallJumpingCounter;
    private float wallJumpingDuration = 0.4f;
    public Vector2 wallJumpingPower = new Vector2(-8f, 16f);

    public float airInterpolant = 1f;
    public float airInterpolantchange;



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

        if (rb.linearVelocity.y == 0)
        {
            isGrounded = true;
        } else { isGrounded = false;
          
        }


        //jump input
        if (Input.GetButtonDown("Jump") && isGrounded == true)
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
        if (isGrounded == true)
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
            Vector2 direction = new Vector2(mover, 0);

            Walk(direction);



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


    }



    //GroudCheck

    private void GroundCheck()
    {

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
        if (isWalled() && rb.linearVelocityY < 0)
        {
            isWallSliding = true;
            rb.linearVelocityY -= (rb.linearVelocityY - wallSlidingSpeed);
        }
        else {
            isWallSliding = false;
        }
    }


    //WallJump

    private void WallJump()
    {
        if (isWalled() && rb.linearVelocityY < 2f && mover != 0)
        {
            isWallJumping = false;
            wallJumpingDirection = -transform.localScale.x;
            wallJumpingCounter = wallJumpingTime;

            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpingCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && wallJumpingCounter > 0f)
        {
            isWallJumping = true;
            airInterpolant = 0f;
            rb.linearVelocity = new Vector2(wallJumpingDirection * wallJumpingPower.x, wallJumpingPower.y);
            wallJumpingCounter = 0f;
            Invoke(nameof(StopWallJumping), wallJumpingCounter);
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

}