// 27.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using System.Collections;

public class CanvasShake : MonoBehaviour
{
    [Header("Shake Settings")]
    public float shakeDuration = 0.5f; // Duration of the shake
    public float shakeMagnitude = 10f; // Magnitude of the shake

    private Vector3 originalPosition; // Original position of the canvas
    private bool isShaking = false;

    private void Awake()
    {
        originalPosition = transform.localPosition; // Store the original position of the canvas
    }

    private void OnEnable()
    {
        isShaking = true;
        StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        float elapsedTime = 0f;

        while (elapsedTime < shakeDuration)
        {
            float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
            float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);

            transform.localPosition = originalPosition + new Vector3(offsetX, offsetY, 0);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition; // Reset to original position
        isShaking = false;
        enabled = false; // Disable the script after shaking instead of deactivating the GameObject
    }
}