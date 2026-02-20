using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls Bus visibility in the left corner:
/// - hidden at scene start,
/// - reveals to half when a player is squeezed in the left corner,
/// - adds a small braking bounce when it stops.
/// </summary>
[DisallowMultipleComponent]
public class BusCornerRevealController : MonoBehaviour
{
    private enum BusFlowState
    {
        Idle,
        WaitingBoardInput,
        Transporting
    }

    private sealed class PassengerSnapshot
    {
        public Transform passenger;
        public Rigidbody rb;
        public bool hadRb;
        public bool rbWasKinematic;
        public bool rbDetectCollisions;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<Collider> colliders = new List<Collider>();
        public readonly List<MonoBehaviour> disabledBehaviours = new List<MonoBehaviour>();
    }

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string leftWallName = "Left_wall";
    [SerializeField] private string rightWallName = "Right_wall";
    [SerializeField] private float cornerOffsetFromWall = 5.5f;
    [SerializeField] private float squeezeDistance = 2.2f;
    [SerializeField] private float minForwardPressure = 0.2f;
    [SerializeField] private float depthTolerance = 1.8f;

    [Header("Reveal")]
    [Range(0f, 1f)]
    [SerializeField] private float visibleFractionWhenStopped = 0.5f;
    [SerializeField] private float leftRevealX = -10.92f;
    [SerializeField] private float rightRevealX = 10.23f;
    [SerializeField] private float hiddenExtraOffset = 0.3f;

    [Header("Door Sprites")]
    [SerializeField] private Sprite doorsClosedSprite;
    [SerializeField] private Sprite doorsOpenSprite;
    [SerializeField] private Sprite arrowSprite;

    [Header("Arrow")]
    [SerializeField] private float arrowYOffset = 0.55f;
    [SerializeField] private float arrowScale = 0.12f;
    [SerializeField] private float arrowBlinkPeriod = 0.35f;
    [SerializeField] private int arrowSortingOrder = 120;
    [SerializeField] private float arrowZOffset = -0.35f;

    [Header("Transport")]
    [SerializeField] private float leftTransportStopX = -12.71f;
    [SerializeField] private float rightTransportStopX = 11.77f;
    [SerializeField] private float leftDropOffX = -11.93f;
    [SerializeField] private float rightDropOffX = 10.81f;
    [SerializeField] private float doorPause = 0.2f;

    [Header("Motion")]
    [SerializeField] private float maxMoveSpeed = 8f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float deceleration = 30f;
    [SerializeField] private float brakeZone = 0.8f;

    [Header("Brake Effect")]
    [SerializeField] private float brakeBounceDistance = 0.15f;
    [SerializeField] private float brakeBounceDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource loopAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioClip busStayingClip;
    [SerializeField] private AudioClip busOpenCloseDoorsClip;
    [SerializeField] private AudioClip busStartedClip;
    [SerializeField] private float loopFadeOutOnStartSeconds = 0.22f;

    private Transform[] players;
    private Collider leftWall;
    private Collider rightWall;
    private SpriteRenderer spriteRenderer;

    private float leftWallRightX;
    private float rightWallLeftX;
    private float leftCornerXThreshold;
    private float rightCornerXThreshold;
    private float spriteWorldWidth;
    private float hiddenLeftX;
    private float hiddenRightX;
    private float revealLeftX;
    private float revealRightX;

    private float velocityX;
    private bool brakeFxPlayed;
    private Coroutine brakeFxCoroutine;
    private Coroutine transportRoutine;
    private BusFlowState flowState = BusFlowState.Idle;
    private Transform currentCorneredPlayer;
    private BusSide currentCornerSide;
    private PassengerSnapshot passengerSnapshot;
    private bool playedStartSfxForHideReturn;
    private bool applicationIsQuitting;
    private GameObject arrowObject;
    private SpriteRenderer arrowRenderer;
    private Coroutine arrowBlinkRoutine;

    private enum BusSide
    {
        Left,
        Right
    }

