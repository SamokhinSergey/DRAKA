// 18.02.2026 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Handles the Curse status effect applied to a player.
/// Attach to Player1 and Player2. Configure references in Inspector.
/// When ApplyCurse() is called:
///  - reduces moveSpeed and attack speed by 30%
///  - tints the player green
///  - tints the health bar green
///  - shows countdown timer in the UI
///  - plays cursed sound
/// After curseDuration seconds everything is restored.
/// While curse is active it cannot be reapplied.
/// </summary>
public class CurseEffect : MonoBehaviour
{
    [Header("Curse Settings")]
    public float curseDuration = 10f;
    [Range(0f, 1f)]
    public float slowPercent = 0.30f;   // 30% slow

    [Header("Visual - Character Tint")]
    [Tooltip("Renderers to tint green. If empty all SkinnedMesh/MeshRenderers in children are used.")]
    public Renderer[] targetRenderers;
    public Color curseColor = new Color(0.2f, 0.9f, 0.2f, 1f);  // green

    [Header("Visual - Healthbar")]
    [Tooltip("The fill Image of this player's healthbar (FillP1 / Fill).")]
    public Image healthbarFillImage;
    public Color healthbarCurseColor = new Color(0.2f, 0.85f, 0.2f, 1f);

    [Header("UI - Status Timer")]
    [Tooltip("The status_time_pX GameObject. Its first child with TextMeshProUGUI will be used.")]
    public GameObject statusTimeObject;

    [Header("Audio")]
    public AudioClip cursedSound;
    public AudioSource audioSource;

    // ── internals ──────────────────────────────────────────────────────────
    private PlayerController _pc;
    private bool _isCursed = false;

    // original values
    private Color[] _originalColors;
    private Color _originalHealthbarColor;
    private float _originalMoveSpeed;
    private float _originalAttackDelay;          // we speed-up delay == slow attack
    private bool _colorsCached = false;

    // UI
    private TextMeshProUGUI _timerText;
    private Coroutine _curseCoroutine;

    // ── public read-only ───────────────────────────────────────────────────
    public bool IsCursed => _isCursed;

    // ──────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _pc = GetComponent<PlayerController>();

        // Auto-find renderers
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        // Find AudioSource
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Find timer text
        if (statusTimeObject != null)
        {
            _timerText = statusTimeObject.GetComponentInChildren<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning($"[CurseEffect] statusTimeObject not assigned on {name}. Timer UI won't work.");
        }

        // Hide timer initially
        if (statusTimeObject != null) statusTimeObject.SetActive(false);

        // Cache original renderer colors
        CacheColors();
    }

    private void CacheColors()
    {
        if (_colorsCached) return;
        _originalColors = new Color[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null && targetRenderers[i].material != null)
                _originalColors[i] = targetRenderers[i].material.color;
            else
                _originalColors[i] = Color.white;
        }

        if (healthbarFillImage != null)
            _originalHealthbarColor = healthbarFillImage.color;

        _colorsCached = true;
    }

    // ──────────────────────────────────────────────────────────────────────
    /// <summary>Call this to apply the curse to this player.</summary>
    public void ApplyCurse()
    {
        if (_isCursed) return;   // Cannot stack
        if (_pc != null && _pc.isDead) return;

        if (_curseCoroutine != null) StopCoroutine(_curseCoroutine);
        _curseCoroutine = StartCoroutine(CurseRoutine());
    }

    private IEnumerator CurseRoutine()
    {
        _isCursed = true;
        CacheColors();  // ensure original values are known

        // 1. Apply speed slow
        if (_pc != null)
        {
            _originalMoveSpeed = _pc.moveSpeed;
            _originalAttackDelay = _pc.attack_delay;
            _pc.moveSpeed = _originalMoveSpeed * (1f - slowPercent);
            // Larger attack_delay → slower attack (we increase it by 30%)
            _pc.attack_delay = Mathf.RoundToInt(_originalAttackDelay / (1f - slowPercent));
        }

        // 2. Tint character green
        SetCharacterTint(curseColor);

        // 3. Tint healthbar green
        SetHealthbarTint(healthbarCurseColor);

        // 4. Play cursed sound
        if (audioSource != null && cursedSound != null)
            audioSource.PlayOneShot(cursedSound);

        // 5. Show timer
        if (statusTimeObject != null) statusTimeObject.SetActive(true);

        // 6. Countdown
        float remaining = curseDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (_timerText != null)
                _timerText.text = Mathf.CeilToInt(remaining).ToString();
            yield return null;
        }

        // 7. Remove curse
        RemoveCurse();
    }

    private void RemoveCurse()
    {
        _isCursed = false;

        // Restore speed
        if (_pc != null)
        {
            _pc.moveSpeed = _originalMoveSpeed;
            _pc.attack_delay = (int)_originalAttackDelay;
        }

        // Restore character color
        RestoreCharacterTint();

        // Restore healthbar color
        RestoreHealthbarTint();

        // Hide timer
        if (statusTimeObject != null) statusTimeObject.SetActive(false);
        if (_timerText != null) _timerText.text = "";
    }

    // ── Color helpers ──────────────────────────────────────────────────────
    private void SetCharacterTint(Color color)
    {
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null && targetRenderers[i].material != null)
                targetRenderers[i].material.color = color;
        }
    }

    private void RestoreCharacterTint()
    {
        if (_originalColors == null) return;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null && targetRenderers[i].material != null)
                targetRenderers[i].material.color = _originalColors[i];
        }
    }

    private void SetHealthbarTint(Color color)
    {
        if (healthbarFillImage != null)
            healthbarFillImage.color = color;
    }

    private void RestoreHealthbarTint()
    {
        if (healthbarFillImage != null)
            healthbarFillImage.color = _originalHealthbarColor;
    }

    // ── Cleanup on death ──────────────────────────────────────────────────
    // If the player dies while cursed — clean up gracefully.
    private void Update()
    {
        if (_isCursed && _pc != null && _pc.isDead)
        {
            if (_curseCoroutine != null) StopCoroutine(_curseCoroutine);
            RemoveCurse();
        }
    }
}
