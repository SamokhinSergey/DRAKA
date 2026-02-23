// 28.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Reaction after attacking")]
    public int attack_delay;
    [Header("Kick Timing")]
    [Tooltip("Normalized kick contact point (0..1 of kick animation).")]
    public float kickHitNormalized = 0.45f;
    [Tooltip("Fallback seconds from kick start to damage application when normalized timing is disabled.")]
    public float kickHitDelay = 0.2f;
    [Tooltip("Maximum kick attack duration in seconds.")]
    public float kickMaxDuration = 0.65f;

    [Header("Character name")]
    public string characterName;

    [Header("Player Settings")]
    public float health = 100f;
    public float attackDamage = 25f;
    public float groinDamage = 20f;
    public float headDamage = 50f;
    [Tooltip("Multiplier for special attacks.")]
    public float specialAttackMultiplier = 3f;


    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [Header("Facing Settings")]
    [Tooltip("If enabled, character rotates to always face the opponent.")]
    public bool autoFaceOpponent = true;
    [Tooltip("Minimum horizontal delta before flipping facing direction.")]
    public float facingDeadZone = 0.05f;

    [Header("Animation Settings")]
    public Animator animator;
    public AnimationClip walkAnimation;
    public AnimationClip idle;
    public AnimationClip jumpAnimation;
    public AnimationClip crouchAnimation;
    public AnimationClip upperAttackAnimation;
    public AnimationClip lowerAttackAnimation;
    public AnimationClip crouchAttackAnimation;
    public AnimationClip kickAttackAnimation;
    public AnimationClip upperBlockAnimation;
    public AnimationClip lowerBlockAnimation;
    public AnimationClip groinShotAnimation;
    public AnimationClip headShotAnimation;
    public AnimationClip deadAnimation;
    public AnimationClip specialAbilityAnimation; // Added for special ability animation
    public AnimationClip take_big_damage;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip winsSound;
    public AudioClip attackSound;
    public AudioClip damageSound;
    public AudioClip deathSound;
    public AudioClip specialAbilitySound; // Added for special ability sound
    public AudioClip take_damage_sound;
    public AudioClip take_big_damage_sound;

    [Header("Special Ability")]
    public SpecialAbilityBase specialAbility; // Reference to the base class for special abilities

    [Header("Fatigue Settings")]
    public FatigueSystem fatigueSystem; // Optional fatigue system (e.g. for Babushka)

    private Rigidbody rb;
    private Vector3 moveInput;
    private bool isGrounded = true;
    private bool isAttacking = false;
    public bool isCrouching = false;
    public bool isBlocking = false;
    private bool isTakingDamage = false;
    public bool isDead = false;

    // =====================
    // Curse System
    // =====================
    [Header("Curse Settings")]
    public bool isCursed = false;
    public float curseDuration = 10f;
    public float curseSpeedMultiplier = 0.5f; // 50% slowdown (half speed)
    public AudioClip cursedSound;

    [Tooltip("Assign: Healthbar/Canvas/Player1 or Player2 fill Image")]
    public Image healthbarFillImage;
    [Tooltip("Assign: status_time_p1 or status_time_p2 GameObject")]
    public GameObject statusTimerObject;

    private float _originalMoveSpeed;
    private Color _originalHealthbarColor;
    private bool _healthbarColorSaved = false;
    private Coroutine _curseCoroutine;
    private Transform _opponentTransform;
    private float _yawFacingRight;
    private float _yawFacingLeft;
    private PlayerInput _playerInput;


    private bool hasTakeBigDamageParameter = false;

    [Header("Round End Flags")]
    [Tooltip("Set to true if this character died because fatigue reached 100%.")]
    public bool diedByInfarction = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing! Please add a Rigidbody to the Player GameObject.");
        }

        if (audioSource == null)
        {
            Debug.LogError("AudioSource component is missing! Please add an AudioSource to the Player GameObject.");
        }

        if (specialAbility == null)
        {
            Debug.LogWarning("Special Ability script is not assigned! This player will not have a special ability.");
        }

        // Auto-find curse UI references if not assigned in Inspector
        if (healthbarFillImage == null || statusTimerObject == null)
        {
            // Determine player index by game object name
            string playerIndex = gameObject.name.Contains("1") ? "1" : "2";
            string fillName    = playerIndex == "1" ? "FillP1" : "Fill";
            string timerName   = "status_time_p" + playerIndex;

            // Search in the Healthbar canvas
            var allImages = FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None);
            foreach (var img in allImages)
            {
                if (img.name == fillName && healthbarFillImage == null)
                    healthbarFillImage = img;
            }
            var allGOs = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allGOs)
            {
                if (t.name == timerName && statusTimerObject == null)
                    statusTimerObject = t.gameObject;
            }
        }

        
