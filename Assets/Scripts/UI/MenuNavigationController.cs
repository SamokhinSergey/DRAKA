using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Comprehensive menu navigation controller that handles all input conflicts.
/// This script completely takes over input when menu is active.
/// Attach to RoundMenu GameObject.
/// </summary>
public class MenuNavigationController : MonoBehaviour
{
    [Header("Button References")]
    [Tooltip("Assign all menu buttons in order (top to bottom)")]
    public Button[] menuButtons;
    
    [Header("Settings")]
    [Tooltip("Time between navigation inputs")]
    public float navigationCooldown = 0.15f;
    
    [Tooltip("Automatically find buttons if not assigned")]
    public bool autoFindButtons = true;
    
    private int selectedIndex = 0;
    private float lastNavigationTime;
    private bool isActive = false;
    
    private void OnEnable()
    {
        StartCoroutine(InitializeMenu());
    }
    
    private IEnumerator InitializeMenu()
    {
        // Wait a frame for menu to fully activate
        yield return null;
        
        // Auto-find buttons if needed
        if (autoFindButtons && (menuButtons == null || menuButtons.Length == 0))
        {
            menuButtons = GetComponentsInChildren<Button>(true);
            Debug.Log($"[MenuNavigation] Auto-found {menuButtons.Length} buttons");
        }
        
        // Disable all competing input sources
        DisableCompetingInputs();
        
        // Select first button
        selectedIndex = 0;
        SelectCurrentButton();
        
        isActive = true;
        
        Debug.Log("[MenuNavigation] Menu navigation initialized and active");
    }
    
    private void OnDisable()
    {
        isActive = false;
        Debug.Log("[MenuNavigation] Menu navigation disabled");
    }
    
    private void Update()
    {
        if (!isActive || menuButtons == null || menuButtons.Length == 0)
            return;
        
        // Handle navigation input
        HandleNavigationInput();
        
        // Handle submit input
        HandleSubmitInput();
    }
    
    private void HandleNavigationInput()
    {
        if (Time.time - lastNavigationTime < navigationCooldown)
            return;
        
        int direction = 0;
        
        // Keyboard navigation
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                direction = -1;
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                direction = 1;
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                direction = -1;
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                direction = 1;
        }
        
        // Gamepad navigation
        var gamepad = Gamepad.current;
        if (gamepad != null && direction == 0)
        {
            // D-pad
            if (gamepad.dpad.up.wasPressedThisFrame)
                direction = -1;
            else if (gamepad.dpad.down.wasPressedThisFrame)
                direction = 1;
            else if (gamepad.dpad.left.wasPressedThisFrame)
                direction = -1;
            else if (gamepad.dpad.right.wasPressedThisFrame)
                direction = 1;
            
            // Left stick
            var stick = gamepad.leftStick.ReadValue();
            if (Mathf.Abs(stick.y) > 0.5f || Mathf.Abs(stick.x) > 0.5f)
            {
                if (stick.y > 0.5f)
                    direction = -1;
                else if (stick.y < -0.5f)
                    direction = 1;
                else if (stick.x < -0.5f)
                    direction = -1;
                else if (stick.x > 0.5f)
                    direction = 1;
            }
        }
        
        // Apply navigation
        if (direction != 0)
        {
            Navigate(direction);
            lastNavigationTime = Time.time;
        }
    }
    
    private void HandleSubmitInput()
    {
        bool submit = false;
        
        // Keyboard submit
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.enterKey.wasPressedThisFrame || 
                keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                submit = true;
            }
        }
        
        // Gamepad submit
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame || // A/Cross
                gamepad.buttonEast.wasPressedThisFrame ||  // B/Circle (also common for submit)
                gamepad.startButton.wasPressedThisFrame)
            {
                submit = true;
            }
        }
        
        if (submit)
        {
            ClickCurrentButton();
        }
    }
    
    private void Navigate(int direction)
    {
        // Find next valid button
        int attempts = 0;
        int newIndex = selectedIndex;
        
        do
        {
            newIndex += direction;
            
            // Wrap around
            if (newIndex < 0)
                newIndex = menuButtons.Length - 1;
            else if (newIndex >= menuButtons.Length)
                newIndex = 0;
            
            attempts++;
            
            // Check if button is valid
            if (menuButtons[newIndex] != null && 
                menuButtons[newIndex].interactable && 
                menuButtons[newIndex].gameObject.activeInHierarchy)
            {
                selectedIndex = newIndex;
                SelectCurrentButton();
                break;
            }
            
        } while (attempts < menuButtons.Length);
    }
    
    private void SelectCurrentButton()
    {
        if (selectedIndex < 0 || selectedIndex >= menuButtons.Length)
            return;
        
        var button = menuButtons[selectedIndex];
        if (button == null) return;
        
        // Clear previous selection
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        
        // Select button
        button.Select();
        
        // Also set via EventSystem
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
        
        Debug.Log($"[MenuNavigation] Selected: {button.name} (index {selectedIndex})");
    }
    
    private void ClickCurrentButton()
    {
        if (selectedIndex < 0 || selectedIndex >= menuButtons.Length)
            return;
        
        var button = menuButtons[selectedIndex];
        if (button != null && button.interactable)
        {
            Debug.Log($"[MenuNavigation] Clicking: {button.name}");
            button.onClick.Invoke();
        }
    }
    
    private void DisableCompetingInputs()
    {
        // Find all PlayerInput components in scene
        var playerInputs = FindObjectsOfType<PlayerInput>();
        foreach (var pi in playerInputs)
        {
            if (pi.enabled)
            {
                pi.enabled = false;
                Debug.Log($"[MenuNavigation] Disabled PlayerInput on {pi.gameObject.name}");
            }
        }
        
        // Note: SceneController.anyKeyAction should be disabled by SceneController.EnableUINavigation()
    }
}