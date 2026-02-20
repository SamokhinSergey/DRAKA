// 18.02.2026 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;

/// <summary>
/// Attach to the saliva projectile prefab.
/// When it hits a player it applies the Curse effect and then destroys itself.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SalivaProjectile : MonoBehaviour
{
    [Tooltip("The player that fired this projectile (to avoid self-hit).")]
    public GameObject owner;

private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner) return;

        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.ApplyCurse();
            }
            else
            {
                Debug.LogWarning($"[SalivaProjectile] Target {other.name} has no PlayerController component.");
            }

            Destroy(gameObject);
        }
        // Destroy on hitting non-trigger geometry
        else if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
