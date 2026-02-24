using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private Color lowColor = new Color(0.25f, 0.9f, 0.25f, 1f);      // 1..9
    [SerializeField] private Color midColor = new Color(1f, 0.85f, 0.1f, 1f);          // 10..14
    [SerializeField] private Color highColor = new Color(0.9f, 0.25f, 0.25f, 1f);      // 15..20

    [Header("Debuff Throw")]
    [SerializeField] private PlayerController mycopPlayer;
    [SerializeField] private PlayerController attackerPlayer;
    [SerializeField] private Transform mycopHeadPoint;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Sprite[] throwSprites;
    [SerializeField] private float throwFlightDuration = 0.55f;
    [SerializeField] private float throwArcHeight = 0.45f;
    [SerializeField] private float throwSpinSpeedMin = 900f;
    [SerializeField] private float throwSpinSpeedMax = 1800f;
    [SerializeField] private float throwObjectScale = 0.35f;
    [SerializeField] private int throwSortingOrder = 20;

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
        ResolveGameplayReferences();

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
            diceImage.color = lowColor;

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
            SetDiceValue(flickerValue, lowColor);

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
        Color resultColor = GetColorForRoll(finalValue);

        SetDiceValue(finalValue, resultColor);
        TriggerDebuffEffect(finalValue);
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

    private Color GetColorForRoll(int rollValue)
    {
        if (rollValue >= 15) return highColor;
        if (rollValue >= 10) return midColor;
        return lowColor;
    }

    private void TriggerDebuffEffect(int rollValue)
    {
        int throwCount = 0;
        if (rollValue >= 20) throwCount = 3;
        else if (rollValue >= 18) throwCount = 2;
        else if (rollValue >= 15) throwCount = 1;

        if (throwCount <= 0)
            return;

        if (mycopPlayer == null || throwSprites == null || throwSprites.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
            return;

        List<Transform> selectedPoints = SelectUniqueSpawnPoints(throwCount);
        for (int i = 0; i < selectedPoints.Count; i++)
            StartCoroutine(ThrowObjectRoutine(selectedPoints[i]));
    }

    private List<Transform> SelectUniqueSpawnPoints(int count)
    {
        List<Transform> pool = new List<Transform>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
                pool.Add(spawnPoints[i]);
        }

        int take = Mathf.Min(count, pool.Count);
        List<Transform> selected = new List<Transform>(take);
        for (int i = 0; i < take; i++)
        {
            int idx = Random.Range(0, pool.Count);
            selected.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return selected;
    }

    private IEnumerator ThrowObjectRoutine(Transform spawnPoint)
    {
        if (spawnPoint == null)
            yield break;

        Sprite sprite = throwSprites[Random.Range(0, throwSprites.Length)];
        if (sprite == null)
            yield break;

        Vector3 targetPos = GetMycopHeadPosition();
        GameObject flyingObject = new GameObject("MycopDebuffThrowObject");
        SpriteRenderer renderer = flyingObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = throwSortingOrder;
        flyingObject.transform.position = spawnPoint.position;
        flyingObject.transform.localScale = Vector3.one * throwObjectScale;

        Vector3 spinAxis = new Vector3(Random.Range(0.35f, 1f), Random.Range(0.35f, 1f), Random.Range(0.35f, 1f)).normalized;
        float spinSpeed = Random.Range(throwSpinSpeedMin, throwSpinSpeedMax);

        float elapsed = 0f;
        Vector3 startPos = spawnPoint.position;
        while (elapsed < throwFlightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwFlightDuration);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * throwArcHeight;
            flyingObject.transform.position = pos;
            flyingObject.transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
            yield return null;
        }

        ApplyHeadLikeDamageToMycop();
        Destroy(flyingObject);
    }

    private void ApplyHeadLikeDamageToMycop()
    {
        if (mycopPlayer == null || mycopPlayer.isDead)
            return;

        PlayerController attacker = attackerPlayer != null ? attackerPlayer : mycopPlayer;
        float upperDamage = attacker.GetDamageForAttack("upper", "head");
        mycopPlayer.ApplyDamage("head", "upper", attacker, upperDamage);
    }

    private Vector3 GetMycopHeadPosition()
    {
        if (mycopHeadPoint != null)
            return mycopHeadPoint.position;

        if (mycopPlayer != null)
        {
            Collider col = mycopPlayer.GetComponent<Collider>();
            if (col != null)
                return new Vector3(col.bounds.center.x, col.bounds.max.y - 0.1f, col.bounds.center.z);

            return mycopPlayer.transform.position + Vector3.up * 1.4f;
        }

        return transform.position;
    }

    private void ResolveGameplayReferences()
    {
        if (mycopPlayer == null)
        {
            GameObject mycopObj = GameObject.Find("Player2");
            if (mycopObj != null)
                mycopPlayer = mycopObj.GetComponent<PlayerController>();
        }

        if (attackerPlayer == null)
        {
            GameObject attackerObj = GameObject.Find("Player1");
            if (attackerObj != null)
                attackerPlayer = attackerObj.GetComponent<PlayerController>();
        }

        if (mycopHeadPoint == null)
        {
            GameObject headObj = GameObject.Find("Player2/Head_point");
            if (headObj != null)
                mycopHeadPoint = headObj.transform;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            List<Transform> found = new List<Transform>();
            TryAddSpawnPoint(found, "Player2/Obj_spawn_point");
            TryAddSpawnPoint(found, "Player2/Obj_spawn_point/Obj_spawn_point_1");
            TryAddSpawnPoint(found, "Player2/Obj_spawn_point/Obj_spawn_point_3");
            spawnPoints = found.ToArray();
        }
    }

    private static void TryAddSpawnPoint(List<Transform> list, string path)
    {
        GameObject go = GameObject.Find(path);
        if (go != null)
            list.Add(go.transform);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (throwSprites != null && throwSprites.Length > 0)
            return;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Textures/Mycop_debuf" });
        if (guids.Length == 0)
            return;

        List<Sprite> sprites = new List<Sprite>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        if (sprites.Count > 0)
            throwSprites = sprites.ToArray();
    }
#endif

}
