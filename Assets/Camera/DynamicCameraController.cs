// 13.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;

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
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, cinematicZoomSize, t);
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
        transform.position = new Vector3(middlePoint.x, clampedY + clampedYcoef, transform.position.z);

        // Smoothly adjust the camera size
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomSpeed);
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
}
