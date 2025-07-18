using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(TrailRenderer))]
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
    [SerializeField]
    public float basicKnockBackPower; //The 

    [Header("Bruise Gauge")]
    [SerializeField]
    private float maxBruise = 100f; //Determines how big the Bruise Gauge of an enemy is.
    [SerializeField]
    private float currentBruise = 0f; //The current level of Bruise an enemy has.

    [Header("Bruise Gauge Cooloff")]
    [SerializeField]
    private float bruiseCoolOffDelay = 2f;
    [SerializeField]
    private float bruiseCoolOffRate = 1f; //How much the bruise gauge cools off.
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
    private bool gaugeIsBroken = false; //Bool that determines if a gauge has been broken or not.
    private Vector2 lastHitDirection; //The direction the last hit on the enemy was applied.
    private Rigidbody2D rb;
    [SerializeField]
    private float EnemyMass;
    private TrailRenderer enemyTrail;

    [Header("Bounce FX")]
    [Tooltip("Baseline physics material before break")]
    public PhysicsMaterial2D defaultMaterial;
    [Tooltip("High-bounce material applied on break")]
    public PhysicsMaterial2D highBounceMaterial;
    private Collider2D col;

    [Header("Flyback Curve")]
    [Tooltip("Duration of each flyback curve segment")]
    public float flybackDuration = 0.5f; // Editable duration for curve
    [Tooltip("Speed multiplier curve over time (0-1)")]
    public AnimationCurve flybackCurve = new AnimationCurve(
        new Keyframe(0f, 1f),    // pop immediately to full speed
        new Keyframe(0.2f, 1f),  // hold full speed until 20% of duration
        new Keyframe(1f, 0f)     // taper to zero by end
    ); // Pop launch curve: full power burst then deceleration

    private Coroutine flybackCoroutine;

    void Start()
    {
        currentHealth = maxHealthAmount; //When the scene loads, this GameObject will start with max health.
        currentBruise = 0; // When the scene loads, this GameObject has a gauge of zero.
        rb = GetComponent<Rigidbody2D>();
        rb.mass = EnemyMass;
        enemyTrail = GetComponent<TrailRenderer>();
        enemyTrail.enabled = false;

        col = GetComponent<Collider2D>();
        col.sharedMaterial = defaultMaterial;
    }

    void Update()
    {
        // Handle bruise cooldown decay after delay
        if (currentBruise > 0)
        {
            if (timeSinceLastHit < bruiseCoolOffDelay)
                timeSinceLastHit += Time.deltaTime;

            if (timeSinceLastHit > bruiseCoolOffDelay)
            {
                float decay = (bruiseCoolOffRate * Time.deltaTime);
                currentBruise = Mathf.Max(0, currentBruise - decay);
            }
        }

        if (rb.linearVelocity.y == 0)
        {
            rb.mass = EnemyMass;
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
            else if (!gaugeIsBroken)
            {
                //rb.mass = 1;
                rb.linearVelocity = Vector2.zero; // zeros out any existing velocity.
                var kb = GetComponent<KnockbackController>();
                if (kb != null)
                    kb.TriggerKnockback(lastHitDirection);
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
        rb.mass = 1;
        StopAllCoroutines();
        flybackCoroutine = StartCoroutine(DoFlyback(lastHitDirection));
        enemyTrail.enabled = true;

        // STYLIZED FLYBACK:
        // • no gravity, no drag → pure straight‑line motion
        rb.linearDamping = 0;
        rb.angularDamping = 0;

        // swap to high-bounce material
        col.sharedMaterial = highBounceMaterial;
    }

    private IEnumerator DoFlyback(Vector2 direction)
    {
        gameObject.layer = LayerMask.NameToLayer("BrokenEnemy"); //changes the layer to a broken enemy layer

        // 1) curve segment
        float timer = 0f;
        while (timer < flybackDuration)
        {
            float t = timer / flybackDuration;
            float speedMul = flybackCurve.Evaluate(t);
            rb.linearVelocity = direction * flybackSpeed * speedMul;
            timer += Time.deltaTime;
            yield return null;
        }

        // 2) constant velocity until next ricochet or reset
        rb.linearVelocity = direction * flybackSpeed;

        // 3) wait for collisions to trigger ricochet or reset
        while (gaugeIsBroken && currentHealth > 0)
            yield return null;

        // restore defaults
        col.sharedMaterial = defaultMaterial;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        enemyTrail.enabled = false;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!gaugeIsBroken) return;

        // 1) Sum up all contact normals
        Vector2 avgNormal = Vector2.zero;
        foreach (var contact in col.contacts)
            avgNormal += contact.normal;

        // 2) If we somehow got a zero vector (corner tip), fall back to the first contact normal
        if (avgNormal.sqrMagnitude < 0.0001f)
            avgNormal = col.contacts[0].normal;
        else
            avgNormal.Normalize();

        // 3) Make sure lastHitDirection is valid; if not, shoot opposite the wall
        Vector2 hitDir = lastHitDirection;
        if (hitDir.sqrMagnitude < 0.0001f)
            hitDir = -avgNormal;

        // 4) Compute reflected direction off the averaged normal
        Vector2 reflDir = Vector2.Reflect(hitDir, avgNormal).normalized;
        //    Self‑bounce is opposite
        Vector2 selfBounceDir = -reflDir;

        // 5) Sanity‑check: if selfBounceDir still points into the wall, flip it again
        if (Vector2.Dot(selfBounceDir, avgNormal) < 0f)
            selfBounceDir = Vector2.Reflect(selfBounceDir, avgNormal).normalized;

        // 6) Tiny nudge out of the wall so physics doesn’t re‑stick you
        const float cornerPush = 0.1f;
        transform.position += (Vector3)(avgNormal * cornerPush);

        // 7) Debug draw
        ContactPoint2D primary = col.contacts[0];
        Debug.DrawLine(primary.point, primary.point + reflDir * flybackSpeed, Color.magenta, 1f);

        // 8) Ricochet‑damage to neighbors
        if (col.collider.GetComponentInParent<EnemyHealth>() is EnemyHealth other)
            other.Damage(ricochetDamage, ricochetBruise, reflDir);

        // 9) Self‑damage & flyback with the corrected bounce direction
        Damage(ricochetDamage, ricochetBruise, selfBounceDir);
        if (flybackCoroutine != null)
            StopCoroutine(flybackCoroutine);
        flybackCoroutine = StartCoroutine(DoFlyback(selfBounceDir));
    }

    private void Die() //What happens when this GameObject Dies.
    {
        currentHealth = 0; // Sets health to 0 to keep logic cleaner.
        gameObject.SetActive(false); //Removes this GameObject from scene.
    }
}