    private BusSide currentSide = BusSide.Left;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
#if UNITY_EDITOR
        TryAutoAssignDoorSpritesInEditor();
#endif
        EnsureAudioSources();
        EnsureArrowObject();
        CacheReferences();
        RecalculatePositions();
    }

    private void Start()
    {
        SetBusX(hiddenLeftX);
        SetFacing(BusSide.Left);
        velocityX = 0f;
    }

    private void Update()
    {
        if (!HasValidSetup())
        {
            CacheReferences();
            RecalculatePositions();
            if (!HasValidSetup())
            {
                return;
            }
        }

        if (flowState == BusFlowState.Transporting)
        {
            return;
        }

        bool squeezedLeft = TryGetCorneredPlayerInLeftCorner(out Transform leftCornered);
        bool squeezedRight = TryGetCorneredPlayerInRightCorner(out Transform rightCornered);
        bool shouldReveal = squeezedLeft || squeezedRight;

        BusSide desiredSide = currentSide;
        if (squeezedLeft && !squeezedRight)
        {
            desiredSide = BusSide.Left;
            currentCorneredPlayer = leftCornered;
            currentCornerSide = BusSide.Left;
        }
        else if (squeezedRight && !squeezedLeft)
        {
            desiredSide = BusSide.Right;
            currentCorneredPlayer = rightCornered;
            currentCornerSide = BusSide.Right;
        }
        else if (squeezedLeft && squeezedRight)
        {
            // If both are technically cornered, use the deeper one as candidate passenger.
            if (Mathf.Abs(leftCornered.position.x) >= Mathf.Abs(rightCornered.position.x))
            {
                currentCorneredPlayer = leftCornered;
                currentCornerSide = BusSide.Left;
            }
            else
            {
                currentCorneredPlayer = rightCornered;
                currentCornerSide = BusSide.Right;
            }
        }

        if (desiredSide != currentSide)
        {
            SwitchSide(desiredSide);
        }

        float targetX = shouldReveal
            ? (currentSide == BusSide.Left ? revealLeftX : revealRightX)
            : (currentSide == BusSide.Left ? hiddenLeftX : hiddenRightX);

        if (shouldReveal)
        {
            StartStayingLoop();
            playedStartSfxForHideReturn = false;
        }
        else if (flowState == BusFlowState.Idle)
        {
            bool returningToHide = Mathf.Abs(transform.position.x - targetX) > 0.03f;
            if (returningToHide && !playedStartSfxForHideReturn)
            {
                PlayStartMovementSound();
                playedStartSfxForHideReturn = true;
            }

            if (!returningToHide)
            {
                StopStayingLoopImmediate();
            }
        }

        if (!shouldReveal)
        {
            brakeFxPlayed = false;
            if (brakeFxCoroutine != null)
            {
                StopCoroutine(brakeFxCoroutine);
                brakeFxCoroutine = null;
            }
        }

        // Keep bus nose aligned with actual movement direction on regular reveal/hide motion.
        SetFacingForDirection(targetX - transform.position.x);
        MoveTowardsTarget(targetX);

        float revealX = currentSide == BusSide.Left ? revealLeftX : revealRightX;
        bool reachedReveal = Mathf.Abs(transform.position.x - revealX) <= 0.02f && Mathf.Abs(velocityX) <= 0.08f;
        bool movingNow = Mathf.Abs(velocityX) > 0.08f || Mathf.Abs(transform.position.x - targetX) > 0.03f;
        bool shouldShowOpenDoors = shouldReveal && reachedReveal && !movingNow;
        ApplyDoorSprite(shouldShowOpenDoors);
        UpdateArrowVisual(shouldShowOpenDoors);

        if (shouldShowOpenDoors)
        {
            if (flowState != BusFlowState.WaitingBoardInput)
            {
                flowState = BusFlowState.WaitingBoardInput;
            }

            if (transportRoutine == null && TryGetBoardingCandidateForCurrentSide(out Transform boardingPlayer))
            {
                transportRoutine = StartCoroutine(TransportPassengerRoutine(boardingPlayer, currentCornerSide));
                return;
            }
        }
        else if (flowState == BusFlowState.WaitingBoardInput)
        {
            flowState = BusFlowState.Idle;
        }

        if (shouldReveal && reachedReveal && !brakeFxPlayed)
        {
            brakeFxPlayed = true;
            brakeFxCoroutine = StartCoroutine(PlayBrakeBounce());
        }
    }

    private void OnDisable()
    {
        if (applicationIsQuitting)
        {
            return;
        }

        StopStayingLoopImmediate();
        HideArrow();
    }

    private void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }

    private void CacheReferences()
    {
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag(playerTag);
        players = new Transform[playerObjects.Length];
        for (int i = 0; i < playerObjects.Length; i++)
        {
            players[i] = playerObjects[i].transform;
        }

        GameObject wallByName = GameObject.Find(leftWallName);
        if (wallByName != null)
        {
            leftWall = wallByName.GetComponent<Collider>();
        }

        wallByName = GameObject.Find(rightWallName);
        if (wallByName != null)
        {
            rightWall = wallByName.GetComponent<Collider>();
        }

        if (leftWall == null || rightWall == null)
        {
            GameObject[] walls = GameObject.FindGameObjectsWithTag("wall");
            float leftMostX = float.PositiveInfinity;
            float rightMostX = float.NegativeInfinity;
            Collider bestLeft = null;
            Collider bestRight = null;

            for (int i = 0; i < walls.Length; i++)
            {
                Collider c = walls[i].GetComponent<Collider>();
                if (c == null) continue;

                float x = c.bounds.center.x;
                if (x < leftMostX)
                {
                    leftMostX = x;
                    bestLeft = c;
                }
                if (x > rightMostX)
                {
                    rightMostX = x;
                    bestRight = c;
                }
            }

            if (leftWall == null) leftWall = bestLeft;
            if (rightWall == null) rightWall = bestRight;
        }
    }

    private void RecalculatePositions()
    {
        if (spriteRenderer == null || leftWall == null || rightWall == null)
        {
            return;
        }

        leftWallRightX = leftWall.bounds.max.x;
        rightWallLeftX = rightWall.bounds.min.x;
        leftCornerXThreshold = leftWallRightX + cornerOffsetFromWall;
        rightCornerXThreshold = rightWallLeftX - cornerOffsetFromWall;

        spriteWorldWidth = Mathf.Max(0.01f, spriteRenderer.bounds.size.x);
        float halfWidth = spriteWorldWidth * 0.5f;

        revealLeftX = leftRevealX;
        revealRightX = rightRevealX;

        float rightEdgeWhenHidden = leftWallRightX - Mathf.Abs(hiddenExtraOffset);
        hiddenLeftX = rightEdgeWhenHidden - halfWidth;

        float leftEdgeWhenHidden = rightWallLeftX + Mathf.Abs(hiddenExtraOffset);
        hiddenRightX = leftEdgeWhenHidden + halfWidth;
    }

    private bool HasValidSetup()
    {
        return spriteRenderer != null && leftWall != null && rightWall != null && players != null && players.Length >= 2;
    }

    private bool TryGetCorneredPlayerInLeftCorner(out Transform corneredPlayer)
    {
        corneredPlayer = null;
        for (int i = 0; i < players.Length; i++)
        {
            Transform cornered = players[i];
            if (cornered == null) continue;

            if (cornered.position.x > leftCornerXThreshold)
            {
                continue;
            }

            for (int j = 0; j < players.Length; j++)
            {
                if (i == j) continue;

                Transform attacker = players[j];
                if (attacker == null) continue;

                float dx = attacker.position.x - cornered.position.x;
                float dz = Mathf.Abs(attacker.position.z - cornered.position.z);

                bool pressingFromRight = dx >= minForwardPressure && dx <= squeezeDistance;
                bool sameDepth = dz <= depthTolerance;

                if (pressingFromRight && sameDepth)
                {
                    corneredPlayer = cornered;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetCorneredPlayerInRightCorner(out Transform corneredPlayer)
    {
        corneredPlayer = null;
        for (int i = 0; i < players.Length; i++)
        {
            Transform cornered = players[i];
            if (cornered == null) continue;

            if (cornered.position.x < rightCornerXThreshold)
            {
                continue;
            }

            for (int j = 0; j < players.Length; j++)
            {
                if (i == j) continue;

                Transform attacker = players[j];
                if (attacker == null) continue;

                float dx = cornered.position.x - attacker.position.x;
                float dz = Mathf.Abs(attacker.position.z - cornered.position.z);

                bool pressingFromLeft = dx >= minForwardPressure && dx <= squeezeDistance;
                bool sameDepth = dz <= depthTolerance;

                if (pressingFromLeft && sameDepth)
                {
                    corneredPlayer = cornered;
                    return true;
                }
            }
        }

        return false;
    }

    private void MoveTowardsTarget(float targetX)
    {
        float currentX = transform.position.x;
        float dist = targetX - currentX;

        if (Mathf.Abs(dist) <= 0.0005f)
        {
            SetBusX(targetX);
            velocityX = 0f;
            return;
        }

        float desiredSpeed = Mathf.Sign(dist) * maxMoveSpeed;

        float distanceFactor = 1f;
        if (Mathf.Abs(dist) < brakeZone)
        {
            distanceFactor = Mathf.Clamp01(Mathf.Abs(dist) / Mathf.Max(0.0001f, brakeZone));
        }
        desiredSpeed *= distanceFactor;

        float accel = Mathf.Abs(desiredSpeed) > Mathf.Abs(velocityX) ? acceleration : deceleration;
        velocityX = Mathf.MoveTowards(velocityX, desiredSpeed, accel * Time.deltaTime);

        float newX = currentX + velocityX * Time.deltaTime;

        if ((dist > 0f && newX > targetX) || (dist < 0f && newX < targetX))
        {
            newX = targetX;
            velocityX = 0f;
        }

        SetBusX(newX);
    }

    private System.Collections.IEnumerator PlayBrakeBounce()
    {
        float startX = transform.position.x;
        float revealX = currentSide == BusSide.Left ? revealLeftX : revealRightX;
        float bounceBackX = currentSide == BusSide.Left
            ? revealX - Mathf.Abs(brakeBounceDistance)
            : revealX + Mathf.Abs(brakeBounceDistance);

        float halfA = Mathf.Max(0.01f, brakeBounceDuration * 0.45f);
        float halfB = Mathf.Max(0.01f, brakeBounceDuration * 0.55f);

        float t = 0f;
        while (t < halfA)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / halfA);
            float eased = 1f - Mathf.Pow(1f - k, 2f);
            SetBusX(Mathf.Lerp(startX, bounceBackX, eased));
            yield return null;
        }

        t = 0f;
        while (t < halfB)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / halfB);
            float eased = 1f - Mathf.Pow(1f - k, 2f);
            SetBusX(Mathf.Lerp(bounceBackX, revealX, eased));
            yield return null;
        }

        SetBusX(revealX);
        velocityX = 0f;
        brakeFxCoroutine = null;
    }

    private void SwitchSide(BusSide side)
    {
        currentSide = side;
        brakeFxPlayed = false;
        velocityX = 0f;

        if (brakeFxCoroutine != null)
        {
            StopCoroutine(brakeFxCoroutine);
            brakeFxCoroutine = null;
        }

        SetFacing(currentSide);

        if (currentSide == BusSide.Left)
        {
            if (transform.position.x > leftCornerXThreshold)
            {
                SetBusX(hiddenLeftX);
            }
        }
        else
        {
            if (transform.position.x < rightCornerXThreshold)
            {
                SetBusX(hiddenRightX);
            }
        }
    }

    private void SetFacing(BusSide side)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.flipX = (side == BusSide.Right);
    }

    private void SetFacingForDirection(float deltaX)
    {
        if (spriteRenderer == null || Mathf.Abs(deltaX) <= 0.001f)
        {
            return;
        }

        // Source bus sprite looks to the right by default.
        spriteRenderer.flipX = deltaX < 0f;
    }

    private void ApplyDoorSprite(bool openDoors)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite target = openDoors ? doorsOpenSprite : doorsClosedSprite;
        if (target != null && spriteRenderer.sprite != target)
        {
            spriteRenderer.sprite = target;
            PlayDoorSfx();
        }
    }

    private void EnsureArrowObject()
    {
        if (arrowObject == null)
        {
            Transform existing = transform.Find("BoardArrow");
            arrowObject = existing != null ? existing.gameObject : new GameObject("BoardArrow");
            if (existing == null)
            {
                arrowObject.transform.SetParent(transform, false);
            }
        }

        arrowRenderer = arrowObject.GetComponent<SpriteRenderer>();
        if (arrowRenderer == null)
        {
            arrowRenderer = arrowObject.AddComponent<SpriteRenderer>();
        }

        arrowRenderer.sortingOrder = arrowSortingOrder;
        arrowRenderer.enabled = false;
        arrowObject.SetActive(false);
    }

    private void UpdateArrowVisual(bool shouldShowOpenDoors)
    {
        if (!shouldShowOpenDoors || flowState == BusFlowState.Transporting)
        {
            HideArrow();
            return;
        }

        Transform target = currentCorneredPlayer;
        if (target == null && players != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                Transform p = players[i];
                if (p == null) continue;
                bool nearActiveCorner = currentCornerSide == BusSide.Left
                    ? p.position.x <= leftCornerXThreshold
                    : p.position.x >= rightCornerXThreshold;
                if (nearActiveCorner)
                {
                    target = p;
                    break;
                }
            }
        }

        if (target == null || arrowRenderer == null)
        {
            HideArrow();
            return;
        }

        if (arrowSprite == null)
        {
            return;
        }

        bool wasHidden = !arrowObject.activeSelf;
        arrowRenderer.sprite = arrowSprite;
        arrowObject.SetActive(true);
        if (wasHidden)
        {
            arrowRenderer.enabled = true;
        }
        arrowObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, arrowScale);

        float headY = target.position.y;
        Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            headY = targetRenderer.bounds.max.y;
        }

        arrowObject.transform.position = new Vector3(
            target.position.x,
            headY + arrowYOffset,
            target.position.z + arrowZOffset
        );

        if (arrowBlinkRoutine == null)
        {
            arrowBlinkRoutine = StartCoroutine(BlinkArrowRoutine());
        }
    }

    private IEnumerator BlinkArrowRoutine()
    {
        float half = Mathf.Max(0.05f, arrowBlinkPeriod * 0.5f);
        bool visible = true;

        while (arrowObject != null && arrowObject.activeSelf)
        {
            visible = !visible;
            if (arrowRenderer != null)
            {
                arrowRenderer.enabled = visible;
            }
            yield return new WaitForSeconds(half);
        }

        arrowBlinkRoutine = null;
    }

    private void HideArrow()
    {
        if (arrowBlinkRoutine != null)
        {
            StopCoroutine(arrowBlinkRoutine);
            arrowBlinkRoutine = null;
        }

        if (arrowRenderer != null)
        {
            arrowRenderer.enabled = false;
        }

        if (arrowObject != null)
        {
            arrowObject.SetActive(false);
        }
    }

    private bool TryConsumeBoardInput(Transform passenger)
    {
        if (passenger == null)
        {
            return false;
        }

        PlayerInput pi = passenger.GetComponent<PlayerInput>();
        if (pi == null)
        {
            pi = passenger.GetComponentInChildren<PlayerInput>(true);
        }
        if (pi == null)
        {
            pi = passenger.GetComponentInParent<PlayerInput>();
        }

        if (pi != null)
        {
            if (TryReadUpFromActions(pi) || TryReadUpFromPairedDevices(pi))
            {
                return true;
            }
        }

        if (Keyboard.current != null &&
            (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame))
        {
            return true;
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad gp = Gamepad.all[i];
            if (gp == null)
            {
                continue;
            }

            if (gp.dpad.up.wasPressedThisFrame || gp.leftStick.ReadValue().y >= 0.85f)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryReadUpFromActions(PlayerInput pi)
    {
        if (pi == null || pi.actions == null)
        {
            return false;
        }

        string[] candidates = { "Move", "Movement", "Navigate", "Up", "Jump", "Submit", "Interact" };
        for (int i = 0; i < candidates.Length; i++)
        {
            InputAction action = pi.actions.FindAction(candidates[i], throwIfNotFound: false);
            if (action == null)
            {
                continue;
            }

            try
            {
                Vector2 axis = action.ReadValue<Vector2>();
                if (axis.y >= 0.6f)
                {
                    return true;
                }
            }
            catch
            {
                if (action.WasPressedThisFrame() || action.IsPressed())
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryReadUpFromPairedDevices(PlayerInput pi)
    {
        if (pi == null || !pi.user.valid)
        {
            return false;
        }

        var devices = pi.user.pairedDevices;
        for (int i = 0; i < devices.Count; i++)
        {
            InputDevice device = devices[i];
            if (device is Keyboard kb)
            {
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
            else if (device is Gamepad gp)
            {
                if (gp.dpad.up.wasPressedThisFrame || gp.leftStick.ReadValue().y >= 0.85f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetBoardingCandidateForCurrentSide(out Transform boardingPlayer)
    {
        boardingPlayer = null;
        if (players == null || players.Length == 0)
        {
            return false;
        }

        // Prefer players that are physically at the active corner.
        for (int i = 0; i < players.Length; i++)
        {
            Transform p = players[i];
            if (p == null)
            {
                continue;
            }

            bool nearActiveCorner = currentCornerSide == BusSide.Left
                ? p.position.x <= leftCornerXThreshold
                : p.position.x >= rightCornerXThreshold;

            if (!nearActiveCorner)
            {
                continue;
            }

            if (TryConsumeBoardInput(p))
            {
                boardingPlayer = p;
                return true;
            }
        }

        // Fallback: if thresholds are noisy, allow any player's explicit up press.
        for (int i = 0; i < players.Length; i++)
        {
            Transform p = players[i];
            if (p == null)
            {
                continue;
            }

            if (TryConsumeBoardInput(p))
            {
                boardingPlayer = p;
                return true;
            }
        }

        return false;
    }

    private IEnumerator TransportPassengerRoutine(Transform passenger, BusSide fromSide)
    {
        flowState = BusFlowState.Transporting;
        currentCorneredPlayer = null;
        HideArrow();

        if (brakeFxCoroutine != null)
        {
            StopCoroutine(brakeFxCoroutine);
            brakeFxCoroutine = null;
        }

        ApplyDoorSprite(false);
        yield return new WaitForSeconds(doorPause);

        BoardPassenger(passenger);

        BusSide toSide = fromSide == BusSide.Left ? BusSide.Right : BusSide.Left;

        float transportStop = toSide == BusSide.Left ? leftTransportStopX : rightTransportStopX;
        float dropX = toSide == BusSide.Left ? leftDropOffX : rightDropOffX;
        float hideX = toSide == BusSide.Left ? hiddenLeftX : hiddenRightX;

        PlayStartMovementSound();
        SetFacingForDirection(transportStop - transform.position.x);
        yield return MoveBusToX(transportStop);
        currentSide = toSide;

        ApplyDoorSprite(true);
        yield return new WaitForSeconds(doorPause);

        UnboardPassenger(dropX);

        ApplyDoorSprite(false);
        yield return new WaitForSeconds(doorPause);

        SetFacingForDirection(hideX - transform.position.x);
        yield return MoveBusToX(hideX);

        velocityX = 0f;
        flowState = BusFlowState.Idle;
        transportRoutine = null;
    }

    private IEnumerator MoveBusToX(float targetX)
    {
        float timeout = Time.time + 6f;
        while (Time.time < timeout)
        {
            MoveTowardsTarget(targetX);
            float dist = Mathf.Abs(transform.position.x - targetX);
            if (dist <= 0.03f && Mathf.Abs(velocityX) <= 0.08f)
            {
                break;
            }
            yield return null;
        }

        SetBusX(targetX);
        velocityX = 0f;
    }

    private void BoardPassenger(Transform passenger)
    {
        if (passenger == null)
        {
            return;
        }

        passengerSnapshot = new PassengerSnapshot
        {
            passenger = passenger,
            rb = passenger.GetComponent<Rigidbody>()
        };

        if (passengerSnapshot.rb != null)
        {
            passengerSnapshot.hadRb = true;
            passengerSnapshot.rbWasKinematic = passengerSnapshot.rb.isKinematic;
            passengerSnapshot.rbDetectCollisions = passengerSnapshot.rb.detectCollisions;
            passengerSnapshot.rb.linearVelocity = Vector3.zero;
            passengerSnapshot.rb.isKinematic = true;
            passengerSnapshot.rb.detectCollisions = false;
        }

        passenger.GetComponentsInChildren(true, passengerSnapshot.renderers);
        for (int i = 0; i < passengerSnapshot.renderers.Count; i++)
        {
            passengerSnapshot.renderers[i].enabled = false;
        }

        passenger.GetComponentsInChildren(true, passengerSnapshot.colliders);
        for (int i = 0; i < passengerSnapshot.colliders.Count; i++)
        {
            passengerSnapshot.colliders[i].enabled = false;
        }

        MonoBehaviour[] behaviours = passenger.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null || !mb.enabled)
            {
                continue;
            }

            if (mb is PlayerInput)
            {
                continue;
            }

            passengerSnapshot.disabledBehaviours.Add(mb);
            mb.enabled = false;
        }
    }

    private void UnboardPassenger(float x)
    {
        if (passengerSnapshot == null || passengerSnapshot.passenger == null)
        {
            return;
        }

        Vector3 pos = passengerSnapshot.passenger.position;
        pos.x = x;
        passengerSnapshot.passenger.position = pos;

        for (int i = 0; i < passengerSnapshot.renderers.Count; i++)
        {
            if (passengerSnapshot.renderers[i] != null)
            {
                passengerSnapshot.renderers[i].enabled = true;
            }
        }

        for (int i = 0; i < passengerSnapshot.colliders.Count; i++)
        {
            if (passengerSnapshot.colliders[i] != null)
            {
                passengerSnapshot.colliders[i].enabled = true;
            }
        }

        for (int i = 0; i < passengerSnapshot.disabledBehaviours.Count; i++)
        {
            if (passengerSnapshot.disabledBehaviours[i] != null)
            {
                passengerSnapshot.disabledBehaviours[i].enabled = true;
            }
        }

        if (passengerSnapshot.hadRb && passengerSnapshot.rb != null)
        {
            passengerSnapshot.rb.isKinematic = passengerSnapshot.rbWasKinematic;
            passengerSnapshot.rb.detectCollisions = passengerSnapshot.rbDetectCollisions;
            passengerSnapshot.rb.linearVelocity = Vector3.zero;
        }

        passengerSnapshot = null;
    }

    private void EnsureAudioSources()
    {
        if (loopAudioSource == null)
        {
            loopAudioSource = GetComponent<AudioSource>();
            if (loopAudioSource == null)
            {
                loopAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (sfxAudioSource == null)
        {
            GameObject sfxGo = new GameObject("BusSFX");
            sfxGo.transform.SetParent(transform, false);
            sfxAudioSource = sfxGo.AddComponent<AudioSource>();
        }

        loopAudioSource.playOnAwake = false;
        loopAudioSource.loop = true;
        loopAudioSource.spatialBlend = 0f;

        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;
        sfxAudioSource.spatialBlend = 0f;
    }

    private void StartStayingLoop()
    {
        if (loopAudioSource == null || busStayingClip == null)
        {
            return;
        }

        if (loopAudioSource.clip != busStayingClip)
        {
            loopAudioSource.clip = busStayingClip;
        }

        loopAudioSource.volume = 1f;
        if (!loopAudioSource.isPlaying)
        {
            loopAudioSource.Play();
        }
    }

    private void StopStayingLoopImmediate()
    {
        if (loopAudioSource == null)
        {
            return;
        }

        loopAudioSource.Stop();
        loopAudioSource.volume = 1f;
    }

    private void PlayStartMovementSound()
    {
        if (sfxAudioSource != null && busStartedClip != null)
        {
            sfxAudioSource.PlayOneShot(busStartedClip);
        }

        if (loopAudioSource != null && loopAudioSource.isPlaying)
        {
            StartCoroutine(FadeOutLoopRoutine(Mathf.Max(0.01f, loopFadeOutOnStartSeconds)));
        }
    }

    private IEnumerator FadeOutLoopRoutine(float seconds)
    {
        if (loopAudioSource == null || !loopAudioSource.isPlaying)
        {
            yield break;
        }

        float startVolume = loopAudioSource.volume;
        float elapsed = 0f;
        while (elapsed < seconds && loopAudioSource != null && loopAudioSource.isPlaying)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            loopAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (loopAudioSource != null)
        {
            loopAudioSource.Stop();
            loopAudioSource.volume = 1f;
        }
    }

    private void PlayDoorSfx()
    {
        if (sfxAudioSource != null && busOpenCloseDoorsClip != null)
        {
            sfxAudioSource.PlayOneShot(busOpenCloseDoorsClip);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        TryAutoAssignDoorSpritesInEditor();
    }

    private void TryAutoAssignDoorSpritesInEditor()
    {
        if (doorsClosedSprite == null)
        {
            doorsClosedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Bus/Bus_doors_close.png");
        }

        if (doorsOpenSprite == null)
        {
            doorsOpenSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Bus/Bus_doors_open.png");
        }

        if (arrowSprite == null)
        {
            UnityEngine.Object[] all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/Bus/arrow.png");
            float bestArea = -1f;
            Sprite best = null;
            for (int i = 0; i < all.Length; i++)
            {
                Sprite s = all[i] as Sprite;
                if (s == null) continue;
                float area = s.rect.width * s.rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = s;
                }
            }
            arrowSprite = best;
        }

        if (busStayingClip == null)
        {
            busStayingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/bus_staying.mp3");
        }

        if (busOpenCloseDoorsClip == null)
        {
            busOpenCloseDoorsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/bus_open_and_closed_doors.mp3");
        }

        if (busStartedClip == null)
        {
            busStartedClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Bus_started.mp3");
        }
    }
#endif

    private void SetBusX(float x)
    {
        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
    }
}
