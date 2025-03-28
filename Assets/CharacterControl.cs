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





    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {

        //JUMPING

        if (rb.linearVelocity.y == 0)
        {
            isGrounded = true;
        } else { isGrounded = false; }


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



    public void Walk(Vector2 direction)
    {
        rb.linearVelocity = (new Vector2(direction.x * moveSpeed, rb.linearVelocity.y));
    }



    public void Flip()
    {
        Vector3 currentScale = gameObject.transform.localScale;
        currentScale.x *= -1;
        gameObject.transform.localScale = currentScale;

        facingRight = !facingRight;
    }

}