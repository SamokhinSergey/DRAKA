// 18.02.2026 v3: вращение через transform (не физика), Z зафиксирован, constraints не трогаем
using UnityEngine;
using System.Collections;

public class MyCopSpecialAbility : SpecialAbilityBase
{
    [Header("Kick Settings")]
    public float kickForce     = 18f;   // Горизонтальная сила отбрасывания
    public float spinSpeed     = 720f;  // Скорость визуального вращения (градусы/сек)
    public float attackRadius  = 1.3f;  // Радиус поражения

    [Header("References")]
    public Animator animator;           // Аниматор MyCop

    private const string TakeBigDamageParam = "TakeBigDamage";

    public override void TriggerSpecialAbility()
    {
        StartCoroutine(ApplyKick());
    }

    private IEnumerator ApplyKick()
    {
        yield return null; // 1 кадр — хит-бокс совпадает с визуальным ударом

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRadius);
        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player") || col.gameObject == gameObject) continue;

            Rigidbody        rb   = col.GetComponent<Rigidbody>();
            PlayerController pc   = col.GetComponent<PlayerController>();
            Animator         anim = col.GetComponent<Animator>();

            if (rb == null || pc == null || anim == null) continue;

            // Направление удара — только по X (арена 2.5D, Z фиксирован)
            Vector3 dir = (col.transform.position - transform.position);
            dir.y = 0f;
            dir.z = 0f; // двигаем ТОЛЬКО по X, Z не трогаем
            if (Mathf.Abs(dir.x) < 0.001f) dir.x = transform.forward.x >= 0 ? 1f : -1f;
            dir.Normalize();

            // Сохраняем Z позицию противника — он должен остаться в плоскости арены
            float lockedZ = col.transform.position.z;

            // Импульс только по X — constraints остаются нетронутыми
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(dir * kickForce, ForceMode.Impulse);

            // Запускаем визуальное вращение и восстановление
            StartCoroutine(SpinAndRecover(rb, pc, anim, lockedZ, dir));

            ReduceHealth(pc);
        }
    }

    /// <summary>
    /// Крутим персонажа визуально через transform.Rotate, удерживаем Z,
    /// ждём пока он остановится, затем восстанавливаем всё.
    /// </summary>
private IEnumerator SpinAndRecover(
        Rigidbody        rb,
        PlayerController pc,
        Animator         anim,
        float            lockedZ,
        Vector3          dir)
    {
        // Сохраняем правильный Y-поворот ДО кувырка, пока он ещё не испорчен
        float savedYRot = pc.transform.eulerAngles.y;

        // Сразу блокируем управление
        if (HasBoolParameter(anim, TakeBigDamageParam))
            anim.SetBool(TakeBigDamageParam, true);
        anim.SetFloat("Speed", 0f);

        // Знак вращения: кувыркается «вперёд» по направлению полёта
        float spinSign = dir.x >= 0 ? 1f : -1f;

        // Фаза 1: вращаем пока персонаж летит (скорость > порога)
        float maxSpin = 2.5f;
        float spun    = 0f;
        while (spun < maxSpin)
        {
            spun += Time.deltaTime;

            // Визуальное вращение вокруг Z (кувырок)
            pc.transform.Rotate(0f, 0f, spinSign * spinSpeed * Time.deltaTime, Space.World);

            // Жёстко фиксируем Z позицию — не даём уйти за плоскость арены
            Vector3 pos = rb.position;
            pos.z = lockedZ;
            rb.position = pos;

            // Останавливаем вращение когда скорость упала
            bool slowed = rb.linearVelocity.magnitude < 1.5f;
            if (slowed && spun > 0.2f) break;

            yield return null;
        }

        // Фаза 2: ещё небольшая пауза «лёжа»
        yield return new WaitForSeconds(0.25f);

        // Останавливаем физику
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Восстанавливаем ротацию используя Y сохранённый ДО кувырка
        pc.transform.rotation = Quaternion.Euler(0f, savedYRot, 0f);

        // Финальная фиксация Z
        Vector3 finalPos = rb.position;
        finalPos.z = lockedZ;
        rb.position = finalPos;

        // Сбрасываем анимационный флаг
        if (HasBoolParameter(anim, TakeBigDamageParam))
            anim.SetBool(TakeBigDamageParam, false);

        // Анимация вставания, если назначена
        if (pc.take_big_damage != null)
        {
            pc.PlayAnimation(pc.take_big_damage);
            yield return new WaitForSeconds(pc.take_big_damage.length);
        }

        // Финальный сброс флага
        if (HasBoolParameter(anim, TakeBigDamageParam))
            anim.SetBool(TakeBigDamageParam, false);
    }

    // ── Вспомогательные ───────────────────────────────────────────────────

    private static bool HasBoolParameter(Animator anim, string name)
    {
        if (anim == null) return false;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
                return true;
        return false;
    }

    private static void ReduceHealth(PlayerController pc)
    {
        pc.health /= 2;
        if (pc.health < 0.5f) pc.health = 0f;
        Debug.Log($"[MyCop] Здоровье после удара: {pc.health}");
    }
}
