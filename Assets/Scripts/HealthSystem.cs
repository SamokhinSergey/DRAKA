// 15.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

// 15.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviour
{
    [Header("Health Settings")]
    public PlayerController playerController; // Reference to the PlayerController script
    public Image healthBarFill; // Reference to the health bar fill image

    private void Start()
    {
        // Initialize health bar fill to full
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = 1f;
        }
    }

    private void Update()
    {
        if (playerController != null && healthBarFill != null)
        {
            // Update the health bar fill amount based on the player's current health
            healthBarFill.fillAmount = Mathf.Clamp01(playerController.health / 100f);
        }
    }
}