using System.Collections;
using UnityEngine;

public class MeleeWeapon : MonoBehaviour
{
    //How much damage the attack does.
    [SerializeField] private int damageAmount = 20;

    //References the main player movement script.
    private CharacterControl characterControl;

    //References the RigitBody2D on the player.
    private Rigidbody2D rb;

    //References the MeleeAttackManager script on the player.
    private MeleeAttackManager meleeAttackManager;

    //References the direction the player needs to move in after a melee attack collides.
    private Vector2 direction;

    //Bool that determines if a player should move after a melee attack collides.
    private bool hasCollided;

    //Determines if the melee strike is downwards to apply upward force against gravity.
    private bool downwardStrike;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //references components
        characterControl = GetComponent<CharacterControl>();
        rb = GetComponent<Rigidbody2D>();
        meleeAttackManager = GetComponent<MeleeAttackManager>();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<EnemyHealth>()) //Checks to see if the GameObject the MeleeWeapon is colliding with has an EnemyHealth script attached to it.
        {
            HandleCollision(collision.GetComponent<EnemyHealth>());
        } 
    }

    private void HandleCollision(EnemyHealth objHealth)
    {

        //THIS STUFF CONTROLLS WHAT HAPPENS WHEN THE PLAYER ATTACKS FROM EACH DIRRECTION.
        if (objHealth.giveUpwardForce && Input.GetAxis("Vertical") < 0 && !characterControl.isGrounded) {
            direction = Vector2.up; //Sets the direction to up.
            downwardStrike = true; //Sets downwardStrike to true;
            hasCollided = true; //The attack has now collided.
        }
        if (Input.GetAxis("Vertical") > 0 && !characterControl.isGrounded) //If the player is below an enemy and attacks them, this will push them back down.
                                                                           //(This will need to change in the future to allow for 3 hit combo.)
        {
            direction = Vector2.down; //Sets the direction to down.
            hasCollided = true;//The attack has now collided.
        }
        //Checks to see if melee attack is just a standard melee attack.
        if ((Input.GetAxis("vertical") <= 0 || Input.GetAxis("Vertical") == 0)) {
            if (characterControl.facingRight)
            {
                direction = Vector2.right; //When the player attacks right, sets the direction to right
            }
            else {
                direction = Vector2.left; //When the player attacks left, sets the direction to left
            }
            hasCollided = true;
        }

        //Deals (damageAmount) amount of damage.
        objHealth.Damage(damageAmount);
        StartCoroutine(NoLongerColliding());
    }

    //Coroutine that turns off all the bools related to melee attack collision and direction.
    private IEnumerator NoLongerColliding()
    {
        //Waits for (attackMovementTime) amount of time, set up in the melleAttackManager script, to pass.
        yield return new WaitForSeconds(meleeAttackManager.attackMovementTime);
        hasCollided = false; //Resets hasCollided.
        downwardStrike = false; //Resets downwardStrike;
    }


    //Method that actually applies the movement from a melee attack in the appropriate direction. 
    private void HandleMovement()
    {
        //Will only run if the attack has collided.
        if (hasCollided)
        {
            //if the attack was in a downward direction
            if (downwardStrike)
            {
                rb.AddForce(direction * meleeAttackManager.upwardsForce); //Propels the player upwards by the amount of upwardsForce in the meleeAttackManager
            }
            else
            {
                rb.AddForce(direction * meleeAttackManager.defaultForce); //Propels the player upwards by the amount of defaultForce in the meleeAttackManager
            }
        }
    }

}
