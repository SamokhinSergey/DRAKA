// 19.02.2026 AI-Tag
// AI Strategy for Babushka character

using UnityEngine;
using System.Collections;

/// <summary>
/// Babushka's AI strategy focuses on:
/// - Using saliva projectile from safe distance
/// - Managing fatigue carefully (avoiding infarction)
/// - Keeping distance when fatigue is high
/// </summary>
public class BabushkaAIStrategy : AIStrategy
{
    [Header("Babushka-Specific Settings")]
    [Tooltip("Fatigue threshold above which Babushka becomes more cautious")]
    [Range(0f, 100f)]
    public float dangerousFatigueThreshold = 75f;

    [Tooltip("Fatigue threshold above which Babushka tries to avoid all combat")]
    [Range(0f, 100f)]
    public float criticalFatigueThreshold = 90f;

    [Tooltip("Preferred distance when using saliva special (projectile attack)")]
    public float salivaPreferredDistance = 3f;

    [Tooltip("Minimum distance for special ability (too close = don't use)")]
    public float salivaMinDistance = 2f;

    [Tooltip("Increased special ability chance when at good distance")]
    [Range(0f, 1f)]
    public float salivaRangeSpecialChance = 0.5f;

    private FatigueSystem fatigueSystem;

    private void Start()
    {
        // Get fatigue system reference
        if (self != null)
        {
            fatigueSystem = self.fatigueSystem;
        }
    }

public override void UpdateStrategy()
    {
        // Monitor fatigue every frame
        if (fatigueSystem == null) return;

        float fatigue = fatigueSystem.currentFatigue;
        float fatiguePercent = (fatigueSystem.maxFatigue > 0) 
            ? (fatigue / fatigueSystem.maxFatigue) * 100f 
            : 0f;

        // Debug info every second with more detail
        if (Time.frameCount % 60 == 0)
        {
            string mode = "Normal";
            if (fatiguePercent >= criticalFatigueThreshold)
                mode = "CRITICAL - Defense Only!";
            else if (fatiguePercent >= 70f)
                mode = "HIGH - No Specials!";
            else if (fatiguePercent >= dangerousFatigueThreshold)
                mode = "Cautious - Reduced Specials";
            
            Debug.Log($"[Babushka AI] Fatigue: {fatiguePercent:F1}% | Health: {self.GetHealth():F0} | Mode: {mode}");
        }
    }

public override bool DecideAction(float distanceToOpponent)
    {
        if (fatigueSystem == null) return false;

        float fatigue = fatigueSystem.currentFatigue;
        float fatiguePercent = (fatigueSystem.maxFatigue > 0) 
            ? (fatigue / fatigueSystem.maxFatigue) * 100f 
            : 0f;

        // CRITICAL FATIGUE: Avoid all combat, only defend
        if (fatiguePercent >= criticalFatigueThreshold)
        {
            if (distanceToOpponent < aiController.attackRange && IsOpponentAttacking())
            {
                // Only block when opponent is close and attacking
                StartCoroutine(PerformBlock(0.3f));
                return true;
            }
            // Otherwise just maintain distance (let base movement handle it)
            return false;
        }

        // HIGH FATIGUE (70%+): NO SPECIAL ATTACKS! They increase fatigue too much
        // Only use normal attacks and blocking
        if (fatiguePercent >= 70f)
        {
            // Be defensive - prefer blocking over attacking
            if (distanceToOpponent < salivaMinDistance)
            {
                if (Random.value < 0.6f)
                {
                    // Block more often when tired
                    StartCoroutine(PerformBlock(0.4f));
                    return true;
                }
                else
                {
                    // Occasional light attack (lower attacks preferred - less tiring)
                    self.AI_LowerAttack();
                    return true;
                }
            }
            // If not close, just maintain distance - don't use special!
            return false;
        }

        // MODERATE FATIGUE: Still use special but more carefully
        if (fatiguePercent >= dangerousFatigueThreshold)
        {
            // Only use saliva at good distance, but with lower chance
            if (distanceToOpponent >= salivaMinDistance && distanceToOpponent <= salivaPreferredDistance)
            {
                if (Random.value < 0.3f) // Reduced from 0.6f
                {
                    self.AI_SpecialAttack();
                    return true;
                }
            }

            // Be more defensive in close combat
            if (distanceToOpponent < salivaMinDistance)
            {
                if (Random.value < 0.5f)
                {
                    StartCoroutine(PerformBlock(0.4f));
                    return true;
                }
            }
        }

        // NORMAL FATIGUE (<75%): Use special from good distance
        if (distanceToOpponent >= salivaMinDistance && distanceToOpponent <= salivaPreferredDistance)
        {
            // Good range for saliva - use it freely
            if (Random.value < salivaRangeSpecialChance)
            {
                self.AI_SpecialAttack();
                return true;
            }
        }

        // Only use melee attacks if VERY close (closer than saliva min distance)
        if (distanceToOpponent < salivaMinDistance)
        {
            // Mix of attacks, slightly favor lower attacks (less exhausting)
            if (Random.value < 0.6f)
            {
                self.AI_LowerAttack();
            }
            else
            {
                self.AI_UpperAttack();
            }
            return true;
        }

        // If distance is between salivaPreferredDistance and attackRange,
        // don't handle it here - let base AI or movement handle repositioning
        return false;
    }

public override float GetPreferredDistance()
    {
        if (fatigueSystem == null) return base.GetPreferredDistance();

        float fatiguePercent = (fatigueSystem.maxFatigue > 0) 
            ? (fatigueSystem.currentFatigue / fatigueSystem.maxFatigue) * 100f 
            : 0f;

        // When very tired (90%+), keep maximum distance
        if (fatiguePercent >= criticalFatigueThreshold)
        {
            return salivaPreferredDistance + 1.0f;
        }
        // When moderately tired (70-90%), keep safe distance but not too far
        else if (fatiguePercent >= 70f)
        {
            return salivaPreferredDistance + 0.3f;
        }
        // When getting tired (75%+), prefer saliva distance
        else if (fatiguePercent >= dangerousFatigueThreshold)
        {
            return salivaPreferredDistance;
        }

        // Normal distance - slightly closer for occasional melee
        return salivaPreferredDistance - 0.5f;
    }

public override float GetSpecialAbilityChance(float distanceToOpponent, float currentHealth, float opponentHealth)
    {
        if (fatigueSystem == null) return 0f;

        float fatiguePercent = (fatigueSystem.maxFatigue > 0) 
            ? (fatigueSystem.currentFatigue / fatigueSystem.maxFatigue) * 100f 
            : 0f;

        // CRITICAL: Never use special when fatigue is 70% or higher
        if (fatiguePercent >= 70f)
        {
            return 0f;
        }

        // Don't use special if too close (saliva is ranged)
        if (distanceToOpponent < salivaMinDistance)
        {
            return 0f;
        }

        // In good range, chance depends on fatigue
        if (distanceToOpponent >= salivaMinDistance && distanceToOpponent <= salivaPreferredDistance)
        {
            // Reduce chance as fatigue increases
            if (fatiguePercent >= dangerousFatigueThreshold)
            {
                return salivaRangeSpecialChance * 0.5f; // Half chance when getting tired
            }
            return salivaRangeSpecialChance;
        }

        // Too far, very low chance
        return aiController.specialChance * 0.2f;
    }

    public override bool ShouldRetreat(float distanceToOpponent, float currentHealth, float opponentHealth)
    {
        if (fatigueSystem == null) return false;

        float fatiguePercent = (fatigueSystem.maxFatigue > 0) 
            ? (fatigueSystem.currentFatigue / fatigueSystem.maxFatigue) * 100f 
            : 0f;

        // Force retreat when fatigue is critical and opponent is close
        if (fatiguePercent >= criticalFatigueThreshold && distanceToOpponent < salivaMinDistance)
        {
            return true;
        }

        // More likely to retreat when tired
        if (fatiguePercent >= dangerousFatigueThreshold)
        {
            return Random.value < 0.4f; // 40% chance
        }

        return false;
    }

    private IEnumerator PerformBlock(float duration)
    {
        self.AI_StartBlock();
        yield return new WaitForSeconds(duration);
        self.AI_StopBlock();
    }
}
