using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the Control Select scene — lets the player choose
/// Keyboard, Phone, or ESP32 before the game starts.
///
/// Routes to a different scene based on the selected mode:
///   Keyboard / ESP32 → StartScene
///   Phone            → PhoneStartScene
/// </summary>
public class ControlSelectSceneManager : MonoBehaviour
{
    [SerializeField] private Button keyboardButton;
    [SerializeField] private Button phoneButton;
    [SerializeField] private Button esp32Button;

    [SerializeField] private string keyboardStartSceneName = "StartScene";
    [SerializeField] private string phoneStartSceneName    = "PhoneStartScene";
    [SerializeField] private string esp32StartSceneName    = "ArduinoStartScene";

    private void Awake()
    {
        GameConfig.EnsureExists();

        if (keyboardButton != null) keyboardButton.onClick.AddListener(() => SelectMode(ControlMode.Keyboard));
        if (phoneButton    != null) phoneButton.onClick.AddListener(()    => SelectMode(ControlMode.Phone));
        if (esp32Button    != null) esp32Button.onClick.AddListener(()    => SelectMode(ControlMode.ESP32));
    }

    private void SelectMode(ControlMode mode)
    {
        GameConfig.Instance.Mode = mode;
        string scene;
        switch (mode)
        {
            case ControlMode.Phone: scene = phoneStartSceneName; break;
            case ControlMode.ESP32: scene = esp32StartSceneName; break;
            default:                scene = keyboardStartSceneName; break;
        }
        SceneManager.LoadScene(scene);
    }
}
