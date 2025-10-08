using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] public float maxBruise = 100f;
    [SerializeField] public float currentBruise = 0f;
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

    public bool Damageable => damageable;       // wraps [SerializeField] private bool damageable
    public bool GaugeIsBroken => gaugeIsBroken;    // wraps [SerializeField] private bool gaugeIsBroken

    // Your bruise values are floats; outline logic can work in ints
    public int MaxBruise => Mathf.RoundToInt(maxBruise);
    public int CurrentBruise => Mathf.RoundToInt(currentBruise);


    [Header("Ricochet OverlapBox Settings")]
    [SerializeField] private Vector2 overlapBoxSize = new Vector2(1.0f, 1.0f);
    [SerializeField] private float overlapBoxAngle = 0f;
    [SerializeField] private LayerMask bounceLayers;

    [Header("Enemy Hit Settings")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("DOMINO: Enemy-on-Enemy while flying")]
    [SerializeField] private bool enemyDominoEnabled = true;
    [Tooltip("Circle cast radius used to detect other enemies along the fly path each frame")]
    [SerializeField] private float enemyHitRadius = 0.6f;
    [Tooltip("Extra distance added to the cast per frame to avoid tunneling through thin colliders")]
    [SerializeField] private float enemyHitPadding = 0.05f;
    [Tooltip("Min seconds before the same struck enemy can be hit again by this flyer")]
    [SerializeField] private float enemyHitCooldown = 0.08f;

    private Vector2 currentFlybackDir;
    private Coroutine flybackCoroutine;

    [Header("Effects")]
    private TrailRenderer enemyTrail;
    private CinemachineImpulseSource impulseSource;
    [SerializeField] private GameObject bruiseBreakVFX;
    [SerializeField] private GameObject enemyDeathVFX;

    // Camera director (nudge version)
    private CameraDirectorCM3 cam;

    // track recent enemy hits so we don't spam the same target every frame
    private readonly Dictionary<int, float> recentHitUntil = new Dictionary<int, float>();

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

    // Legacy 3-arg entry — keep for full backward compatibility
    public void Damage(int hpDamage, int bruiseDamage, Vector2 hitDir)
        => Damage(hpDamage, bruiseDamage, hitDir, 1f, false);

    // 4-arg: knockback multiplier (linear knockback path)
    public void Damage(int hpDamage, int bruiseDamage, Vector2 hitDir, float knockbackMul)
        => Damage(hpDamage, bruiseDamage, hitDir, knockbackMul, false);

    // NEW 5-arg: multiplier + finisher flag (finisher uses Throw path)
    public void Damage(int hpDamage, int bruiseDamage, Vector2 hitDir, float knockbackMul, bool finisherThrow)
    {
        if (!damageable || hit || currentHealth <= 0) return;

        hit = true;
        currentHealth -= hpDamage;
        currentBruise += bruiseDamage;

        // Preserve full aim direction (don’t flatten Y — finisher can be Up/UpDiag)
        lastHitDirection = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : Vector2.right;
        timeSinceLastHit = 0f;
        JustBrokeBruise = false;

        // --- your existing per-hit feedback (unchanged) ---
        cam?.NotifyHit(lastHitDirection, 1f);            // camera tap
        if (impulseSource)                                // small impulse/shake for normal hits
            CameraShakeManager.instance.CameraShake(impulseSource);

        // Bruise-break branch (UNCHANGED): do your break VFX/FX and flyback, then return
        if (currentBruise >= maxBruise && !gaugeIsBroken)
        {
            JustBrokeBruise = true;
            BruiseBreak(); // <-- your existing method; includes DoFlyback(), VFX, materials, etc.

        }
        else
        {
            // Normal (non-break) knockback routing
            var kb = GetComponent<KnockbackController>();
            if (kb != null)
            {
                if (finisherThrow)
                {
                    // Finisher: directional bounce → free physics (your new finisher path)
                    kb.TriggerFinisherThrow(lastHitDirection);
                }
                else
                {
                    // Linear knockback (supports multiplier)
                    kb.TriggerKnockback(lastHitDirection, knockbackMul);
                }
            }
        }

        // Death / visuals / invuln — keep your originals
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

            // drive motion along currentFlybackDir
            rb.linearVelocity = currentFlybackDir * flybackSpeed * speedMul;

            // ---------- NEW: Domino hit detection (enemy-on-enemy mid-flight) ----------
            if (enemyDominoEnabled)
            {
                float castDist = rb.linearVelocity.magnitude * Time.deltaTime + enemyHitPadding;
                var hits = Physics2D.CircleCastAll(rb.position, enemyHitRadius, currentFlybackDir, castDist, enemyLayers);

                foreach (var h in hits)
                {
                    var tgt = h.collider ? h.collider.GetComponent<EnemyHealth>() : null;
                    if (tgt == null || tgt == this) continue;

                    int id = tgt.GetInstanceID();
                    if (recentHitUntil.TryGetValue(id, out float until) && until > Time.time)
                        continue; // recently hit this target; skip this frame

                    recentHitUntil[id] = Time.time + enemyHitCooldown;

                    // Treat as "player-like" attack: HP + BRUISE
                    tgt.Damage(ricochetDamage, ricochetBruise, currentFlybackDir);

                    // Optional reflect off the struck enemy using cast normal (keeps the pinball feel)
                    Vector2 n = h.normal;
                    if (n.sqrMagnitude > 0.0001f && Vector2.Dot(currentFlybackDir, n) < 0f)
                    {
                        currentFlybackDir = Vector2.Reflect(currentFlybackDir, n.normalized);
                        timer = 0f; // restart segment timing after impact
                        break;      // handle one impact this frame
                    }
                }
            }

            // ---------- Existing: wall/floor ricochet & splash damage ----------
            var surfaceHits = Physics2D.OverlapBoxAll(
                transform.position, overlapBoxSize, overlapBoxAngle, bounceLayers);

            foreach (var surf in surfaceHits)
            {
                Vector2 closest = surf.ClosestPoint(transform.position);
                Vector2 normal = ((Vector2)transform.position - closest).normalized;

                if (Vector2.Dot(currentFlybackDir, normal) < 0f)
                {
                    // self chip & extra bruise on bounce
                    currentHealth = Mathf.Max(0, currentHealth - ricochetDamage);
                    currentBruise = Mathf.Min(maxBruise, currentBruise + ricochetBruise);
                    StartCoroutine(FlashWhite(whiteLengthFrames));

                    if (currentHealth <= 0) { Die(); yield break; }

                    // splash other enemies in the overlap box (your original behavior)
                    var others = Physics2D.OverlapBoxAll(
                        transform.position, overlapBoxSize, overlapBoxAngle, enemyLayers);

                    foreach (var colHit in others)
                    {
                        if (colHit.gameObject == gameObject) continue;
                        if (colHit.TryGetComponent<EnemyHealth>(out var eHealth))
                        {
                            int id = eHealth.GetInstanceID();
                            if (recentHitUntil.TryGetValue(id, out float until) && until > Time.time)
                                continue;

                            recentHitUntil[id] = Time.time + enemyHitCooldown;
                            eHealth.Damage(ricochetDamage, ricochetBruise, currentFlybackDir);
                        }
                    }

                    // reflect off the surface and continue
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

        // Domino cast gizmo (scene-view aid)
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, enemyHitRadius);
    }
}
