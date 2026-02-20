using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Forces UI navigation to work by manually processing input and selecting buttons.
/// Attach this to RoundMenu GameObject.
/// </summary>
public class ForceUINavigation : MonoBehaviour
{
    [Header("References")]
    public Button[] buttons;
    public EventSystem eventSystem;
    
    private int currentIndex = 0;
    private float nextInputTime = 0f;
    private float inputCooldown = 0.2f;
    
    private void OnEnable()
    {
        currentIndex = 0;
        nextInputTime = Time.time;
        
        // Ensure we have buttons
        if (buttons == null || buttons.Length == 0)
        {
            buttons = GetComponentsInChildren<Button>(true);
        }
        
        // Get EventSystem if not assigned
        if (eventSystem == null)
        {
            eventSystem = EventSystem.current;
        }
        
        // Select first button
        if (buttons.Length > 0 && buttons[0] != null)
        {
            SelectButton(0);
        }
        
        Debug.Log($"[ForceUINavigation] Enabled with {buttons.Length} buttons");
    }
    
    private void Update()
    {
        if (buttons == null || buttons.Length == 0) return;
        if (Time.time < nextInputTime) return;
        
        bool inputDetected = false;
        int direction = 0;
        
        // Check keyboard input
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            {
                direction = -1;
                inputDetected = true;
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                direction = 1;
                inputDetected = true;
            }
            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ClickCurrentButton();
                inputDetected = true;
            }
        }
        
        // Check gamepad input
        if (Gamepad.current != null)
        {
            var dpad = Gamepad.current.dpad;
            var leftStick = Gamepad.current.leftStick;
            
            if (dpad.up.wasPressedThisFrame || leftStick.up.wasPressedThisFrame)
            {
                direction = -1;
                inputDetected = true;
            }
            else if (dpad.down.wasPressedThisFrame || leftStick.down.wasPressedThisFrame)
            {
                direction = 1;
                inputDetected = true;
            }
            else if (Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                ClickCurrentButton();
                inputDetected = true;
            }
        }
        
        // Navigate
        if (direction != 0)
        {
            currentIndex += direction;
            if (currentIndex < 0) currentIndex = buttons.Length - 1;
            if (currentIndex >= buttons.Length) currentIndex = 0;
            
            SelectButton(currentIndex);
            nextInputTime = Time.time + inputCooldown;
        }
        else if (inputDetected)
        {
            nextInputTime = Time.time + inputCooldown;
        }
    }
    
    private void SelectButton(int index)
    {
        if (index < 0 || index >= buttons.Length) return;
        
        var button = buttons[index];
        if (button == null || !button.interactable) return;
        
        // Visual selection
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(button.gameObject);
        }
        
        // Highlight the button
        button.Select();
        
        Debug.Log($"[ForceUINavigation] Selected button {index}: {button.name}");
    }
    
    private void ClickCurrentButton()
    {
        if (currentIndex < 0 || currentIndex >= buttons.Length) return;
        
        var button = buttons[currentIndex];
        if (button != null && button.interactable)
        {
            Debug.Log($"[ForceUINavigation] Clicking button: {button.name}");
            button.onClick.Invoke();
        }
    }
}