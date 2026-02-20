// 13.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;

namespace Ilumisoft.HealthSystem.UI
{
    [AddComponentMenu("Health System/UI/Healthbar")]
    public class Healthbar : MonoBehaviour
    {
        [field: SerializeField]
        public HealthComponent Health { get; set; }

        [SerializeField]
        Canvas canvas;

        [SerializeField]
        Image fillImage;

        [SerializeField, Tooltip("Whether the healthbar should be hidden when health is empty")]
        bool hideEmpty = false;

        [SerializeField, Tooltip("Makes the healthbar being aligned with the camera")]
        bool alignWithCamera = false;

        [SerializeField, Min(0.1f), Tooltip("Controls how fast changes will be animated in points/second")]
        float changeSpeed = 100;

        float currentValue;

        protected virtual void Reset()
        {
            if (Health == null)
            {
                Health = GetComponentInParent<HealthComponent>();
            }
        }

        private void Start()
        {
            if (Health != null)
            {
                currentValue = Health.CurrentHealth;
            }
            else
            {
                Debug.LogError("HealthComponent is not assigned to the Healthbar. Please assign it in the Inspector.");
            }
        }

        private void Update()
        {
            if (Health == null)
            {
                Debug.LogWarning("HealthComponent is not assigned to the Healthbar.");
                return;
            }

            if (alignWithCamera)
            {
                AlignWithCamera();
            }

            currentValue = Mathf.MoveTowards(currentValue, Health.CurrentHealth, Time.deltaTime * changeSpeed);

            UpdateFillbar();
            UpdateVisibility();
        }

        private void AlignWithCamera()
        {
            transform.forward = Camera.main.transform.forward;
        }

        void UpdateFillbar()
        {
            if (Health != null && fillImage != null)
            {
                // Update the fill amount
                float value = Mathf.InverseLerp(0, Health.MaxHealth, currentValue);
                fillImage.fillAmount = value;
            }
        }

        void UpdateVisibility()
        {
            if (canvas != null && fillImage != null)
            {
                float value = fillImage.fillAmount;

                // Hide if empty
                if (Mathf.Approximately(value, 0))
                {
                    if (hideEmpty && canvas.gameObject.activeSelf)
                    {
                        canvas.gameObject.SetActive(false);
                    }
                }
                // Make sure the canvas is enabled if health is not empty
                else if (value > 0 && !canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(true);
                }
            }
        }

        // New method to dynamically assign HealthComponent
        public void SetHealthComponent(HealthComponent healthComponent)
        {
            Health = healthComponent;
            currentValue = Health.CurrentHealth;
        }
    }
}