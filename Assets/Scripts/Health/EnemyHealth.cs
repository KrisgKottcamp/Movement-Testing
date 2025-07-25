using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(TrailRenderer))]
public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private bool damageable = true;
    [SerializeField] private float invulnerabilitiyTime = .2f;
    public bool giveUpwardForce = true;
    private bool hit;
    
    [Header("Health Gauge")]
    [SerializeField] private int maxHealthAmount = 100;
    public int currentHealth;
    [SerializeField] private float basicKnockBackPower;

    [Header("White Flash Settings")]
    public Material silhouetteMat;
    private Material originalMat;
    private SpriteRenderer sr;
    [SerializeField] private int whiteLengthFrames;

    [Header("Bruise Gauge")]
    [SerializeField] private float maxBruise = 100f;
    [SerializeField] private float currentBruise = 0f;

    [Header("Bruise Gauge Cooloff")]
    [SerializeField] private float bruiseCoolOffDelay = 2f;
    [SerializeField] private float bruiseCoolOffRate = 1f;
    private float timeSinceLastHit = 0f;

    [Header("Flyback Settings")]
    [SerializeField] private float flybackSpeed = 10f;
    [SerializeField] private int ricochetDamage = 10;
    [SerializeField] private int ricochetBruise = 20;
    [SerializeField] private bool gaugeIsBroken = false;
    private Vector2 lastHitDirection;

    private Rigidbody2D rb;
    [SerializeField] private float EnemyMass;
    private TrailRenderer enemyTrail;

    [Header("Bounce FX")]
    [Tooltip("Baseline physics material before break")]
    public PhysicsMaterial2D defaultMaterial;
    [Tooltip("High‑bounce material applied on break")]
    public PhysicsMaterial2D highBounceMaterial;
    private Collider2D col;

    [Header("Flyback Curve")]
    [Tooltip("Duration of each flyback curve segment")]
    public float flybackDuration = 0.5f;
    [Tooltip("Speed multiplier curve over time (0‑1)")]
    public AnimationCurve flybackCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.2f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Ricochet OverlapBox Settings")]
    [SerializeField] private Vector2 overlapBoxSize = new Vector2(1f, 1f);
    [SerializeField] private float overlapBoxAngle = 0f;
    [SerializeField] private LayerMask bounceLayers;

    [Header("Enemy Hit Settings")]
    [SerializeField] private LayerMask enemyLayers;

    private Vector2 currentFlybackDir;
    private Coroutine flybackCoroutine;

    private MeleeWeapon meleeWeapon;

    void Start()
    {
        currentHealth = maxHealthAmount;
        currentBruise = 0f;
        rb = GetComponent<Rigidbody2D>();
        rb.mass = EnemyMass;
        enemyTrail = GetComponent<TrailRenderer>();
        enemyTrail.enabled = false;
        col = GetComponent<Collider2D>();
        col.sharedMaterial = defaultMaterial;
        sr = GetComponent<SpriteRenderer>();
        originalMat = sr.material;
    }

    void Update()
    {
        // Bruise cooldown decay
        if (currentBruise > 0f)
        {
            if (timeSinceLastHit < bruiseCoolOffDelay)
                timeSinceLastHit += Time.deltaTime;
            else
                currentBruise = Mathf.Max(0f, currentBruise - bruiseCoolOffRate * Time.deltaTime);
        }

        // Reset mass after vertical motion stops
        if (Mathf.Approximately(rb.linearVelocity.y, 0f))
            rb.mass = EnemyMass;
        
    }

    private IEnumerator TurnOffHit()
    {
        yield return new WaitForSeconds(invulnerabilitiyTime);
        hit = false;
    }

    public void Damage(int hpDamage, int bruiseDamage, Vector2 hitDir)
    {
        if (!damageable || hit || currentHealth <= 0) return;
        
        hit = true;
        currentHealth -= hpDamage;
        currentBruise += bruiseDamage;
        lastHitDirection = hitDir.normalized;
        timeSinceLastHit = 0f;

        if (currentBruise >= maxBruise && !gaugeIsBroken)
            BruiseBreak();
        else if (!gaugeIsBroken)
        {
            rb.linearVelocity = Vector2.zero;
            var kb = GetComponent<KnockbackController>();
            if (kb != null)
                kb.TriggerKnockback(lastHitDirection);
        }

        if (currentHealth <= 0)
            Die();
        else
        
        StartCoroutine(FlashWhite(whiteLengthFrames));
        StartCoroutine(TurnOffHit());
    }

    private void BruiseBreak()
    {
        gaugeIsBroken = true;
        rb.mass = 1f;
        StopAllCoroutines();
        enemyTrail.enabled = true;
        rb.angularDamping = 0f;
        rb.linearDamping = 0f;
        col.sharedMaterial = highBounceMaterial;
        flybackCoroutine = StartCoroutine(DoFlyback(lastHitDirection));
    }

    private IEnumerator DoFlyback(Vector2 direction)
    {
        // Switch layer if needed
        gameObject.layer = LayerMask.NameToLayer("Broken Enemy");
        currentFlybackDir = direction.normalized;

        float timer = 0f;

        // Continue bouncing until death
        while (gaugeIsBroken && currentHealth > 0)
        {
            // Apply curve-based velocity
            float t = timer / flybackDuration;
            float speedMul = flybackCurve.Evaluate(t);
            rb.linearVelocity = currentFlybackDir * flybackSpeed * speedMul;

            // Check for bounce surfaces
            var surfaceHits = Physics2D.OverlapBoxAll(
                transform.position,
                overlapBoxSize,
                overlapBoxAngle,
                bounceLayers
            );

            foreach (var surf in surfaceHits)
            {
                Vector2 closest = surf.ClosestPoint(transform.position);
                Vector2 normal = ((Vector2)transform.position - closest).normalized;

                if (Vector2.Dot(currentFlybackDir, normal) < 0f)
                {
                    // Self-damage on bounce
                    currentHealth = Mathf.Max(0, currentHealth - ricochetDamage);
                    currentBruise += ricochetBruise;
                    StartCoroutine(FlashWhite(whiteLengthFrames));
                    if (currentHealth <= 0) { Die(); yield break; }

                    // Damage other enemies in the box
                    var others = Physics2D.OverlapBoxAll(
                        transform.position,
                        overlapBoxSize,
                        overlapBoxAngle,
                        enemyLayers
                    );
                    foreach (var colHit in others)
                    {
                        if (colHit.gameObject == gameObject) continue;
                        if (colHit.TryGetComponent<EnemyHealth>(out var eHealth))
                            eHealth.Damage(ricochetDamage, ricochetBruise, currentFlybackDir);
                    }

                    // Reflect direction and reset curve timer
                    currentFlybackDir = Vector2.Reflect(currentFlybackDir, normal);
                    timer = 0f;
                    break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Restore defaults
        col.sharedMaterial = defaultMaterial;
        enemyTrail.enabled = false;
        gaugeIsBroken = false;
    }

    public IEnumerator FlashWhite(int frameCount)
    {
        // swap to the all‑white silhouette material
        sr.material = silhouetteMat;

        // hold it for the specified number of frames
        for (int i = 0; i < frameCount; i++)
            yield return null;

        // restore the original material
        sr.material = originalMat;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position,
            Quaternion.Euler(0f, 0f, overlapBoxAngle),
            Vector3.one
        );
        Gizmos.DrawWireCube(Vector3.zero, overlapBoxSize);
    }

    private void Die()
    {
        currentHealth = 0;
        gameObject.SetActive(false);
    }
}
