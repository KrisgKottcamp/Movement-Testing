using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MeleeWeapon : MonoBehaviour
{
    [Header("Damage & Pogo Settings")]
    [SerializeField] private int damageAmount = 20;
    [SerializeField] private float attackCooldown = 0.2f;         // Time before next pogo
    [SerializeField] private float pogoForce = 12f;          // Upward bounce strength
    [SerializeField] private Vector2 hitboxSize = new Vector2(1f, 0.25f);   // Width x Height
    [SerializeField] private Vector2 hitboxOffset = new Vector2(0f, -0.5f);   // Local space offset
    [SerializeField] private LayerMask enemyLayer;                    // Layer mask for enemies

    private CharacterControl characterControl;
    private MeleeAttackManager meleeAttackManager;
    private bool hasAttackedThisSwing = false;
    private Coroutine cooldownCoroutine;

    private void Awake()
    {
        characterControl = GetComponentInParent<CharacterControl>();
        meleeAttackManager = GetComponentInParent<MeleeAttackManager>();
    }


    public void PerformAttack()
    {
        // Only allow pogo when airborne and input is downward
        if (characterControl.isGrounded)
            return;
        if (meleeAttackManager.meleeAttackDir.y >= 0f)
            return;

        // Calculate world-space origin of the hitbox (handles flipping)
        Vector2 origin = (Vector2)transform.TransformPoint(hitboxOffset);

        // Find all colliders overlapping the box
        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, hitboxSize, 2f, enemyLayer);
        foreach (var col in hits)
        {
            
            // Find the EnemyHealth component on this collider or its parents
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null || !enemy.giveUpwardForce || hasAttackedThisSwing)
                continue;

            // Damage the enemy and apply pogo
            enemy.Damage(damageAmount);
            characterControl.ApplyPogoForce(Vector2.up * pogoForce);
            hasAttackedThisSwing = true;

            // Start cooldown to allow next pogo
            if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = StartCoroutine(ResetAttack());
            break;
        }
    }

    private IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(attackCooldown);
        hasAttackedThisSwing = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Draws box for tuning
        Gizmos.color = Color.red;
        Vector3 origin = transform.TransformPoint(hitboxOffset);
        Gizmos.DrawWireCube(origin, hitboxSize);
    }
}
