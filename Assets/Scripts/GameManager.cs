// ============================================================
//  GameManager.cs  (อัพเกรด — Phase 1)
//  ─────────────────────────────────────────────────────────────
//  เกม Whack-a-Mole — อ่านเวลาจาก PlayerData
//  ใช้ SharedSerialReceiver แทน HammerController Serial
//  จบเกม → เก็บผลลัพธ์ใน PlayerData → แสดง Summary Panel
// ============================================================

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("UI Settings")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI playerInfoText;   // แสดงชื่อ + เวลาที่ตั้ง

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI summaryDetailText;  // ข้อมูลสรุปละเอียด

    [Header("Buttons (Game Over Panel)")]
    public Button btnPlayAgain;
    public Button btnHome;

    [Header("Exit Confirmation Popup")]
    private GameObject exitConfirmPopup;
    private bool isConfirmingExit = false;
    private bool isGameOverState = false;

    [Header("Instruction Panel")]
    public GameObject instructionPanel;    // คำอธิบายก่อนเริ่มเล่น
    public Button btnStartGame;            // ปุ่มเริ่มเกมในหน้า Instruction

    [Header("Data Logger")]
    public DataLogger dataLogger;          // ลาก DataLogger มาใส่ใน Inspector

    [Header("Mole Spawner")]
    public MoleController[] moles;
    [Tooltip("ระยะเวลาที่ตุ่นจะโผล่ค้างไว้ก่อนมุดลงหลุมถ้าไม่มีใครตี (วินาที)")]
    public float moleActiveTime = 4f;
    [Tooltip("ช่วงเวลาเว้นว่างหลังจากตีตุ่นตาย ก่อนที่ตุ่นตัวใหม่จะเกิด (วินาที)")]
    public float respawnDelay = 1.0f;

    private float moleTimer;
    private float respawnTimer;
    private bool isWaitingForNextMole = false;
    private MoleController currentMole;

    private float currentTime;
    private float totalTimePlayed = 0f;
    private int score = 0;
    private bool isGameActive = false;

    // ═══════ สถิติ ═══════
    private int   totalStomps    = 0;
    private float totalKgForce   = 0f;
    private float maxSingleForce = 0f;
    private float[] padForces = new float[4];
    private int[] padStomps = new int[4];

    // ═══════ FSR state tracking (ป้องกันนับซ้ำ) ═══════
    private bool[] prevFsr = new bool[4];

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        // ตั้งเวลาจาก PlayerData (ถ้ามาจาก PlayerSetup)
        currentTime = PlayerData.GameDuration;
        if (currentTime <= 0f) currentTime = 60f;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // ตรวจสอบและสร้าง Instruction Panel อัตโนมัติถ้ายังไม่มีใน Scene
        if (instructionPanel == null)
        {
            CreateDefaultInstructionPanel();
        }

        // แสดง Instruction Panel ก่อน
        if (instructionPanel != null)
        {
            instructionPanel.SetActive(true);
            isGameActive = false;

            if (btnStartGame != null)
                btnStartGame.onClick.AddListener(StartGameAfterInstructions);
        }
        else
        {
            // ไม่มี Instruction Panel → เริ่มเลย
            StartGameAfterInstructions();
        }

        // ค้นหาปุ่มอัตโนมัติหากไม่ได้ลากใส่ใน Inspector
        if (btnPlayAgain == null)
        {
            GameObject rObj = GameObject.Find("RestartButton");
            if (rObj != null) btnPlayAgain = rObj.GetComponent<Button>();
            else if (gameOverPanel != null) btnPlayAgain = gameOverPanel.GetComponentInChildren<Button>();
        }

        if (btnHome == null)
        {
            GameObject hObj = GameObject.Find("HomeButton");
            if (hObj == null) hObj = GameObject.Find("Button");
            if (hObj != null) btnHome = hObj.GetComponent<Button>();
        }

        // ผูก Buttons
        if (btnPlayAgain != null)
        {
            btnPlayAgain.onClick.RemoveListener(PlayAgain);
            btnPlayAgain.onClick.AddListener(PlayAgain);
        }

        if (btnHome != null)
        {
            btnHome.onClick.RemoveListener(OpenExitConfirmPopup);
            btnHome.onClick.AddListener(OpenExitConfirmPopup);
            AttachHomeSubLabel(btnHome);
        }

        // แสดงข้อมูลผู้เล่น
        if (playerInfoText != null)
        {
            int mins = Mathf.FloorToInt(PlayerData.GameDuration / 60f);
            playerInfoText.text = PlayerData.PlayerName + " | " + mins + " นาที";
        }

        score = 0;
        UpdateScoreUI();
        isWaitingForNextMole = false;

        // ซ่อน Mole ทั้งหมดก่อน
        if (moles != null)
        {
            foreach (MoleController mole in moles)
            {
                if (mole != null) mole.ForceHide();
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  สร้าง Popup คำอธิบายอัตโนมัติหากใน Scene ยังไม่มี
    // ═══════════════════════════════════════════════════════
    private void CreateDefaultInstructionPanel()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset thaiFont = Resources.Load<TMP_FontAsset>("LeelawadeeUI SDF");
        Sprite cardSprite = Resources.Load<Sprite>("Card_Popup");
        if (cardSprite == null) cardSprite = Resources.Load<Sprite>("RoundedCard_White");

        // 1. Overlay สีดำโปร่งแสง
        instructionPanel = new GameObject("InstructionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        instructionPanel.transform.SetParent(canvas.transform, false);
        instructionPanel.transform.SetAsLastSibling();

        RectTransform overlayRt = instructionPanel.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.anchoredPosition = Vector2.zero;
        overlayRt.sizeDelta = Vector2.zero;

        Image overlayImg = instructionPanel.GetComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.65f);

        // 2. Card พื้นหลังสีขาวขอบมน
        GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(instructionPanel.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(950, 620);

        Image cardImg = cardObj.GetComponent<Image>();
        if (cardSprite != null)
        {
            cardImg.sprite = cardSprite;
            cardImg.type = Image.Type.Sliced;
        }
        cardImg.color = Color.white;

        // 3. Title Text
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchoredPosition = new Vector2(0, 220);
        titleRt.sizeDelta = new Vector2(850, 65);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) titleTmp.font = thaiFont;
        titleTmp.text = "วิธีเล่นเกมตีตุ่น";
        titleTmp.fontSize = 50;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.85f, 0.45f, 0.05f, 1f); // ส้มทอง Amber

        // 4. Description Text
        GameObject descObj = new GameObject("DescText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(cardObj.transform, false);
        RectTransform descRt = descObj.GetComponent<RectTransform>();
        descRt.anchoredPosition = new Vector2(0, 30);
        descRt.sizeDelta = new Vector2(850, 240);

        TextMeshProUGUI descTmp = descObj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) descTmp.font = thaiFont;
        descTmp.text = "เมื่อเริ่มเกม ตัวตุ่นจะสุ่มโผล่ขึ้นมาจากหลุม 4 มุม\n\n<b>เหยียบแผ่นเท้าตามมุมที่ตัวตุ่นโผล่ขึ้นมาให้เร็วที่สุด!</b>\n\nตีให้ทันก่อนที่ตุ่นจะมุดลงหลุมเพื่อเก็บคะแนน";
        descTmp.fontSize = 32;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = new Color(0.2f, 0.25f, 0.33f, 1f);
        descTmp.lineSpacing = 12;

        // 5. Start Prompt Text
        GameObject promptObj = new GameObject("PromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObj.transform.SetParent(cardObj.transform, false);
        RectTransform promptRt = promptObj.GetComponent<RectTransform>();
        promptRt.anchoredPosition = new Vector2(0, -225);
        promptRt.sizeDelta = new Vector2(850, 55);

        TextMeshProUGUI promptTmp = promptObj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) promptTmp.font = thaiFont;
        promptTmp.text = "เหยียบปุ่มใดก็ได้ (หรือคลิก) เพื่อเริ่มเล่นเกม";
        promptTmp.fontSize = 34;
        promptTmp.fontStyle = FontStyles.Bold;
        promptTmp.alignment = TextAlignmentOptions.Center;
        promptTmp.color = new Color(0.05f, 0.6f, 0.35f, 1f); // เขียว Emerald

        // สามารถคลิกบนการ์ดเพื่อเริ่มเกมได้ด้วย
        Button btn = cardObj.AddComponent<Button>();
        btn.onClick.AddListener(StartGameAfterInstructions);
    }

    // ═══════════════════════════════════════════════════════
    //  เริ่มเกมหลังอ่านคำอธิบาย
    // ═══════════════════════════════════════════════════════
    public void StartGameAfterInstructions()
    {
        if (instructionPanel != null) instructionPanel.SetActive(false);

        // - เริ่มบันทึก CSV ตอนเกมเริ่มจริงๆ
        if (dataLogger != null) dataLogger.BeginSession();

        isGameActive = true;
        isWaitingForNextMole = false;
        SpawnRandomMole();
    }

    // ═══════════════════════════════════════════════════════
    void Update()
    {
        // ─── ช่วงเปิดหน้าต่างถามยืนยันก่อนออกจากเกม ───
        if (isConfirmingExit)
        {
            // บนซ้าย (TL / Pad 0 / Q / Enter) -> ยืนยันออกจากเกม
            bool tlStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(0));
            bool tlKey = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Return);
            if (tlStomp || tlKey)
            {
                ConfirmExitGame();
                return;
            }

            // ล่างขวา (BR / Pad 3 / S / Esc) -> ยกเลิก/เล่นต่อ
            bool brStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(3));
            bool brKey = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Escape);
            if (brStomp || brKey)
            {
                CancelExitGame();
                return;
            }
            return;
        }

        // ─── ช่วงจบเกม (Game Over) ───
        if (isGameOverState)
        {
            bool tlStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(0));
            bool tlKey = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Return);
            if (tlStomp || tlKey)
            {
                PlayAgain();
                return;
            }

            bool brStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(3));
            bool brKey = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Escape);
            if (brStomp || brKey)
            {
                GoHome();
                return;
            }
            return;
        }

        // ─── ปุ่มลัดเปิดหน้าต่างยืนยันกลับหน้าหลัก (Esc หรือ H) ───
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H))
        {
            OpenExitConfirmPopup();
            return;
        }

        // หากหน้าคำอธิบายยังเปิดอยู่ ให้รอผู้เล่นเหยียบปุ่มใดก็ได้ หรือกดคีย์บอร์ด / คลิก เพื่อเริ่มเกม
        if (!isGameActive)
        {
            if (instructionPanel != null && instructionPanel.activeSelf)
            {
                bool anyPad = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.GetFirstPressedPad() != -1);
                bool anyKey = Input.anyKeyDown;
                bool anyClick = Input.GetMouseButtonDown(0);

                if (anyPad || anyKey || anyClick)
                {
                    StartGameAfterInstructions();
                }
            }
            return;
        }

        // ─── นับเวลาถอยหลัง ───
        currentTime -= Time.deltaTime;
        totalTimePlayed += Time.deltaTime;

        if (timeText != null)
        {
            int mins = Mathf.FloorToInt(currentTime / 60f);
            int secs = Mathf.FloorToInt(currentTime % 60f);
            timeText.text = string.Format("{0:00}:{1:00}", mins, secs);
        }

        // ─── หมดเวลา? ───
        if (currentTime <= 0)
        {
            currentTime = 0;
            GameOver();
            return;
        }

        // ─── การจัดการตุ่น (State Machine: โผล่อยู่ vs รอเกิดใหม่) ───
        if (isWaitingForNextMole)
        {
            // ช่วงเว้นเวลา (เช่น 1 วินาที) หลังตีตุ่นตาย ก่อนตัวใหม่จะเกิด
            // เวลานี้จะนับถอยหลังอย่างคงที่ ไม่ถูกการกดย้ำๆ รีเซ็ตเวลาเพิ่มเด็ดขาด
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                isWaitingForNextMole = false;
                SpawnRandomMole();
            }
        }
        else
        {
            // ตุ่นกำลังโผล่อยู่ รอให้ผู้เล่นตี
            moleTimer -= Time.deltaTime;
            if (moleTimer <= 0f)
            {
                // ตีไม่ทันจนหมดเวลา -> มุดลงหลุม แล้วเว้นช่วงเวลาสั้นๆ ก่อนเกิดตัวใหม่
                if (currentMole != null)
                {
                    currentMole.ForceHide();
                }
                isWaitingForNextMole = true;
                respawnTimer = 0.5f;
            }
        }

        // ══════════════════════════════════════════════════
        //  Input: ใช้ SharedSerialReceiver (ถ้ามี) หรือ Keyboard
        // ══════════════════════════════════════════════════
        bool[] currentFsr = new bool[4];

        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            // ─── Hardware Mode (SharedSerialReceiver) ───
            for (int i = 0; i < 4; i++)
                currentFsr[i] = SharedSerialReceiver.Instance.IsPadPressed(i);
        }
        else if (HammerController.IsConnected)
        {
            // ─── Hardware Mode (HammerController) ───
            for (int i = 0; i < 4; i++)
                currentFsr[i] = (HammerController.FsrStates[i] == 1);
        }
        else
        {
            // ─── Keyboard Fallback ───
            currentFsr[0] = Input.GetKey(KeyCode.Q);   // Top-Left
            currentFsr[1] = Input.GetKey(KeyCode.W);   // Top-Right
            currentFsr[2] = Input.GetKey(KeyCode.A);   // Bottom-Left
            currentFsr[3] = Input.GetKey(KeyCode.S);   // Bottom-Right
        }

        // ─── เหยียบ BL + BR พร้อมกัน = เปิดหน้าต่างยืนยันกลับหน้าหลัก ───
        if (currentFsr[2] && currentFsr[3])
        {
            OpenExitConfirmPopup();
            return;
        }

        // ─── ตรวจจับ "เพิ่งกด" (Rising Edge) เพื่อไม่ให้นับซ้ำ ───
        for (int i = 0; i < 4; i++)
        {
            if (currentFsr[i] && !prevFsr[i])
            {
                // เพิ่งกดแผ่นที่ i → เช็คว่า Mole อยู่ตำแหน่งนี้ไหม
                OnPadStomped(i);
            }
            prevFsr[i] = currentFsr[i];
        }
    }

    // ═══════════════════════════════════════════════════════
    //  เมื่อเหยียบแผ่นที่ padIndex (0-3)
    // ═══════════════════════════════════════════════════════
    private void OnPadStomped(int padIndex)
    {
        // ปฏิเสธทันทีถ้าเกมไม่ทำงาน กำลังรอเกิดใหม่ หรือตุ่นตายไปแล้ว (1 ตุ่น = 1 คะแนน)
        if (!isGameActive || isWaitingForNextMole || currentMole == null || !currentMole.IsAlive) return;

        // เช็คว่า Mole ตัวปัจจุบันอยู่ตำแหน่ง padIndex หรือไม่
        int moleIndex = System.Array.IndexOf(moles, currentMole);
        if (moleIndex == padIndex)
        {
            ProcessHit(padIndex);
        }
    }

    /// <summary>
    /// เรียกจาก HammerController เมื่อชนด้วย Collider (Physics)
    /// </summary>
    public void OnMoleHitByHammer(MoleController mole)
    {
        if (!isGameActive || isWaitingForNextMole || currentMole == null || !currentMole.IsAlive) return;
        if (mole != currentMole) return;

        int moleIndex = System.Array.IndexOf(moles, currentMole);
        if (moleIndex >= 0)
        {
            ProcessHit(moleIndex);
        }
    }

    /// <summary>
    /// ประมวลผลการตีตุ่นสำเร็จ: ตรวจสอบสถานะ ให้ 1 คะแนนแน่นอน และเริ่มนับเวลาเว้นช่วงก่อนเกิดใหม่
    /// </summary>
    private void ProcessHit(int padIndex)
    {
        // สั่งให้ตุ่นเปลี่ยนเป็นรูปตาย — Hit() คืนค่า true เฉพาะการตีครั้งแรกที่ถูกต้องเท่านั้น
        if (currentMole == null || !currentMole.Hit()) return;

        // 1 ตุ่น = 1 คะแนนอย่างแน่นอน
        score++;
        totalStomps++;
        UpdateScoreUI();

        // อ่านค่า Load Cell (ถ้ามี)
        float force = 0f;
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            force = Mathf.Abs(SharedSerialReceiver.Instance.GetLoadCellRaw(padIndex));
        }
        else if (HammerController.IsConnected && HammerController.LoadCellValues != null)
        {
            force = Mathf.Abs(HammerController.LoadCellValues[padIndex]);
        }
        else
        {
            force = Random.Range(30f, 60f); // จำลองตอนไม่มี Hardware
        }
        totalKgForce += force;
        if (force > maxSingleForce) maxSingleForce = force;
        if (padIndex >= 0 && padIndex < 4)
        {
            padStomps[padIndex]++;
            padForces[padIndex] += force;
        }

        // เข้าสู่สถานะรอเกิดใหม่ (เว้นช่วงเวลา respawnDelay เช่น 1.0 วินาที)
        // ระหว่างนี้จะไม่รับการตีซ้ำ และจะไม่รีเซ็ตเวลาเพิ่มเด็ดขาด
        isWaitingForNextMole = true;
        respawnTimer = respawnDelay;
    }

    // ═══════════════════════════════════════════════════════
    //  Mole Spawner
    // ═══════════════════════════════════════════════════════
    public void SpawnRandomMole()
    {
        if (moles == null || moles.Length == 0) return;

        // ซ่อนตุ่นทุกตัวให้เรียบร้อยก่อน
        foreach (MoleController mole in moles)
        {
            if (mole != null) mole.ForceHide();
        }

        int randomIndex = Random.Range(0, moles.Length);

        // ไม่ให้ spawn ซ้ำตำแหน่งเดิม
        int attempts = 0;
        while (moles.Length > 1 && moles[randomIndex] == currentMole && attempts < 10)
        {
            randomIndex = Random.Range(0, moles.Length);
            attempts++;
        }

        currentMole = moles[randomIndex];
        currentMole.ShowMole();

        isWaitingForNextMole = false;
        moleTimer = moleActiveTime;
    }

    // ═══════════════════════════════════════════════════════
    //  Score & Game Over
    // ═══════════════════════════════════════════════════════
    public void AddScore(int amount)
    {
        if (!isGameActive) return;
        score += amount;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }

    private void GameOver()
    {
        isGameActive = false;
        isWaitingForNextMole = false;

        // - หยุดบันทึก CSV
        if (dataLogger != null) dataLogger.EndSession();

        if (timeText != null)
        {
            timeText.text = "00:00";
        }

        // ซ่อน Mole ทั้งหมด
        if (moles != null)
        {
            foreach (MoleController mole in moles)
            {
                if (mole != null) mole.ForceHide();
            }
        }

        // ═══ เก็บผลลัพธ์ใน PlayerData ═══
        PlayerData.FinalScore       = score;
        PlayerData.TimePlayed       = totalTimePlayed;
        PlayerData.TotalKgForce     = totalKgForce;
        PlayerData.TotalStomps      = totalStomps;
        PlayerData.MaxSingleForce   = maxSingleForce;
        PlayerData.AvgForcePerStomp = totalStomps > 0 ? totalKgForce / totalStomps : 0f;

        // ═══ แสดง Game Over Panel / Dashboard สรุปผล ═══
        if (gameOverPanel != null)
        {
            isGameOverState = true;
            GameSummaryUI summaryUI = gameOverPanel.GetComponent<GameSummaryUI>();
            if (summaryUI == null) summaryUI = gameOverPanel.AddComponent<GameSummaryUI>();

            float avgSecPerMole = score > 0 ? totalTimePlayed / score : 0f;
            string perfDetail = $"ตีสำเร็จ: {score} ครั้ง\nความเร็วเฉลี่ย: {avgSecPerMole:F1} วินาที/ตัว";

            summaryUI.ShowSummary(
                gameDisplayName: "Whack-a-Mole",
                score: score,
                timePlayed: totalTimePlayed,
                totalForce: totalKgForce,
                totalStomps: totalStomps,
                maxForce: maxSingleForce,
                padForces: padForces,
                padStomps: padStomps,
                extraPerfDetail: perfDetail,
                onPlayAgain: PlayAgain,
                onGoHome: GoHome
            );
        }

        // ═══ นำปุ่ม Home ขึ้นมาอยู่บนสุด เพื่อไม่ให้ GameOverPanel บังการคลิก ═══
        if (btnHome != null)
        {
            btnHome.transform.SetAsLastSibling();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Navigation Buttons
    // ═══════════════════════════════════════════════════════
    public void PlayAgain()
    {
        PlayerData.ResetResults();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Alias สำหรับปุ่มใน Unity Inspector ที่ตั้งชื่อ RestartGame
    public void RestartGame()
    {
        PlayAgain();
    }

    public void GoHome()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void Home()
    {
        GoHome();
    }

    private void AttachHomeSubLabel(Button btn)
    {
        if (btn == null) return;
        Transform existing = btn.transform.Find("HomeSubLabel");
        if (existing != null) return;

        GameObject labelObj = new GameObject("HomeSubLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(btn.transform, false);

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -6);
        rt.sizeDelta = new Vector2(220, 36);

        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset thaiFont = Resources.Load<TMP_FontAsset>("LeelawadeeUI SDF");
        if (thaiFont != null) tmp.font = thaiFont;
        tmp.text = "ล่างซ้าย + ล่างขวา";
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.18f, 0.22f, 0.32f, 1f);
    }

    private void SetButtonText(Button btn, string text)
    {
        if (btn == null) return;
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  หน้าต่างยืนยันก่อนออกจากเกมกลับหน้าหลัก
    // ═══════════════════════════════════════════════════════
    public void OpenExitConfirmPopup()
    {
        if (exitConfirmPopup == null)
        {
            CreateExitConfirmPopup();
        }
        isConfirmingExit = true;
        exitConfirmPopup.SetActive(true);
        exitConfirmPopup.transform.SetAsLastSibling();
    }

    public void CancelExitGame()
    {
        isConfirmingExit = false;
        if (exitConfirmPopup != null)
        {
            exitConfirmPopup.SetActive(false);
        }
    }

    public void ConfirmExitGame()
    {
        isConfirmingExit = false;
        if (dataLogger != null)
        {
            dataLogger.CancelSession();
        }
        PlayerData.ResetResults();
        SceneManager.LoadScene("MainMenu");
    }

    private void CreateExitConfirmPopup()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("LeelawadeeUI SDF");
        Sprite cardSprite = Resources.Load<Sprite>("Card_Popup");

        // 1. Dark Overlay
        exitConfirmPopup = new GameObject("ExitConfirmPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        exitConfirmPopup.transform.SetParent(canvas.transform, false);
        exitConfirmPopup.transform.SetAsLastSibling();

        RectTransform overlayRt = exitConfirmPopup.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.anchoredPosition = Vector2.zero;
        overlayRt.sizeDelta = Vector2.zero;

        Image overlayImg = exitConfirmPopup.GetComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.72f);

        // 2. White Card
        GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(exitConfirmPopup.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(820, 520);

        Image cardImg = cardObj.GetComponent<Image>();
        if (cardSprite != null)
        {
            cardImg.sprite = cardSprite;
            cardImg.type = Image.Type.Sliced;
        }
        cardImg.color = Color.white;

        // 3. Title Text
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchoredPosition = new Vector2(0, 170);
        titleRt.sizeDelta = new Vector2(760, 55);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (font != null) titleTmp.font = font;
        titleTmp.text = "ต้องการกลับหน้าหลักหรือไม่?";
        titleTmp.fontSize = 42;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.18f, 0.22f, 0.30f, 1f);

        // 4. Subtitle Text
        GameObject subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchoredPosition = new Vector2(0, 95);
        subRt.sizeDelta = new Vector2(760, 45);

        TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>();
        if (font != null) subTmp.font = font;
        subTmp.text = "หากออกตอนนี้ ข้อมูลการเล่นในรอบนี้จะไม่ถูกบันทึก";
        subTmp.fontSize = 26;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.45f, 0.50f, 0.60f, 1f);

        // 5. Button Exit (Confirm) - Red / Coral
        GameObject btnExitObj = new GameObject("BtnConfirmExit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnExitObj.transform.SetParent(cardObj.transform, false);
        RectTransform btnExitRt = btnExitObj.GetComponent<RectTransform>();
        btnExitRt.anchoredPosition = new Vector2(-175, -45);
        btnExitRt.sizeDelta = new Vector2(280, 115);

        Image exitImg = btnExitObj.GetComponent<Image>();
        if (cardSprite != null) { exitImg.sprite = cardSprite; exitImg.type = Image.Type.Sliced; }
        exitImg.color = new Color(0.90f, 0.30f, 0.25f, 1f);

        Button exitBtn = btnExitObj.GetComponent<Button>();
        exitBtn.onClick.AddListener(ConfirmExitGame);

        GameObject exitTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        exitTxtObj.transform.SetParent(btnExitObj.transform, false);
        RectTransform exitTxtRt = exitTxtObj.GetComponent<RectTransform>();
        exitTxtRt.anchorMin = Vector2.zero;
        exitTxtRt.anchorMax = Vector2.one;
        exitTxtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI exitTxt = exitTxtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) exitTxt.font = font;
        exitTxt.text = "ออกจากเกม\n<size=75%>(เหยียบ บนซ้าย)</size>";
        exitTxt.fontSize = 26;
        exitTxt.fontStyle = FontStyles.Bold;
        exitTxt.alignment = TextAlignmentOptions.Center;
        exitTxt.color = Color.white;

        // 6. Button Cancel (Stay) - Green
        GameObject btnCancelObj = new GameObject("BtnCancelExit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnCancelObj.transform.SetParent(cardObj.transform, false);
        RectTransform btnCancelRt = btnCancelObj.GetComponent<RectTransform>();
        btnCancelRt.anchoredPosition = new Vector2(175, -45);
        btnCancelRt.sizeDelta = new Vector2(280, 115);

        Image cancelImg = btnCancelObj.GetComponent<Image>();
        if (cardSprite != null) { cancelImg.sprite = cardSprite; cancelImg.type = Image.Type.Sliced; }
        cancelImg.color = new Color(0.18f, 0.70f, 0.38f, 1f);

        Button cancelBtn = btnCancelObj.GetComponent<Button>();
        cancelBtn.onClick.AddListener(CancelExitGame);

        GameObject cancelTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        cancelTxtObj.transform.SetParent(btnCancelObj.transform, false);
        RectTransform cancelTxtRt = cancelTxtObj.GetComponent<RectTransform>();
        cancelTxtRt.anchorMin = Vector2.zero;
        cancelTxtRt.anchorMax = Vector2.one;
        cancelTxtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI cancelTxt = cancelTxtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) cancelTxt.font = font;
        cancelTxt.text = "เล่นต่อ\n<size=75%>(เหยียบ ล่างขวา)</size>";
        cancelTxt.fontSize = 26;
        cancelTxt.fontStyle = FontStyles.Bold;
        cancelTxt.alignment = TextAlignmentOptions.Center;
        cancelTxt.color = Color.white;

        // 7. Hint text at bottom
        GameObject hintObj = new GameObject("HintText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        hintObj.transform.SetParent(cardObj.transform, false);
        RectTransform hintRt = hintObj.GetComponent<RectTransform>();
        hintRt.anchoredPosition = new Vector2(0, -185);
        hintRt.sizeDelta = new Vector2(760, 40);

        TextMeshProUGUI hintTmp = hintObj.GetComponent<TextMeshProUGUI>();
        if (font != null) hintTmp.font = font;
        hintTmp.text = "เหยียบแผ่นเท้าตามคำสั่งบนปุ่ม หรือคลิกเลือกได้เลย";
        hintTmp.fontSize = 22;
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.color = new Color(0.55f, 0.60f, 0.68f, 1f);
    }
}