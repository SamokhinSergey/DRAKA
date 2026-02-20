using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures Button Navigation is set to Automatic for menu navigation to work with keyboard/gamepad.
/// Attach this to the RoundMenu GameObject or run it via [ContextMenu] in editor.
/// </summary>
public class ButtonNavigationFixer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Automatically fix button navigation on Start")]
    public bool fixOnStart = true;
    
    [Tooltip("Apply to all buttons in children, not just this GameObject")]
    public bool includeChildren = true;

    private void Start()
    {
        if (fixOnStart)
        {
            FixButtonNavigation();
        }
    }

    [ContextMenu("Fix Button Navigation")]
    public void FixButtonNavigation()
    {
        Button[] buttons;
        
        if (includeChildren)
        {
            buttons = GetComponentsInChildren<Button>(true);
        }
        else
        {
            buttons = GetComponents<Button>();
        }

        int fixedCount = 0;
        
        foreach (var button in buttons)
        {
            var nav = button.navigation;
            
            // Check if navigation is disabled
            if (nav.mode == Navigation.Mode.None)
            {
                nav.mode = Navigation.Mode.Automatic;
                button.navigation = nav;
                fixedCount++;
                Debug.Log($"[ButtonNavigationFixer] Fixed navigation for button: {button.name}");
            }
        }
        
        if (fixedCount > 0)
        {
            Debug.Log($"[ButtonNavigationFixer] Fixed {fixedCount} button(s) with Navigation.Mode.None");
        }
        else
        {
            Debug.Log("[ButtonNavigationFixer] All buttons already have navigation enabled");
        }
    }
}