// Some characters/controllers may not have this bool parameter.
        if (animator != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == "TakeBigDamage")
                {
                    hasTakeBigDamageParameter = true;
                    break;
                }
            }
        }

        InitializeFacingYaws();
    }

    private void Update()
    {
        HandleKickInput();
        HandleMovement();
        UpdateFacingDirection();

        if (health <= 0 && !isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// Called by FatigueSystem when fatigue reaches 100%.
    /// </summary>
    public void TriggerInfarction()
    {
        if (isDead)
        {
            return;
        }

        diedByInfarction = true;
        health = 0f;
        Die();
    }

    /// <summary>
    /// Reset per-round flags (call at round start).
    /// </summary>
    public void ResetRoundFlags()
    {
        diedByInfarction = false;
    }

    private void FixedUpdate()
    {
        // обработка, связанная со стенами вокруг арены
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.1f);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("wall"))
            {
                Vector3 closestPoint = collider.ClosestPoint(transform.position);
                Vector3 direction = transform.position - closestPoint;
                direction.y = 0; // игнорируем вертикальную составляющую

                // останавливаем, если зашли в стену
                rb.linearVelocity = Vector3.zero; // сбросить скорость
                rb.AddForce(direction.normalized * 1000f, ForceMode.VelocityChange);

                // слегка отталкиваем персонажа от стены
                transform.position = closestPoint + direction.normalized * 7f;
            }
        }
    }

