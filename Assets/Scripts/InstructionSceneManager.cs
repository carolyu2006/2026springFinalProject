using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InstructionSceneManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Cartoon";
    [SerializeField] private float displayDuration = 2f;

    private void Start()
    {
        StartCoroutine(LoadGameAfterDelay());
    }

    private IEnumerator LoadGameAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        SceneManager.LoadScene(nextSceneName);
    }
}
