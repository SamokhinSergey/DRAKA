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

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color critColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color fumbleColor = new Color(0.9f, 0.25f, 0.25f, 1f);

    private float countdown;
    private bool isRolling;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private int lastShownSeconds = -1;

    private void Awake()
    {
        if (diceValueText == null)
        {
            Transform value = transform.Find("DiceValueText");
            if (value != null) diceValueText = value.GetComponent<TextMeshProUGUI>();
        }

        if (timerText == null)
        {
            Transform timer = transform.Find("DiceTimerText");
            if (timer != null) timerText = timer.GetComponent<TextMeshProUGUI>();
        }

        if (diceImage == null)
            diceImage = GetComponent<Image>();

        if (timerText == null)
            timerText = CreateTimerText();
    }

    private void Start()
    {
        countdown = rollIntervalSeconds;
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;

        SetDiceValue(Random.Range(1, diceSides + 1), normalColor);
        lastShownSeconds = -1;
        UpdateTimerText(force: true);
    }

    private void Update()
    {
        if (isRolling)
            return;

        countdown -= Time.deltaTime;
        UpdateTimerText();

        if (countdown <= 0f)
            StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        isRolling = true;

        float flickerStep = 0.08f;
        int flickerCount = Mathf.Max(1, Mathf.RoundToInt(rollAnimationSeconds / flickerStep));

        for (int i = 0; i < flickerCount; i++)
        {
            int flickerValue = Random.Range(1, diceSides + 1);
            SetDiceValue(flickerValue, normalColor);

            float t = (float)i / flickerCount;
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
            transform.localScale = baseScale * pulse;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, i * (720f / flickerCount));

            yield return new WaitForSeconds(rollAnimationSeconds / flickerCount);
        }

        int finalValue = Random.Range(1, diceSides + 1);
        Color resultColor = normalColor;
        if (finalValue == diceSides) resultColor = critColor;
        else if (finalValue == 1) resultColor = fumbleColor;

        SetDiceValue(finalValue, resultColor);
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;

        countdown = rollIntervalSeconds;
        lastShownSeconds = -1;
        UpdateTimerText(force: true);
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

    private TextMeshProUGUI CreateTimerText()
    {
        GameObject go = new GameObject("DiceTimerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -78f);
        rt.sizeDelta = new Vector2(120f, 36f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (diceValueText != null)
        {
            tmp.font = diceValueText.font;
            tmp.fontSize = diceValueText.fontSize;
            tmp.fontStyle = diceValueText.fontStyle;
            tmp.color = diceValueText.color;
            tmp.outlineWidth = diceValueText.outlineWidth;
            tmp.alignment = diceValueText.alignment;
            tmp.fontMaterial = diceValueText.fontMaterial;
        }
        else
        {
            tmp.fontSize = 30f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        tmp.text = "0:10";
        return tmp;
    }
}
