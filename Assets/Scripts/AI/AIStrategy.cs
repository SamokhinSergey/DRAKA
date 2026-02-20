// 19.02.2026 AI-Tag
// Base class for character-specific AI strategies

using UnityEngine;

/// <summary>
/// Abstract base class for AI strategies. Each character can have its own strategy implementation.
/// </summary>
public abstract class AIStrategy : MonoBehaviour
{
    [Header("Strategy References")]
    public AIPlayerController aiController;
    public PlayerController self;
    public PlayerController opponent;

    /// <summary>
    /// Called every decision tick to determine what action AI should take.
    /// Return true if strategy handled the decision, false to use default behavior.
    /// </summary>
    public abstract bool DecideAction(float distanceToOpponent);

    /// <summary>
    /// Called every frame to update character-specific logic (e.g., monitoring fatigue).
    /// </summary>
    public virtual void UpdateStrategy()
    {
        // Override in derived classes if needed
    }

    /// <summary>
    /// Called to determine preferred attack range for this character.
    /// </summary>
    public virtual float GetPreferredAttackRange()
    {
        return aiController.attackRange;
    }

    /// <summary>
    /// Called to determine preferred distance to maintain from opponent.
    /// </summary>
    public virtual float GetPreferredDistance()
    {
        return aiController.preferredDistance;
    }

    /// <summary>
    /// Called to modify special ability usage chance based on situation.
    /// </summary>
    public virtual float GetSpecialAbilityChance(float distanceToOpponent, float currentHealth, float opponentHealth)
    {
        return aiController.specialChance;
    }

    /// <summary>
    /// Called to determine if character should retreat in current situation.
    /// </summary>
    public virtual bool ShouldRetreat(float distanceToOpponent, float currentHealth, float opponentHealth)
    {
        // Default: use base AI retreat logic
        return false;
    }

    /// <summary>
    /// Helper method to check if opponent is attacking.
    /// </summary>
    protected bool IsOpponentAttacking()
    {
        if (opponent == null || opponent.animator == null) return false;
        return opponent.animator.GetBool("Attack") || opponent.animator.GetBool("special");
    }

    /// <summary>
    /// Helper method to safely start a coroutine action.
    /// </summary>
    protected void StartActionCoroutine(System.Collections.IEnumerator action)
    {
        StartCoroutine(action);
    }
}
