// ============================================================
//  PlayerSetupController.cs
//  ─────────────────────────────────────────────────────────────
//  หน้ากรอกข้อมูลก่อนเริ่มเกม:
//    1. ชื่อผู้เล่น + น้ำหนัก (ผู้ดูแลกรอกด้วยคีย์บอร์ด)
//    2. เลือกเวลาด้วยปุ่มเท้า 4 มุม:
//         TL(บนซ้าย)=1นาที  TR(บนขวา)=3นาที
//         BL(ล่างซ้าย)=5นาที  BR(ล่างขวา)=10นาที
//    3. เหยียบปุ่มเดิมซ้ำ = เริ่มเกม!
//    4. เหยียบ BL+BR พร้อมกัน = กลับ MainMenu
//
//  ★ ไม่มีปุ่ม START — ใช้ double-step แทน
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class PlayerSetupController : MonoBehaviour
{
    [Header("Input Fields (ผู้ดูแลกรอก)")]
    public TMP_InputField nameInput;       // ช่องกรอกชื่อ
    public TMP_InputField weightInput;     // ช่องกรอกน้ำหนัก (kg)

    [Header("Time Selection Buttons (4 มุมจอ)")]
    public Button btn1Min;     // บนซ้าย (TL)
    public Button btn3Min;     // บนขวา (TR)
    public Button btn5Min;     // ล่างซ้าย (BL)
    public Button btn10Min;    // ล่างขวา (BR)

    [Header("Time Button Visual")]
    public Color selectedColor   = new Color(1f, 1f, 1f, 1f);       // สีขาว (ไม่ tint)
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f); // สีเทาจางลง
    public float selectedScale   = 1.05f;    // ขยายปุ่มที่เลือก
    public float unselectedScale = 1.0f;

    [Header("Display")]
    public TextMeshProUGUI selectedGameText;   // แสดงชื่อเกมที่เลือก
    public TextMeshProUGUI selectedTimeText;   // แสดงเวลาที่เลือก (กึ่งกลาง)
    public TextMeshProUGUI instructionText;    // คำแนะนำ

    [Header("Back Button (Optional — สำหรับคลิก)")]
    public Button btnBack;

    // ─── Internal State ───
    private float selectedDuration = 0f;       // 0 = ยังไม่ได้เลือก
    private bool  timeSelected     = false;
    private float cooldownTimer    = 0f;
    private const float COOLDOWN   = 0.8f;     // cooldown วินาที

    // ─── Dual-press Back ───
    private const float DUAL_PRESS_WINDOW = 0.15f;  // ต้องเหยียบ 2 ปุ่มภายใน 150ms

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        // แสดงชื่อเกมที่เลือก
        if (selectedGameText != null)
            selectedGameText.text = "เกม: " + PlayerData.GameDisplayName;

        // ตั้งค่า default จาก PlayerData
        if (nameInput != null)
            nameInput.text = PlayerData.PlayerName;

        if (weightInput != null)
            weightInput.text = PlayerData.PlayerWeight.ToString();

        // ผูก Button onClick (คลิกได้ด้วย)
        if (btn1Min  != null) btn1Min.onClick.AddListener(() => SelectTime(60f));
        if (btn3Min  != null) btn3Min.onClick.AddListener(() => SelectTime(180f));
        if (btn5Min  != null) btn5Min.onClick.AddListener(() => SelectTime(300f));
        if (btn10Min != null) btn10Min.onClick.AddListener(() => SelectTime(600f));
        if (btnBack  != null) btnBack.onClick.AddListener(GoBack);

        // เริ่มต้น — ยังไม่ได้เลือกเวลา
        timeSelected = false;
        UpdateInstruction();

        HighlightButton(btn1Min,  false);
        HighlightButton(btn3Min,  false);
        HighlightButton(btn5Min,  false);
        HighlightButton(btn10Min, false);

        if (selectedTimeText != null)
            selectedTimeText.text = " -- นาที";
    }

    // ═══════════════════════════════════════════════════════
    void Update()
    {
        // ─── Cooldown ───
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        // ─── Keyboard Controls ───
        if (Input.GetKeyDown(KeyCode.Alpha1)) { SelectTime(60f);  return; }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { SelectTime(180f); return; }
        if (Input.GetKeyDown(KeyCode.Alpha5)) { SelectTime(300f); return; }
        if (Input.GetKeyDown(KeyCode.Alpha0)) { SelectTime(600f); return; }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (timeSelected) StartGame();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            GoBack();
            return;
        }

        // ─── FSR Hardware Controls ───
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            bool bl = SharedSerialReceiver.Instance.IsPadPressed(2);  // ล่างซ้าย
            bool br = SharedSerialReceiver.Instance.IsPadPressed(3);  // ล่างขวา

            // ★ เหยียบ BL + BR พร้อมกัน = กลับ MainMenu
            if (bl && br)
            {
                GoBack();
                return;
            }

            // TL (pad 0) = 1 นาที
            if (SharedSerialReceiver.Instance.IsPadPressed(0))
            {
                HandleFootPress(60f);
                return;
            }
            // TR (pad 1) = 3 นาที
            if (SharedSerialReceiver.Instance.IsPadPressed(1))
            {
                HandleFootPress(180f);
                return;
            }
            // BL (pad 2) = 5 นาที (ถ้าไม่ได้กด BR พร้อมกัน)
            if (bl)
            {
                HandleFootPress(300f);
                return;
            }
            // BR (pad 3) = 10 นาที (ถ้าไม่ได้กด BL พร้อมกัน)
            if (br)
            {
                HandleFootPress(600f);
                return;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  เลือกเวลา (กดครั้งแรก = เลือก, กดซ้ำปุ่มเดิม = เริ่มเกม!)
    //  ใช้ได้ทั้งคลิกเมาส์, กดคีย์บอร์ด, และเหยียบ FSR
    // ═══════════════════════════════════════════════════════
    public void SelectTime(float seconds)
    {
        // ★ ถ้าเลือกเวลานี้อยู่แล้ว แล้วกดซ้ำอีกครั้ง → เริ่มเกม!
        if (timeSelected && Mathf.Approximately(selectedDuration, seconds))
        {
            StartGame();
            return;
        }

        selectedDuration = seconds;
        timeSelected = true;
        cooldownTimer = COOLDOWN;

        if (selectedTimeText != null)
        {
            int mins = Mathf.FloorToInt(seconds / 60f);
            selectedTimeText.text = mins + " นาที";
        }

        HighlightButton(btn1Min,  seconds == 60f);
        HighlightButton(btn3Min,  seconds == 180f);
        HighlightButton(btn5Min,  seconds == 300f);
        HighlightButton(btn10Min, seconds == 600f);

        UpdateInstruction();
    }

    private void HandleFootPress(float seconds)
    {
        SelectTime(seconds);
    }

    private void HighlightButton(Button btn, bool isSelected)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isSelected ? selectedColor : unselectedColor;

        float s = isSelected ? selectedScale : unselectedScale;
        btn.transform.localScale = new Vector3(s, s, 1f);
    }

    private void UpdateInstruction()
    {
        if (instructionText == null) return;

        if (!timeSelected)
            instructionText.text = "เหยียบปุ่มเท้าเพื่อเลือกเวลา";
        else
            instructionText.text = "เหยียบปุ่มเดิมอีกครั้งเพื่อเริ่มเกม!";
    }

    // ═══════════════════════════════════════════════════════
    //  เริ่มเกม!
    // ═══════════════════════════════════════════════════════
    public void StartGame()
    {
        if (!timeSelected) return;

        if (nameInput != null && !string.IsNullOrEmpty(nameInput.text))
            PlayerData.PlayerName = nameInput.text;

        if (weightInput != null && float.TryParse(weightInput.text, out float w))
            PlayerData.PlayerWeight = w;

        PlayerData.GameDuration = selectedDuration;
        PlayerData.ResetResults();

        // ตรวจสอบชื่อ Scene ป้องกันกรณีเปิดเล่นจากหน้า PlayerSetup โดยตรง
        string sceneToLoad = PlayerData.SelectedGame;
        if (string.IsNullOrEmpty(sceneToLoad) || sceneToLoad == "MoleGame")
        {
            sceneToLoad = "SampleScene";
        }

        Debug.Log($"[PlayerSetup] Starting game '{sceneToLoad}' for {PlayerData.PlayerName}, duration: {PlayerData.GameDuration}s");
        SceneManager.LoadScene(sceneToLoad);
    }

    // ═══════════════════════════════════════════════════════
    //  กลับ MainMenu
    // ═══════════════════════════════════════════════════════
    public void GoBack()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
