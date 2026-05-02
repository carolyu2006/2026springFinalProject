using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Phone-only "holding / lobby" scene for GoWithTheCrow 同流合乌.
/// Shows the room code, counts connected phones, and enables "Start"
/// once at least one phone is connected.
///
/// Visual styling mirrors OrangeVerdict's HoldingUI (colors, fonts, layout).
///
/// Scene setup (Unity Editor):
///   1. Create a new scene "PhoneStartScene" and add this script to an empty GO.
///   2. Right-click the component → "Build UI Hierarchy" to generate the UI.
///   3. Drag the three networking prefabs into the Inspector fields.
///   4. Add PhoneStartScene to Build Settings.
/// </summary>
public class PhoneStartSceneManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] private string nextSceneName = "InstructionScene";
    [SerializeField] private string englishNextSceneName = "EnglishInstructionScene";

    [Header("Language")]
    [Tooltip("Sets GameConfig.Language on Awake. Default = Chinese instruction/end scenes; English = English variants.")]
    [SerializeField] private GameLanguage language = GameLanguage.Default;

    [Header("Networking Prefabs")]
    [SerializeField] private WebSocketClient wsClientPrefab;
    [SerializeField] private PhoneInputManager phoneInputManagerPrefab;
    [SerializeField] private JoystickNetworkManager networkManagerPrefab;

    [Header("UI References (auto-filled by Build UI Hierarchy)")]
    [SerializeField] private GameObject holdingPanel;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text hintText;

    [Header("Per-Player Slots (assign in Inspector)")]
    [SerializeField] private GameObject player1Slot;
    [SerializeField] private TMP_Text   player1NameText;
    [SerializeField] private Image      player1Image;
    [SerializeField] private GameObject player1PressHint;
    [SerializeField] private GameObject player2Slot;
    [SerializeField] private TMP_Text   player2NameText;
    [SerializeField] private Image      player2Image;
    [SerializeField] private GameObject player2PressHint;

    [Header("Character Sprites (pending shown until ready; ready swaps on press)")]
    [SerializeField] private Sprite pendingSprite;
    [SerializeField] private Sprite readySprite;

    private bool p1Ready;
    private bool p2Ready;
    private bool loading;

    private void Awake()
    {
        GameConfig.EnsureExists();
        GameConfig.Instance.Mode = ControlMode.Phone;
        GameConfig.Instance.Language = language;
    }

    private void Start()
    {
        EnsureNetworkingObjects();

        if (JoystickNetworkManager.Instance == null)
        {
            Debug.LogError("[PhoneStartSceneManager] JoystickNetworkManager missing — assign networkManagerPrefab in the Inspector.");
            return;
        }

        JoystickNetworkManager.Instance.OnRoomCodeReceived += code =>
        {
            if (roomCodeText != null) roomCodeText.text = code;
        };
        JoystickNetworkManager.Instance.OnPlayerConnected    += HandlePlayerConnected;
        JoystickNetworkManager.Instance.OnPlayerDisconnected += HandlePlayerDisconnected;

        if (player1PressHint == null && player1Slot != null)
        {
            var t = player1Slot.transform.Find("textbox");
            if (t != null) player1PressHint = t.gameObject;
        }
        if (player2PressHint == null && player2Slot != null)
        {
            var t = player2Slot.transform.Find("textbox");
            if (t != null) player2PressHint = t.gameObject;
        }

        if (player1Slot != null) player1Slot.SetActive(false);
        if (player2Slot != null) player2Slot.SetActive(false);
        if (player1PressHint != null) player1PressHint.SetActive(false);
        if (player2PressHint != null) player2PressHint.SetActive(false);

        JoystickNetworkManager.Instance.CreateRoomAndConnect();
        RefreshUI();
    }

    private void HandlePlayerConnected(int playerId, string name)
    {
        string displayName = string.IsNullOrEmpty(name) ? $"Player {playerId}" : name;
        int idx = playerId - 1;
        if (GameConfig.Instance != null && idx >= 0 && idx < GameConfig.Instance.PlayerNames.Length)
            GameConfig.Instance.PlayerNames[idx] = displayName;
        if (playerId == 1)
        {
            if (player1Slot != null) player1Slot.SetActive(true);
            if (player1NameText != null) player1NameText.text = displayName;
            SetSprite(player1Image, pendingSprite);
            if (player1PressHint != null) player1PressHint.SetActive(!p1Ready);
        }
        else if (playerId == 2)
        {
            if (player2Slot != null) player2Slot.SetActive(true);
            if (player2NameText != null) player2NameText.text = displayName;
            SetSprite(player2Image, pendingSprite);
            if (player2PressHint != null) player2PressHint.SetActive(!p2Ready);
        }
        RefreshUI();
    }

    private void HandlePlayerDisconnected(int playerId)
    {
        int idx = playerId - 1;
        if (GameConfig.Instance != null && idx >= 0 && idx < GameConfig.Instance.PlayerNames.Length)
            GameConfig.Instance.PlayerNames[idx] = null;
        if (playerId == 1)
        {
            p1Ready = false;
            if (player1Slot != null) player1Slot.SetActive(false);
            SetSprite(player1Image, pendingSprite);
            if (player1PressHint != null) player1PressHint.SetActive(false);
        }
        else if (playerId == 2)
        {
            p2Ready = false;
            if (player2Slot != null) player2Slot.SetActive(false);
            SetSprite(player2Image, pendingSprite);
            if (player2PressHint != null) player2PressHint.SetActive(false);
        }
        RefreshUI();
    }

    private static void SetSprite(Image img, Sprite sprite)
    {
        if (img != null && sprite != null) img.sprite = sprite;
    }

    private void Update()
    {
        if (loading) return;

        var pim = PhoneInputManager.Instance;
        if (pim != null)
        {
            if (!p1Ready && pim.ConsumeAction(0))
            {
                p1Ready = true;
                SetSprite(player1Image, readySprite);
                if (player1PressHint != null) player1PressHint.SetActive(false);
                RefreshUI();
            }
            if (!p2Ready && pim.ConsumeAction(1))
            {
                p2Ready = true;
                SetSprite(player2Image, readySprite);
                if (player2PressHint != null) player2PressHint.SetActive(false);
                RefreshUI();
            }
        }

        int count = GameConfig.Instance?.ConnectedPhoneCount ?? 0;
        if (p1Ready && p2Ready && count >= 2)
        {
            loading = true;
            LoadNextScene();
        }
    }

    private void EnsureNetworkingObjects()
    {
        if (PhoneInputManager.Instance == null && phoneInputManagerPrefab != null)
            Instantiate(phoneInputManagerPrefab);
        if (WebSocketClient.Instance == null && wsClientPrefab != null)
            Instantiate(wsClientPrefab);
        if (JoystickNetworkManager.Instance == null && networkManagerPrefab != null)
            Instantiate(networkManagerPrefab);
    }

    private void RefreshUI()
    {
        int count = GameConfig.Instance?.ConnectedPhoneCount ?? 0;

        if (playerCountText != null)
        {
            string p1 = p1Ready ? "P1 ✓" : "P1 …";
            string p2 = p2Ready ? "P2 ✓" : "P2 …";
            playerCountText.text = count > 0 ? $"{p1}   {p2}" : "";
        }

        if (hintText != null)
        {
            if (count < 2)
                hintText.text = "Waiting for players...";
            else if (!(p1Ready && p2Ready))
                hintText.text = "Both players: press the joystick button to ready up";
            else
                hintText.text = "Starting...";
        }
    }

    private void LoadNextScene()
    {
        bool english = language == GameLanguage.English;
        string target = english && !string.IsNullOrEmpty(englishNextSceneName) ? englishNextSceneName : nextSceneName;
        SceneManager.LoadScene(target);
    }