private void HandleMovement()
    {
        // Check if the animator is in Fall or take_big_damage state using tags
        if (animator.GetBool("Fall") || (hasTakeBigDamageParameter && animator.GetBool("TakeBigDamage")))
        {
            Debug.Log("Player is in Fall or take_big_damage state, movement restricted.");
            return; // Restrict movement if the player is in Fall or take_big_damage state
        }

        // --- Fatigue tick (runs every frame regardless of action state) ---
        if (fatigueSystem != null && !isDead)
        {
            if (isBlocking)
                fatigueSystem.OnBlock(Time.deltaTime);
            else if (moveInput.magnitude > 0)
                fatigueSystem.OnWalk(Time.deltaTime);
            else
                fatigueSystem.OnIdle(Time.deltaTime);
        }

        if (isAttacking || isBlocking || isCrouching || isTakingDamage || isDead)
        {
            return; // Restrict movement if the player is performing an action or is dead
        }

        Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.World);

        if (moveInput.magnitude > 0)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
            PlayAnimation(walkAnimation);
        }
        else
        {
            animator.SetFloat("Speed", 0);
            PlayAnimation(idle);
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (animator.GetBool("Fall") || (hasTakeBigDamageParameter && animator.GetBool("TakeBigDamage")))
        {
            Debug.Log("Player is in Fall or take_big_damage state, movement input restricted.");
            moveInput = Vector3.zero; // Stop movement
            return;
        }
        if (!isBlocking && !isAttacking && !isCrouching && !isTakingDamage && !isDead)
        {
            moveInput = context.ReadValue<Vector2>();
            moveInput = new Vector3(moveInput.x, 0, moveInput.y);
        }
        else
        {
            moveInput = Vector3.zero; // Stop movement if the player is performing an action or is dead
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && isGrounded && !isAttacking && !isBlocking && !isCrouching && !isTakingDamage && !isDead)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            PlayAnimation(jumpAnimation);
            isGrounded = false;
        }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.started && !isAttacking && !isBlocking && !isTakingDamage && !isDead)
        {
            isCrouching = true;
            animator.SetFloat("Speed", 0);
            animator.SetBool("Crouch", true);
            PlayAnimation(crouchAnimation);
        }
        else if (context.canceled)
        {
            isCrouching = false;
            animator.SetBool("Crouch", false);
            animator.Play(idle.ToString());
        }
    }

    public void OnUpperAttack(InputAction.CallbackContext context)
    {
        if (context.started && !isAttacking && !isBlocking && !isTakingDamage && !isDead)
        {
            if (isCrouching)
            {
                StartCoroutine(PerformAttack(crouchAttackAnimation, "crouch"));
            }
            else
            {
                StartCoroutine(PerformAttack(upperAttackAnimation, "upper"));
            }
        }
    }

    public void OnLowerAttack(InputAction.CallbackContext context)
    {
        if (context.started && !isAttacking && !isBlocking && !isCrouching && !isTakingDamage && !isDead)
        {
            StartCoroutine(PerformAttack(lowerAttackAnimation, "lower"));
        }
    }

    public void OnKickAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            TryKickAttack();
        }
    }

    public void OnBlock(InputAction.CallbackContext context)
    {
        if (context.started && !IsActionRestricted())
        {
            isBlocking = true;
            animator.SetBool("block", true); // Enable the block state in the Animator
            AnimationClip blockClip = isCrouching ? lowerBlockAnimation : upperBlockAnimation;
            PlayAnimation(blockClip);
        }
        else if (context.canceled)
        {
            isBlocking = false;
            animator.SetBool("block", false); // Disable the block state in the Animator
            PlayAnimation(isCrouching ? crouchAnimation : idle); // Immediately transition to Idle or Crouch animation
        }
    }

    public void OnSpecialAttack(InputAction.CallbackContext context)
    {
        if (context.started && specialAbility != null && !isAttacking && !isBlocking && !isCrouching && !isTakingDamage && !isDead)
        {
            // Stop movement and set attacking state
            moveInput = Vector3.zero;
            isAttacking = true;

            // Trigger animation and sound for special ability
        //  animator.SetBool("special", true);
            PlayAnimation(specialAbilityAnimation);
            PlaySound(specialAbilitySound);

            if (fatigueSystem != null)
            {
                fatigueSystem.OnSpecialAttack();
            }

            // Trigger the specific special ability logic
            //specialAbility.TriggerSpecialAbility();

            StartCoroutine(PerformSpecialAttack(specialAbilityAnimation.length));
        }
    }

    private IEnumerator PerformSpecialAttack(float duration)
    {
        if (specialAbility == null)
        {
            isAttacking = false;
            yield break;
        }

        yield return new WaitForSeconds(duration / 2); // Wait for the animation duration
        specialAbility.TriggerSpecialAbility();
        yield return new WaitForSeconds(duration / 2);
        animator.SetBool("special", false); // Reset the special bool
        isAttacking = false; // Reset attacking state
    }

    private void HandleKickInput()
    {
        if (!CanStartKickAttack())
        {
            return;
        }

        if (kickAttackAnimation == null)
        {
            return;
        }

        // Ignore raw device input for inactive PlayerInput owners (e.g. AI-controlled players).
        if (_playerInput != null && !_playerInput.inputIsActive)
        {
            return;
        }

        bool isPlayer1 = gameObject.name == "Player1";
        bool isPlayer2 = gameObject.name == "Player2";

        // Input ownership split to avoid cross-triggering on shared devices:
        // Player1 kick -> Gamepad A, Player2 kick -> Right Ctrl.
        bool keyboardKick = isPlayer2 && Keyboard.current != null && Keyboard.current.rightCtrlKey.wasPressedThisFrame;
        bool gamepadKick = isPlayer1 && Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        if (keyboardKick || gamepadKick)
        {
            TryKickAttack();
        }
    }

    private bool CanStartKickAttack()
    {
        return !isAttacking && !isBlocking && !isCrouching && !isTakingDamage && !isDead;
    }

    private void TryKickAttack()
    {
        if (!CanStartKickAttack())
        {
            return;
        }

        AnimationClip clipToUse = kickAttackAnimation != null ? kickAttackAnimation : lowerAttackAnimation;
        StartCoroutine(PerformAttack(clipToUse, "kick"));
    }

