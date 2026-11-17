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
//  - ไม่มีปุ่ม START — ใช้ double-step แทน
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
    public Color unselectedColor = new Color(0.78f, 0.78f, 0.78f, 1f); // สีสว่างชัดเจน ไม่มืดเกินไป
    public float selectedScale   = 1.05f;    // ขยายปุ่มที่เลือก
    public float unselectedScale = 1.0f;

    [Header("Display")]
    public TextMeshProUGUI selectedGameText;   // แสดงชื่อเกมที่เลือก
    public TextMeshProUGUI selectedTimeText;   // แสดงเวลาที่เลือก (กึ่งกลาง)
    public TextMeshProUGUI instructionText;    // คำแนะนำ
    public TextMeshProUGUI playerInfoText;     // แสดงข้อมูลผู้เล่นรวม
    public TextMeshProUGUI selectedNameText;   // แสดงชื่อต่อจาก LabelName (เช่น "นายสมชาย ใจดี")
    public TextMeshProUGUI selectedWeightText; // แสดงน้ำหนักต่อจาก LabelWeight (เช่น "65.0 กก.")

    [Header("Back Button (Optional — สำหรับคลิก)")]
    public Button btnBack;

    // ─── Internal State ───
    private float selectedDuration = 0f;       // 0 = ยังไม่ได้เลือก
    private int   selectedDifficulty = 1;      // ระดับ 1-4
    private int   selectedFruitCount = 1;      // จำนวนผลไม้ที่ต้องจำ (1, 3, 5, 7)
    private bool  isMemoryGame     = false;
    private bool  timeSelected     = false;
    private float cooldownTimer    = 0f;
    private const float COOLDOWN   = 0.8f;     // cooldown วินาที

    // ─── Dual-press Back ───
    private const float DUAL_PRESS_WINDOW = 0.15f;  // ต้องเหยียบ 2 ปุ่มภายใน 150ms

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        isMemoryGame = (PlayerData.SelectedGame == "MemoryGame");

        // แสดงชื่อเกมที่เลือก
        if (selectedGameText != null)
            selectedGameText.text = "เกม: " + PlayerData.GameDisplayName;

        // แสดงชื่อและน้ำหนักผู้เล่น (จาก Login)
        if (selectedNameText != null)
            selectedNameText.text = PlayerData.PlayerName;

        if (selectedWeightText != null)
            selectedWeightText.text = $"{PlayerData.PlayerWeight:F1} กก.";

        // แสดงข้อมูลผู้เล่นรวม (ถ้ามี)
        if (playerInfoText != null)
            playerInfoText.text = $"ผู้เล่น: {PlayerData.PlayerName}   (น้ำหนัก: {PlayerData.PlayerWeight:F1} kg)";

        // ตั้งค่า default จาก PlayerData (กรณีที่ยังใช้ InputField อยู่)
        if (nameInput != null)
            nameInput.text = PlayerData.PlayerName;

        if (weightInput != null)
            weightInput.text = PlayerData.PlayerWeight.ToString();

        if (btnBack != null)
        {
            btnBack.onClick.AddListener(GoBack);
        }

        // ปรับแต่งข้อความปุ่มย้อนกลับที่มีอยู่แล้วใน Scene ให้ชัดเจนและตัวใหญ่ (ป้องกันข้อความซ้อน)
        GameObject backHintObj = GameObject.Find("BackHintText");
        if (backHintObj != null)
        {
            RectTransform rt = backHintObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0, -480);
                rt.sizeDelta = new Vector2(360, 50);
            }
            TextMeshProUGUI tmp = backHintObj.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 32;
                tmp.fontStyle = FontStyles.Bold;
                tmp.text = "ล่างซ้าย + ล่างขวา";
            }
        }

        // อัปเดตภาพปุ่มเลข 7 สำหรับปุ่มที่ 4
        Sprite s7 = Resources.Load<Sprite>("BtnTime7");
        if (s7 != null && btn10Min != null)
        {
            Image img = btn10Min.GetComponent<Image>();
            if (img != null) img.sprite = s7;
        }

        // จัดตำแหน่ง LabelTime และ SelectedTimeText แยกซ้ายขวาอย่างชัดเจนด้วย Pivot ซ้าย-ขวา ไม่ให้ทับกันเด็ดขาด
        GameObject labelTimeObj = GameObject.Find("LabelTime");
        if (labelTimeObj != null)
        {
            RectTransform rt = labelTimeObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-25f, -20f);
                rt.sizeDelta = new Vector2(380f, 55f);
            }
            var lblTmp = labelTimeObj.GetComponent<TextMeshProUGUI>();
            if (lblTmp != null)
            {
                lblTmp.alignment = TextAlignmentOptions.Right;
                lblTmp.text = isMemoryGame ? "ระดับความยาก :" : "เลือกเวลาเล่น :";
                lblTmp.fontSize = 38;
                lblTmp.fontStyle = FontStyles.Bold;
            }
        }

        if (selectedTimeText != null)
        {
            RectTransform rt = selectedTimeText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(25f, -20f);
                rt.sizeDelta = new Vector2(420f, 55f);
            }
            selectedTimeText.alignment = TextAlignmentOptions.Left;
            selectedTimeText.fontSize = 40;
            selectedTimeText.fontStyle = FontStyles.Bold;
        }

        if (isMemoryGame)
        {
            // ปรับข้อความ 4 มุม สำหรับเกมจำภาพผลไม้
            UpdateCornerLabel("LabelTL", "ระดับ 1: จำ 1 รูป", "เหยียบ บนซ้าย");
            UpdateCornerLabel("LabelTR", "ระดับ 2: จำ 3 รูป", "เหยียบ บนขวา");
            UpdateCornerLabel("LabelBL", "ระดับ 3: จำ 5 รูป", "เหยียบ ล่างซ้าย");
            UpdateCornerLabel("LabelBR", "ระดับ 4: จำ 7 รูป", "เหยียบ ล่างขวา");

            if (btn1Min  != null) btn1Min.onClick.AddListener(() => SelectDifficulty(1, 1));
            if (btn3Min  != null) btn3Min.onClick.AddListener(() => SelectDifficulty(2, 3));
            if (btn5Min  != null) btn5Min.onClick.AddListener(() => SelectDifficulty(3, 5));
            if (btn10Min != null) btn10Min.onClick.AddListener(() => SelectDifficulty(4, 7));

            if (selectedTimeText != null)
                selectedTimeText.text = "ระดับ --";
        }
        else
        {
            // ปรับข้อความ 4 มุม สำหรับเกมตีตุ่น (1, 3, 5, 7 นาที)
            UpdateCornerLabel("LabelTL", "1 นาที", "เหยียบ บนซ้าย");
            UpdateCornerLabel("LabelTR", "3 นาที", "เหยียบ บนขวา");
            UpdateCornerLabel("LabelBL", "5 นาที", "เหยียบ ล่างซ้าย");
            UpdateCornerLabel("LabelBR", "7 นาที", "เหยียบ ล่างขวา");

            if (btn1Min  != null) btn1Min.onClick.AddListener(() => SelectTime(60f));
            if (btn3Min  != null) btn3Min.onClick.AddListener(() => SelectTime(180f));
            if (btn5Min  != null) btn5Min.onClick.AddListener(() => SelectTime(300f));
            if (btn10Min != null) btn10Min.onClick.AddListener(() => SelectTime(420f));

            if (selectedTimeText != null)
                selectedTimeText.text = "-- นาที";
        }

        // เริ่มต้น — ยังไม่ได้เลือก
        timeSelected = false;
        UpdateInstruction();

        HighlightButton(btn1Min,  false);
        HighlightButton(btn3Min,  false);
        HighlightButton(btn5Min,  false);
        HighlightButton(btn10Min, false);
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
        if (isMemoryGame)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q)) { SelectDifficulty(1, 1); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.W)) { SelectDifficulty(2, 3); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.A)) { SelectDifficulty(3, 5); return; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.S)) { SelectDifficulty(4, 7); return; }
        }
        else
        {
            // ตุ่น: 1 นาที (60s), 3 นาที (180s), 5 นาที (300s), 7 นาที (420s)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q)) { SelectTime(60f);  return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.W)) { SelectTime(180f); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.A)) { SelectTime(300f); return; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.S)) { SelectTime(420f); return; }
        }

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

            // - เหยียบ BL + BR พร้อมกัน = กลับ MainMenu
            if (bl && br)
            {
                GoBack();
                return;
            }

            if (isMemoryGame)
            {
                if (SharedSerialReceiver.Instance.IsPadPressed(0)) { SelectDifficulty(1, 1); return; }
                if (SharedSerialReceiver.Instance.IsPadPressed(1)) { SelectDifficulty(2, 3); return; }
                if (bl) { SelectDifficulty(3, 5); return; }
                if (br) { SelectDifficulty(4, 7); return; }
            }
            else
            {
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
                // BR (pad 3) = 7 นาที (ถ้าไม่ได้กด BL พร้อมกัน)
                if (br)
                {
                    HandleFootPress(420f);
                    return;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  เลือกระดับความยาก (สำหรับ MemoryGame)
    // ═══════════════════════════════════════════════════════
    public void SelectDifficulty(int level, int fruitCount)
    {
        if (timeSelected && selectedDifficulty == level)
        {
            StartGame();
            return;
        }

        selectedDifficulty = level;
        selectedFruitCount = fruitCount;
        timeSelected = true;
        cooldownTimer = COOLDOWN;

        if (selectedTimeText != null)
        {
            selectedTimeText.text = $"ระดับ {level} (จำ {fruitCount} รูป)";
        }

        HighlightButton(btn1Min,  level == 1);
        HighlightButton(btn3Min,  level == 2);
        HighlightButton(btn5Min,  level == 3);
        HighlightButton(btn10Min, level == 4);

        UpdateInstruction();
    }

    // ═══════════════════════════════════════════════════════
    //  เลือกเวลา (กดครั้งแรก = เลือก, กดซ้ำปุ่มเดิม = เริ่มเกม!)
    //  ใช้ได้ทั้งคลิกเมาส์, กดคีย์บอร์ด, และเหยียบ FSR
    // ═══════════════════════════════════════════════════════
    public void SelectTime(float seconds)
    {
        // - ถ้าเลือกเวลานี้อยู่แล้ว แล้วกดซ้ำอีกครั้ง → เริ่มเกม!
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

        HighlightButton(btn1Min,  Mathf.Approximately(seconds, 60f));
        HighlightButton(btn3Min,  Mathf.Approximately(seconds, 180f));
        HighlightButton(btn5Min,  Mathf.Approximately(seconds, 300f));
        HighlightButton(btn10Min, Mathf.Approximately(seconds, 420f));

        UpdateInstruction();
    }

    private void HandleFootPress(float seconds)
    {
        SelectTime(seconds);
    }

    private void UpdateCornerLabel(string objName, string mainText, string padText)
    {
        GameObject obj = GameObject.Find(objName);
        if (obj != null)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(340, 90);
            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 28;
                tmp.fontStyle = FontStyles.Bold;
                tmp.text = $"{mainText}\n<size=80%>({padText})</size>";
            }
        }
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
            instructionText.text = isMemoryGame ? "เหยียบปุ่มเท้าเพื่อเลือกระดับความยาก" : "เหยียบปุ่มเท้าเพื่อเลือกเวลา";
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

        if (isMemoryGame)
        {
            PlayerData.GameDifficulty = selectedFruitCount; // จำ 1, 3, 5, 7 รูป
            PlayerData.GameDuration = 180f; // 3 นาที default
        }
        else
        {
            PlayerData.GameDuration = selectedDuration;
        }

        PlayerData.ResetResults();

        // ตรวจสอบชื่อ Scene ป้องกันกรณีเปิดเล่นจากหน้า PlayerSetup โดยตรง
        string sceneToLoad = PlayerData.SelectedGame;
        if (string.IsNullOrEmpty(sceneToLoad) || sceneToLoad == "MoleGame")
        {
            sceneToLoad = "SampleScene";
        }

        Debug.Log($"[PlayerSetup] Starting game '{sceneToLoad}' for {PlayerData.PlayerName}, Diff: {PlayerData.GameDifficulty}, Duration: {PlayerData.GameDuration}s");
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
