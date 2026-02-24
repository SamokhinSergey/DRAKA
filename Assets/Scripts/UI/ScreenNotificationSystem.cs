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

    public enum NotificationType
    {
        Curse,
        Busification,
        Healthsplit,
        Nationlove
    }

    [System.Serializable]
    private class NotificationPreset
    {
        public NotificationType type;
        public string text;
        public Color color = Color.white;
        public AudioClip sound;
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
    [SerializeField] private float textNoseInset = 44f;
    [SerializeField] private float textVerticalInset = 14f;
    [SerializeField] private float textCenterYOffset = 6f;
    [SerializeField] private float fontSize = 42f;

    [Header("Timing")]
    [SerializeField] private float visibleDuration = 1.5f;
    [SerializeField] private float slideInDuration = 0.2f;
    [SerializeField] private float slideOutDuration = 0.2f;
    [SerializeField] private float restackDuration = 0.12f;

    [Header("Layout")]
    [SerializeField] private float edgePadding = 40f;
    [SerializeField] private float edgeFlushOffset = 18f;
    [SerializeField] private float centerYOffset = 190f;
    [SerializeField] private float stackSpacing = 14f;

    [Header("Notification Presets")]
    [SerializeField] private NotificationPreset cursePreset = new NotificationPreset
    {
        type = NotificationType.Curse,
        text = "CURSE",
        color = Color.white
    };
    [SerializeField] private NotificationPreset busificationPreset = new NotificationPreset
    {
        type = NotificationType.Busification,
        text = "BUSSIFICATION",
        color = Color.white
    };
    [SerializeField] private NotificationPreset healthsplitPreset = new NotificationPreset
    {
        type = NotificationType.Healthsplit,
        text = "HEALTHSPLIT",
        color = Color.white
    };
    [SerializeField] private NotificationPreset nationlovePreset = new NotificationPreset
    {
        type = NotificationType.Nationlove,
        text = "NATIONLOVE",
        color = Color.white
    };

    [Header("Audio")]
    [SerializeField] private AudioSource notificationAudioSource;
    [SerializeField, Range(0f, 1f)] private float notificationVolume = 1f;

    private RectTransform _hostRect;
    private readonly List<NotificationItem> _leftItems = new List<NotificationItem>();
    private readonly List<NotificationItem> _rightItems = new List<NotificationItem>();

    public static void ShowForPlayer(PlayerController player, string text)
    {
        if (player == null) return;

        bool isPlayerOne = player.gameObject.name.Contains("1");
        Show(text, isPlayerOne ? NotificationSide.Left : NotificationSide.Right);
    }

    public static void ShowForPlayer(PlayerController player, NotificationType type)
    {
        if (player == null) return;

        var system = EnsureInstance();
        if (system == null) return;

        bool isPlayerOne = player.gameObject.name.Contains("1");
        var side = isPlayerOne ? NotificationSide.Left : NotificationSide.Right;
        system.ShowTyped(type, side);
    }

    public static void Show(string text, NotificationSide side)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var system = EnsureInstance();
        if (system == null) return;

        system.Enqueue(text, side, system.textColor, null);
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
        EnsureAudioSource();

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
            brandedFont = ResolveAttackOfMonsterFont();
        }

        AutoAssignPresetClips();
    }

    private void EnsureAudioSource()
    {
        if (notificationAudioSource != null) return;
        notificationAudioSource = GetComponent<AudioSource>();
        if (notificationAudioSource == null)
            notificationAudioSource = gameObject.AddComponent<AudioSource>();

        notificationAudioSource.playOnAwake = false;
        notificationAudioSource.loop = false;
        notificationAudioSource.spatialBlend = 0f;
    }

    private void AutoAssignPresetClips()
    {
        if (cursePreset != null && cursePreset.sound == null)
            cursePreset.sound = FindClipByName("curse");
        if (busificationPreset != null && busificationPreset.sound == null)
            busificationPreset.sound = FindClipByName("busification");
        if (healthsplitPreset != null && healthsplitPreset.sound == null)
            healthsplitPreset.sound = FindClipByName("healthsplit");
        if (nationlovePreset != null && nationlovePreset.sound == null)
            nationlovePreset.sound = FindClipByName("nationlove");
    }

    private AudioClip FindClipByName(string needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return null;
        string lower = needle.ToLowerInvariant();
        var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (var clip in allClips)
        {
            if (clip == null) continue;
            if (clip.name.ToLowerInvariant().Contains(lower))
                return clip;
        }
        return null;
    }

    private void ShowTyped(NotificationType type, NotificationSide side)
    {
        var preset = GetPreset(type);
        if (preset == null) return;

        string text = string.IsNullOrWhiteSpace(preset.text) ? type.ToString().ToUpperInvariant() : preset.text;
        Enqueue(text, side, preset.color, preset.sound);
    }

    private NotificationPreset GetPreset(NotificationType type)
    {
        if (cursePreset != null && cursePreset.type == type) return cursePreset;
        if (busificationPreset != null && busificationPreset.type == type) return busificationPreset;
        if (healthsplitPreset != null && healthsplitPreset.type == type) return healthsplitPreset;
        if (nationlovePreset != null && nationlovePreset.type == type) return nationlovePreset;
        return null;
    }

    private TMP_FontAsset ResolveAttackOfMonsterFont()
    {
        var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var asset in fontAssets)
        {
            if (asset == null) continue;
            string nameLower = asset.name.ToLowerInvariant();
            if (nameLower.Contains("attack of monster"))
                return asset;
        }

        var unityFonts = Resources.FindObjectsOfTypeAll<Font>();
        foreach (var font in unityFonts)
        {
            if (font == null) continue;
            string nameLower = font.name.ToLowerInvariant();
            if (!nameLower.Contains("attack of monster")) continue;

            try
            {
                return TMP_FontAsset.CreateFontAsset(font);
            }
            catch
            {
                // ignore and continue fallback chain
            }
        }

        return TMP_Settings.defaultFontAsset;
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

    private void Enqueue(string text, NotificationSide side, Color color, AudioClip sound)
    {
        if (_hostRect == null) TryAttachToCanvas();
        if (_hostRect == null) return;

        if (sound != null && notificationAudioSource != null)
            notificationAudioSource.PlayOneShot(sound, Mathf.Clamp01(notificationVolume));

        var list = GetList(side);
        int slot = list.Count;

        var itemGo = new GameObject($"Notification_{side}_{slot}", typeof(RectTransform));
        var itemRect = itemGo.GetComponent<RectTransform>();
        itemRect.SetParent(_hostRect, false);

        Vector2 size = ResolveNotificationSize();
        itemRect.sizeDelta = size;

        bool left = side == NotificationSide.Left;
        itemRect.anchorMin = new Vector2(left ? 0f : 1f, 0.5f);
        itemRect.anchorMax = itemRect.anchorMin;
        itemRect.pivot = new Vector2(left ? 0f : 1f, 0.5f);

        // Compensate transparent margins in the sprite so it visually touches the screen edge.
        float shownX = left ? -edgeFlushOffset : edgeFlushOffset;
        float hiddenX = left ? -size.x - edgePadding : size.x + edgePadding;
        float y = SlotToY(size.y, slot);

        itemRect.anchoredPosition = new Vector2(hiddenX, y);

        var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var backgroundRect = backgroundGo.GetComponent<RectTransform>();
        backgroundRect.SetParent(itemRect, false);
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        if (!left)
            backgroundRect.localScale = new Vector3(-1f, 1f, 1f);

        var image = backgroundGo.GetComponent<Image>();
        image.sprite = notificationSprite;
        image.preserveAspect = false;
        image.type = Image.Type.Simple;
        image.color = Color.white;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(itemRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        float leftInset = left ? textPaddingX : textPaddingX + textNoseInset;
        float rightInset = left ? textPaddingX + textNoseInset : textPaddingX;
        textRect.offsetMin = new Vector2(leftInset, textPaddingY + textVerticalInset);
        textRect.offsetMax = new Vector2(-rightInset, -(textPaddingY + textVerticalInset));
        textRect.anchoredPosition = new Vector2(0f, textCenterYOffset);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = fontSize;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Truncate;
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
