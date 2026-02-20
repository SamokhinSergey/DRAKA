using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// DnD-style D20 dice UI element.
/// Shows a random value (1–20) every 5 seconds with a rolling animation.
/// </summary>
public class DiceRoller : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI diceValueText;
    public Image diceImage;
    public Image glowImage;

    [Header("Settings")]
    public float rollInterval = 5f;
    public int diceSides = 20;
    public float rollAnimDuration = 0.6f;

    [Header("Colors")]
    public Color critColor   = new Color(1f, 0.85f, 0.1f, 1f);   // gold  – nat 20
    public Color fumbleColor = new Color(0.9f, 0.2f, 0.2f, 1f);  // red   – nat 1
    public Color normalColor = new Color(0.95f, 0.95f, 1f, 1f);  // white – everything else

    private int   currentValue    = 1;
    private bool  isRolling       = false;
    private float timer           = 0f;

    // ── Cached original scale so we can pulse from it ──
    private Vector3 originalScale;

    private void Start()
    {
        originalScale = transform.localScale;

        // First roll immediately
        StartCoroutine(RollAnimation());
    }

    private void Update()
    {
        if (isRolling) return;

        timer += Time.deltaTime;
        if (timer >= rollInterval)
        {
            timer = 0f;
            StartCoroutine(RollAnimation());
        }
    }

    // ── Rolling coroutine ──────────────────────────────────────────────────
    private IEnumerator RollAnimation()
    {
        isRolling = true;

        float elapsed = 0f;
        int   flickerCount = Mathf.RoundToInt(rollAnimDuration / 0.07f);

        // Rapid flicker – show random numbers
        for (int i = 0; i < flickerCount; i++)
        {
            int flicker = Random.Range(1, diceSides + 1);
            SetDiceDisplay(flicker, normalColor);

            float t = (float)i / flickerCount;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            transform.localScale = originalScale * scale;

            yield return new WaitForSeconds(rollAnimDuration / flickerCount);
        }

        // Final value
        currentValue = Random.Range(1, diceSides + 1);

        Color resultColor = normalColor;
        if      (currentValue == diceSides) resultColor = critColor;
        else if (currentValue == 1)         resultColor = fumbleColor;

        SetDiceDisplay(currentValue, resultColor);
        transform.localScale = originalScale;

        // Pulse on landing
        StartCoroutine(PulseScale(1.25f, 0.18f));

        // Glow flash
        if (glowImage != null)
            StartCoroutine(FlashGlow(resultColor));

        isRolling = false;
    }

    private void SetDiceDisplay(int value, Color color)
    {
        if (diceValueText != null)
        {
            diceValueText.text  = value.ToString();
            diceValueText.color = color;
        }

        if (diceImage != null)
            diceImage.color = color;
    }

    private IEnumerator PulseScale(float targetScale, float duration)
    {
        float half = duration / 2f;

        // Scale up
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, targetScale, t / half);
            transform.localScale = originalScale * s;
            yield return null;
        }

        // Scale back
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(targetScale, 1f, t / half);
            transform.localScale = originalScale * s;
            yield return null;
        }

        transform.localScale = originalScale;
    }

    private IEnumerator FlashGlow(Color glowColor)
    {
        if (glowImage == null) yield break;

        glowColor.a = 0.8f;
        glowImage.color = glowColor;
        glowImage.enabled = true;

        float duration = 0.5f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Color c = glowImage.color;
            c.a = Mathf.Lerp(0.8f, 0f, t / duration);
            glowImage.color = c;
            yield return null;
        }

        glowImage.enabled = false;
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void ForceRoll()
    {
        if (!isRolling)
        {
            timer = 0f;
            StartCoroutine(RollAnimation());
        }
    }

    public int GetCurrentValue() => currentValue;
}
