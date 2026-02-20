// 13.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DynamicCameraController : MonoBehaviour
{
   
  
    public Transform player1; // Reference to the first player
    public Transform player2; // Reference to the second player
    public float zoomSpeed = 5f;
    public float extraHeightcoef = 0.19f;
    public float clampedYcoef = 0.3f;
    [Header("Bus Tracking")]
    public bool includeBusDuringTransport = true;
    public BusCornerRevealController busController;
    [Tooltip("Track bus only while it is inside arena X-range to avoid zooming into off-screen areas.")]
    public float busTrackMinX = -13.5f;
    [Tooltip("Track bus only while it is inside arena X-range to avoid zooming into off-screen areas.")]
    public float busTrackMaxX = 13.5f;

    private Camera cam;
    private BoxCollider player1Collider;
    private BoxCollider player2Collider;

    [Header("Cinematic (Infarction)")]
    public float cinematicLerpSpeed = 8f;

    private bool cinematicActive = false;
    private Transform cinematicTarget;
    private float cinematicZoomSize = 2.5f;
    private float cinematicYOffset = 0.3f;

    private Vector3 savedPosition;
    private float savedOrthoSize;
    private bool hasSavedState = false;
    private Coroutine cinematicRestoreCoroutine;
    private Quaternion baseRotation;

    [Header("Impact FX")]
    public float impactShakeMagnitude = 0.14f;
    public float impactShakeDuration = 0.22f;
    public float impactGlitchMagnitude = 0.08f;
    public float impactGlitchDuration = 0.14f;
    public float impactRollDegrees = 1.2f;
    public float impactSizeJitter = 0.04f;

    private float impactShakeEndsAt = -1f;
    private float impactGlitchEndsAt = -1f;
    private float impactShakeMagRuntime = 0f;
    private float impactGlitchMagRuntime = 0f;
    private float impactRollRuntime = 0f;
    private float impactSizeJitterRuntime = 0f;
    private Coroutine impactOverlayCoroutine;
    private Canvas impactCanvas;
    private RawImage impactNoiseImage;
    private RectTransform impactStarRoot;
    private Texture2D impactNoiseTexture;
    private Sprite impactStarSprite;

    [Header("Impact Overlay FX")]
    public float impactNoiseMaxAlpha = 0.45f;
    public int impactNoiseResolution = 64;
    public int impactStarCount = 16;
    public float impactStarBurstRadius = 260f;
    public float impactStarLifeMin = 0.65f;
    public float impactStarLifeMax = 1.35f;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic)
        {
            Debug.LogError("The camera must be orthographic!");
        }

        // Get BoxCollider components of players
        player1Collider = player1.GetComponent<BoxCollider>();
        player2Collider = player2.GetComponent<BoxCollider>();

        if (player1Collider == null || player2Collider == null)
        {
            Debug.LogError("Ensure Player1 and Player2 objects have BoxCollider components!");
        }

        if (busController == null)
        {
            busController = FindAnyObjectByType<BusCornerRevealController>();
        }

        baseRotation = transform.rotation;
    }

    public void TriggerImpact(float shakeScale = 1f, float glitchScale = 1f)
    {
        impactShakeMagRuntime = Mathf.Max(0f, impactShakeMagnitude * shakeScale);
        impactGlitchMagRuntime = Mathf.Max(0f, impactGlitchMagnitude * glitchScale);
        impactRollRuntime = Mathf.Max(0f, impactRollDegrees * glitchScale);
        impactSizeJitterRuntime = Mathf.Max(0f, impactSizeJitter * glitchScale);

        float now = Time.unscaledTime;
        impactShakeEndsAt = now + impactShakeDuration;
        impactGlitchEndsAt = now + impactGlitchDuration;

        if (impactOverlayCoroutine != null)
        {
            StopCoroutine(impactOverlayCoroutine);
        }
        impactOverlayCoroutine = StartCoroutine(PlayImpactOverlayFx(Mathf.Max(impactShakeDuration, impactGlitchDuration)));
    }

    /// <summary>
    /// Enter a temporary cinematic mode that focuses and zooms on a target.
    /// Uses unscaled time so it still works during slow motion.
    /// </summary>
    public void EnterCinematic(Transform target, float zoomSize, float yOffset)
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (cam == null)
        {
            return;
        }

        if (cinematicRestoreCoroutine != null)
        {
            StopCoroutine(cinematicRestoreCoroutine);
            cinematicRestoreCoroutine = null;
        }

        if (!hasSavedState)
        {
            savedPosition = transform.position;
            savedOrthoSize = cam.orthographicSize;
            hasSavedState = true;
        }

        cinematicTarget = target;
        cinematicZoomSize = zoomSize;
        cinematicYOffset = yOffset;
        cinematicActive = (cinematicTarget != null);
    }

    /// <summary>
    /// Smoothly return the camera to the state it had before EnterCinematic.
    /// </summary>
    public void ExitCinematic(float restoreSeconds = 0.35f)
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (!hasSavedState || cam == null)
        {
            cinematicActive = false;
            cinematicTarget = null;
            hasSavedState = false;
            return;
        }

        if (cinematicRestoreCoroutine != null)
        {
            StopCoroutine(cinematicRestoreCoroutine);
        }

        cinematicRestoreCoroutine = StartCoroutine(RestoreRoutine(restoreSeconds));
    }

    void LateUpdate()
    {
        if (busController == null)
        {
            busController = FindAnyObjectByType<BusCornerRevealController>();
        }

        if (cinematicActive && cam != null && cinematicTarget != null)
        {
            // Focus on target in unscaled time (works under time slowdown).
            Vector3 targetPos = new Vector3(
                cinematicTarget.position.x,
                cinematicTarget.position.y + cinematicYOffset,
                transform.position.z
            );

            float t = Mathf.Clamp01(Time.unscaledDeltaTime * cinematicLerpSpeed);
            Vector3 cinematicPos = Vector3.Lerp(transform.position, targetPos, t);
            float cinematicSize = Mathf.Lerp(cam.orthographicSize, cinematicZoomSize, t);
            ApplyImpactFx(ref cinematicPos, ref cinematicSize);
            transform.position = cinematicPos;
            cam.orthographicSize = cinematicSize;
            return;
        }

        if (player1 == null || player2 == null || player1Collider == null || player2Collider == null)
        {
            Debug.LogError("References to Player1 and Player2 objects are required!");
            return;
        }

        Bounds player1Bounds = GetPlayerBounds(player1, player1Collider);
        Bounds player2Bounds = GetPlayerBounds(player2, player2Collider);

        Bounds combinedBounds = player1Bounds;
        combinedBounds.Encapsulate(player2Bounds);

        if (includeBusDuringTransport && busController != null)
        {
            if (TryGetBusBoundsForCamera(out Bounds busBounds))
            {
                combinedBounds.Encapsulate(busBounds);
            }
        }

        float leftBound = combinedBounds.min.x;
        float rightBound = combinedBounds.max.x;
        float topBound = combinedBounds.max.y;
        float bottomBound = combinedBounds.min.y;
        Vector3 middlePoint = combinedBounds.center;

        // Calculate the required camera size to keep both players fully visible
        float maxHeight = Mathf.Max(player1Bounds.size.y, player2Bounds.size.y, combinedBounds.size.y);
        float extraHeight = maxHeight * extraHeightcoef;

        float horizontalSize = Mathf.Abs(rightBound - leftBound) / 2f / cam.aspect;
        float verticalSize = Mathf.Abs(topBound - bottomBound) / 2f + extraHeight;
        float targetSize = Mathf.Max(horizontalSize, verticalSize);

        // Ensure the camera does not go below the bottom or above the top of the players' colliders
        float clampedY = Mathf.Clamp(middlePoint.y, bottomBound - extraHeight + targetSize, topBound + extraHeight - targetSize);

        // Set the camera position
        Vector3 finalPos = new Vector3(middlePoint.x, clampedY + clampedYcoef, transform.position.z);

        // Smoothly adjust the camera size
        float finalSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomSpeed);
        ApplyImpactFx(ref finalPos, ref finalSize);
        transform.position = finalPos;
        cam.orthographicSize = finalSize;
    }

    private Bounds GetPlayerBounds(Transform player, BoxCollider collider)
    {
        if (collider != null && collider.enabled)
        {
            return collider.bounds;
        }

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        return new Bounds(player.position, new Vector3(1.2f, 2f, 1f));
    }

    private bool TryGetBusBoundsForCamera(out Bounds bounds)
    {
        bounds = default;

        if (busController == null)
        {
            return false;
        }

        SpriteRenderer busRenderer = busController.GetComponentInChildren<SpriteRenderer>(true);
        if (busRenderer == null || !busRenderer.enabled)
        {
            return false;
        }

        float busX = busRenderer.bounds.center.x;
        if (busX < busTrackMinX || busX > busTrackMaxX)
        {
            return false;
        }

        bounds = busRenderer.bounds;
        return true;
    }

    private System.Collections.IEnumerator RestoreRoutine(float restoreSeconds)
    {
        cinematicActive = true;
        cinematicTarget = null;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, restoreSeconds);

        Vector3 startPos = transform.position;
        float startSize = cam.orthographicSize;

        while (elapsed < duration)
        {
            float k = elapsed / duration;
            float smooth = Mathf.SmoothStep(0f, 1f, k);

            transform.position = Vector3.Lerp(startPos, savedPosition, smooth);
            cam.orthographicSize = Mathf.Lerp(startSize, savedOrthoSize, smooth);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.position = savedPosition;
        cam.orthographicSize = savedOrthoSize;

        hasSavedState = false;
        cinematicActive = false;
        cinematicRestoreCoroutine = null;
    }

    private void ApplyImpactFx(ref Vector3 targetPos, ref float targetSize)
    {
        float now = Time.unscaledTime;
        bool hasShake = now < impactShakeEndsAt;
        bool hasGlitch = now < impactGlitchEndsAt;

        if (hasShake)
        {
            float k = Mathf.Clamp01((impactShakeEndsAt - now) / Mathf.Max(0.0001f, impactShakeDuration));
            float mag = impactShakeMagRuntime * k;
            targetPos.x += Random.Range(-mag, mag);
            targetPos.y += Random.Range(-mag, mag);
        }

        if (hasGlitch)
        {
            float k = Mathf.Clamp01((impactGlitchEndsAt - now) / Mathf.Max(0.0001f, impactGlitchDuration));
            float g = impactGlitchMagRuntime * k;
            targetPos.x += Random.Range(-g, g);
            targetPos.y += Random.Range(-g, g);

            targetSize = Mathf.Max(0.1f, targetSize + Random.Range(-impactSizeJitterRuntime, impactSizeJitterRuntime) * k);
            float roll = Random.Range(-impactRollRuntime, impactRollRuntime) * k;
            transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
        }
        else
        {
            transform.rotation = baseRotation;
        }
    }

    private IEnumerator PlayImpactOverlayFx(float totalDuration)
    {
        EnsureImpactOverlay();
        if (impactNoiseImage == null || impactStarRoot == null)
        {
            yield break;
        }

        SpawnImpactStars();

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, totalDuration);
        float noiseTick = 0f;

        while (elapsed < duration)
        {
            float t = 1f - (elapsed / duration);
            float flicker = Random.Range(0.55f, 1f);
            impactNoiseImage.color = new Color(1f, 1f, 1f, impactNoiseMaxAlpha * t * flicker);

            noiseTick += Time.unscaledDeltaTime;
            if (noiseTick >= 0.03f)
            {
                noiseTick = 0f;
                RefreshNoiseTexture();
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        impactNoiseImage.color = new Color(1f, 1f, 1f, 0f);
        impactOverlayCoroutine = null;
    }

    private void EnsureImpactOverlay()
    {
        if (impactCanvas != null && impactNoiseImage != null && impactStarRoot != null)
        {
            return;
        }

        GameObject canvasGo = new GameObject("ImpactOverlayCanvas");
        impactCanvas = canvasGo.AddComponent<Canvas>();
        impactCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        impactCanvas.sortingOrder = 5000;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGo);

        GameObject noiseGo = new GameObject("ImpactNoise");
        noiseGo.transform.SetParent(canvasGo.transform, false);
        impactNoiseImage = noiseGo.AddComponent<RawImage>();
        impactNoiseImage.raycastTarget = false;
        RectTransform noiseRt = impactNoiseImage.rectTransform;
        noiseRt.anchorMin = Vector2.zero;
        noiseRt.anchorMax = Vector2.one;
        noiseRt.offsetMin = Vector2.zero;
        noiseRt.offsetMax = Vector2.zero;

        impactStarRoot = new GameObject("ImpactStars").AddComponent<RectTransform>();
        impactStarRoot.SetParent(canvasGo.transform, false);
        impactStarRoot.anchorMin = Vector2.zero;
        impactStarRoot.anchorMax = Vector2.one;
        impactStarRoot.offsetMin = Vector2.zero;
        impactStarRoot.offsetMax = Vector2.zero;

        CreateNoiseTexture();
        CreateStarSprite();
    }

    private void CreateNoiseTexture()
    {
        int size = Mathf.Clamp(impactNoiseResolution, 16, 256);
        impactNoiseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        impactNoiseTexture.filterMode = FilterMode.Point;
        impactNoiseTexture.wrapMode = TextureWrapMode.Repeat;
        impactNoiseImage.texture = impactNoiseTexture;
        RefreshNoiseTexture();
    }

    private void RefreshNoiseTexture()
    {
        if (impactNoiseTexture == null)
        {
            return;
        }

        int w = impactNoiseTexture.width;
        int h = impactNoiseTexture.height;
        Color32[] pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(35, 255);
            pixels[i] = new Color32(v, v, v, 255);
        }
        impactNoiseTexture.SetPixels32(pixels);
        impactNoiseTexture.Apply(false, false);
    }

    private void CreateStarSprite()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c.x);
                float dy = Mathf.Abs(y - c.y);
                float cross = Mathf.Max(1f - (dx / 1.8f), 1f - (dy / 1.8f));
                float diag = Mathf.Max(1f - (Mathf.Abs(dx - dy) / 1.5f), 0f) * 0.65f;
                float a = Mathf.Clamp01(Mathf.Max(cross, diag) - Mathf.Max(dx, dy) * 0.06f);
                tex.SetPixel(x, y, new Color(1f, 0.98f, 0.7f, a));
            }
        }
        tex.Apply(false, false);
        impactStarSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void SpawnImpactStars()
    {
        if (impactStarRoot == null || impactStarSprite == null)
        {
            return;
        }

        for (int i = 0; i < impactStarCount; i++)
        {
            GameObject starGo = new GameObject($"ImpactStar_{i}");
            starGo.transform.SetParent(impactStarRoot, false);
            Image img = starGo.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = impactStarSprite;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Random.insideUnitCircle * (impactStarBurstRadius * 0.35f);
            float size = Random.Range(22f, 44f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector2 drift = Random.insideUnitCircle.normalized * Random.Range(70f, impactStarBurstRadius);
            float life = Random.Range(impactStarLifeMin, impactStarLifeMax);
            StartCoroutine(AnimateImpactStar(rt, img, drift, life));
        }
    }

    private IEnumerator AnimateImpactStar(RectTransform rt, Image img, Vector2 drift, float life)
    {
        float elapsed = 0f;
        Vector2 start = rt.anchoredPosition;
        Color c = img.color;

        while (elapsed < life)
        {
            float t = elapsed / life;
            float ease = 1f - Mathf.Pow(1f - t, 2f);
            rt.anchoredPosition = start + drift * ease;
            float scale = Mathf.Lerp(1.25f, 0.2f, t);
            rt.localScale = new Vector3(scale, scale, 1f);
            img.color = new Color(c.r, c.g, c.b, 1f - t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (rt != null)
        {
            Destroy(rt.gameObject);
        }
    }
}
