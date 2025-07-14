using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField]
    private bool damageable = true; //Determines if this GameObject can receive damage or not.
    [SerializeField]
    private float invulnerabilitiyTime = .2f; //Short invulnerability period after an enemy has been hit so it does not get hit twice by the same attack.

    public bool giveUpwardForce = true; //Determines if this GameObject will give upward force to the player when hit.

    private bool hit; //Bool that manages if the enemy can receive more damage.


    [Header("Health Gauge")]
    [SerializeField]
    private int maxHealthAmount = 100; //Determines max health this GameObject has.
    [SerializeField]
    public int currentHealth; //The current amount of health this game object has after receiving damage.

    [Header("Bruise Gauge")]
    [SerializeField]
    private float maxBruise = 100f; //Determines how big the Bruise Gauge of an enemy is.
    [SerializeField]
    private float currentBruise = 0f; //The current level of Bruise an enemy has.

    [Header("Bruise Gauge Cooloff")]
    [SerializeField]
    private float bruiseCoolOffDelay = 2f;
    [SerializeField]
    private float bruiseCoolOffRate = 1f; //How much the bruise cage cools off.
    [SerializeField]
    private float timeSinceLastHit = 0f;

    [Header("Flyback Settings")]
    [SerializeField]
    private float flybackSpeed = 10f; // How fast an enemy flys back when their gauge is broken.
    [SerializeField]
    private int ricochetDamage = 10; // How much health damage is applied to other enemies if hit with flyback.
    [SerializeField]
    private int ricochetBruise = 20; // How much bruise damage is applied to other enemies if hit with flyback.
    [SerializeField]
    private bool gaugeIsBroken = false; //Bool that detmines if a gauge has been broken or not.
    private Vector2 lastHitDirection; //The direction the last hit on the enemy was applied.
    private Rigidbody2D rb;
    [SerializeField]
    private float EnemyMass;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealthAmount; //When the scene loads, this GameObject will start with max health.
        currentBruise = 0; // When the scene loads, this GameObject has a gauge of zero.
        rb = GetComponent<Rigidbody2D>();
        rb.mass = EnemyMass;
    }


    public void Update()
    {
        // Handle bruise cooldown decay after delay
        if (currentBruise > 0)
        {

            if (timeSinceLastHit < bruiseCoolOffDelay + 1)
            {
                timeSinceLastHit += Time.deltaTime;
            }

            if (timeSinceLastHit > bruiseCoolOffDelay)
            {
                float decay = (bruiseCoolOffRate * Time.deltaTime);
                currentBruise = Mathf.Max(0, currentBruise - decay);

            }

        }
    }

    public void Damage(int hpDamage, int bruiseDamage, Vector2 hitDir)
    {

        if (damageable && !hit && currentHealth > 0)
        {
            hit = true; //First sets hit to true.
            currentHealth -= hpDamage; //Reduces current health by the amount value that is set from whatever script called this method.
            currentBruise += bruiseDamage;
            lastHitDirection = hitDir.normalized;
            timeSinceLastHit = 0f;


            if (currentBruise >= maxBruise && !gaugeIsBroken) //checks if the bruise gauge has passed 100%, 
            {
                BruiseBreak();       // send flying
            }

            if (currentBruise < maxBruise) {
                rb.mass = EnemyMass;
            }

            //If current health goes below zero, this GameObject is considered dead.
            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                StartCoroutine(TurnOffHit());
            }
        }
    }

    private IEnumerator TurnOffHit() //Coroutine that runs to allow the enemy to receive damage again. 
                                     //(In the future I may need to adjust this to allow for a 3 hit combo.)
    {
        yield return new WaitForSeconds(invulnerabilitiyTime); //Waits for the amount of invulnerabilitiyTime to count down.
        hit = false; //Turns off hit bool so this GameObject can receive damage again.
    }


    private void BruiseBreak()
    {
        gaugeIsBroken = true;
        rb.linearVelocity = Vector2.zero; // zeros out any existing velocity.
        rb.mass = 1;
        rb.AddForce(lastHitDirection * flybackSpeed, ForceMode2D.Impulse); //sends them flying back in the last hit direction
        StartCoroutine(FlybackRoutine());
    }

    private IEnumerator FlybackRoutine()
    {
        gameObject.layer = LayerMask.NameToLayer("BrokenEnemy"); //changes the layer to a broken enemy layer
        while (gaugeIsBroken && currentHealth > 0)
        {
            yield return null; //waits for collisions to handle ricochet
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!gaugeIsBroken) return;

        //reflect off whatever it hits
        Vector2 normal = col.contacts[0].normal;
        Vector2 reflectDir = Vector2.Reflect(lastHitDirection, normal).normalized;
        rb.linearVelocity = reflectDir * flybackSpeed;

        //if it hits another eenemy, deal ricochet damage
        var other = col.collider.GetComponentInParent<EnemyHealth>();
        if (other != null)
            other.Damage(ricochetDamage, ricochetBruise, reflectDir);
        Damage(ricochetDamage, ricochetBruise, -reflectDir); // Apply same damage/bruise to self


        //Triggers death if health reaches zero
        if (currentHealth <= 0)
            Die();
    }

    private void Die() //What happens when this GameObject Dies.
    {
        currentHealth = 0; // Sets health to 0 to keep logic cleaner.
        gameObject.SetActive(false); //Removes this GameObject from scene.
    }

}
