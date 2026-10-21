// ============================================================
//  FruitMemoryManager.cs
//  ─────────────────────────────────────────────────────────────
//  เกมที่ 2: เกมจำภาพผลไม้ (Game02: MemoryGame)
//  สำหรับฟื้นฟูสมรรถภาพความจำและการลงน้ำหนักเท้าของผู้สูงอายุ
//
//  ★ ทำงานร่วมกับ:
//    - SharedSerialReceiver (รับค่า FSR 4 แผ่น + Load Cell)
//    - DataLogger (บันทึก CSV รหัส Game02 อัตโนมัติ)
//    - PlayerData & FirebaseManager (บันทึกสถิติขึ้นคลาวด์)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class FruitMemoryManager : MonoBehaviour
{
    public enum GamePhase
    {
        Instruction,
        Memorize,
        Answer,
        Feedback,
        GameOver
    }

    [Header("Game State")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Instruction;

    [Header("UI - Header")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI playerInfoText;
    public TextMeshProUGUI promptText;       // ข้อความแนะนำ เช่น "จดจำผลไม้นี้!" หรือ "เหยียบเลือกผลไม้ที่เห็น!"

    [Header("UI - Memorize Card (Center)")]
    public GameObject memorizeCard;          // การ์ดตรงกลางที่แสดงภาพโจทย์
    public Image targetFruitImage;           // ภาพผลไม้ที่ต้องจำ
    public TextMeshProUGUI targetFruitName;  // ชื่อผลไม้ (เช่น "แอปเปิ้ล")
    public Slider memorizeTimerSlider;       // แถบนับถอยหลังช่วงให้จำ
    public float memorizeDuration = 4.0f;    // เวลาให้จำต่อข้อ (วินาที)

    [Header("UI - Choices (4 Corners)")]
    public GameObject choicesContainer;      // แผงครอบตัวเลือก 4 มุม
    public Button[] choiceButtons;           // 4 ปุ่ม: 0=TL(Q), 1=TR(W), 2=BL(A), 3=BR(S)
    public Image[] choiceFruitImages;        // รูปผลไม้ในแต่ละช่อง
    public TextMeshProUGUI[] choicePadLabels;// ข้อความบอกแผ่น เช่น "บนซ้าย (Q)", "บนขวา (W)"...
    public Image[] choiceCardBackgrounds;    // แบล็คกราวด์การ์ด เพื่อเปลี่ยนสี เขียว/แดง ตอนตอบ

    [Header("UI - Instruction & Game Over")]
    public GameObject instructionPanel;
    public Button btnStartGame;
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI summaryDetailText;
    public Button btnPlayAgain;
    public Button btnHome;

    [Header("Fruit Database (Sprites)")]
    public Sprite[] fruitSprites;            // ลาก 6 รูปจาก Assets/Image ใส่ในนี้
    public string[] fruitNames = new string[]
    {
        "แอปเปิ้ล",
        "กล้วย",
        "ส้ม",
        "แตงโม",
        "องุ่น",
        "สตรอว์เบอร์รี"
    };

    [Header("Data Logger")]
    public DataLogger dataLogger;

    // ── Internal Game Variables ──
    private float totalGameTime;
    private float timePlayed = 0f;
    private int score = 0;
    private int correctCount = 0;
    private int wrongCount = 0;
    private int totalStomps = 0;
    private float totalKgForce = 0f;
    private float maxSingleForce = 0f;

    private int targetFruitIndex = -1;
    private int correctChoiceSlot = -1;      // ตำแหน่งปุ่มที่ถูกต้อง (0-3)
    private int[] currentChoices = new int[4];
    private float currentMemorizeTimer = 0f;
    private bool isHandlingAnswer = false;

    // สีสำหรับการแสดงผล
    private readonly Color normalCardColor  = new Color(1f, 1f, 1f, 1f);
    private readonly Color correctCardColor = new Color(0.4f, 0.9f, 0.4f, 1f);
    private readonly Color wrongCardColor   = new Color(1f, 0.45f, 0.45f, 1f);

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        // กำหนดชื่อเกมให้ระบบ
        PlayerData.GameDisplayName = "เกมจำภาพผลไม้";

        // โหลดเวลาจาก PlayerSetup
        totalGameTime = PlayerData.GameDuration;
        if (totalGameTime <= 0f) totalGameTime = 60f;

        // อัปเดตข้อมูลผู้เล่น
        if (playerInfoText != null)
        {
            playerInfoText.text = $"ผู้เล่น: {PlayerData.PlayerName}";
        }

        UpdateScoreUI();
        UpdateTimeUI();

        // ซ่อน Game Over
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // ผูกปุ่มเมนูจบเกม
        if (btnPlayAgain != null) btnPlayAgain.onClick.AddListener(PlayAgain);
        if (btnHome != null) btnHome.onClick.AddListener(GoHome);

        // ผูกปุ่มตัวเลือกทั้ง 4 ช่อง (สำหรับคลิกด้วยเมาส์ทดสอบได้)
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.AddListener(() => OnPlayerChoose(index));
            }
        }

        // หน้า Instruction แนะนำการเล่น
        if (instructionPanel != null)
        {
            instructionPanel.SetActive(true);
            currentPhase = GamePhase.Instruction;
            if (btnStartGame != null)
            {
                btnStartGame.onClick.AddListener(StartGameAfterInstruction);
            }
        }
        else
        {
            StartGameAfterInstruction();
        }

        // Auto load fruit sprites if not set in Inspector
        AutoLoadFruitSpritesIfEmpty();
    }

    // ═══════════════════════════════════════════════════════
    void Update()
    {
        if (currentPhase == GamePhase.Instruction || currentPhase == GamePhase.GameOver) return;

        // 1. นับเวลาเกมรวม
        totalGameTime -= Time.deltaTime;
        timePlayed += Time.deltaTime;
        if (totalGameTime <= 0f)
        {
            totalGameTime = 0f;
            UpdateTimeUI();
            EndGame();
            return;
        }
        UpdateTimeUI();

        // 2. จัดการ Phase
        if (currentPhase == GamePhase.Memorize)
        {
            currentMemorizeTimer -= Time.deltaTime;
            if (memorizeTimerSlider != null)
            {
                memorizeTimerSlider.value = Mathf.Clamp01(currentMemorizeTimer / memorizeDuration);
            }

            if (currentMemorizeTimer <= 0f)
            {
                SwitchToAnswerPhase();
            }
        }
        else if (currentPhase == GamePhase.Answer && !isHandlingAnswer)
        {
            CheckPlayerInput();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Input Handling (Hardware FSR + Keyboard)
    // ═══════════════════════════════════════════════════════
    private void CheckPlayerInput()
    {
        // ─── Hardware FSR Stomp ───
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            for (int i = 0; i < 4; i++)
            {
                if (SharedSerialReceiver.Instance.IsPadDown(i))
                {
                    OnPlayerChoose(i);
                    return;
                }
            }
        }

        // ─── Keyboard Fallback ───
        if (Input.GetKeyDown(KeyCode.Q)) OnPlayerChoose(0); // Top-Left
        else if (Input.GetKeyDown(KeyCode.W)) OnPlayerChoose(1); // Top-Right
        else if (Input.GetKeyDown(KeyCode.A)) OnPlayerChoose(2); // Bottom-Left
        else if (Input.GetKeyDown(KeyCode.S)) OnPlayerChoose(3); // Bottom-Right
    }

    // ═══════════════════════════════════════════════════════
    //  Game Flow
    // ═══════════════════════════════════════════════════════
    public void StartGameAfterInstruction()
    {
        if (instructionPanel != null) instructionPanel.SetActive(false);

        // เริ่มบันทึก CSV
        if (dataLogger != null)
        {
            dataLogger.BeginSession();
        }

        score = 0;
        correctCount = 0;
        wrongCount = 0;
        totalStomps = 0;
        totalKgForce = 0f;
        maxSingleForce = 0f;
        UpdateScoreUI();

        StartNewRound();
    }

    private void StartNewRound()
    {
        if (fruitSprites == null || fruitSprites.Length == 0) return;

        isHandlingAnswer = false;
        currentPhase = GamePhase.Memorize;

        // สุ่มผลไม้โจทย์ (ไม่ให้ซ้ำกับข้อก่อนหน้า)
        int newTarget = Random.Range(0, fruitSprites.Length);
        if (fruitSprites.Length > 1 && newTarget == targetFruitIndex)
        {
            newTarget = (newTarget + 1) % fruitSprites.Length;
        }
        targetFruitIndex = newTarget;

        // แสดงผลไม้ตรงกลาง
        if (memorizeCard != null) memorizeCard.SetActive(true);
        if (targetFruitImage != null) targetFruitImage.sprite = fruitSprites[targetFruitIndex];
        if (targetFruitName != null)
        {
            targetFruitName.text = (targetFruitIndex < fruitNames.Length) ? fruitNames[targetFruitIndex] : "";
        }

        // ซ่อนตัวเลือก
        if (choicesContainer != null) choicesContainer.SetActive(false);

        // รีเซ็ตการนับเวลาจำ
        currentMemorizeTimer = memorizeDuration;
        if (memorizeTimerSlider != null) memorizeTimerSlider.value = 1f;

        if (promptText != null)
        {
            promptText.text = "👀 <b>จดจำผลไม้นี้ให้ดี!</b>";
        }
    }

    private void SwitchToAnswerPhase()
    {
        currentPhase = GamePhase.Answer;

        // ซ่อนการ์ดจำ
        if (memorizeCard != null) memorizeCard.SetActive(false);

        // สร้างตัวเลือก 4 ช่อง โดยสุ่มตำแหน่งของคำตอบที่ถูกต้อง
        correctChoiceSlot = Random.Range(0, 4);
        currentChoices[correctChoiceSlot] = targetFruitIndex;

        // สุ่มตัวเลือกผิด 3 ตัวไม่ให้ซ้ำกัน
        List<int> otherIndices = new List<int>();
        for (int i = 0; i < fruitSprites.Length; i++)
        {
            if (i != targetFruitIndex) otherIndices.Add(i);
        }
        // Shuffle other indices
        for (int i = 0; i < otherIndices.Count; i++)
        {
            int r = Random.Range(i, otherIndices.Count);
            int temp = otherIndices[i];
            otherIndices[i] = otherIndices[r];
            otherIndices[r] = temp;
        }

        int otherTracker = 0;
        for (int slot = 0; slot < 4; slot++)
        {
            if (slot != correctChoiceSlot)
            {
                currentChoices[slot] = otherIndices[otherTracker % otherIndices.Count];
                otherTracker++;
            }
        }

        // แสดงผลตัวเลือก 4 ช่อง
        for (int slot = 0; slot < 4; slot++)
        {
            int fruitIdx = currentChoices[slot];
            if (choiceFruitImages != null && slot < choiceFruitImages.Length && choiceFruitImages[slot] != null)
            {
                choiceFruitImages[slot].sprite = fruitSprites[fruitIdx];
            }
            if (choiceCardBackgrounds != null && slot < choiceCardBackgrounds.Length && choiceCardBackgrounds[slot] != null)
            {
                choiceCardBackgrounds[slot].color = normalCardColor;
            }
        }

        if (choicesContainer != null) choicesContainer.SetActive(true);

        if (promptText != null)
        {
            promptText.text = "🦶 <b>เหยียบเลือกผลไม้ที่จำได้!</b>";
        }
    }

    private void OnPlayerChoose(int slotIndex)
    {
        if (currentPhase != GamePhase.Answer || isHandlingAnswer) return;

        isHandlingAnswer = true;
        totalStomps++;

        // อ่านแรงเหยียบจาก Load Cell
        float force = 0f;
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            force = Mathf.Abs(SharedSerialReceiver.Instance.GetLoadCellRaw(slotIndex));
        }
        else
        {
            force = Random.Range(30f, 60f); // จำลองตอนเทสไม่มี Hardware
        }
        totalKgForce += force;
        if (force > maxSingleForce) maxSingleForce = force;

        bool isCorrect = (slotIndex == correctChoiceSlot);
        StartCoroutine(HandleChoiceFeedbackRoutine(slotIndex, isCorrect));
    }

    private IEnumerator HandleChoiceFeedbackRoutine(int chosenSlot, bool isCorrect)
    {
        currentPhase = GamePhase.Feedback;

        // ไฮไลต์การ์ดที่เหยียบ
        if (choiceCardBackgrounds != null && chosenSlot < choiceCardBackgrounds.Length && choiceCardBackgrounds[chosenSlot] != null)
        {
            choiceCardBackgrounds[chosenSlot].color = isCorrect ? correctCardColor : wrongCardColor;
        }

        if (isCorrect)
        {
            score++;
            correctCount++;
            UpdateScoreUI();
            if (promptText != null) promptText.text = "<color=#2ECC71>✨ <b>ถูกต้องยอดเยี่ยม! +1</b></color>";
        }
        else
        {
            wrongCount++;
            // ไฮไลต์ช่องที่ถูกให้ผู้เล่นดูเฉลยด้วย
            if (choiceCardBackgrounds != null && correctChoiceSlot < choiceCardBackgrounds.Length && choiceCardBackgrounds[correctChoiceSlot] != null)
            {
                choiceCardBackgrounds[correctChoiceSlot].color = correctCardColor;
            }
            if (promptText != null) promptText.text = "<color=#E74C3C>❌ <b>ยังไม่ถูกต้องนะ</b></color>";
        }

        // รอเวลาให้ผู้เล่นดูผลลัพธ์ 1 วินาที
        yield return new WaitForSeconds(1.2f);

        // คืนสีเดิม
        for (int slot = 0; slot < 4; slot++)
        {
            if (choiceCardBackgrounds != null && slot < choiceCardBackgrounds.Length && choiceCardBackgrounds[slot] != null)
            {
                choiceCardBackgrounds[slot].color = normalCardColor;
            }
        }

        // เริ่มข้อถัดไป
        StartNewRound();
    }

    // ═══════════════════════════════════════════════════════
    //  Game Over & Save Results
    // ═══════════════════════════════════════════════════════
    private void EndGame()
    {
        currentPhase = GamePhase.GameOver;

        // หยุดบันทึก CSV
        if (dataLogger != null) dataLogger.EndSession();

        // ซ่อนการ์ดทั้งหมด
        if (memorizeCard != null) memorizeCard.SetActive(false);
        if (choicesContainer != null) choicesContainer.SetActive(false);

        // บันทึกลง PlayerData
        PlayerData.FinalScore       = score;
        PlayerData.CorrectCount     = correctCount;
        PlayerData.WrongCount       = wrongCount;
        PlayerData.TimePlayed       = timePlayed;
        PlayerData.TotalKgForce     = totalKgForce;
        PlayerData.TotalStomps      = totalStomps;
        PlayerData.MaxSingleForce   = maxSingleForce;
        PlayerData.AvgForcePerStomp = totalStomps > 0 ? totalKgForce / totalStomps : 0f;

        // แสดงหน้าสรุปผล
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (finalScoreText != null)
            {
                finalScoreText.text = "คะแนน: " + score;
            }

            if (summaryDetailText != null)
            {
                int mins = Mathf.FloorToInt(timePlayed / 60f);
                int secs = Mathf.FloorToInt(timePlayed % 60f);
                summaryDetailText.text =
                    $"ผู้เล่น: {PlayerData.PlayerName}\n" +
                    $"เวลาเล่น: {mins:00}:{secs:00}\n" +
                    $"ตอบถูก: {correctCount} ข้อ  |  ผิด: {wrongCount} ข้อ\n" +
                    $"เหยียบทั้งหมด: {totalStomps} ครั้ง\n" +
                    $"Total Exercise: {totalKgForce:N0} kg-force\n" +
                    $"แรงเฉลี่ย: {PlayerData.AvgForcePerStomp:F1} kg/ครั้ง";
            }
        }

        // บันทึกผลขึ้น Firebase Firestore
        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SaveGameResult((success, msg) =>
            {
                Debug.Log($"[FruitMemory] Save to Firestore: {success} ({msg})");
            });
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "คะแนน: " + score;
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int mins = Mathf.FloorToInt(totalGameTime / 60f);
            int secs = Mathf.FloorToInt(totalGameTime % 60f);
            timeText.text = $"{mins:00}:{secs:00}";
        }
    }

    private void AutoLoadFruitSpritesIfEmpty()
    {
        // หากใน Inspector ยังไม่ได้ลากใส่ ให้ค้นหาไฟล์ใน Assets/Image/ อัตโนมัติ
        if (fruitSprites == null || fruitSprites.Length == 0)
        {
            fruitSprites = new Sprite[6];
            fruitSprites[0] = Resources.Load<Sprite>("Fruit_Apple");
            fruitSprites[1] = Resources.Load<Sprite>("Fruit_Banana");
            fruitSprites[2] = Resources.Load<Sprite>("Fruit_Orange");
            fruitSprites[3] = Resources.Load<Sprite>("Fruit_Watermelon");
            fruitSprites[4] = Resources.Load<Sprite>("Fruit_Grape");
            fruitSprites[5] = Resources.Load<Sprite>("Fruit_Strawberry");
        }
    }

    public void PlayAgain()
    {
        PlayerData.ResetResults();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoHome()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
