// 18.12.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using System.Collections;

public class MyCopSpecialAbility : SpecialAbilityBase
{
    public float kickForce = 10f; // Сила удара, задается в инспекторе
    public Animator animator; // Аниматор для MyCop
    private const string TakeBigDamageParam = "TakeBigDamage";

    public override void TriggerSpecialAbility()
    {
        UseSpecialAbility();
    }

    private void UseSpecialAbility()
    {
        // Запуск анимации специальной атаки
        animator.SetTrigger("special");

        // Поиск игроков в радиусе действия способности
        Collider[] hitPlayers = Physics.OverlapSphere(transform.position, 1f); // Настройте радиус действия
        foreach (Collider player in hitPlayers)
        {
            if (player.CompareTag("Player") && player.gameObject != this.gameObject) // Убедитесь, что это не тот же игрок
            {
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                PlayerController playerController = player.GetComponent<PlayerController>();
                Animator playerAnimator = player.GetComponent<Animator>();

                if (playerRb != null && playerController != null && playerAnimator != null)
                {
                    // Вычисление направления отталкивания (от MyCop к противнику)
                    Vector3 backwardDirection = (player.transform.position - transform.position).normalized;
                    backwardDirection.y = 0; // Убираем вертикальную составляющую

                    playerRb.linearVelocity = Vector3.zero; // Сброс текущей скорости
                    playerRb.AddForce(backwardDirection * kickForce, ForceMode.Impulse);

                    // Установка состояния анимации TakeBigDamage
                    if (playerController.take_big_damage != null)
                    {
                        playerController.PlayAnimation(playerController.take_big_damage);
                    }

                    if (HasBoolParameter(playerAnimator, TakeBigDamageParam))
                    {
                        playerAnimator.SetBool(TakeBigDamageParam, true);
                    }

                    playerAnimator.SetFloat("Speed", 0);

                    // Запуск корутины для сброса состояния анимации после ее завершения
                    float duration = 2f;
                    if (playerController.take_big_damage != null)
                    {
                        duration += playerController.take_big_damage.length;
                    }
                    StartCoroutine(ResetDamageState(playerAnimator, duration));

                    // Уменьшение здоровья противника
                    ReduceHealth(playerController);
                }
            }
        }
    }

    private IEnumerator ResetDamageState(Animator playerAnimator, float animationDuration)
    {
        // Ожидание завершения анимации take_big_damage
        yield return new WaitForSeconds(animationDuration);

        // Сброс состояния TakeBigDamage
        if (HasBoolParameter(playerAnimator, TakeBigDamageParam))
        {
            playerAnimator.SetBool(TakeBigDamageParam, false);
        }
    }

    private static bool HasBoolParameter(Animator animator, string name)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
            {
                return true;
            }
        }

        return false;
    }

    private void ReduceHealth(PlayerController playerController)
    {
        // Делим текущее здоровье на 2
        playerController.health /= 2;

        // Проверяем, чтобы здоровье не стало отрицательным
        if (playerController.health < 0.5f)
        {
            playerController.health = 0;
        }

        Debug.Log($"Текущее здоровье персонажа после атаки: {playerController.health}");
    }
}