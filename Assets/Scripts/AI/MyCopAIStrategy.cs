// 19.02.2026 AI-Tag
// AI Strategy for MyCop character

using UnityEngine;
using System.Collections;

/// <summary>
/// MyCop's AI strategy focuses on:
/// - Aggressive close-range combat
/// - Using devastating kick special when close
/// - High aggression when opponent is low HP
/// </summary>
public class MyCopAIStrategy : AIStrategy
{
    [Header("MyCop-Specific Settings")]
    [Tooltip("Preferred close combat range")]
    public float closeRangeDistance = 1.5f;

    [Tooltip("Distance at which kick special is most effective")]
    public float kickOptimalDistance = 1.3f;

    [Tooltip("Special ability chance when in optimal kick range")]
    [Range(0f, 1f)]
    public float kickRangeSpecialChance = 0.55f;

    [Tooltip("Health threshold below which MyCop becomes more aggressive")]
    [Range(0f, 100f)]
    public float aggressiveHealthThreshold = 40f;

    [Tooltip("Opponent health threshold to trigger finishing aggression")]
    [Range(0f, 100f)]
    public float finisherHealthThreshold = 30f;

    [Tooltip("Chance to use special as finisher when opponent is low")]
    [Range(0f, 1f)]
    public float finisherSpecialChance = 0.75f;

    public override void UpdateStrategy()
    {
        // MyCop doesn't need continuous monitoring like Babushka
        // Could add combo tracking or aggression building here
    }

    public override bool DecideAction(float distanceToOpponent)
    {
        float myHealth = self.GetHealth();
        float opponentHealth = opponent.GetHealth();

        // FINISHER MODE: Opponent is very low, go for the kill
        if (opponentHealth <= finisherHealthThreshold)
        {
            if (distanceToOpponent <= kickOptimalDistance * 1.2f)
            {
                // High chance to use devastating kick
                if (Random.value < finisherSpecialChance)
                {
                    self.AI_SpecialAttack();
                    return true;
                }
            }

            // Rush in with attacks
            if (distanceToOpponent <= aiController.attackRange)
            {
                // Aggressive combo
                float finisherRoll = Random.value;
                if (finisherRoll < 0.35f)
                {
                    self.AI_KickAttack();
                }
                else if (finisherRoll < 0.8f)
                {
                    self.AI_UpperAttack(); // Headshots for maximum damage
                }
                else
                {
                    self.AI_LowerAttack();
                }
                return true;
            }
        }

        // OPTIMAL KICK RANGE: Use special frequently
        if (distanceToOpponent <= kickOptimalDistance && distanceToOpponent >= kickOptimalDistance * 0.7f)
        {
            if (Random.value < kickRangeSpecialChance)
            {
                self.AI_SpecialAttack();
                return true;
            }
        }

        // CLOSE COMBAT: MyCop's specialty
        if (distanceToOpponent <= closeRangeDistance)
        {
            bool opponentLow = opponent.isCrouching;
            
            // Counter opponent's stance
            if (opponentLow)
            {
                self.AI_UpperAttack(); // Hit crouching opponent
            }
            else
            {
                // Mix up attacks, favor upper for more damage
                float meleeRoll = Random.value;
                if (meleeRoll < 0.25f)
                {
                    self.AI_KickAttack();
                }
                else if (meleeRoll < 0.7f)
                {
                    self.AI_UpperAttack();
                }
                else
                {
                    self.AI_LowerAttack();
                }
            }
            return true;
        }

        // AGGRESSIVE MODE: Low HP makes MyCop fight harder
        if (myHealth <= aggressiveHealthThreshold)
        {
            if (distanceToOpponent <= aiController.attackRange)
            {
                // More aggressive, less blocking
                if (Random.value < 0.8f)
                {
                    self.AI_UpperAttack();
                }
                else
                {
                    StartCoroutine(PerformQuickBlock(0.2f));
                }
                return true;
            }
        }

        // DEFENSIVE STANCE: Only when opponent is attacking
        if (distanceToOpponent <= aiController.attackRange * 0.9f && IsOpponentAttacking())
        {
            if (Random.value < 0.4f) // Less blocking than Babushka
            {
                StartCoroutine(PerformQuickBlock(0.3f));
                return true;
            }
        }

        // Let base AI handle this situation
        return false;
    }

    public override float GetPreferredDistance()
    {
        float opponentHealth = opponent != null ? opponent.GetHealth() : 100f;

        // When opponent is low, close the distance
        if (opponentHealth <= finisherHealthThreshold)
        {
            return closeRangeDistance * 0.8f;
        }

        // Normal close-range preference
        return closeRangeDistance;
    }

    public override float GetPreferredAttackRange()
    {
        // Slightly extended for kicks
        return kickOptimalDistance * 1.2f;
    }

    public override float GetSpecialAbilityChance(float distanceToOpponent, float currentHealth, float opponentHealth)
    {
        // Finisher situation
        if (opponentHealth <= finisherHealthThreshold)
        {
            return finisherSpecialChance;
        }

        // Optimal kick range
        if (distanceToOpponent <= kickOptimalDistance && distanceToOpponent >= kickOptimalDistance * 0.7f)
        {
            return kickRangeSpecialChance;
        }

        // Too far, don't waste kick
        if (distanceToOpponent > kickOptimalDistance * 1.5f)
        {
            return 0.1f;
        }

        // Default
        return aiController.specialChance;
    }

    public override bool ShouldRetreat(float distanceToOpponent, float currentHealth, float opponentHealth)
    {
        // MyCop rarely retreats - he's aggressive
        
        // Only retreat if very low health and opponent is not
        if (currentHealth < 20f && opponentHealth > 50f)
        {
            return Random.value < 0.3f; // 30% chance
        }

        // Almost never retreat when opponent is low
        if (opponentHealth <= finisherHealthThreshold)
        {
            return false;
        }

        return false;
    }

    private IEnumerator PerformQuickBlock(float duration)
    {
        self.AI_StartBlock();
        yield return new WaitForSeconds(duration);
        self.AI_StopBlock();
    }
}
