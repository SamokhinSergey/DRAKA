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

    [Tooltip("Optional TMP text showing blood pressure (e.g. below the heart icon).")]
    public TMPro.TextMeshProUGUI fatiguePercentText;

    [Header("Heartbeat Audio")]
    [Tooltip("AudioSource for heartbeat sound (should be set to Loop).")]
    public AudioSource heartbeatAudioSource;

    [Tooltip("Heartbeat audio clip (plays in loop when fatigue >= 30%).")]
    public AudioClip heartbeatClip;

    [Tooltip("Fatigue threshold (0-1) at which heartbeat starts playing.")]
    [Range(0f, 1f)]
    public float heartbeatStartThreshold = 0.3f;

    [Tooltip("Fatigue threshold (0-1) at which heartbeat stops (too exhausted).")]
    [Range(0f, 1f)]
    public float heartbeatStopThreshold = 0.99f;


    // pressure interval is set by PressureUpdateInterval constant

    [Header("Fatigue Settings")]
    [Tooltip("Maximum fatigue value corresponding to 100%.")]
    public float maxFatigue = 100f;

    [Tooltip("How many percent to add on a normal attack.")]
    public float attackIncreasePercent = 3f;

    [Tooltip("How many percent to add on a special attack.")]
    public float specialAttackIncreasePercent = 5f;

    [Tooltip("How many percent per second to DECREASE while walking.")]
    public float walkDecreasePercentPerSecond = 0.5f;

    [Tooltip("How many percent per second to decrease while idle (no movement).")]
    public float idleDecreasePercentPerSecond = 1f;

    [Tooltip("How many percent per second to decrease while blocking.")]
    public float blockDecreasePercentPerSecond = 2f;

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
    private float _pressureUpdateTimer  = 0f;
    private const float PressureUpdateInterval = 1f;

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
        AddPercent(-walkDecreasePercentPerSecond * deltaTime);
    }

    /// <summary>
    /// Call every frame while the character is completely idle.
    /// </summary>
    public void OnIdle(float deltaTime)
    {
        AddPercent(-idleDecreasePercentPerSecond * deltaTime);
    }

/// <summary>
    /// Call every frame while the character is blocking.
    /// </summary>
    public void OnBlock(float deltaTime)
    {
        AddPercent(-blockDecreasePercentPerSecond * deltaTime);
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
        if (maxFatigue <= 0f) return;

        float percent01 = Mathf.Clamp01(currentFatigue / maxFatigue);

        // Colour & beat speed
        Color targetColor = normalColor;
        float beatSpeed   = normalBeatSpeed;

        if (percent01 >= crazyThreshold)
        {
            targetColor = crazyColor;
            beatSpeed   = crazyBeatSpeed;
        }
        else if (percent01 >= strongThreshold)
        {
            targetColor = strongColor;
            beatSpeed   = strongBeatSpeed;
        }
        else if (percent01 >= normalThreshold)
        {
            targetColor = strongColor;
            beatSpeed   = normalBeatSpeed;
        }

        if (fatigueBarFill != null)
            fatigueBarFill.color = targetColor;

        if (heartTransform != null && beatSpeed > 0f)
        {
            _beatTime += Time.deltaTime * beatSpeed * Mathf.PI * 2f;
            float scaleFactor = 1f + Mathf.Sin(_beatTime) * beatScaleAmplitude;
            heartTransform.localScale = _originalHeartScale * scaleFactor;
        }

        // Blood pressure: update once per second
        if (fatiguePercentText != null)
        {
            fatiguePercentText.color = targetColor;

            _pressureUpdateTimer += Time.deltaTime;
            if (_pressureUpdateTimer >= PressureUpdateInterval)
            {
                _pressureUpdateTimer = 0f;

                int systolic  = Mathf.RoundToInt(Mathf.Lerp(120f, 230f, percent01));
                int diastolic = Mathf.RoundToInt(Mathf.Lerp( 80f, 130f, percent01));
                fatiguePercentText.text = systolic + "/" + diastolic;
            }
        }

        // Heartbeat audio: update every frame
        UpdateHeartbeatAudio(percent01);
    }

private void AddPercent(float percentDelta)
    {
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


private void UpdateHeartbeatAudio(float percent01)
    {
        if (heartbeatAudioSource == null || heartbeatClip == null)
            return;

        // Sound plays when fatigue >= 30% and < 99%
        bool shouldPlay = (percent01 >= heartbeatStartThreshold && percent01 < heartbeatStopThreshold);

        if (shouldPlay)
        {
            // Start playing once (if not already playing)
            if (!heartbeatAudioSource.isPlaying)
                heartbeatAudioSource.Play();

            // Volume scaling: 30% -> 100%, 50% -> 150%, 70% -> 200%, 90% -> 300%
            float volume;
            if (percent01 < 0.5f)
                volume = Mathf.Lerp(1.0f, 1.5f, (percent01 - 0.3f) / 0.2f);  // 30-50%
            else if (percent01 < 0.7f)
                volume = Mathf.Lerp(1.5f, 2.0f, (percent01 - 0.5f) / 0.2f);  // 50-70%
            else if (percent01 < 0.9f)
                volume = Mathf.Lerp(2.0f, 3.0f, (percent01 - 0.7f) / 0.2f);  // 70-90%
            else
                volume = 3.0f;  // 90-99%

            heartbeatAudioSource.volume = volume;

            // Pitch scaling: 1.0x at 0% fatigue -> 3.0x at 99% fatigue (linear)
            float pitch = Mathf.Lerp(1.0f, 3.0f, percent01 / 0.99f);
            heartbeatAudioSource.pitch = pitch;
        }
        else
        {
            // Stop only when outside the active range
            if (heartbeatAudioSource.isPlaying)
                heartbeatAudioSource.Stop();
        }
    }
}

