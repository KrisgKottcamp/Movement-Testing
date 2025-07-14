using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MeleeWeapon : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int damageAmount = 20;
    [SerializeField] private int bruiseDamageAmount = 20;
    [SerializeField] private float attackCooldown = 0.2f;         // Time before next pogo

    [Header("Bounce Settings")]
    [SerializeField] private float pogoForce = 12f;          // Upward bounce strength
    [SerializeField] private float bounceBackForce = 5f;          // Backwards bounce strength
    [SerializeField] public Vector2 hitboxSize = new Vector2(1f, 0.25f);   // Width x Height
    [SerializeField] public Vector2 hitboxOffset = new Vector2(0f, -0.5f);   // Local space offset
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

        Vector2 origin = (Vector2)transform.position + hitboxOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, hitboxSize, 0f, enemyLayer);

        foreach (var col in hits)
        {
            var enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null || hasAttackedThisSwing)
                continue;

            // 1) Always apply both health and bruise damage:
            Vector2 hitDir = meleeAttackManager.raw.normalized;
            if (hitDir == Vector2.zero) // If I am not holding a stick direction when attacking, Hit direction defaults to forward.
            {
                hitDir = characterControl.facingRight ? Vector2.right : Vector2.left;
            }
            enemy.Damage(damageAmount, bruiseDamageAmount, hitDir);


            bool didPogo = enemy.giveUpwardForce && !characterControl.isGrounded && meleeAttackManager.meleeAttackDir.y <= 0f && meleeAttackManager.meleeAttackDir.x == 0f;

            // 2) Only do the pogo if this enemy is pogoable:
            if (didPogo)
            {
                Debug.Log("Did Pogo!");
                characterControl.ApplyPogoForce(Vector2.up * pogoForce);
            }
            else
            {
                // bounce the player *away* from the enemy  
                // (hitDir points TOWARD the enemy, so invert it)
                Vector2 bounceDir = -hitDir;
                characterControl.ApplyBounceBackForce(bounceDir * bounceBackForce);
            }
            hasAttackedThisSwing = true;
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

        Vector2 origin = (Vector2)transform.position + hitboxOffset;
        Gizmos.DrawWireCube(origin, hitboxSize);
    }
}
