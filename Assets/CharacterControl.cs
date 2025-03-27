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


    private float coyoteTime = 0.2f;
    private float coyoteTimeCounter;
    private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter;

    public float mover;
    public float startSpeed;
    public float moveSpeed;
    public float maxSpeed = 20f;
    public float accelSpeed;
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

        //jump input
        if (Input.GetButtonDown("Jump"))
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
            if (rb.linearVelocity.y == 0)
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
            rb.linearVelocity = Vector2.up * jumpforce;
            jumpBufferCounter = 0f;
        }
        if (Input.GetButtonDown("Jump"))
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

        if (mover != 0f && moveSpeed < maxSpeed)
        {
            moveSpeed += Time.deltaTime * accelSpeed;
        }

        //if (mover == 0f)
        //{
        //    moveSpeed = startSpeed;
        //}

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