using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EnemyOutlineFX : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private EnemyHealth health;

    [Tooltip("Player weapon used to read BruiseDamagePerHit. If left empty, will try to find one in scene.")]
    [SerializeField] private MeleeWeapon playerWeapon;

    [Tooltip("Fallback if no weapon is assigned/found (or if you want to override).")]
    [Min(0)][SerializeField] private int manualBruisePerHit = 1;

    [Header("Flash")]
    [SerializeField] private float flashSpeed = 8f;

    [Header("(Advanced) Shader Property Names")]
    [SerializeField] private string propBruise = "_Bruise01";
    [SerializeField] private string propFlashOn = "_FlashOn";
    [SerializeField] private string propFlashSpd = "_FlashSpeed";

    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (!health) health = GetComponentInParent<EnemyHealth>();

        if (!playerWeapon)
            playerWeapon = FindObjectOfType<MeleeWeapon>(); // safe in your test scene

        mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (!health || !sr) return;

        // read bruise fill via public accessors
        int cur = health.CurrentBruise;
        int max = Mathf.Max(1, health.MaxBruise); // avoid div by 0
        float bruise01 = Mathf.Clamp01((float)cur / max);

        // what a single next hit would add
        int nextHitBruise = (playerWeapon != null)
            ? Mathf.Max(0, playerWeapon.BruiseDamagePerHit)
            : manualBruisePerHit;

        // "for sure next hit breaks"
        bool willBreakOnNextHit =
            health.currentHealth > 0 &&
            health.Damageable &&
            !health.GaugeIsBroken &&
            (cur + nextHitBruise) >= max;

        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(propBruise, bruise01);
        mpb.SetFloat(propFlashOn, willBreakOnNextHit ? 1f : 0f);
        mpb.SetFloat(propFlashSpd, flashSpeed);
        sr.SetPropertyBlock(mpb);
    }
}
