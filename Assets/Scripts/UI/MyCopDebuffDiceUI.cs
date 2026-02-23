using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MyCopDebuffDiceUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI diceValueText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Image diceImage;

    [Header("Timing")]
    [SerializeField] private float rollIntervalSeconds = 10f;
    [SerializeField] private float rollAnimationSeconds = 1.5f;
    [SerializeField] private int diceSides = 20;

    [Header("Throw Animation")]
    [SerializeField] private float spinSpeedStart = 2600f;
    [SerializeField] private float spinSpeedEnd = 140f;
    [SerializeField] private float shakeStrength = 28f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color critColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color fumbleColor = new Color(0.9f, 0.25f, 0.25f, 1f);

    private float countdown;
    private bool isRolling;
    private bool queuedRoll;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Vector2 baseAnchoredPosition;
    private int lastShownSeconds = -1;
    private RectTransform rectTransform;

    private void Awake()
    {
        if (diceValueText == null)
        {
            Transform value = transform.Find("DiceValueText");
            if (value != null) diceValueText = value.GetComponent<TextMeshProUGUI>();
        }

        if (timerText == null)
        {
            Transform timer = transform.parent != null ? transform.parent.Find("MyCopDebuffTimerText") : null;
            if (timer == null)
                timer = transform.Find("DiceTimerText");
            if (timer != null) timerText = timer.GetComponent<TextMeshProUGUI>();
        }

        if (diceImage == null)
            diceImage = GetComponent<Image>();

        if (timerText == null)
            Debug.LogWarning("[MyCopDebuffDiceUI] Timer text is not assigned. Assign MyCopDebuffTimerText in scene.");
    }

    private void Start()
    {
        rectTransform = transform as RectTransform;
        countdown = rollIntervalSeconds;
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;
        if (rectTransform != null)
            baseAnchoredPosition = rectTransform.anchoredPosition;

        if (diceValueText != null)
            diceValueText.text = string.Empty;
        if (diceImage != null)
            diceImage.color = normalColor;

        lastShownSeconds = -1;
        UpdateTimerText(force: true);
    }

    private void Update()
    {
        countdown -= Time.deltaTime;
        if (countdown <= 0f)
        {
            countdown += rollIntervalSeconds;
            queuedRoll = true;
        }

        UpdateTimerText();

        if (queuedRoll && !isRolling)
        {
            queuedRoll = false;
            StartCoroutine(RollRoutine());
        }
    }

    private IEnumerator RollRoutine()
    {
        isRolling = true;
        float elapsed = 0f;
        while (elapsed < rollAnimationSeconds)
        {
            int flickerValue = Random.Range(1, diceSides + 1);
            SetDiceValue(flickerValue, normalColor);

            float t = elapsed / rollAnimationSeconds;
            float damp = 1f - t;
            float spinSpeed = Mathf.Lerp(spinSpeedStart, spinSpeedEnd, t);
            transform.localRotation *= Quaternion.Euler(spinSpeed * Time.deltaTime, 0f, 0f);

            float shake = shakeStrength * damp;
            if (rectTransform != null)
            {
                float sx = Random.Range(-shake, shake);
                float sy = Random.Range(-shake, shake) * 0.6f;
                rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(sx, sy);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        int finalValue = Random.Range(1, diceSides + 1);
        Color resultColor = normalColor;
        if (finalValue == diceSides) resultColor = critColor;
        else if (finalValue == 1) resultColor = fumbleColor;

        SetDiceValue(finalValue, resultColor);
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;
        if (rectTransform != null)
            rectTransform.anchoredPosition = baseAnchoredPosition;

        isRolling = false;
    }

    private void SetDiceValue(int value, Color color)
    {
        if (diceValueText != null)
        {
            diceValueText.text = value.ToString();
            diceValueText.color = color;
        }

        if (diceImage != null)
            diceImage.color = color;
    }

    private void UpdateTimerText(bool force = false)
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, countdown));
        if (!force && totalSeconds == lastShownSeconds)
            return;

        lastShownSeconds = totalSeconds;
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        timerText.text = $"{minutes}:{secs:00}";
    }

}
