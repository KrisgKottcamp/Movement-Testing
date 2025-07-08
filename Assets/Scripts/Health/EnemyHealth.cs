using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] 
    private bool damageable = true; //Determines if this GameObject can receive damage or not.
    [SerializeField] 
    private int maxHealthAmount = 100; //Determines max health this GameObject has.
    [SerializeField] 
    private float invulnerabilitiyTime = .2f; //Short invulnerability period after an enemy has been hit so it does not get hit twice by the same attack.

    public bool giveUpwardForce = true; //Determines if this GameObject will give upward force to the player when hit.

    private bool hit; //Bool that manages if the enemy can receive more damage.

    public int currentHealth; //The current amount of health this game object has after receiving damage.


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealthAmount; //When the scene loads, this GameObject will start with max health.
    }

    public void Damage(int amount)
    {
        if (damageable && !hit && currentHealth > 0)
        {
            hit = true; //First sets hit to true.
            currentHealth -= amount; //Reduces current health by the amount value that is set from whatever script called this method.

            //If current health goes below zero, this GameObject is considered dead.
            if (currentHealth <= 0)
            {
                currentHealth = 0; // Sets health to 0 to keep logic cleaner.
                gameObject.SetActive(false); //Removes this GameObject from scene.
                                             // (In the future I can run a Dead method to decide what happens next.)
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
}
