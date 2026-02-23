using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenNotificationSystem : MonoBehaviour
{
    public enum NotificationSide
    {
        Left,
        Right
    }

    private class NotificationItem
    {
        public RectTransform rect;
        public NotificationSide side;
        public Coroutine moveCoroutine;
    }

    private static ScreenNotificationSystem _instance;

    [Header("Visuals")]
    [SerializeField] private Sprite notificationSprite;
    [SerializeField] private TMP_FontAsset brandedFont;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Vector2 fallbackSize = new Vector2(330f, 90f);
    [SerializeField] private float textPaddingX = 26f;
    [SerializeField] private float textPaddingY = 10f;
    [SerializeField] private float fontSize = 42f;

    [Header("Timing")]
    [SerializeField] private float visibleDuration = 1.5f;
    [SerializeField] private float slideInDuration = 0.2f;
    [SerializeField] private float slideOutDuration = 0.2f;
    [SerializeField] private float restackDuration = 0.12f;

    [Header("Layout")]
    [SerializeField] private float edgePadding = 40f;
    [SerializeField] private float centerYOffset = 0f;
    [SerializeField] private float stackSpacing = 14f;

    private RectTransform _hostRect;
    private readonly List<NotificationItem> _leftItems = new List<NotificationItem>();
    private readonly List<NotificationItem> _rightItems = new List<NotificationItem>();

    public static void ShowForPlayer(PlayerController player, string text)
    {
        if (player == null) return;

        bool isPlayerOne = player.gameObject.name.Contains("1");
        Show(text, isPlayerOne ? NotificationSide.Left : NotificationSide.Right);
    }

    public static void Show(string text, NotificationSide side)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var system = EnsureInstance();
        if (system == null) return;

        system.Enqueue(text, side);
    }

    private static ScreenNotificationSystem EnsureInstance()
    {
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<ScreenNotificationSystem>();
        if (_instance != null) return _instance;

        GameObject go = new GameObject("ScreenNotificationSystem");
        _instance = go.AddComponent<ScreenNotificationSystem>();
        _instance.TryAttachToCanvas();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        TryAttachToCanvas();

        if (notificationSprite == null)
        {
            notificationSprite = Resources.Load<Sprite>("Textures/Nontifications/Nontification_arrow");
            if (notificationSprite == null)
            {
                var tex = Resources.Load<Texture2D>("Textures/Nontifications/Nontification_arrow");
                if (tex != null)
                {
                    notificationSprite = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }
        }

        if (brandedFont == null)
        {
            var allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in allTexts)
            {
                if (tmp != null && tmp.font != null)
                {
                    brandedFont = tmp.font;
                    break;
                }
            }
        }
    }

    private void TryAttachToCanvas()
    {
        if (_hostRect != null) return;

        Canvas targetCanvas = null;
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c != null && c.gameObject.name == "Canvas" && c.transform.parent != null && c.transform.parent.name == "Healthbar")
            {
                targetCanvas = c;
                break;
            }
        }

        if (targetCanvas == null)
        {
            foreach (var c in canvases)
            {
                if (c != null && c.isActiveAndEnabled)
                {
                    targetCanvas = c;
                    break;
                }
            }
        }

        if (targetCanvas == null) return;

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        transform.SetParent(targetCanvas.transform, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        _hostRect = rt;
    }

    private void Enqueue(string text, NotificationSide side)
    {
        if (_hostRect == null) TryAttachToCanvas();
        if (_hostRect == null) return;

        var list = GetList(side);
        int slot = list.Count;

        var itemGo = new GameObject($"Notification_{side}_{slot}", typeof(RectTransform), typeof(Image));
        var itemRect = itemGo.GetComponent<RectTransform>();
        itemRect.SetParent(_hostRect, false);

        Vector2 size = ResolveNotificationSize();
        itemRect.sizeDelta = size;

        bool left = side == NotificationSide.Left;
        itemRect.anchorMin = new Vector2(left ? 0f : 1f, 0.5f);
        itemRect.anchorMax = itemRect.anchorMin;
        itemRect.pivot = new Vector2(left ? 0f : 1f, 0.5f);

        float shownX = left ? edgePadding : -edgePadding;
        float hiddenX = left ? -size.x - edgePadding : size.x + edgePadding;
        float y = SlotToY(size.y, slot);

        itemRect.anchoredPosition = new Vector2(hiddenX, y);

        var image = itemGo.GetComponent<Image>();
        image.sprite = notificationSprite;
        image.preserveAspect = false;
        image.type = Image.Type.Simple;
        image.color = Color.white;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(itemRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(textPaddingX, textPaddingY);
        textRect.offsetMax = new Vector2(-textPaddingX, -textPaddingY);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = textColor;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = fontSize;
        if (brandedFont != null) tmp.font = brandedFont;

        var item = new NotificationItem
        {
            rect = itemRect,
            side = side
        };

        list.Add(item);
        StartCoroutine(NotificationLifecycle(item, shownX, hiddenX));
    }

    private IEnumerator NotificationLifecycle(NotificationItem item, float shownX, float hiddenX)
    {
        if (item == null || item.rect == null) yield break;

        yield return MoveX(item.rect, shownX, slideInDuration);
        yield return new WaitForSeconds(visibleDuration);
        yield return MoveX(item.rect, hiddenX, slideOutDuration);

        var list = GetList(item.side);
        int removedIndex = list.IndexOf(item);
        if (removedIndex >= 0) list.RemoveAt(removedIndex);

        if (item.rect != null)
            Destroy(item.rect.gameObject);

        Restack(item.side);
    }

    private void Restack(NotificationSide side)
    {
        var list = GetList(side);
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null || item.rect == null) continue;

            float targetY = SlotToY(item.rect.sizeDelta.y, i);
            if (item.moveCoroutine != null) StopCoroutine(item.moveCoroutine);
            item.moveCoroutine = StartCoroutine(MoveY(item.rect, targetY, restackDuration));
        }
    }

    private List<NotificationItem> GetList(NotificationSide side)
    {
        return side == NotificationSide.Left ? _leftItems : _rightItems;
    }

    private float SlotToY(float height, int slotIndex)
    {
        return centerYOffset - slotIndex * (height + stackSpacing);
    }

    private Vector2 ResolveNotificationSize()
    {
        if (notificationSprite == null) return fallbackSize;

        float width = fallbackSize.x;
        float ratio = notificationSprite.rect.height / notificationSprite.rect.width;
        float height = width * ratio;
        return new Vector2(width, height);
    }

    private static IEnumerator MoveX(RectTransform rect, float targetX, float duration)
    {
        Vector2 start = rect.anchoredPosition;
        Vector2 end = new Vector2(targetX, start.y);
        if (duration <= 0f)
        {
            rect.anchoredPosition = end;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, end, EaseOutCubic(k));
            yield return null;
        }
        rect.anchoredPosition = end;
    }

    private static IEnumerator MoveY(RectTransform rect, float targetY, float duration)
    {
        Vector2 start = rect.anchoredPosition;
        Vector2 end = new Vector2(start.x, targetY);
        if (duration <= 0f)
        {
            rect.anchoredPosition = end;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, end, EaseOutCubic(k));
            yield return null;
        }
        rect.anchoredPosition = end;
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }
}