#if UNITY_EDITOR
    static readonly Color C_BG     = new Color(0.984f, 0.949f, 0.847f); // #FBF2D8
    static readonly Color C_TEXT   = new Color(0.227f, 0.4f, 0.294f);
    static readonly Color C_DIM    = new Color(0.482f, 0.612f, 0.529f);
    static readonly Color C_ACCENT = new Color(0.227f, 0.4f, 0.294f);

    [ContextMenu("Build UI Hierarchy")]
    private void EditorBuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var canvasGO = MakeCanvas("HoldingCanvas");

        var bg = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        bg.AddComponent<Image>().color = C_BG;
        Stretch(bg.GetComponent<RectTransform>());

        holdingPanel = MakePanel(canvasGO.transform, "HoldingPanel");
        holdingPanel.SetActive(true);

        Txt(holdingPanel.transform, "Title",   "GoWithTheCrow",    80, C_ACCENT, FontStyles.Bold,   0,  340);
        Txt(holdingPanel.transform, "Sub",     "同流合乌",          22, C_DIM,    FontStyles.Normal, 0,  275, 0.2f);
        roomCodeText    = Txt(holdingPanel.transform, "RoomCode",    "——", 160, C_TEXT, FontStyles.Bold,   0,  100);
        playerCountText = Txt(holdingPanel.transform, "PlayerCount", "",    26, C_DIM,  FontStyles.Normal, 0,  -20);

        hintText = Txt(holdingPanel.transform, "Hint", "Waiting for players...", 22, C_DIM, FontStyles.Italic, 0, -320);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[PhoneStartSceneManager] TMP hierarchy built.");
    }

    GameObject MakeCanvas(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        s.referencePixelsPerUnit = 100;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    GameObject MakePanel(Transform parent, string name)
    {
        var p = new GameObject(name);
        p.transform.SetParent(parent, false);
        p.AddComponent<RectTransform>();
        Stretch(p.GetComponent<RectTransform>());
        p.SetActive(false);
        return p;
    }

    static TMP_FontAsset _cachedFont;
    static TMP_FontAsset GetFont()
    {
        if (_cachedFont == null)
            _cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/AaManHuaJia-2.asset");
        return _cachedFont;
    }

    TMP_Text Txt(Transform parent, string name, string content, float size,
        Color color, FontStyles style, float x, float y, float charSpacing = 0, float width = 1000)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        var font = GetFont();
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = charSpacing;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, size * 1.6f);
        rt.anchoredPosition = new Vector2(x, y);
        return tmp;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
    }
#endif
}
