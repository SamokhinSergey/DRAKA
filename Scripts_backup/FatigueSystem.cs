// 11.02.2026 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks a character's fatigue ("risk of infarct") and drives a UI bar.
/// Designed to be optional: simply don't assign it for characters who don't use fatigue.
/// </summary>
public class FatigueSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PlayerController whose activity will affect fatigue.")]
    public PlayerController playerController;

    [Tooltip("UI Image used as the fill for the main fatigue bar (typically the heart).")]
    public Image fatigueBarFill;

    [Tooltip("Optional secondary bar Image (e.g. classic horizontal bar under HP).")]
    public Image secondaryBarFill;

    [Header("Fatigue Settings")]
    [Tooltip("Maximum fatigue value corresponding to 100%.")]
    public float maxFatigue = 100f;

    [Tooltip("How many percent to add on a normal attack.")]
    public float attackIncreasePercent = 3f;

    [Tooltip("How many percent to add on a special attack.")]
    public float specialAttackIncreasePercent = 5f;

    [Tooltip("How many percent per second to add while walking.")]
    public float walkIncreasePercentPerSecond = 0.1f;

    [Tooltip("How many percent per second to remove while completely idle (no movement input).")]
    public float idleDecreasePercentPerSecond = 1f;

    [Header("Infarction")]
    [Tooltip("If true, reaching 100% fatigue will instantly trigger infarction (death).")]
    public bool triggerInfarctionOnMax = true;

    private bool _infarctionTriggered = false;

    [Header("Debug")]
    [Tooltip("Current fatigue value (0..maxFatigue).")]
    [Range(0f, 100f)]
    public float currentFatigue;

    [Header("Visual Settings")]
    [Tooltip("Transform of the heart icon to scale (can be same object as Image).")]
    public RectTransform heartTransform;

    [Tooltip("Color of the heart when fatigue is low (below normalThreshold).")]
    public Color normalColor = new Color(1f, 0.4f, 0.4f); // светло‑красный/розовый

    [Tooltip("Color of the heart when fatigue is medium (between normalThreshold and strongThreshold).")]
    public Color strongColor = new Color(1f, 0.2f, 0.2f);

    [Tooltip("Color of the heart when fatigue is high (above crazyThreshold).")]
    public Color crazyColor = new Color(1f, 0f, 0f); // ярко‑красный

    [Tooltip("Threshold (0..1) below which heart is considered 'normal'.")]
    [Range(0f, 1f)]
    public float normalThreshold = 0.3f;

    [Tooltip("Threshold (0..1) above which heart is 'strong', but below crazy.")]
    [Range(0f, 1f)]
    public float strongThreshold = 0.5f;

    [Tooltip("Threshold (0..1) above which heart is 'crazy'.")]
    [Range(0f, 1f)]
    public float crazyThreshold = 0.7f;

    [Header("Beat Settings")]
    [Tooltip("Beat amplitude for heart scale.")]
    public float beatScaleAmplitude = 0.05f;

    [Tooltip("Beat speed (Hz) when heart is in normal range.")]
    public float normalBeatSpeed = 1f;

    [Tooltip("Beat speed (Hz) when heart is in strong range.")]
    public float strongBeatSpeed = 1.8f;

    [Tooltip("Beat speed (Hz) when heart is in crazy range.")]
    public float crazyBeatSpeed = 2.5f;

    private Vector3 _originalHeartScale;
    private float _beatTime;

    private void Awake()
    {
        // Initialize UI
        if (heartTransform == null && fatigueBarFill != null)
        {
            // Fallback: use the same transform as the image
            heartTransform = fatigueBarFill.rectTransform;
        }

        if (heartTransform != null)
        {
            _originalHeartScale = heartTransform.localScale;
        }

        UpdateBar();
    }

    private void Update()
    {
        UpdateVisuals();
    }

    /// <summary>
    /// Call when the character performs a normal (non‑special) attack.
    /// </summary>
    public void OnNormalAttack()
    {
        AddPercent(attackIncreasePercent);
    }

    /// <summary>
    /// Call when the character performs a special attack.
    /// </summary>
    public void OnSpecialAttack()
    {
        AddPercent(specialAttackIncreasePercent);
    }

    /// <summary>
    /// Call every frame while the character is walking.
    /// </summary>
    public void OnWalk(float deltaTime)
    {
        AddPercent(walkIncreasePercentPerSecond * deltaTime);
    }

    /// <summary>
    /// Call every frame while the character is completely idle.
    /// </summary>
    public void OnIdle(float deltaTime)
    {
        AddPercent(-idleDecreasePercentPerSecond * deltaTime);
    }

    /// <summary>
    /// Reset fatigue value to zero (e.g. at the start of a new round).
    /// </summary>
    public void ResetFatigue()
    {
        currentFatigue = 0f;
        _infarctionTriggered = false;
        UpdateBar();
    }

    private void UpdateVisuals()
    {
        if (maxFatigue <= 0f)
        {
            return;
        }

        float percent01 = Mathf.Clamp01(currentFatigue / maxFatigue);

        // Decide color & beat speed based on thresholds
        Color targetColor = normalColor;
        float beatSpeed = normalBeatSpeed;

        if (percent01 >= crazyThreshold)
        {
            targetColor = crazyColor;
            beatSpeed = crazyBeatSpeed;
        }
        else if (percent01 >= strongThreshold)
        {
            targetColor = strongColor;
            beatSpeed = strongBeatSpeed;
        }
        else if (percent01 >= normalThreshold)
        {
            targetColor = strongColor;
            beatSpeed = normalBeatSpeed;
        }

        if (fatigueBarFill != null)
        {
            fatigueBarFill.color = targetColor;
        }

        if (heartTransform != null && beatSpeed > 0f)
        {
            _beatTime += Time.deltaTime * beatSpeed * Mathf.PI * 2f;
            float scaleFactor = 1f + Mathf.Sin(_beatTime) * beatScaleAmplitude;
            heartTransform.localScale = _originalHeartScale * scaleFactor;
        }
    }

    private void AddPercent(float percentDelta)
    {
        // Convert percent to absolute fatigue units using maxFatigue as 100%.
        float delta = (percentDelta / 100f) * maxFatigue;
        currentFatigue = Mathf.Clamp(currentFatigue + delta, 0f, maxFatigue);
        UpdateBar();
        TryTriggerInfarction();
    }

    private void TryTriggerInfarction()
    {
        if (!triggerInfarctionOnMax || _infarctionTriggered)
        {
            return;
        }

        if (playerController == null)
        {
            return;
        }

        if (maxFatigue <= 0f)
        {
            return;
        }

        if (currentFatigue >= maxFatigue)
        {
            _infarctionTriggered = true;

            // Mark that this death was caused by infarction so SceneController
            // can show the special text and play Infarction.mp3.
            playerController.diedByInfarction = true;

            // Minimal infarction logic: just drop health to zero and let the
            // existing death flow (Die + SceneController) handle everything.
            playerController.health = 0f;
        }
    }

    private void UpdateBar()
    {
        if (maxFatigue <= 0f)
        {
            return;
        }

        float value01 = Mathf.Clamp01(currentFatigue / maxFatigue);

        if (fatigueBarFill != null)
        {
            fatigueBarFill.fillAmount = value01;
        }

        if (secondaryBarFill != null)
        {
            secondaryBarFill.fillAmount = value01;
        }
    }
}

