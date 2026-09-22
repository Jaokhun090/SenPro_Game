// ============================================================
//  MainMenuController.cs
//  ─────────────────────────────────────────────────────────────
//  หน้าเลือกเกม — กดลูกศรซ้าย/ขวา หรือ FSR เหยียบซ้าย/ขวา
//  เพื่อเลื่อนดูเกม  แล้วกดเลือกเพื่อเข้าหน้า PlayerSetup
//
//  ★ ใส่ GameInfo ใน Inspector:
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

    private int currentIndex = 0;

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        // ผูก Button onClick (ถ้ามี)
        if (btnLeft  != null) btnLeft.onClick.AddListener(PreviousGame);
        if (btnRight != null) btnRight.onClick.AddListener(NextGame);
        if (btnPlay  != null) btnPlay.onClick.AddListener(PlaySelectedGame);

        UpdateDisplay();
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

        // ─── FSR Hardware Controls (ถ้าเชื่อมต่อ Arduino) ───
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            // FSR 0 (ซ้ายบน) = เลื่อนซ้าย
            if (SharedSerialReceiver.Instance.IsPadPressed(0))
            {
                PreviousGame();
            }
            // FSR 1 (ขวาบน) = เลื่อนขวา
            if (SharedSerialReceiver.Instance.IsPadPressed(1))
            {
                NextGame();
            }
            // FSR 2 หรือ 3 (ล่าง) = เลือกเกม
            if (SharedSerialReceiver.Instance.IsPadPressed(2) ||
                SharedSerialReceiver.Instance.IsPadPressed(3))
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
}
