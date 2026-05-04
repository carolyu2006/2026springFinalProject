using TMPro;
using UnityEngine;

public class GameSceneHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text player1ScoreText;
    [SerializeField] private TMP_Text player2ScoreText;
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;

    [SerializeField] private string scoreFormat = "x{0}";
    [SerializeField] private string player1Fallback = "Player 1";
    [SerializeField] private string player2Fallback = "Player 2";

    private void Start()
    {
        ApplyName(player1NameText, 0, player1Fallback);
        ApplyName(player2NameText, 1, player2Fallback);
        RefreshScores();
    }

    private void Update()
    {
        RefreshScores();
    }

    private void RefreshScores()
    {
        var players = GameManager.Instance != null ? GameManager.Instance.GetPlayers() : null;
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            TMP_Text label = players[i].PlayerIndex == 0 ? player1ScoreText : player2ScoreText;
            if (label != null) label.text = string.Format(scoreFormat, players[i].OrangeCount);
        }
    }

    private static void ApplyName(TMP_Text label, int slot, string fallback)
    {
        if (label == null) return;
        string name = null;
        var cfg = GameConfig.Instance;
        if (cfg != null && cfg.PlayerNames != null && slot < cfg.PlayerNames.Length)
            name = cfg.PlayerNames[slot];
        label.text = string.IsNullOrEmpty(name) ? fallback : name;
    }
}