private IEnumerator PerformAttack(AnimationClip clip, string attackType)
    {
        bool usesAttackBool = attackType != "kick";
        if (usesAttackBool)
        {
            animator.SetBool("Attack", true);
        }
        isAttacking = true;
        
        // IMPORTANT: If this is a crouch attack, ensure Crouch bool stays true
        bool wasCrouchingBeforeAttack = isCrouching;
        if (attackType == "crouch")
        {
            animator.SetBool("Crouch", true);
        }

        if (clip == null)
        {
            Debug.LogWarning($"Attack clip is missing for attackType='{attackType}' on {name}. Attack cancelled.");
            isAttacking = false;
            animator.SetBool("Attack", false);
            yield break;
        }

        if (fatigueSystem != null)
        {
            fatigueSystem.OnNormalAttack();
        }

        PlayAnimation(clip);
        PlaySound(attackSound);

        // Ждём момента удара: clip.length / attack_delay при ТЕКУЩЕЙ скорости аниматора.
        float effectiveDuration = clip.length;
        float hitThreshold;
        if (attackType == "kick")
        {
            effectiveDuration = Mathf.Min(clip.length, Mathf.Max(0.1f, kickMaxDuration));
            float kickHitTime;
            if (kickHitNormalized > 0f)
            {
                kickHitTime = Mathf.Clamp01(kickHitNormalized) * effectiveDuration;
            }
            else
            {
                kickHitTime = Mathf.Clamp(kickHitDelay, 0f, effectiveDuration);
            }
            hitThreshold = Mathf.Clamp01(kickHitTime / effectiveDuration);
        }
        else
        {
            hitThreshold = 1f / attack_delay;
        }
        float normalizedTime = 0f;
        while (normalizedTime < hitThreshold)
        {
            normalizedTime += Time.deltaTime * (animator != null && animator.speed > 0f ? animator.speed : 1f) / effectiveDuration;
            yield return null;
        }
        ApplyAttackDamage(attackType);

        // Ждём окончания анимации
        while (normalizedTime < 1f)
        {
            normalizedTime += Time.deltaTime * (animator != null && animator.speed > 0f ? animator.speed : 1f) / effectiveDuration;
            yield return null;
        }

        isAttacking = false;
        if (usesAttackBool)
        {
            animator.SetBool("Attack", false);
        }
        
        // After attack finishes, restore crouch state if we were crouching
        if (wasCrouchingBeforeAttack && isCrouching)
        {
            animator.SetBool("Crouch", true);
            PlayAnimation(crouchAnimation);
        }
    }

    private void ApplyAttackDamage(string attackType)
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1.5f);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.gameObject != gameObject)
            {
                PlayerController otherPlayer = hitCollider.GetComponent<PlayerController>();
                if (otherPlayer != null)
                {
                    if (attackType == "upper")
                    {
                        otherPlayer.ApplyDamage("head", "upper", this);
                    }
                    else if (attackType == "lower" || attackType == "crouch" || attackType == "kick")
                    {
                        otherPlayer.ApplyDamage("groin", attackType, this);
                    }
                }
            }
        }
    }

    public float GetDamageForAttack(string attackType, string hitArea)
    {
        switch (attackType)
        {
            case "upper":
                return headDamage;
            case "lower":
            case "crouch":
                return groinDamage;
            case "kick":
                return attackDamage;
            case "special":
            {
                float baseDamage = hitArea == "head" ? headDamage : groinDamage;
                return baseDamage * specialAttackMultiplier;
            }
            default:
                return attackDamage;
        }
    }

    public void ApplyDamage(string hitArea, string attackType, PlayerController attacker = null, float damageOverride = -1f)
    {
        float incomingDamage = damageOverride >= 0f
            ? damageOverride
            : (attacker != null
                ? attacker.GetDamageForAttack(attackType, hitArea)
                : GetDamageForAttack(attackType, hitArea));

        if (isTakingDamage || isDead) return;

        // Play damage sound
        PlaySound(damageSound);

        if (isBlocking)
        {
            if (isCrouching && (attackType == "lower" || attackType == "crouch" || attackType == "kick"))
            {
                Debug.Log("Lower or crouch attack bypasses upper block.");
            }
            else if (!isCrouching && attackType == "upper" && hitArea == "head")
            {
                Debug.Log("Upper block active, attack blocked.");
                return;
            }
            else
            {
                Debug.Log("Attack blocked.");
                return;
            }
        }

        if (isCrouching && hitArea == "head")
        {
            Debug.Log("Player is crouching, no damage taken from upper attack.");
            return;
        }

        isTakingDamage = true;

        if (hitArea == "groin")
        {
            health -= incomingDamage;
            animator.SetBool("GroinShot", true);
            StartCoroutine(PlayDamageAnimation(groinShotAnimation, "GroinShot"));
        }
        else if (hitArea == "head")
        {
            health -= incomingDamage;
            animator.SetBool("HeadShot", true);
            StartCoroutine(PlayDamageAnimation(headShotAnimation, "HeadShot"));
        }
    }

    private IEnumerator PlayDamageAnimation(AnimationClip clip, string animationParameter)
    {
        PlayAnimation(clip);

        yield return new WaitForSeconds(clip.length);

        animator.SetBool(animationParameter, false);
        isTakingDamage = false;

        if (isCrouching)
        {
            animator.Play(crouchAnimation.ToString());
        }
        else
        {
            if (!isDead)
                animator.Play(idle.ToString());
        }
    }

    public void PlayAnimation(AnimationClip clip)
    {
        if (animator == null || clip == null)
        {
            return;
        }

        // Death animation should always be allowed to play,
        // even if the character is already flagged as falling or taking big damage.
        if (clip != deadAnimation)
        {
            // Check if the animator is in Fall or take_big_damage state using tags
            if (animator.GetBool("Fall") || (hasTakeBigDamageParameter && animator.GetBool("TakeBigDamage")))
            {
                Debug.Log("Player is in Fall or take_big_damage state, animation change restricted.");
                return; // Prevent playing other animations if the player is in Fall or take_big_damage state
            }
        }

        // Avoid errors when the clip name is not a state in this Animator Controller.
        if (animator.HasState(0, Animator.StringToHash(clip.name)))
        {
            animator.Play(clip.name);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void Die()
    {
        isDead = true;

        // Play death sound
        PlaySound(deathSound);

        StartCoroutine(DisableAnimatorAfterAnimation());

        animator.SetTrigger("Fall");
        StartCoroutine(PlayDamageAnimation(deadAnimation, "Fall"));

        moveInput = Vector3.zero;
        rb.linearVelocity = Vector3.zero;

        isAttacking = false;
        isBlocking = false;
        isCrouching = false;
        isTakingDamage = false;
    }

    private IEnumerator DisableAnimatorAfterAnimation()
    {
        yield return new WaitForSeconds(deadAnimation.length);
        if (isDead)
        {
            animator.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("damage_object"))
        {
            HandleDamageObjectCollision(collision.gameObject);
        }
    }

    private void HandleDamageObjectCollision(GameObject damageObject)
    {
        // Determine the hit area based on the collision position
        Vector3 hitPosition = damageObject.transform.position;
        string hitArea = hitPosition.y > transform.position.y + 1f ? "head" : "groin";

        PlayerController attacker = null;
        SalivaProjectile salivaProjectile = damageObject.GetComponent<SalivaProjectile>();
        if (salivaProjectile != null && salivaProjectile.owner != null)
        {
            attacker = salivaProjectile.owner.GetComponent<PlayerController>();
        }

        ApplyDamage(hitArea, "special", attacker);

        // Destroy the damage object after collision
        Destroy(damageObject);
    }

    private bool IsActionRestricted()
    {
        return isAttacking || isBlocking || isCrouching || isTakingDamage || isDead;
    }

    private void InitializeFacingYaws()
    {
        float currentYaw = transform.eulerAngles.y;
        bool currentlyFacesRight = transform.forward.x >= 0f;

        if (currentlyFacesRight)
        {
            _yawFacingRight = currentYaw;
            _yawFacingLeft = Mathf.Repeat(currentYaw + 180f, 360f);
        }
        else
        {
            _yawFacingLeft = currentYaw;
            _yawFacingRight = Mathf.Repeat(currentYaw + 180f, 360f);
        }
    }

    private void UpdateFacingDirection()
    {
        if (!autoFaceOpponent || isDead)
        {
            return;
        }

        if (animator != null && (animator.GetBool("Fall") || (hasTakeBigDamageParameter && animator.GetBool("TakeBigDamage"))))
        {
            return;
        }

        if (_opponentTransform == null || !_opponentTransform.gameObject.activeInHierarchy)
        {
            FindOpponentTransform();
            if (_opponentTransform == null)
            {
                return;
            }
        }

        float dx = _opponentTransform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= facingDeadZone)
        {
            return;
        }

        float targetYaw = dx > 0f ? _yawFacingRight : _yawFacingLeft;
        Vector3 euler = transform.eulerAngles;
        euler.y = targetYaw;
        transform.eulerAngles = euler;
    }

    private void FindOpponentTransform()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].transform != transform)
            {
                _opponentTransform = players[i].transform;
                return;
            }
        }

        _opponentTransform = null;
    }

    // =========================
    // AI control helper methods
    // =========================

    /// <summary>
    /// Returns true if this character is currently dead.
    /// </summary>
    public bool IsDead()
    {
        return isDead;
    }

    /// <summary>
    /// Returns current health value.
    /// </summary>
    public float GetHealth()
    {
        return health;
    }

    /// <summary>
    /// Sets movement input from AI code (bypassing the Input System callbacks).
    /// </summary>
    public void AI_SetMove(Vector2 move)
    {
        if (IsActionRestricted())
        {
            moveInput = Vector3.zero;
            return;
        }

        moveInput = new Vector3(move.x, 0, move.y);
    }

    /// <summary>
    /// Stops movement immediately (used by AI).
    /// </summary>
    public void AI_StopMove()
    {
        moveInput = Vector3.zero;
    }

    /// <summary>
    /// Starts crouching (used by AI). Mirrors OnCrouch(started).
    /// </summary>
    public void AI_StartCrouch()
    {
        if (isDead || isTakingDamage || isAttacking || isBlocking)
        {
            return;
        }

        if (isCrouching)
        {
            return;
        }

        isCrouching = true;
        animator.SetFloat("Speed", 0);
        animator.SetBool("Crouch", true);
        PlayAnimation(crouchAnimation);
    }

    /// <summary>
    /// Stops crouching (used by AI). Mirrors OnCrouch(canceled).
    /// </summary>
    public void AI_StopCrouch()
    {
        // Do not force-exit crouch while an attack is being performed,
        // otherwise attack visuals can be overridden by idle.
        if (isAttacking)
        {
            return;
        }

        if (!isCrouching)
        {
            return;
        }

        isCrouching = false;
        animator.SetBool("Crouch", false);
        PlayAnimation(idle);
    }

    /// <summary>
    /// Triggers an upper attack from AI (standing or crouching).
    /// </summary>
    public void AI_UpperAttack()
    {
        if (IsActionRestricted())
        {
            return;
        }

        if (isCrouching)
        {
            StartCoroutine(AI_PerformCrouchAttack());
        }
        else
        {
            StartCoroutine(PerformAttack(upperAttackAnimation, "upper"));
        }
    }

    /// <summary>
    /// Triggers a lower attack from AI.
    /// </summary>
    public void AI_LowerAttack()
    {
        if (IsActionRestricted() || isCrouching)
        {
            return;
        }

        AnimationClip clipToUse = lowerAttackAnimation;
        if (!CanPlayClipOnAnimator(clipToUse))
        {
            // Fallback when dedicated lower-attack state is missing.
            clipToUse = CanPlayClipOnAnimator(upperAttackAnimation) ? upperAttackAnimation : clipToUse;
        }

        if (clipToUse != null)
        {
            StartCoroutine(PerformAttack(clipToUse, "lower"));
        }
    }

    private IEnumerator AI_PerformCrouchAttack()
    {
        if (isDead || isTakingDamage || isBlocking)
        {
            yield break;
        }

        // Ensure crouch pose is active for at least one frame before attack.
        isCrouching = true;
        animator.SetBool("Crouch", true);
        PlayAnimation(crouchAnimation);
        yield return null;

        AnimationClip clipToUse = crouchAttackAnimation;
        if (!CanPlayClipOnAnimator(clipToUse))
        {
            // Fallback if dedicated crouch-attack state is absent in Animator.
            clipToUse = lowerAttackAnimation != null ? lowerAttackAnimation : upperAttackAnimation;
        }

        if (clipToUse != null)
        {
            yield return StartCoroutine(PerformAttack(clipToUse, "crouch"));
        }
    }

    private bool CanPlayClipOnAnimator(AnimationClip clip)
    {
        if (animator == null || clip == null)
        {
            return false;
        }

        return animator.HasState(0, Animator.StringToHash(clip.name));
    }

    /// <summary>
    /// Triggers a block for AI. Call AI_StopBlock() to release.
    /// </summary>
    public void AI_StartBlock()
    {
        if (IsActionRestricted())
        {
            return;
        }

        isBlocking = true;
        animator.SetBool("block", true);
        AnimationClip blockClip = isCrouching ? lowerBlockAnimation : upperBlockAnimation;
        PlayAnimation(blockClip);
    }

    /// <summary>
    /// Releases block for AI.
    /// </summary>
    public void AI_StopBlock()
    {
        isBlocking = false;
        animator.SetBool("block", false);
        PlayAnimation(isCrouching ? crouchAnimation : idle);
    }

    /// <summary>
    /// Triggers a special attack from AI if available.
    /// </summary>
    // =====================
    // Curse Methods
    // =====================

    /// <summary>
    /// Apply curse to this player. If already cursed, does nothing.
    /// </summary>
    public void ApplyCurse()
    {
        if (isCursed || isDead) return;

        ScreenNotificationSystem.ShowForPlayer(this, "CURSE");

        if (_curseCoroutine != null)
            StopCoroutine(_curseCoroutine);

        _curseCoroutine = StartCoroutine(CurseRoutine());
    }

