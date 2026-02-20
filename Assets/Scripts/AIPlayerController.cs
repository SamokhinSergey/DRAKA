// 17.02.2026 AI-Tag
// Smart AI controller for second player in Crazy Russian Fighting.
// Attach this to the second player's GameObject and assign references in the Inspector.

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class AIPlayerController : MonoBehaviour
{
    [Header("References")]
    public PlayerController self;       // This player's controller
    public PlayerController opponent;   // Opponent controller (first player)
    public PlayerInput playerInput;

    [Header("AI Strategy")]
    [Tooltip("Optional: character-specific AI strategy. If not set, uses default behavior.")]
    public AIStrategy strategy;
     // Optional: will be disabled while AI is enabled

    [Header("AI Settings")]
    public bool aiEnabled = true;       // Can be toggled in Inspector to enable/disable AI

    [Tooltip("Preferred distance to opponent on the horizontal plane.")]
    public float preferredDistance = 1.2f;

    [Tooltip("Maximum distance at which AI will try to attack.")]
    public float attackRange = 1.8f;

    [Tooltip("Delay between decisions/attacks.")]
    public float decisionCooldown = 0.4f;

    [Header("Movement Smoothing")]
    [Tooltip("Dead zone around preferredDistance to avoid jitter.")]
    public float distanceDeadZone = 0.15f;

    [Tooltip("How fast movement input changes (higher = snappier, lower = smoother).")]
    public float moveSmoothing = 10f;

    [Header("Retreat (step back)")]
    [Tooltip("Chance (0..1) to start retreat on a decision tick when near the opponent.")]
    [Range(0f, 1f)]
    public float retreatChance = 0.18f;

    [Tooltip("Minimum retreat time in seconds.")]
    public float retreatMinDuration = 0.35f;

    [Tooltip("Maximum retreat time in seconds.")]
    public float retreatMaxDuration = 0.8f;

    [Tooltip("Cooldown in seconds before AI can retreat again.")]
    public float retreatCooldown = 1.8f;

    [Header("Projectile Avoidance (Babushka saliva)")]
    [Tooltip("Projectile tags to detect (saliva should have one of these).")]
    public string projectileTag1 = "damage_object";

    [Tooltip("Optional second tag (some assets may use a typo tag).")]
    public string projectileTag2 = "damage_obect";

    [Tooltip("How far around the AI to scan for projectiles.")]
    public float projectileDetectRadius = 6f;

    [Tooltip("If projectile is above this height relative to player, treat it as a head shot and crouch.")]
    public float headThreatHeightOffset = 1.0f;

    [Tooltip("How strongly projectile must be moving toward the AI to count as 'approaching'. (-1..1)")]
    [Range(-1f, 1f)]
    public float projectileApproachDotThreshold = 0.35f;

    [Tooltip("Minimum projectile speed to consider.")]
    public float projectileMinSpeed = 1.0f;

    [Tooltip("How long AI holds crouch after detecting a head projectile.")]
    public float crouchHoldTime = 0.55f;

    [Header("Corner behavior")]
    [Tooltip("Radius around opponent to check for wall colliders. If opponent is cornered, AI steps back a little.")]
    public float wallCheckRadius = 0.65f;

    [Header("Post-attack spacing")]
    [Tooltip("Chance to step back briefly after an attack when close.")]
    [Range(0f, 1f)]
    public float afterAttackBackstepChance = 0.28f;

    [Tooltip("Chance (0..1) to try blocking when opponent is attacking and in range.")]
    [Range(0f, 1f)]
    public float blockReactionChance = 0.65f;

    [Tooltip("Chance (0..1) to use special when in range and not exhausted.")]
    [Range(0f, 1f)]
    public float specialChance = 0.25f;

    [Header("Bus Escape")]
    [Tooltip("Enable AI usage of bus escape mechanic.")]
    public bool allowBusEscape = true;
    [Tooltip("Health percentage threshold to prefer bus usage.")]
    [Range(0f, 1f)]
    public float busLowHealthThreshold = 0.4f;
    [Tooltip("Fatigue percentage threshold for Babushka to prefer bus usage.")]
    [Range(0f, 1f)]
    public float busHighFatigueThreshold = 0.75f;
    [Tooltip("Random chance per check to use bus even without urgent condition.")]
    [Range(0f, 1f)]
    public float busRandomEscapeChance = 0.28f;
    [Tooltip("How often AI evaluates bus usage while cornered (seconds).")]
    public float busCheckInterval = 0.2f;
    [Tooltip("Minimum seconds between successful/attempted bus requests.")]
    public float busAttemptCooldown = 0.35f;

    private bool _isActing;
    private float _nextDecisionTime;
    private Vector2 _smoothedMove;
    private float _retreatUntilTime;
    private float _nextRetreatAllowedTime;
    private float _crouchUntilTime;
    private BusCornerRevealController _busController;
    private float _nextBusCheckTime;
    private float _nextBusAttemptTime;
    private Vector3 _lastSelfPos;
    private float _postBusReorientUntil;

private void Reset()
    {
        // Auto-assign PlayerController on this GameObject if possible.
        if (self == null)
        {
            self = GetComponent<PlayerController>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        // Auto-assign strategy based on character name
        if (strategy == null && self != null)
        {
            AutoAssignStrategy();
        }
    }

    private void AutoAssignStrategy()
    {
        if (self == null) return;

        string characterName = self.characterName.ToLower();

        if (characterName.Contains("babushka"))
        {
            strategy = GetComponent<BabushkaAIStrategy>();
            if (strategy == null)
            {
                strategy = gameObject.AddComponent<BabushkaAIStrategy>();
            }
        }
        else if (characterName.Contains("cop"))
        {
            strategy = GetComponent<MyCopAIStrategy>();
            if (strategy == null)
            {
                strategy = gameObject.AddComponent<MyCopAIStrategy>();
            }
        }

        // Initialize strategy references
        if (strategy != null)
        {
            strategy.aiController = this;
            strategy.self = self;
            strategy.opponent = opponent;
        }
    }

private void Update()
    {
        if (playerInput != null)
        {
            // Prevent human input from fighting with AI.
            playerInput.enabled = !aiEnabled;
        }

        if (!aiEnabled || self == null || opponent == null)
        {
            if (self != null)
            {
                self.AI_StopMove();
                self.AI_StopBlock();
            }
            return;
        }

        if (self.IsDead() || opponent.IsDead())
        {
            self.AI_StopMove();
            return;
        }

        DetectBusRelocationAndResetIntent();

        // Initialize strategy if not done yet
        if (strategy != null && (strategy.aiController == null || strategy.self == null))
        {
            strategy.aiController = this;
            strategy.self = self;
            strategy.opponent = opponent;
        }

        // Update character-specific strategy logic
        if (strategy != null)
        {
            strategy.UpdateStrategy();
        }

        if (allowBusEscape)
        {
            UpdateBusEscape();
        }

        UpdateThreatResponses();
        UpdateMovement();

        if (_isActing)
        {
            return;
        }

        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + decisionCooldown;
            StartCoroutine(DecideAction());
        }
    }

    private void UpdateBusEscape()
    {
        if (Time.time < _nextBusCheckTime)
        {
            return;
        }
        _nextBusCheckTime = Time.time + Mathf.Max(0.05f, busCheckInterval);

        if (_busController == null)
        {
            _busController = FindAnyObjectByType<BusCornerRevealController>();
            if (_busController == null)
            {
                return;
            }
        }

        if (_busController.IsTransportInProgress)
        {
            return;
        }

        if (Time.time < _nextBusAttemptTime)
        {
            return;
        }

        if (!IsSelfCorneredAndPressured())
        {
            return;
        }

        bool shouldUseBus = false;
        bool urgentCondition = false;

        float health01 = Mathf.Clamp01(self.GetHealth() / 100f);
        if (health01 <= busLowHealthThreshold)
        {
            shouldUseBus = true;
            urgentCondition = true;
        }

        if (self.isCursed)
        {
            shouldUseBus = true;
            urgentCondition = true;
        }

        if (self.fatigueSystem != null && self.characterName.ToLower().Contains("babushka"))
        {
            float fatigue01 = self.fatigueSystem.maxFatigue > 0f
                ? Mathf.Clamp01(self.fatigueSystem.currentFatigue / self.fatigueSystem.maxFatigue)
                : 0f;
            if (fatigue01 >= busHighFatigueThreshold)
            {
                shouldUseBus = true;
                urgentCondition = true;
            }
        }

        if (!shouldUseBus && Random.value < busRandomEscapeChance)
        {
            shouldUseBus = true;
        }

        if (!shouldUseBus)
        {
            return;
        }

        bool requested = _busController.AI_RequestBoarding(self.transform);
        float cooldown = urgentCondition ? 0.1f : Mathf.Max(0.1f, busAttemptCooldown);
        _nextBusAttemptTime = Time.time + cooldown;

        if (requested)
        {
            // Stop conflicting action intent when boarding starts.
            self.AI_StopMove();
            self.AI_StopBlock();
            _retreatUntilTime = 0f;
        }
    }

    private bool IsSelfCorneredAndPressured()
    {
        Vector3 toOpponent = opponent.transform.position - self.transform.position;
        toOpponent.y = 0f;
        float distance = toOpponent.magnitude;
        if (distance > 3.2f)
        {
            return false;
        }

        bool nearLeft = self.transform.position.x <= -8.5f;
        bool nearRight = self.transform.position.x >= 8.5f;
        if (!nearLeft && !nearRight)
        {
            return false;
        }

        if (nearLeft && opponent.transform.position.x <= self.transform.position.x)
        {
            return false;
        }

        if (nearRight && opponent.transform.position.x >= self.transform.position.x)
        {
            return false;
        }

        return true;
    }

    private void UpdateThreatResponses()
    {
        // 1) Projectile avoidance: if a "head" projectile is approaching, crouch.
        if (DetectIncomingHeadProjectile())
        {
            _crouchUntilTime = Mathf.Max(_crouchUntilTime, Time.time + crouchHoldTime);
        }

        if (Time.time < _crouchUntilTime)
        {
            self.AI_StopBlock();
            self.AI_StopMove();
            self.AI_StartCrouch();
        }
        else
        {
            // Only release crouch if AI isn't retreating for spacing.
            self.AI_StopCrouch();
        }

        // 2) If opponent is cornered (touching wall) and we are very close, step back a bit.
        if (IsOpponentCornered() && Time.time >= _nextRetreatAllowedTime)
        {
            float distance = Vector3.Distance(
                new Vector3(self.transform.position.x, 0f, self.transform.position.z),
                new Vector3(opponent.transform.position.x, 0f, opponent.transform.position.z)
            );

            if (distance <= preferredDistance + 0.25f)
            {
                _nextRetreatAllowedTime = Time.time + Mathf.Max(0.8f, retreatCooldown * 0.6f);
                _retreatUntilTime = Time.time + Random.Range(0.25f, 0.55f);
            }
        }
    }

private void UpdateMovement()
    {
        // Compute horizontal delta on XZ plane
        Vector3 toOpponent = opponent.transform.position - self.transform.position;
        toOpponent.y = 0f;
        float distance = toOpponent.magnitude;

        Vector2 desiredMove = Vector2.zero;
        Vector3 dir3 = distance > 0.0001f ? (toOpponent / distance) : Vector3.zero;

        // Immediately after bus drop-off, re-orient by approaching opponent
        // and cancel any stale retreat intent from pre-transport state.
        if (Time.time < _postBusReorientUntil)
        {
            if (distance > 0.0001f)
            {
                desiredMove = new Vector2(dir3.x, dir3.z);
            }
            else
            {
                desiredMove = Vector2.zero;
            }
        }
        // If crouching to dodge projectile, don't move.
        else
        if (self.isCrouching)
        {
            desiredMove = Vector2.zero;
        }
        // Retreat overrides spacing temporarily.
        else if (Time.time < _retreatUntilTime)
        {
            desiredMove = new Vector2(-dir3.x, -dir3.z);
        }
        else
        {
            // Get preferred distance from strategy if available
            float targetDistance = (strategy != null) 
                ? strategy.GetPreferredDistance() 
                : preferredDistance;

            float tooFar = targetDistance + distanceDeadZone;
            float tooClose = targetDistance - distanceDeadZone;

            if (distance > tooFar)
            {
                desiredMove = new Vector2(dir3.x, dir3.z);
            }
            else if (distance < tooClose)
            {
                desiredMove = new Vector2(-dir3.x, -dir3.z);
            }
            else
            {
                desiredMove = Vector2.zero;
            }
        }

        float t = 1f - Mathf.Exp(-moveSmoothing * Time.deltaTime);
        _smoothedMove = Vector2.Lerp(_smoothedMove, desiredMove, t);
        self.AI_SetMove(_smoothedMove);
    }

    private void DetectBusRelocationAndResetIntent()
    {
        if (self == null)
        {
            return;
        }

        if (_lastSelfPos == Vector3.zero)
        {
            _lastSelfPos = self.transform.position;
            return;
        }

        // Bus transfer causes a large position jump in one frame.
        float deltaX = Mathf.Abs(self.transform.position.x - _lastSelfPos.x);
        if (deltaX >= 4.0f)
        {
            _retreatUntilTime = 0f;
            _nextRetreatAllowedTime = Time.time + 0.7f;
            _crouchUntilTime = 0f;
            _smoothedMove = Vector2.zero;
            _postBusReorientUntil = Time.time + 0.8f;
            self.AI_StopBlock();
            self.AI_StopCrouch();
        }

        _lastSelfPos = self.transform.position;
    }

private IEnumerator DecideAction()
    {
        _isActing = true;

        Vector3 toOpponent = opponent.transform.position - self.transform.position;
        toOpponent.y = 0f;
        float distance = toOpponent.magnitude;

        // If we are currently ducking a projectile, don't try to do anything else.
        if (Time.time < _crouchUntilTime)
        {
            _isActing = false;
            yield break;
        }

        // Check if strategy wants to handle this decision
        if (strategy != null)
        {
            bool strategyHandled = strategy.DecideAction(distance);
            if (strategyHandled)
            {
                // Wait a moment before allowing next action
                yield return new WaitForSeconds(Mathf.Max(0.15f, decisionCooldown * 0.85f));
                _isActing = false;
                yield break;
            }
        }

        // Strategy didn't handle it, use default AI behavior
        bool opponentAttacking = false;
        bool opponentSpecial = false;
        if (opponent.animator != null)
        {
            opponentAttacking = opponent.animator.GetBool("Attack");
            opponentSpecial = opponent.animator.GetBool("special");
        }

        // Get attack range from strategy if available
        float effectiveAttackRange = (strategy != null) 
            ? strategy.GetPreferredAttackRange() 
            : attackRange;

        // Check if strategy wants to force retreat
        bool nearOpponent = distance <= effectiveAttackRange * 1.15f;
        bool shouldRetreat = (strategy != null) 
            ? strategy.ShouldRetreat(distance, self.GetHealth(), opponent.GetHealth())
            : false;

        if (shouldRetreat || (nearOpponent && Time.time >= _nextRetreatAllowedTime && Random.value < retreatChance))
        {
            _nextRetreatAllowedTime = Time.time + retreatCooldown;
            _retreatUntilTime = Time.time + Random.Range(retreatMinDuration, retreatMaxDuration);
            yield return new WaitForSeconds(0.1f);
            _isActing = false;
            yield break;
        }

        // Defensive reaction
        float blockChance = blockReactionChance;
        if (!opponentAttacking && !opponentSpecial)
        {
            blockChance *= 0.25f;
        }
        else
        {
            blockChance = Mathf.Clamp01(blockChance + 0.2f);
        }

        if (nearOpponent && Random.value < blockChance)
        {
            self.AI_StartBlock();
            yield return new WaitForSeconds(Mathf.Max(0.12f, decisionCooldown * 0.6f));
            self.AI_StopBlock();
            _isActing = false;
            yield break;
        }

        // Attack decision if in range
        if (distance <= effectiveAttackRange)
        {
            float roll = Random.value;
            bool canUseSpecial = self.specialAbility != null;

            // Get special chance from strategy
            float specialChanceModified = (strategy != null)
                ? strategy.GetSpecialAbilityChance(distance, self.GetHealth(), opponent.GetHealth())
                : specialChance;

            if (canUseSpecial && roll < specialChanceModified)
            {
                self.AI_SpecialAttack();
                yield return new WaitForSeconds(Mathf.Max(0.15f, decisionCooldown));
            }
            else
            {
                // Mix high/low attacks
                bool opponentLow = opponent.isCrouching;
                if (opponentLow)
                {
                    self.AI_UpperAttack();
                }
                else
                {
                    if (roll < 0.5f)
                    {
                        self.AI_LowerAttack();
                    }
                    else
                    {
                        self.AI_UpperAttack();
                    }
                }

                yield return new WaitForSeconds(Mathf.Max(0.15f, decisionCooldown * 0.85f));
            }

            // After an attack, sometimes step back briefly
            if (distance <= preferredDistance + 0.1f && Time.time >= _nextRetreatAllowedTime && Random.value < afterAttackBackstepChance)
            {
                _nextRetreatAllowedTime = Time.time + Mathf.Max(0.8f, retreatCooldown * 0.6f);
                _retreatUntilTime = Time.time + Random.Range(0.2f, 0.45f);
            }
        }

        _isActing = false;
    }

    private bool DetectIncomingHeadProjectile()
    {
        Vector3 center = self.transform.position;
        Collider[] hits = Physics.OverlapSphere(center, projectileDetectRadius);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        float headY = center.y + headThreatHeightOffset;
        float minSpeedSqr = projectileMinSpeed * projectileMinSpeed;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null)
            {
                continue;
            }

            // Filter by tag (supports typo tag too).
            if (!(c.CompareTag(projectileTag1) || c.CompareTag(projectileTag2)))
            {
                continue;
            }

            Rigidbody rb = c.attachedRigidbody != null ? c.attachedRigidbody : c.GetComponent<Rigidbody>();
            if (rb == null)
            {
                continue;
            }

            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude < minSpeedSqr)
            {
                continue;
            }

            Vector3 toSelf = center - c.transform.position;
            toSelf.y = 0f;
            if (toSelf.sqrMagnitude < 0.01f)
            {
                continue;
            }

            float dot = Vector3.Dot(v.normalized, toSelf.normalized);
            if (dot < projectileApproachDotThreshold)
            {
                continue;
            }

            // Only duck if projectile is "high" (likely head hit).
            if (c.transform.position.y >= headY)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOpponentCornered()
    {
        Vector3 pos = opponent.transform.position;
        Collider[] hits = Physics.OverlapSphere(pos, wallCheckRadius);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c != null && c.CompareTag("wall"))
            {
                return true;
            }
        }

        return false;
    }
}

