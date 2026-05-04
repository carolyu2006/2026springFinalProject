using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private string endSceneName = "EndScene";
    [SerializeField] private string englishEndSceneName = "EnglishEndScene";

    // The winner when game ends
    public static int WinnerPlayerIndex = -1; // -1 = draw
    public static int[] FinalOrangeCounts = new int[0];

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
    /// Called by CircularCountdown when time runs out.
    /// Whoever has the most oranges wins; tie = draw.
    /// </summary>
    public void OnTimeUp()
    {
        if (gameOver) return;
        gameOver = true;

        WinnerPlayerIndex = -1;
        if (players != null && players.Length > 0)
        {
            FinalOrangeCounts = new int[players.Length];
            int bestCount = -1;
            int bestIndex = -1;
            bool tied = false;
            for (int i = 0; i < players.Length; i++)
            {
                int count = players[i] != null ? players[i].OrangeCount : 0;
                FinalOrangeCounts[i] = count;
                if (count > bestCount) { bestCount = count; bestIndex = players[i].PlayerIndex; tied = false; }
                else if (count == bestCount) { tied = true; }
            }
            WinnerPlayerIndex = (bestCount > 0 && !tied) ? bestIndex : -1;
        }

        if (WebSocketClient.Instance != null)
        {
            WebSocketClient.Instance.Send("{\"type\":\"game_ended\"}");
        }

        SceneManager.LoadScene(ResolveEndSceneName());
    }

    private string ResolveEndSceneName()
    {
        bool english = GameConfig.Instance != null && GameConfig.Instance.Language == GameLanguage.English;
        return english && !string.IsNullOrEmpty(englishEndSceneName) ? englishEndSceneName : endSceneName;
    }

    public bool IsGameOver()
    {
        return gameOver;
    }
}
