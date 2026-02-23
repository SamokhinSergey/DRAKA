using UnityEngine;
using System.Collections;

public class MyCopSpecialAbility : SpecialAbilityBase
{
    [Header("Kick Settings")]
    public float kickForce = 18f;
    public float spinSpeed = 720f;
    public float attackRadius = 1.3f;

    [Header("References")]
    public Animator animator;

    [Header("Impact Feedback")]
    public AudioClip highKickSound;
    [Range(0f, 1f)] public float highKickVolume = 1f;
    [Range(0.75f, 1.5f)] public float highKickPitch = 1.08f;
    [Range(0f, 1f)] public float highKickDistortion = 0.86f;
    public float highKickDistortionDuration = 0.22f;
    public float impactShakeScale = 1f;
    public float impactGlitchScale = 1f;

    private const string TakeBigDamageParam = "TakeBigDamage";
    private const string HighKickAssetPath = "Assets/Sounds/high_kick.mp3";

    private DynamicCameraController cameraController;
    private AudioSource impactAudioSource;
    private AudioDistortionFilter impactDistortionFilter;
    private Coroutine resetDistortionCoroutine;
    private PlayerController selfPlayerController;

    private void Awake()
    {
        selfPlayerController = GetComponent<PlayerController>();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cameraController = cam.GetComponent<DynamicCameraController>();

            impactAudioSource = cam.GetComponent<AudioSource>();
            if (impactAudioSource == null)
            {
                impactAudioSource = cam.gameObject.AddComponent<AudioSource>();
                impactAudioSource.playOnAwake = false;
                impactAudioSource.spatialBlend = 0f;
            }

            impactDistortionFilter = cam.GetComponent<AudioDistortionFilter>();
            if (impactDistortionFilter == null)
            {
                impactDistortionFilter = cam.gameObject.AddComponent<AudioDistortionFilter>();
            }
            impactDistortionFilter.enabled = false;
        }

#if UNITY_EDITOR
        if (highKickSound == null)
        {
            highKickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(HighKickAssetPath);
        }
#endif
    }

    public override void TriggerSpecialAbility()
    {
        StartCoroutine(ApplyKick());
    }

    private IEnumerator ApplyKick()
    {
        // Wait one frame so hit timing better matches the visible contact moment.
        yield return null;

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRadius);
        bool hitApplied = false;

        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player") || col.gameObject == gameObject) continue;

            Rigidbody rb = col.GetComponent<Rigidbody>();
            PlayerController pc = col.GetComponent<PlayerController>();
            Animator anim = col.GetComponent<Animator>();

            if (rb == null || pc == null || anim == null) continue;

            Vector3 dir = (col.transform.position - transform.position);
            dir.y = 0f;
            dir.z = 0f;
            if (Mathf.Abs(dir.x) < 0.001f) dir.x = transform.forward.x >= 0 ? 1f : -1f;
            dir.Normalize();

            float lockedZ = col.transform.position.z;

            rb.linearVelocity = Vector3.zero;
            rb.AddForce(dir * kickForce, ForceMode.Impulse);

            StartCoroutine(SpinAndRecover(rb, pc, anim, lockedZ, dir));

            pc.ApplyDamage("head", "special", selfPlayerController);
            ScreenNotificationSystem.ShowForPlayer(pc, ScreenNotificationSystem.NotificationType.Healthsplit);
            hitApplied = true;
        }

        if (hitApplied)
        {
            PlayImpactFeedback();
        }
    }

    private IEnumerator SpinAndRecover(
        Rigidbody rb,
        PlayerController pc,
        Animator anim,
        float lockedZ,
        Vector3 dir)
    {
        float savedYRot = pc.transform.eulerAngles.y;

        if (HasBoolParameter(anim, TakeBigDamageParam))
            anim.SetBool(TakeBigDamageParam, true);
        anim.SetFloat("Speed", 0f);

        float spinSign = dir.x >= 0 ? 1f : -1f;

        float maxSpin = 2.5f;
        float spun = 0f;
        while (spun < maxSpin)
        {
            spun += Time.deltaTime;

            pc.transform.Rotate(0f, 0f, spinSign * spinSpeed * Time.deltaTime, Space.World);

            Vector3 pos = rb.position;
            pos.z = lockedZ;
            rb.position = pos;

            bool slowed = rb.linearVelocity.magnitude < 1.5f;
            if (slowed && spun > 0.2f) break;

            yield return null;
        }

        yield return new WaitForSeconds(0.25f);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        pc.transform.rotation = Quaternion.Euler(0f, savedYRot, 0f);

        Vector3 finalPos = rb.position;
        finalPos.z = lockedZ;
        rb.position = finalPos;

        if (HasBoolParameter(anim, TakeBigDamageParam))
            anim.SetBool(TakeBigDamageParam, false);

        if (pc.take_big_damage != null)
        {
            pc.PlayAnimation(pc.take_big_damage);
            yield return new WaitForSeconds(pc.take_big_damage.length);
        }

        if (HasBoolParameter(anim, TakeBigDamageParam))
            anim.SetBool(TakeBigDamageParam, false);
    }

    private static bool HasBoolParameter(Animator anim, string name)
    {
        if (anim == null) return false;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
                return true;
        return false;
    }

    private void PlayImpactFeedback()
    {
        if (cameraController == null && Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<DynamicCameraController>();
        }

        cameraController?.TriggerImpact(impactShakeScale, impactGlitchScale);

        if (impactAudioSource == null && Camera.main != null)
        {
            impactAudioSource = Camera.main.GetComponent<AudioSource>();
            if (impactAudioSource == null)
            {
                impactAudioSource = Camera.main.gameObject.AddComponent<AudioSource>();
                impactAudioSource.playOnAwake = false;
                impactAudioSource.spatialBlend = 0f;
            }
        }

        if (impactDistortionFilter == null && Camera.main != null)
        {
            impactDistortionFilter = Camera.main.GetComponent<AudioDistortionFilter>();
            if (impactDistortionFilter == null)
            {
                impactDistortionFilter = Camera.main.gameObject.AddComponent<AudioDistortionFilter>();
            }
        }

        if (impactAudioSource != null && highKickSound != null)
        {
            impactAudioSource.pitch = highKickPitch;
            impactAudioSource.PlayOneShot(highKickSound, highKickVolume);
        }

        if (impactDistortionFilter != null)
        {
            impactDistortionFilter.distortionLevel = highKickDistortion;
            impactDistortionFilter.enabled = true;

            if (resetDistortionCoroutine != null)
            {
                StopCoroutine(resetDistortionCoroutine);
            }
            resetDistortionCoroutine = StartCoroutine(ResetDistortionAfterDelay());
        }
    }

    private IEnumerator ResetDistortionAfterDelay()
    {
        yield return new WaitForSecondsRealtime(highKickDistortionDuration);
        if (impactDistortionFilter != null)
        {
            impactDistortionFilter.distortionLevel = 0f;
            impactDistortionFilter.enabled = false;
        }
    }
}
