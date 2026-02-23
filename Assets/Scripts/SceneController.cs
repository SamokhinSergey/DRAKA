// 03.12.2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }
    public bool IsRoundActive { get; private set; }

    [Header("Scene Control Settings")]
    public PlayerController[] players;
    public GameObject winCanvas;
    public TextMeshProUGUI winPlayerText;
    public GameObject pressAnyKeyText;

    [Header("Start Canvas Settings")]
    public GameObject startCanvas;
    public TextMeshProUGUI startText;
    public AudioClip[] roundStartSounds;
    public AudioClip fightSound;
    public AudioClip infarctionSound;
    public AudioSource audioSource;
    public float minPitch = 0.85f;
    public float maxPitch = 1.15f;

    [Header("Round Menu Settings")]
    public GameObject roundMenu;
    public Button revancheButton;
    public Button firstSelectedButton;

    [Header("Score Settings")]
    public RawImage[] player1ScoreImages;
    public RawImage[] player2ScoreImages;

    private bool isGameOver = false;
    private bool canPressAnyKey = false;
    private bool bothPlayersAI = false;
    private Coroutine autoProgressCoroutine;

    private Vector3[] initialPositions;
    private InputAction anyKeyAction;
    private int currentRound = 1;
    private int player1Wins = 0;
    private int player2Wins = 0;

private void Start()
    {
        Instance = this;
        IsRoundActive = false;

        initialPositions = new Vector3[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            initialPositions[i] = players[i].transform.position;
            players[i].enabled = false;
            players[i].ResetRoundFlags();
        }

        // Check if both players are AI
        CheckBothPlayersAI();

        anyKeyAction = new InputAction("AnyKey", binding: "<Keyboard>/anyKey");
        anyKeyAction.performed += OnAnyKey;
        anyKeyAction.Enable();
        revancheButton.onClick.AddListener(RestartScene);

        StartCoroutine(StartRoundSequence());
    }

    private void CheckBothPlayersAI()
    {
        bothPlayersAI = true;
        foreach (var player in players)
        {
            var aiController = player.GetComponent<AIPlayerController>();
            if (aiController == null || !aiController.aiEnabled)
            {
                bothPlayersAI = false;
                break;
            }
        }
        Debug.Log($"[SceneController] Both players AI: {bothPlayersAI}");
    }

    private IEnumerator StartRoundSequence()
    {
        startCanvas.SetActive(true);
        startText.text = $"ROUND {currentRound}";
        PlaySound(roundStartSounds[currentRound - 1]);

        yield return new WaitForSeconds(1.5f);

        startText.text = "DRAKA";
        PlaySoundWithRandomPitch(fightSound);

        var canvasShake = startText.GetComponent<CanvasShake>();
        if (canvasShake != null)
        {
            canvasShake.enabled = true;
        }

        yield return new WaitForSeconds(1.5f);

        startCanvas.SetActive(false);
        ActivatePlayerInputs();
    }

    private void ActivatePlayerInputs()
    {
        IsRoundActive = true;
        foreach (var player in players)
        {
            player.enabled = true;
        }
    }

    private void DeactivatePlayerInputs()
    {
        IsRoundActive = false;
        foreach (var player in players)
        {
            player.enabled = false;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(clip);
        }
    }

    private void PlaySoundWithRandomPitch(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(clip);
        }
    }

    private void Update()
    {
        if (!isGameOver)
        {
            CheckPlayersHealth();
        }
    }

    private void CheckPlayersHealth()
    {
        int alivePlayers = 0;
        PlayerController winner = null;

        foreach (var player in players)
        {
            if (player.health > 0)
            {
                alivePlayers++;
                winner = player;
            }
            else if (!player.isDead)
            {
                player.isDead = true;
                player.enabled = false;
                var animator = player.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetTrigger("Fall");
                }
            }
        }

        if (alivePlayers == 1 && !isGameOver)
        {
            PlayerController loser = null;
            if (players != null && players.Length >= 2 && winner != null)
            {
                loser = (winner == players[0]) ? players[1] : players[0];
            }

            bool infarct = (loser != null && loser.diedByInfarction);
            StartCoroutine(HandleRoundEnd(winner, infarct));
        }
    }

