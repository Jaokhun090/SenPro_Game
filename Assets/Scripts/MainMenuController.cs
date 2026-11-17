// ============================================================
//  MainMenuController.cs
//  ─────────────────────────────────────────────────────────────
//  หน้าเลือกเกม — กดลูกศรซ้าย/ขวา หรือ FSR เหยียบซ้าย/ขวา
//  เพื่อเลื่อนดูเกม  แล้วกดเลือกเพื่อเข้าหน้า PlayerSetup
//
//  - ใส่ GameInfo ใน Inspector:
//    gameName = "Whack-a-Mole"
//    sceneName = "MoleGame"
//    gameIcon = (Sprite ไอคอนเกม)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// ─── ข้อมูลเกมแต่ละตัว (กรอกใน Inspector) ───
[System.Serializable]
public class GameInfo
{
    public string gameName;   // ชื่อแสดงผล เช่น "Whack-a-Mole"
    public string sceneName;  // ชื่อ Scene เช่น "MoleGame"
    public Sprite gameIcon;   // ไอคอนเกม
}

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public Image            gameIconDisplay;     // รูปไอคอนเกมตรงกลาง
    public TextMeshProUGUI  gameNameText;        // ชื่อเกมที่แสดง
    public TextMeshProUGUI  gameCountText;       // เช่น "1/4"

    [Header("Game List")]
    public GameInfo[] games;   // ใส่เกมทั้งหมดใน Inspector

    [Header("Navigation Buttons (Optional)")]
    public Button btnLeft;
    public Button btnRight;
    public Button btnPlay;

    [Header("User Info & Logout (Firebase)")]
    public TextMeshProUGUI userGreetingText; // แสดงชื่อผู้เล่น เช่น "ผู้เล่น: สมชาย"
    public Button btnLogout;                 // ปุ่มออกจากระบบ (กลับหน้า LoginScene)

    private int currentIndex = 0;

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        // เริ่มเชื่อมต่อ Arduino ทันทีที่เข้าสู่หน้า MainMenu
        _ = SharedSerialReceiver.Instance;

        // ผูก Button onClick (ถ้ามี)
        if (btnLeft   != null) btnLeft.onClick.AddListener(PreviousGame);
        if (btnRight  != null) btnRight.onClick.AddListener(NextGame);
        if (btnPlay   != null) btnPlay.onClick.AddListener(PlaySelectedGame);
        if (btnLogout != null) btnLogout.onClick.AddListener(Logout);

        if (userGreetingText != null)
        {
            userGreetingText.text = "ผู้เล่น: " + PlayerData.PlayerName;
        }

        // ติดป้ายกำกับบอกวิธีเหยียบใต้ปุ่มทุกปุ่มในหน้า MainMenu
        AttachSubLabel(btnLeft, "(บนซ้าย)");
        AttachSubLabel(btnRight, "(บนขวา)");
        AttachSubLabel(btnPlay, "(ล่างซ้าย หรือ ล่างขวา)");
        AttachSubLabel(btnLogout, "(คลิกเมาส์)");

        UpdateDisplay();
    }

    private void AttachSubLabel(Button btn, string text, float yOffset = -10f, int fontSize = 24)
    {
        if (btn == null) return;

        Transform existing = btn.transform.Find("SubLabel_PadHint");
        if (existing != null)
        {
            var tmp = existing.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.fontSize = fontSize;
            }
            return;
        }

        GameObject labelObj = new GameObject("SubLabel_PadHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(btn.transform, false);

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(340, 42);

        TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("LeelawadeeUI SDF");
        if (font != null) labelTmp.font = font;
        labelTmp.text = text;
        labelTmp.fontSize = fontSize;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = new Color(0.18f, 0.22f, 0.32f, 1f);
    }

    // ═══════════════════════════════════════════════════════
    void Update()
    {
        // ─── Keyboard Controls ───
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q))
        {
            PreviousGame();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.W))
        {
            NextGame();
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S))
        {
            PlaySelectedGame();
        }

        // ─── FSR Hardware Controls (เหยียบบอร์ด Arduino) ───
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            // FSR 0 (ซ้ายบน / TL) = ปุ่มซ้าย (<)
            if (SharedSerialReceiver.Instance.IsPadDown(0))
            {
                PreviousGame();
            }
            // FSR 1 (ขวาบน / TR) = ปุ่มขวา (>)
            if (SharedSerialReceiver.Instance.IsPadDown(1))
            {
                NextGame();
            }
            // FSR 2 หรือ 3 (ล่างซ้าย / ล่างขวา / BL / BR) = ปุ่มเริ่มเกม
            if (SharedSerialReceiver.Instance.IsPadDown(2) ||
                SharedSerialReceiver.Instance.IsPadDown(3))
            {
                PlaySelectedGame();
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Navigation
    // ═══════════════════════════════════════════════════════
    public void NextGame()
    {
        currentIndex++;
        if (currentIndex >= games.Length) currentIndex = 0;
        UpdateDisplay();
    }

    public void PreviousGame()
    {
        currentIndex--;
        if (currentIndex < 0) currentIndex = games.Length - 1;
        UpdateDisplay();
    }

    // ═══════════════════════════════════════════════════════
    //  Display Update
    // ═══════════════════════════════════════════════════════
    private void UpdateDisplay()
    {
        if (games.Length == 0) return;

        GameInfo info = games[currentIndex];

        if (gameIconDisplay != null && info.gameIcon != null)
            gameIconDisplay.sprite = info.gameIcon;

        if (gameNameText != null)
            gameNameText.text = info.gameName;

        if (gameCountText != null)
            gameCountText.text = (currentIndex + 1) + "/" + games.Length;
    }

    // ═══════════════════════════════════════════════════════
    //  Play — เก็บเกมที่เลือกแล้วไปหน้า PlayerSetup
    // ═══════════════════════════════════════════════════════
    public void PlaySelectedGame()
    {
        if (games.Length == 0) return;

        GameInfo info = games[currentIndex];
        PlayerData.SelectedGame   = info.sceneName;
        PlayerData.GameDisplayName = info.gameName;

        // ไปหน้ากรอกชื่อ/น้ำหนัก/เวลา ก่อนเริ่มเกม
        SceneManager.LoadScene("PlayerSetup");
    }

    // ═══════════════════════════════════════════════════════
    //  ออกจากระบบ Firebase แล้วกลับไปหน้า LoginScene
    // ═══════════════════════════════════════════════════════
    public void Logout()
    {
        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SignOut();
        }
        SceneManager.LoadScene("LoginScene");
    }
}
