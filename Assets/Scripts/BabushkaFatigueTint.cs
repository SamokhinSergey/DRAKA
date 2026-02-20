// 17.02.2026 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;

/// <summary>
/// Adds a blue tint to Babushka based on fatigue level.
/// The more fatigue, the more blue she becomes, while preserving original textures.
/// Designed to be attached only to Player1 (Babushka).
/// </summary>
public class BabushkaFatigueTint : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public FatigueSystem fatigueSystem;

    [Tooltip("Renderers to tint. If empty, all SkinnedMeshRenderer and MeshRenderer in children will be used.")]
    public Renderer[] targetRenderers;

    [Header("Tint Settings")]
    [Tooltip("Maximum blue tint color applied at 100% fatigue.")]
    public Color maxBlueTint = new Color(0.4f, 0.4f, 1.0f, 1.0f);

    [Tooltip("How strongly to blend towards the blue color at 100% fatigue. 0 = no tint, 1 = full maxBlueTint.")]
    [Range(0f, 1f)]
    public float maxBlend = 0.65f;

    private Color[] _originalColors;
    private bool _initialized;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (fatigueSystem == null && playerController != null)
        {
            fatigueSystem = playerController.fatigueSystem;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            _originalColors = new Color[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var r = targetRenderers[i];
                if (r != null && r.material != null)
                {
                    _originalColors[i] = r.material.color;
                }
                else
                {
                    _originalColors[i] = Color.white;
                }
            }
            _initialized = true;
        }
    }

    private void Update()
    {
        if (!_initialized || fatigueSystem == null || fatigueSystem.maxFatigue <= 0f)
        {
            return;
        }

        float t = Mathf.Clamp01(fatigueSystem.currentFatigue / fatigueSystem.maxFatigue);
        float blend = t * maxBlend;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null || r.material == null)
            {
                continue;
            }

            Color baseColor = _originalColors[i];
            // Blend towards blue tint while preserving original texture/colors.
            Color tinted = Color.Lerp(baseColor, maxBlueTint, blend);
            r.material.color = tinted;
        }
    }
}