private IEnumerator HandleRoundEnd(PlayerController winner, bool infarct)
    {
        isGameOver = true;
        IsRoundActive = false;

        // Deactivate player inputs
        DeactivatePlayerInputs();

        if (winner == players[0])
        {
            player1Wins++;
            if (player1Wins <= player1ScoreImages.Length)
            {
                player1ScoreImages[player1Wins - 1].gameObject.SetActive(true);
            }
        }
        else if (winner == players[1])
        {
            player2Wins++;
            if (player2Wins <= player2ScoreImages.Length)
            {
                player2ScoreImages[player2Wins - 1].gameObject.SetActive(true);
            }
        }

        if (player1Wins == 3 || player2Wins == 3)
        {
            // Wait for player animations to finish
            foreach (var player in players)
            {
                var animator = player.GetComponent<Animator>();
                if (animator != null && player.deadAnimation != null)
                {
                    yield return new WaitForSeconds(player.deadAnimation.length / 2);
                }
            }

            // Show WinCanvas with winner's message and sound
            winCanvas.SetActive(true);
            if (infarct)
            {
                winPlayerText.text = "myocardial infarction";
                PlaySound(infarctionSound);
            }
            else
            {
                winPlayerText.text = $"{winner.characterName} FUCKING WINS!";
                PlaySound(winner.winsSound);
            }

            yield return new WaitForSeconds(3f);

            // Hide WinCanvas and show RoundMenu
            winCanvas.SetActive(false);
            
            // Enable UI navigation (disable player inputs and anyKeyAction)
            EnableUINavigation();
            
            roundMenu.SetActive(true);

            // Wait for UI to initialize
            yield return new WaitForEndOfFrame();
            yield return null;
            
            // Set first button as selected
            if (EventSystem.current != null && firstSelectedButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
                Debug.Log($"[SceneController] Selected button: {firstSelectedButton.name}");
            }
            else
            {
                Debug.LogWarning("[SceneController] Cannot set selected button - EventSystem or firstSelectedButton is null!");
            }
        }
        else
        {
            yield return new WaitForSeconds(1f);

            winCanvas.SetActive(true);
            if (infarct)
            {
                winPlayerText.text = "myocardial infarction";
                PlaySound(infarctionSound);
            }
            else
            {
                winPlayerText.text = $" {winner.name} wins!";
            }
            canPressAnyKey = true;

            StartCoroutine(BlinkPressAnyKeyText());

            // If both players are AI, auto-progress after 5 seconds
            if (bothPlayersAI)
            {
                autoProgressCoroutine = StartCoroutine(AutoProgressToNextRound());
            }
        }
    }

    private IEnumerator AutoProgressToNextRound()
    {
        yield return new WaitForSeconds(5f);
        
        if (isGameOver && canPressAnyKey)
        {
            Debug.Log("[SceneController] Auto-progressing to next round (both players AI)");
            ResetGame();
        }
    }

    private IEnumerator BlinkPressAnyKeyText()
    {
        while (isGameOver)
        {
            pressAnyKeyText.SetActive(!pressAnyKeyText.activeSelf);
            yield return new WaitForSeconds(1f);
        }
    }

public void ResetGame()
    {
        if (isGameOver && canPressAnyKey)
        {
            IsRoundActive = false;
            // Stop auto-progress coroutine if it's running
            if (autoProgressCoroutine != null)
            {
                StopCoroutine(autoProgressCoroutine);
                autoProgressCoroutine = null;
            }

            for (int i = 0; i < players.Length; i++)
            {
                players[i].transform.position = initialPositions[i];
                players[i].health = 100;
                players[i].enabled = false;
                players[i].isDead = false;
                players[i].ResetRoundFlags();

                // Reset fatigue (if the character uses fatigue, e.g. Babushka)
                if (players[i].fatigueSystem != null)
                {
                    players[i].fatigueSystem.ResetFatigue();
                }

                var animator = players[i].GetComponent<Animator>();
                if (animator != null)
                {
                    animator.enabled = true;
                    animator.ResetTrigger("Fall");
                    animator.SetTrigger("Idle");
                }
            }

            winCanvas.SetActive(false);
            StopAllCoroutines();
            pressAnyKeyText.SetActive(false);

            isGameOver = false;
            canPressAnyKey = false;

            currentRound++;

            // Re-check if both players are AI (in case it changed)
            CheckBothPlayersAI();

            StartCoroutine(StartRoundSequence());
        }
    }

    private void OnAnyKey(InputAction.CallbackContext context)
    {
        if (context.started && isGameOver && canPressAnyKey)
        {
            ResetGame();
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (anyKeyAction != null)
        {
            anyKeyAction.Disable();
        }
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


private void EnableUINavigation()
    {
        // Disable anyKeyAction to prevent it from consuming input
        if (anyKeyAction != null && anyKeyAction.enabled)
        {
            anyKeyAction.Disable();
        }

        // Disable PlayerInput components on all players
        foreach (var player in players)
        {
            var pi = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null && pi.enabled)
            {
                pi.enabled = false;
            }
        }

        // Ensure EventSystem is enabled
        if (EventSystem.current != null)
        {
            EventSystem.current.enabled = true;
            
            // Ensure InputSystemUIInputModule is present and enabled
            var uiModule = EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiModule != null)
            {
                uiModule.enabled = true;
            }
        }
    }
}

