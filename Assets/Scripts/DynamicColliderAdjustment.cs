// 26.11.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ColliderStateSettings
{
    public string stateName; // Название состояния (например, "Blocking" или "Crouching")
    
    public Vector3 colliderCenter; // Центр коллайдера
    public Vector3 colliderSize; // Размер коллайдера
}

public class DynamicColliderAdjustment : MonoBehaviour
{
    private BoxCollider boxCollider;
    private PlayerController playerController;

    [SerializeField]
    private List<ColliderStateSettings> colliderStates = new List<ColliderStateSettings>();

    private Vector3 defaultColliderSize;
    private Vector3 defaultColliderCenter;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        playerController = GetComponent<PlayerController>();

        if (boxCollider == null || playerController == null)
        {
            Debug.LogError("Missing required components: BoxCollider or PlayerController.");
            return;
        }

        // Установка начальных размеров коллайдера
        defaultColliderSize = boxCollider.size;
        defaultColliderCenter = boxCollider.center;

        // Добавление настроек по умолчанию, если список пуст
        if (colliderStates.Count == 0)
        {
            colliderStates.Add(new ColliderStateSettings
            {
                stateName = "Default",
                colliderSize = defaultColliderSize,
                colliderCenter = defaultColliderCenter
            });
        }
    }

    void Update()
    {
        AdjustColliderBasedOnState();
    }

    private void AdjustColliderBasedOnState()
    {
        if (playerController == null || boxCollider == null) return;

        // Проверка состояний игрока
        if (playerController.isBlocking)
        {
            ApplyColliderSettings("Blocking");
        }
        else if (playerController.isCrouching)
        {
            ApplyColliderSettings("Crouching");
        }
        else
        {
            ApplyColliderSettings("Default");
        }
    }

    private void ApplyColliderSettings(string stateName)
    {
        foreach (var state in colliderStates)
        {
            if (state.stateName == stateName)
            {
                boxCollider.size = state.colliderSize;
                boxCollider.center = state.colliderCenter;
                return;
            }
        }

        // Если состояние не найдено, используем настройки по умолчанию
        boxCollider.size = defaultColliderSize;
        boxCollider.center = defaultColliderCenter;
    }
}