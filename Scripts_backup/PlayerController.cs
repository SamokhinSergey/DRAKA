// 28.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Reaction after attacking")]
    public int attack_delay;

    [Header("Character name")]
    public string characterName;

    [Header("Player Settings")]
    public float health = 100f;
    public float attackDamage = 25f;
    public float groinDamage = 20f;
    public float headDamage = 50f;


    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [Header("Animation Settings")]
    public Animator animator;
    public AnimationClip walkAnimation;
    public AnimationClip idle;
    public AnimationClip jumpAnimation;
    public AnimationClip crouchAnimation;
    public AnimationClip upperAttackAnimation;
    public AnimationClip lowerAttackAnimation;
    public AnimationClip crouchAttackAnimation;
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

    private bool hasTakeBigDamageParameter = false;

    [Header("Round End Flags")]
    [Tooltip("Set to true if this character died because fatigue reached 100%.")]
    public bool diedByInfarction = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
    }

    private void Update()
    {
        HandleMovement();

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

        if (isAttacking || isBlocking || isCrouching || isTakingDamage || isDead)
        {
            return; // Restrict movement if the player is performing an action or is dead
        }

        Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.World);

        if (moveInput.magnitude > 0)
        {
            animator.SetFloat("Speed", moveInput.magnitude); // Update Speed based on movement magnitude
            PlayAnimation(walkAnimation);

            if (fatigueSystem != null)
            {
                fatigueSystem.OnWalk(Time.deltaTime);
            }
        }
        else
        {
            animator.SetFloat("Speed", 0); // Reset Speed to 0 when movement stops
            PlayAnimation(idle);

            if (fatigueSystem != null)
            {
                fatigueSystem.OnIdle(Time.deltaTime);
            }
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

    private IEnumerator PerformAttack(AnimationClip clip, string attackType)
    {
        animator.SetBool("Attack", true);
        isAttacking = true;

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

        // Play attack sound
        PlaySound(attackSound);

        yield return new WaitForSeconds(clip.length / attack_delay);
        ApplyAttackDamage(attackType);

        yield return new WaitForSeconds(clip.length / 2);

        isAttacking = false;
        animator.SetBool("Attack", false);
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
                        otherPlayer.ApplyDamage("head", "upper");
                    }
                    else if (attackType == "lower" || attackType == "crouch")
                    {
                        otherPlayer.ApplyDamage("groin", attackType);
                    }
                }
            }
        }
    }

    public void ApplyDamage(string hitArea, string attackType)
    {
        float specialAttackCoef = 1f;
        if (attackType == "special")
        {
            specialAttackCoef = 3f;
        }

        if (isTakingDamage || isDead) return;

        // Play damage sound
        PlaySound(damageSound);

        if (isBlocking)
        {
            if (isCrouching && (attackType == "lower" || attackType == "crouch"))
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
            health -= groinDamage * specialAttackCoef;
            animator.SetBool("GroinShot", true);
            StartCoroutine(PlayDamageAnimation(groinShotAnimation, "GroinShot"));
        }
        else if (hitArea == "head")
        {
            health -= headDamage * specialAttackCoef;
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

        if (hitPosition.y > transform.position.y + 1f) // Adjust the threshold for "head" hit
        {
            ApplyDamage("head", "special");
        }
        else
        {
            ApplyDamage("groin", "special");
        }

        // Destroy the damage object after collision
        Destroy(damageObject);
    }

    private bool IsActionRestricted()
    {
        return isAttacking || isBlocking || isCrouching || isTakingDamage || isDead;
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
            StartCoroutine(PerformAttack(crouchAttackAnimation, "crouch"));
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

        StartCoroutine(PerformAttack(lowerAttackAnimation, "lower"));
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
