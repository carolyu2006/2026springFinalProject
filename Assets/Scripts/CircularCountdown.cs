using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CircularCountdown : MonoBehaviour
{
    public Image fillImage;
    public AudioSource bgmSource;
    public string endSceneName = "EndScene";

    private float totalTime;
    private float currentTime;
    private bool gameEnded = false;

    void Start()
    {
        if (bgmSource != null && bgmSource.clip != null)
        {
            totalTime = bgmSource.clip.length;
            currentTime = totalTime;
            bgmSource.Play();
        }
    }

    void Update()
    {
        if (gameEnded) return;

        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            float fillAmount = 1 - (currentTime / totalTime);
            fillImage.fillAmount = fillAmount;
        }
        else
        {
            currentTime = 0;
            fillImage.fillAmount = 1f;
            gameEnded = true;

            if (bgmSource != null)
                bgmSource.Stop();

            // Let GameManager determine the winner before loading end scene
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTimeUp();
            }
            else
            {
                SceneManager.LoadScene(endSceneName);
            }
        }
    }
}
