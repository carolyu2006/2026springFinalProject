using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class EndSceneManager : MonoBehaviour
{
    [SerializeField] private Button restartButton;
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private TextMeshProUGUI winnerText;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
    }

    private void Start()
    {
        if (winnerText != null)
        {
            int winner = GameManager.WinnerPlayerIndex;
            if (winner >= 0)
                winnerText.text = $"Player {winner + 1} Wins!";
            else
                winnerText.text = "Draw!";
        }
    }

    private void OnRestartClicked()
    {
        SceneManager.LoadScene(startSceneName);
    }
}