private IEnumerator CurseRoutine()
    {
        isCursed = true;

        // Save original speed and animator speed
        _originalMoveSpeed = moveSpeed;

        // Apply speed slowdown
        moveSpeed *= curseSpeedMultiplier;

        // Slow down attack animations (animator speed)
        if (animator != null)
            animator.speed = curseSpeedMultiplier;

        // Apply green tint to character renderers
        ApplyCurseTint(true);

        // Apply green color to healthbar fill
        if (healthbarFillImage != null)
        {
            if (!_healthbarColorSaved)
            {
                _originalHealthbarColor = healthbarFillImage.color;
                _healthbarColorSaved = true;
            }
            healthbarFillImage.color = Color.green;
        }

        // Show and start countdown timer
        if (statusTimerObject != null)
            statusTimerObject.SetActive(true);

        // Play curse sound
        if (cursedSound != null && audioSource != null)
            audioSource.PlayOneShot(cursedSound);

        // Countdown
        float remaining = curseDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            UpdateCurseTimerText(Mathf.CeilToInt(remaining));
            yield return null;
        }

        // Remove curse
        RemoveCurse();
    }

private void RemoveCurse()
    {
        isCursed = false;
        moveSpeed = _originalMoveSpeed;

        // Restore animator speed
        if (animator != null)
            animator.speed = 1f;

        // Remove green tint
        ApplyCurseTint(false);

        // Restore healthbar color
        if (healthbarFillImage != null && _healthbarColorSaved)
        {
            healthbarFillImage.color = _originalHealthbarColor;
        }

        // Hide timer
        if (statusTimerObject != null)
        {
            UpdateCurseTimerText(0);
            statusTimerObject.SetActive(false);
        }
    }

    private void UpdateCurseTimerText(int seconds)
    {
        if (statusTimerObject == null) return;
        var tmp = statusTimerObject.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
            tmp.text = seconds > 0 ? seconds.ToString() : "";
    }

    private void ApplyCurseTint(bool apply)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in renderers)
        {
            if (r == null || r.material == null) continue;
            if (apply)
            {
                // Store original and apply green tint (similar to BabushkaFatigueTint)
                Color original = r.material.color;
                r.material.color = Color.Lerp(original, new Color(0.3f, 1f, 0.3f, 1f), 0.5f);
            }
            else
            {
                // Restore to white (original will be re-set by BabushkaFatigueTint next frame)
                r.material.color = Color.white;
            }
        }
    }

    
public void AI_SpecialAttack()
    {
        if (specialAbility == null)
        {
            return;
        }

        if (IsActionRestricted() || isCrouching)
        {
            return;
        }

        // This mirrors OnSpecialAttack logic but without InputAction context.
        moveInput = Vector3.zero;
        isAttacking = true;

        PlayAnimation(specialAbilityAnimation);
        PlaySound(specialAbilitySound);

        if (fatigueSystem != null)
        {
            fatigueSystem.OnSpecialAttack();
        }

        StartCoroutine(PerformSpecialAttack(specialAbilityAnimation.length));
    }
}
