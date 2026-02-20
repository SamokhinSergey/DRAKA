using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class MenuNavigationDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    
    [Header("References")]
    public EventSystem eventSystem;
    public InputSystemUIInputModule inputModule;
    public Button firstButton;
    
    private void Start()
    {
        if (eventSystem == null)
            eventSystem = EventSystem.current;
            
        if (inputModule == null && eventSystem != null)
            inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
    }
    
    private void Update()
    {
        if (!enableDebugLogs) return;
        
        if (Input.anyKeyDown || Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            LogUIState();
        }
    }
    
    [ContextMenu("Log UI State")]
    public void LogUIState()
    {
        Debug.Log("=== UI Navigation Debug ===");
        
        // EventSystem
        if (eventSystem == null)
        {
            Debug.LogError("EventSystem is NULL!");
        }
        else
        {
            Debug.Log($"EventSystem: {eventSystem.name}");
            Debug.Log($"EventSystem enabled: {eventSystem.enabled}");
            Debug.Log($"Current selected: {(eventSystem.currentSelectedGameObject ? eventSystem.currentSelectedGameObject.name : "NULL")}");
        }
        
        // InputSystemUIInputModule
        if (inputModule == null)
        {
            Debug.LogError("InputSystemUIInputModule is NULL!");
        }
        else
        {
            Debug.Log($"InputModule enabled: {inputModule.enabled}");
            Debug.Log($"InputModule actionsAsset: {(inputModule.actionsAsset ? inputModule.actionsAsset.name : "NULL")}");
        }
        
        // Button Navigation
        if (firstButton != null)
        {
            Debug.Log($"First button: {firstButton.name}");
            Debug.Log($"Button interactable: {firstButton.interactable}");
            Debug.Log($"Button navigation mode: {firstButton.navigation.mode}");
        }
        
        // Input Actions
        if (inputModule != null && inputModule.actionsAsset != null)
        {
            foreach (var map in inputModule.actionsAsset.actionMaps)
            {
                Debug.Log($"ActionMap '{map.name}' enabled: {map.enabled}");
                foreach (var action in map.actions)
                {
                    Debug.Log($"  Action '{action.name}' enabled: {action.enabled}");
                }
            }
        }
        
        Debug.Log("=========================");
    }
}