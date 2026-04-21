using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private string endSceneName = "EndScene";

    // Which player is currently carrying the orange (null = nobody)
    public Player Carrier { get; private set; }

    // The winner when game ends
    public static int WinnerPlayerIndex = -1; // -1 = draw

    private Player[] players;
    private bool gameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterPlayers(Player[] allPlayers)
    {
        players = allPlayers;
    }

    public Player[] GetPlayers()
    {
        return players;
    }

    /// <summary>
    /// Called when a player picks up the orange.
    /// </summary>
    public void SetCarrier(Player player)
    {
        Carrier = player;
    }

    /// <summary>
    /// Called when the carrier drops the orange (e.g. got punched).
    /// </summary>
    public void ClearCarrier()
    {
        Carrier = null;
    }

    /// <summary>
    /// Instant win: carrier punched the other player out of the crowd.
    /// </summary>
    public void InstantWin(Player winner)
    {
        if (gameOver) return;
        gameOver = true;

        WinnerPlayerIndex = winner.PlayerIndex;
        SceneManager.LoadScene(endSceneName);
    }

    /// <summary>
    /// Called by CircularCountdown when time runs out.
    /// The carrier wins; if no carrier, it's a draw.
    /// </summary>
    public void OnTimeUp()
    {
        if (gameOver) return;
        gameOver = true;

        if (Carrier != null)
        {
            WinnerPlayerIndex = Carrier.PlayerIndex;
        }
        else
        {
            WinnerPlayerIndex = -1;
        }

        SceneManager.LoadScene(endSceneName);
    }

    public bool IsGameOver()
    {
        return gameOver;
    }
}
