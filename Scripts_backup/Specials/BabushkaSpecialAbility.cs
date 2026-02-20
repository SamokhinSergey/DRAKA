// 29.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using System.Collections;

public class BabushkaSpecialAbility : SpecialAbilityBase
{
    [Header("Special Ability Settings")]
    public GameObject salivaPrefab; // Prefab for the saliva projectile
    public Transform spawnPoint; // Spawn point for the projectile
    public float salivaSpeed = 5f; // Speed of the projectile
    public Transform target; // Target to aim the saliva projectile at
    public float DamageCoef = 3f; 

    private bool canUseSpecialAbility = true;

    public override void TriggerSpecialAbility()
    {
        if (canUseSpecialAbility)
        {
            StartCoroutine(UseSpecialAbility());
        }
    }

    private IEnumerator UseSpecialAbility()
    {
        canUseSpecialAbility = false;

        // Spawn the saliva projectile
        if (salivaPrefab != null && spawnPoint != null && target != null)
        {
            GameObject saliva = Instantiate(salivaPrefab, spawnPoint.position, Quaternion.identity);
            Rigidbody rb = saliva.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // Ensure the Rigidbody is not kinematic
                Vector3 direction = (target.position - spawnPoint.position).normalized; // Calculate direction to target
                rb.linearVelocity = direction * salivaSpeed; // Apply velocity to move the saliva
            }
            else
            {
                Debug.LogError("Rigidbody component is missing on SalivaPrefab.");
            }

            // Destroy the projectile after 5 seconds
            Destroy(saliva, 5f);
        }
        else
        {
            Debug.LogError("SalivaPrefab, SpawnPoint, or Target is not assigned.");
        }

        yield return new WaitForSeconds(1f); // Cooldown before the ability can be used again
        canUseSpecialAbility = true;
    }
}