using System.Collections;
using UnityEngine;
using Unity.Cinemachine; // for CinemachineImpulseSource

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(TrailRenderer))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Toggles")]
    [SerializeField] private bool damageable = true;
    [SerializeField] private float invulnerabilitiyTime = 0.20f;
    public bool giveUpwardForce = true;
    private bool hit;

    [Header("Health Gauge")]
    [SerializeField] private int maxHealthAmount = 100;
    public int currentHealth;
    [SerializeField] private float basicKnockBackPower = 0f;

    [Header("White Flash Settings")]
    public Material silhouetteMat;
    private Material originalMat;
    private SpriteRenderer sr;
    [SerializeField] private int whiteLengthFrames = 2;

    [Header("Bruise Gauge")]
    [SerializeField] private float maxBruise = 100f;
    [SerializeField] private float currentBruise = 0f;
    public bool JustBrokeBruise { get; private set; } = false;

    [Header("Bruise Gauge Cooloff")]
    [SerializeField] private float bruiseCoolOffDelay = 1.2f;
    [SerializeField] private float bruiseCoolOffRate = 20f;
    private float timeSinceLastHit = 0f;

    [Header("Flyback Settings")]
    [SerializeField] private float flybackSpeed = 18f;
    [SerializeField] private int ricochetDamage = 10;
    [SerializeField] private int ricochetBruise = 30;
    [SerializeField] private bool gaugeIsBroken = false;
    private Vector2 lastHitDirection;

    private Rigidbody2D rb;
    [SerializeField] private float EnemyMass = 1f;

    [Header("Bounce FX")]
    public PhysicsMaterial2D defaultMaterial;
    public PhysicsMaterial2D highBounceMaterial;
    private Collider2D col;

    [Header("Flyback Curve")]
    [SerializeField] private float flybackDuration = 0.45f;
    [SerializeField]
    private AnimationCurve flybackCurve =
        new AnimationCurve(new Keyframe(0, 1.00f), new Keyframe(0.35f, 1.15f), new Keyframe(1f, 0.85f));

    [Header("Ricochet OverlapBox Settings")]
    [SerializeField] private Vector2 overlapBoxSize = new Vector2(1.0f, 1.0f);
    [SerializeField] private float overlapBoxAngle = 0f;
    [SerializeField] private LayerMask bounceLayers;

    [Header("Enemy Hit Settings")]
    [SerializeField] private LayerMask enemyLayers;

    private Vector2 currentFlybackDir;
    private Coroutine flybackCoroutine;

    [Header("Effects")]
    private TrailRenderer enemyTrail;
    private CinemachineImpulseSource impulseSource;
    [SerializeField] private GameObject bruiseBreakVFX;
    [SerializeField] private GameObject enemyDeathVFX;

    // Camera director (nudge version)
    private CameraDirectorCM3 cam;

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
        originalMat = sr != null ? sr.material : null;

        impulseSource = GetComponent<CinemachineImpulseSource>();

        cam = FindObjectOfType<CameraDirectorCM3>();
    }

    void Update()
    {
        if (currentBruise > 0f)
        {
            timeSinceLastHit += Time.deltaTime;
            if (!gaugeIsBroken && timeSinceLastHit >= bruiseCoolOffDelay)
                currentBruise = Mathf.Max(0f, currentBruise - bruiseCoolOffRate * Time.deltaTime);
        }
    }

    public void Damage(int hpDamage, int bruiseDamage, Vector2 hitDir)
    {
        if (!damageable || hit || currentHealth <= 0) return;

        hit = true;
        currentHealth -= hpDamage;
        currentBruise += bruiseDamage;

        lastHitDirection = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : Vector2.right;
        timeSinceLastHit = 0f;
        JustBrokeBruise = false;

        // Small camera tap on EVERY hit (faces where the enemy will fly)
        cam?.NotifyHit(lastHitDirection, 1f);

        // Small impulse for normal hits
        if (impulseSource) CameraShakeManager.instance.CameraShake(impulseSource);

        if (currentBruise >= maxBruise && !gaugeIsBroken)
        {
            JustBrokeBruise = true;
            BruiseBreak();
        }
        else
        {
            var kb = GetComponent<KnockbackController>();
            if (kb != null)
                kb.TriggerKnockback(lastHitDirection);
        }

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(FlashWhite(whiteLengthFrames));
        StartCoroutine(TurnOffHit());
    }

    private void BruiseBreak()
    {
        gaugeIsBroken = true;
        BruiseBreakFX();

        rb.mass = 1f;                   // lighter for pinball
        rb.angularDamping = 0f;
        rb.linearDamping = 0f;

        StopAllCoroutines();
        if (impulseSource) CameraShakeManager.instance.CameraShake(impulseSource);

        col.sharedMaterial = highBounceMaterial;
        enemyTrail.enabled = true;

        // Big camera push once on break
        float initialMul = flybackCurve != null ? flybackCurve.Evaluate(0f) : 1f;
        cam?.NotifyBruiseBreak(transform, lastHitDirection.normalized, flybackSpeed * initialMul);

        if (flybackCoroutine != null) StopCoroutine(flybackCoroutine);
        flybackCoroutine = StartCoroutine(DoFlyback(lastHitDirection));
    }

    private void BruiseBreakFX()
    {
        if (!bruiseBreakVFX) return;
        var vfxInstance = Instantiate(bruiseBreakVFX, transform.position, Quaternion.identity);
        var ps = vfxInstance.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main; main.loop = false;
            ps.Clear(); ps.Play();
            float lifetime = main.duration + main.startLifetime.constantMax;
            Destroy(vfxInstance, lifetime);
        }
    }

    private IEnumerator DoFlyback(Vector2 direction)
    {
        gameObject.layer = LayerMask.NameToLayer("Broken Enemy");

        currentFlybackDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float timer = 0f;

        while (gaugeIsBroken && currentHealth > 0)
        {
            float t = flybackDuration > 0f ? (timer / flybackDuration) : 1f;
            float speedMul = flybackCurve != null ? flybackCurve.Evaluate(t) : 1f;

            rb.linearVelocity = currentFlybackDir * flybackSpeed * speedMul;

            var surfaceHits = Physics2D.OverlapBoxAll(
                transform.position, overlapBoxSize, overlapBoxAngle, bounceLayers);

            foreach (var surf in surfaceHits)
            {
                Vector2 closest = surf.ClosestPoint(transform.position);
                Vector2 normal = ((Vector2)transform.position - closest).normalized;

                if (Vector2.Dot(currentFlybackDir, normal) < 0f)
                {
                    currentHealth = Mathf.Max(0, currentHealth - ricochetDamage);
                    currentBruise = Mathf.Min(maxBruise, currentBruise + ricochetBruise);
                    StartCoroutine(FlashWhite(whiteLengthFrames));

                    if (currentHealth <= 0) { Die(); yield break; }

                    var others = Physics2D.OverlapBoxAll(
                        transform.position, overlapBoxSize, overlapBoxAngle, enemyLayers);

                    foreach (var colHit in others)
                    {
                        if (colHit.gameObject == gameObject) continue;
                        if (colHit.TryGetComponent<EnemyHealth>(out var eHealth))
                            eHealth.Damage(ricochetDamage, ricochetBruise, currentFlybackDir);
                    }

                    currentFlybackDir = Vector2.Reflect(currentFlybackDir, normal);
                    timer = 0f;
                    break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        col.sharedMaterial = defaultMaterial;
        enemyTrail.enabled = false;
        gaugeIsBroken = false;
    }

    public IEnumerator FlashWhite(int frameCount)
    {
        if (sr == null || silhouetteMat == null || originalMat == null) yield break;
        sr.material = silhouetteMat;
        for (int i = 0; i < Mathf.Max(1, frameCount); i++) yield return null;
        sr.material = originalMat;
    }

    private IEnumerator TurnOffHit()
    {
        yield return new WaitForSeconds(invulnerabilitiyTime);
        hit = false;
    }

    private void Die()
    {
        currentHealth = 0;

        if (enemyDeathVFX)
        {
            var vfxInstance = Instantiate(enemyDeathVFX, transform.position, Quaternion.identity);
            var ps = vfxInstance.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main; main.loop = false;
                ps.Clear(); ps.Play();
                float lifetime = main.duration + main.startLifetime.constantMax;
                Destroy(vfxInstance, lifetime);
            }
        }

        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.92f, 0.16f, 0.35f);
        Matrix4x4 rot = Matrix4x4.TRS(transform.position, Quaternion.Euler(0, 0, overlapBoxAngle), Vector3.one);
        Gizmos.matrix = rot;
        Gizmos.DrawWireCube(Vector3.zero, overlapBoxSize);
    }
}
