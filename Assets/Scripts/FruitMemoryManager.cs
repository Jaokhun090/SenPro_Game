// ============================================================
//  FruitMemoryManager.cs
//  ─────────────────────────────────────────────────────────────
//  เกมที่ 2: จำภาพและลำดับผลไม้ (Game02: Sequence Memory Game)
//  สำหรับฟื้นฟูสมรรถภาพความจำและการลงน้ำหนักเท้าของผู้สูงอายุ
//
//  - กฎการเล่น:
//    - แผ่นเท้า 4 มุม (และปุ่ม Q, W, A, S):
//        TL (Pad 0 / Q) = แอปเปิ้ล (Apple)
//        TR (Pad 1 / W) = มะละกอ (Papaya)
//        BL (Pad 2 / A) = กล้วย (Banana)
//        BR (Pad 3 / S) = ส้ม (Orange)
//    - ก่อนเริ่มแต่ละรอบ: แสดง Popup "จำลำดับผลไม้!" แสดงผลไม้ที่ต้องจำ (1, 3, 5 หรือ 7 รูป)
//        ผู้เล่นสามารถดูและจำผลไม้ได้โดยไม่ถูกตัดเวลา
//        เหยียบปุ่มใดก็ได้ (หรือคลิก) เพื่อเริ่มเล่น
//    - แถบตรงกลาง (แถบสีเขียวเข้ม): แสดงผลไม้ที่ผู้เล่นเหยียบไป (Player Input History)
//        เริ่มรอบจะเป็นช่องว่าง รอรับการเหยียบ
//        เมื่อเหยียบจะแสดงผลไม้ที่เหยียบลงไปในช่อง
//        - ถ้าเหยียบถูก: ขึ้นสีกรอบเขียว + เครื่องหมายถูก และไปลำดับถัดไป
//          (เหยียบครบทั้งชุด: คะแนน +1 และขึ้น Popup ชุดใหม่)
//        - ถ้าเหยียบผิด: ขึ้นสีกรอบแดง และเปิด Popup ลำดับเดิมให้จำใหม่อีกครั้ง
//    - เวลาเล่นรวมทุกระดับความยาก: 3 นาที (180 วินาที)
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
        Memorizing,   // กำลังเปิด Popup แสดงลำดับผลไม้ให้ผู้เล่นจำ
        Playing,      // ผู้เล่นกำลังเหยียบตามลำดับ
        RoundSuccess, // เหยียบครบชุดถูกต้อง
        GameOver      // หมดเวลาเล่น
    }

    [Header("Game State")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Memorizing;

    [Header("Header UI")]
    public TextMeshProUGUI timeText;          // แสดงเวลานับถอยหลัง เช่น "03:00"
    public TextMeshProUGUI scoreText;         // คะแนนรวม
    public TextMeshProUGUI difficultyText;    // ระดับความยาก เช่น "ระดับ 2 (จำ 3 รูป)"
    public TextMeshProUGUI promptText;        // คำแนะนำ
    public Button btnHome;                    // ปุ่มรูปบ้านกลับหน้าหลัก

    [Header("Center Sequence Strip (แถบสีเขียวเข้มตรงกลาง)")]
    public Image sequencePanelImage;          // แผงสีเขียวเข้มตรงกลาง
    public GameObject sequenceContainer;      // Container ของลำดับผลไม้

    [Header("4 Corner Fruit Cards (แผ่นผลไม้ 4 มุม)")]
    // 0 = TL (Apple), 1 = TR (Papaya), 2 = BL (Banana), 3 = BR (Orange)
    public Button[] padButtons;               // ปุ่ม 4 มุม สำหรับคลิกเมาส์
    public Image[] padCardBackgrounds;        // พื้นหลังการ์ดสีขาว (เปลี่ยนเป็นเขียว/แดงตอนเหยียบ)
    public Image[] padFruitImages;            // รูปผลไม้ประจำแต่ละมุม
    public TextMeshProUGUI[] padLabels;       // ป้ายบอกแผ่น เช่น "บนซ้าย (Q)", "บนขวา (W)"...

    [Header("Fruit Database (Sprites)")]
    // 0 = Apple, 1 = Papaya, 2 = Banana, 3 = Orange
    public Sprite[] fruitSprites;
    public string[] fruitNames = new string[]
    {
        "แอปเปิ้ล",
        "มะละกอ",
        "กล้วย",
        "ส้ม"
    };

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI summaryDetailText;
    public Button btnPlayAgain;
    public Button btnGameOverHome;

    [Header("Exit Confirmation Popup")]
    private GameObject exitConfirmPopup;
    private bool isConfirmingExit = false;

    [Header("Thai Font Settings")]
    public TMP_FontAsset thaiFont;

    [Header("Data Logger")]
    public DataLogger dataLogger;

    // ── Internal Slot Tracking in Center Strip ──
    private Image[] sequenceSlotBgs;
    private Image[] sequenceSlotImages;
    private GameObject[] sequenceSlotChecks;
    private TextMeshProUGUI[] sequenceSlotNumbers;

    // ── Popup UI References ──
    private GameObject memorizePopup;
    private Transform popupFruitContainer;
    private TextMeshProUGUI popupTitleText;
    private TextMeshProUGUI popupSubtitleText;

    // ── Internal Game Variables ──
    private float totalGameTime = 180f;       // เวลาเล่น 3 นาทีตายตัว
    private float timePlayed = 0f;
    private int score = 0;
    private int correctRounds = 0;
    private int wrongStomps = 0;
    private int totalStomps = 0;
    private float totalKgForce = 0f;
    private float maxSingleForce = 0f;
    private float[] padForces = new float[4];
    private int[] padStomps = new int[4];

    private int sequenceLength = 1;           // จำนวนผลไม้ที่ต้องจำ (1, 3, 5, หรือ 7)
    private List<int> targetSequence = new List<int>();
    private int currentStepIndex = 0;
    private bool isProcessingRound = false;
    private bool dataLoggerStarted = false;
    private float stompCooldown = 0f;
    private bool currentRoundHadMistake = false; // ตรวจสอบเงียบๆ หลังบ้านว่ารอบนี้มีเหยียบผิดหรือไม่
    private int totalRoundsCompleted = 0;        // จำนวนรอบที่เล่นจบไปแล้ว (สำหรับขึ้นเลขรอบ)

    // สีสำหรับการแสดงผล
    private readonly Color normalCardColor    = Color.white;
    private readonly Color correctCardColor   = new Color(0.35f, 0.90f, 0.45f, 1f); // เขียวพาสเทล
    private readonly Color wrongCardColor     = new Color(1.0f, 0.38f, 0.38f, 1f);  // แดงเตือน
    private readonly Color defaultSlotBgColor = new Color(1f, 1f, 1f, 0.18f);      // พื้นหลังช่องว่างในแถบกลาง
    private readonly Color correctSlotBgColor = new Color(0.35f, 0.90f, 0.45f, 0.95f);
    private readonly Color wrongSlotBgColor   = new Color(1.0f, 0.38f, 0.38f, 0.95f);

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        PlayerData.GameDisplayName = "เกมจำภาพผลไม้";

        // โหลดฟอนต์ภาษาไทยเพื่อไม่ให้เกิดสี่เหลี่ยม □
        if (thaiFont == null)
        {
            thaiFont = Resources.Load<TMP_FontAsset>("LeelawadeeUI SDF");
        }
        ApplyThaiFontToUI();

        // อ่านจำนวนผลไม้ที่ต้องจำจาก PlayerData (1, 3, 5, 7)
        sequenceLength = PlayerData.GameDifficulty;
        if (sequenceLength <= 0) sequenceLength = 1;
        sequenceLength = Mathf.Clamp(sequenceLength, 1, 7);

        // เวลาเล่น 3 นาที (180s)
        totalGameTime = PlayerData.GameDuration;
        if (totalGameTime <= 0f) totalGameTime = 180f;

        // ซ่อนคะแนนบนหน้าจอระหว่างเล่น เพื่อลดความกดดันของผู้สูงอายุตามหลักการแพทย์
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }

        if (promptText != null)
        {
            promptText.color = new Color(0.18f, 0.22f, 0.30f, 1f);
        }

        // ซ่อน Game Over
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // ผูกปุ่ม Navigation
        if (btnHome != null)
        {
            btnHome.onClick.RemoveListener(OpenExitConfirmPopup);
            btnHome.onClick.AddListener(OpenExitConfirmPopup);
            AttachHomeSubLabel(btnHome);
        }
        if (btnPlayAgain != null) btnPlayAgain.onClick.AddListener(PlayAgain);
        if (btnGameOverHome != null) btnGameOverHome.onClick.AddListener(GoHome);

        // โหลดรูปผลไม้ถ้ารายการว่าง
        AutoLoadFruitSprites();

        // ตั้งค่าผลไม้ 4 มุมจอ
        SetupCornerCards();

        // ผูกปุ่มการ์ด 4 มุม (สำหรับคลิกด้วยเมาส์ทดสอบ)
        if (padButtons != null)
        {
            for (int i = 0; i < padButtons.Length; i++)
            {
                int index = i;
                if (padButtons[i] != null)
                {
                    padButtons[i].onClick.AddListener(() => OnPlayerStomp(index));
                }
            }
        }

        // อัปเดต UI เบื้องต้น
        UpdateScoreUI();
        UpdateTimeUI();
        if (difficultyText != null)
        {
            difficultyText.text = $"ระดับ (จำ {sequenceLength} รูป)";
        }

        // ปรับขนาดกรอบ promptText ให้กว้างและสูงพอ สระและวรรณยุกต์ไทยจะไม่หล่นหรือตัดขอบ
        if (promptText != null)
        {
            RectTransform prt = promptText.GetComponent<RectTransform>();
            if (prt != null)
            {
                prt.sizeDelta = new Vector2(950f, 65f);
            }
            promptText.fontSize = 28;
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            promptText.overflowMode = TextOverflowModes.Overflow;
        }

        // สร้างและจัด UI แถบแสดงลำดับผลไม้ตรงกลาง (ช่องว่างรอผู้เล่นเหยียบ)
        InitializeSequenceStripUI();

        // สร้าง Popup คำอธิบายและโชว์โจทย์ผลไม้ที่ต้องจำ
        CreateMemorizePopup();

        // เริ่มชุดแรก
        StartNewSequence(isFirstRound: true);
    }

    private void ApplyFontAndOutline(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        if (thaiFont != null) tmp.font = thaiFont;
    }

    private void ApplyThaiFontToUI()
    {
        ApplyFontAndOutline(promptText);
        ApplyFontAndOutline(scoreText);
        ApplyFontAndOutline(timeText);
        ApplyFontAndOutline(difficultyText);
        ApplyFontAndOutline(finalScoreText);
        ApplyFontAndOutline(summaryDetailText);

        if (gameOverPanel != null)
        {
            foreach (var tmp in gameOverPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                ApplyFontAndOutline(tmp);
            }
        }

        if (padLabels != null)
        {
            foreach (var lbl in padLabels)
            {
                ApplyFontAndOutline(lbl);
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  สร้างและจัดโครงสร้างแถบตรงกลาง (Center Sequence Strip)
    //  แสดงผลไม้ที่ผู้เล่นเหยียบไป (เริ่มต้นจะเป็นช่องว่างรอรับการเหยียบ)
    // ═══════════════════════════════════════════════════════
    private void InitializeSequenceStripUI()
    {
        if (sequenceContainer == null) return;

        // ล้าง child เก่าที่อาจมีอยู่
        for (int i = sequenceContainer.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(sequenceContainer.transform.GetChild(i).gameObject);
        }

        // ตั้งค่าขนาดและพื้นหลังของแถบตรงกลางให้กว้างและสูงขึ้น (300 x 750px) สวยงาม ไม่เบียด ไม่ล้นขอบโค้ง
        RectTransform contRt = sequenceContainer.GetComponent<RectTransform>();
        if (contRt != null)
        {
            contRt.anchoredPosition = new Vector2(0f, -20f);
            contRt.sizeDelta = new Vector2(300f, 750f);
        }

        Image panelImg = sequenceContainer.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.color = new Color(0.05f, 0.28f, 0.16f, 1f);
            panelImg.type = Image.Type.Sliced; // 9-Slice รักษารัศมีมุมมน ไม่ให้ส่วนหัวและท้ายบวมหรือกินพื้นที่เกินไป
        }

        // ตั้งค่า VerticalLayoutGroup พร้อมเพิ่ม Padding หัวและท้าย ไม่ให้การ์ดชนขอบโค้ง
        VerticalLayoutGroup vlg = sequenceContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = sequenceContainer.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.padding = new RectOffset(0, 0, 45, 45); // เว้นระยะหัวและท้ายอย่างละ 45px

        float slotW = 160f;
        float slotH = 135f;
        float spacing = 18f;
        float fruitSize = 105f;
        int numFontSize = 32;

        if (sequenceLength == 1)
        {
            slotW = 200f; slotH = 200f; spacing = 0f;
            fruitSize = 160f; numFontSize = 46;
        }
        else if (sequenceLength == 3)
        {
            slotW = 180f; slotH = 155f; spacing = 28f;
            fruitSize = 125f; numFontSize = 40;
        }
        else if (sequenceLength == 5)
        {
            slotW = 150f; slotH = 105f; spacing = 16f;
            fruitSize = 85f; numFontSize = 30;
        }
        else if (sequenceLength >= 7)
        {
            slotW = 130f; slotH = 78f; spacing = 10f;
            fruitSize = 65f; numFontSize = 24;
        }

        vlg.spacing = spacing;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        Sprite cardSprite = Resources.Load<Sprite>("Card_Popup");
        Sprite checkSprite = Resources.Load<Sprite>("Icon_Checkmark");

        sequenceSlotBgs     = new Image[sequenceLength];
        sequenceSlotImages  = new Image[sequenceLength];
        sequenceSlotChecks  = new GameObject[sequenceLength];
        sequenceSlotNumbers = new TextMeshProUGUI[sequenceLength];

        for (int i = 0; i < sequenceLength; i++)
        {
            // กล่องช่องลำดับที่ i
            GameObject slotObj = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slotObj.transform.SetParent(sequenceContainer.transform, false);

            RectTransform slotRt = slotObj.GetComponent<RectTransform>();
            slotRt.sizeDelta = new Vector2(slotW, slotH);

            Image slotBgImg = slotObj.GetComponent<Image>();
            if (cardSprite != null)
            {
                slotBgImg.sprite = cardSprite;
                slotBgImg.type = Image.Type.Sliced;
            }
            slotBgImg.color = defaultSlotBgColor;
            sequenceSlotBgs[i] = slotBgImg;

            // ตัวเลขบอกลำดับจางๆ ในช่อง
            GameObject numObj = new GameObject("NumberText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(slotObj.transform, false);
            RectTransform numRt = numObj.GetComponent<RectTransform>();
            numRt.anchorMin = Vector2.zero;
            numRt.anchorMax = Vector2.one;
            numRt.anchoredPosition = Vector2.zero;
            numRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI numTmp = numObj.GetComponent<TextMeshProUGUI>();
            if (thaiFont != null) numTmp.font = thaiFont;
            numTmp.text = (i + 1).ToString();
            numTmp.fontSize = numFontSize;
            numTmp.fontStyle = FontStyles.Bold;
            numTmp.alignment = TextAlignmentOptions.Center;
            numTmp.color = new Color(1f, 1f, 1f, 0.45f);
            sequenceSlotNumbers[i] = numTmp;

            // รูปผลไม้ที่ผู้เล่นเหยียบลงมา (เริ่มต้นจะซ่อนอยู่/โปร่งใส)
            GameObject fruitObj = new GameObject("FruitImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fruitObj.transform.SetParent(slotObj.transform, false);
            RectTransform fruitRt = fruitObj.GetComponent<RectTransform>();
            fruitRt.sizeDelta = new Vector2(fruitSize, fruitSize);
            fruitRt.anchoredPosition = Vector2.zero;

            Image fruitImg = fruitObj.GetComponent<Image>();
            fruitImg.preserveAspect = true;
            fruitImg.color = Color.clear; // ซ่อนไว้ก่อนเหยียบ
            sequenceSlotImages[i] = fruitImg;

            // เครื่องหมายถูก overlay ตอนเหยียบถูก (ซ่อนไว้ตามเงื่อนไขใหม่)
            GameObject checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            checkObj.transform.SetParent(slotObj.transform, false);
            RectTransform checkRt = checkObj.GetComponent<RectTransform>();
            float checkSize = slotH * 0.60f;
            checkRt.sizeDelta = new Vector2(checkSize, checkSize);
            checkRt.anchoredPosition = Vector2.zero;

            Image checkImg = checkObj.GetComponent<Image>();
            if (checkSprite != null) checkImg.sprite = checkSprite;
            checkImg.preserveAspect = true;
            checkObj.SetActive(false);
            sequenceSlotChecks[i] = checkObj;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  สร้าง Popup คำอธิบายและโชว์ลำดับผลไม้ให้ผู้เล่นจำ
    // ═══════════════════════════════════════════════════════
    private void CreateMemorizePopup()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Sprite cardSprite = Resources.Load<Sprite>("Card_Popup");

        // 1. Fullscreen Dark Overlay
        memorizePopup = new GameObject("MemorizePopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        memorizePopup.transform.SetParent(canvas.transform, false);
        memorizePopup.transform.SetAsLastSibling();

        RectTransform overlayRt = memorizePopup.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.anchoredPosition = Vector2.zero;
        overlayRt.sizeDelta = Vector2.zero;

        Image overlayImg = memorizePopup.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.72f);

        // 2. White Rounded Card
        GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cardObj.transform.SetParent(memorizePopup.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(1180, 680);

        Image cardImg = cardObj.GetComponent<Image>();
        if (cardSprite != null)
        {
            cardImg.sprite = cardSprite;
            cardImg.type = Image.Type.Sliced;
        }
        cardImg.color = Color.white;

        // คลิกที่ตัวการ์ดหรือแบล็คดรอป เพื่อเริ่มเล่นได้
        Button cardBtn = cardObj.GetComponent<Button>();
        cardBtn.onClick.AddListener(StartPlayingRound);

        Button overlayBtn = memorizePopup.AddComponent<Button>();
        overlayBtn.onClick.AddListener(StartPlayingRound);

        // 3. Title Text
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchoredPosition = new Vector2(0, 255);
        titleRt.sizeDelta = new Vector2(1050, 65);

        popupTitleText = titleObj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) popupTitleText.font = thaiFont;
        popupTitleText.text = "จำลำดับผลไม้!";
        popupTitleText.fontSize = 50;
        popupTitleText.fontStyle = FontStyles.Bold;
        popupTitleText.alignment = TextAlignmentOptions.Center;
        popupTitleText.color = new Color(0.90f, 0.45f, 0.05f, 1f); // สีส้มอำพัน Amber

        // 4. Subtitle Text
        GameObject subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchoredPosition = new Vector2(0, 190);
        subRt.sizeDelta = new Vector2(1050, 50);

        popupSubtitleText = subObj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) popupSubtitleText.font = thaiFont;
        popupSubtitleText.text = "จำลำดับผลไม้จากซ้ายไปขวา แล้วเหยียบตามลำดับให้ครบนะ!";
        popupSubtitleText.fontSize = 28;
        popupSubtitleText.alignment = TextAlignmentOptions.Center;
        popupSubtitleText.color = new Color(0.20f, 0.25f, 0.32f, 1f);

        // 5. Container แนวนอนสำหรับแสดงผลไม้ที่ต้องจำ (ขยายสูง 330px เพื่อให้กรอบการ์ดใหญ่และสระไม่หล่น)
        GameObject containerObj = new GameObject("FruitDisplayContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerObj.transform.SetParent(cardObj.transform, false);
        RectTransform contRt = containerObj.GetComponent<RectTransform>();
        contRt.anchoredPosition = new Vector2(0, -10);
        contRt.sizeDelta = new Vector2(1140, 330);

        HorizontalLayoutGroup hlg = containerObj.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        popupFruitContainer = containerObj.transform;

        // 6. Prompt Text ด้านล่าง
        GameObject promptObj = new GameObject("PromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObj.transform.SetParent(cardObj.transform, false);
        RectTransform promptRt = promptObj.GetComponent<RectTransform>();
        promptRt.anchoredPosition = new Vector2(0, -260);
        promptRt.sizeDelta = new Vector2(1050, 55);

        TextMeshProUGUI promptTmp = promptObj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) promptTmp.font = thaiFont;
        promptTmp.text = "เหยียบปุ่มใดก็ได้ (หรือคลิก) เพื่อเริ่มเล่น!";
        promptTmp.fontSize = 34;
        promptTmp.fontStyle = FontStyles.Bold;
        promptTmp.alignment = TextAlignmentOptions.Center;
        promptTmp.color = new Color(0.08f, 0.65f, 0.35f, 1f); // เขียว Emerald สดใส
    }

    // ═══════════════════════════════════════════════════════
    //  อัปเดตการ์ดผลไม้ใน Popup เพื่อให้ผู้เล่นจำ (กรอบใหญ่ขึ้น ตัวอักษรและสระมีพื้นที่ ไม่หลุดกรอบ)
    // ═══════════════════════════════════════════════════════
    private void PopulateMemorizePopupFruits()
    {
        if (popupFruitContainer == null) return;

        // ล้างของเดิม
        for (int i = popupFruitContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(popupFruitContainer.GetChild(i).gameObject);
        }

        HorizontalLayoutGroup hlg = popupFruitContainer.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.spacing = (sequenceLength <= 3) ? 18f : ((sequenceLength == 5) ? 12f : 6f);
        }

        float cardW = 210f;
        float cardH = 310f;
        float imgSize = 120f;
        int stepFontSize = 26;
        int nameFontSize = 28;

        if (sequenceLength == 1)
        {
            cardW = 260f; cardH = 315f; imgSize = 145f;
            stepFontSize = 28; nameFontSize = 30;
        }
        else if (sequenceLength == 3)
        {
            cardW = 220f; cardH = 310f; imgSize = 120f;
            stepFontSize = 26; nameFontSize = 28;
        }
        else if (sequenceLength == 5)
        {
            cardW = 160f; cardH = 300f; imgSize = 95f;
            stepFontSize = 22; nameFontSize = 24;
        }
        else if (sequenceLength >= 7)
        {
            cardW = 120f; cardH = 290f; imgSize = 75f;
            stepFontSize = 18; nameFontSize = 20;
        }

        Sprite cardSprite = Resources.Load<Sprite>("Card_Popup");
        Sprite arrowSprite = Resources.Load<Sprite>("Arrow_Right");

        for (int i = 0; i < targetSequence.Count; i++)
        {
            int fruitIdx = targetSequence[i];

            // Item Box กรอบการ์ดใหญ่ขึ้น
            GameObject itemBox = new GameObject($"FruitItem_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemBox.transform.SetParent(popupFruitContainer, false);
            RectTransform boxRt = itemBox.GetComponent<RectTransform>();
            boxRt.sizeDelta = new Vector2(cardW, cardH);

            Image boxImg = itemBox.GetComponent<Image>();
            if (cardSprite != null)
            {
                boxImg.sprite = cardSprite;
                boxImg.type = Image.Type.Sliced;
            }
            boxImg.color = new Color(0.94f, 0.96f, 0.99f, 1f);

            // 1. Badge ลำดับ
            GameObject stepObj = new GameObject("StepLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            stepObj.transform.SetParent(itemBox.transform, false);
            RectTransform stepRt = stepObj.GetComponent<RectTransform>();
            stepRt.anchoredPosition = new Vector2(0, cardH * 0.35f);
            stepRt.sizeDelta = new Vector2(cardW - 8f, 42f);

            TextMeshProUGUI stepTmp = stepObj.GetComponent<TextMeshProUGUI>();
            if (thaiFont != null) stepTmp.font = thaiFont;
            stepTmp.text = $"ลำดับ {i + 1}";
            stepTmp.fontSize = stepFontSize;
            stepTmp.fontStyle = FontStyles.Bold;
            stepTmp.alignment = TextAlignmentOptions.Center;
            stepTmp.textWrappingMode = TextWrappingModes.NoWrap;
            stepTmp.overflowMode = TextOverflowModes.Overflow;
            stepTmp.color = new Color(0.40f, 0.45f, 0.55f, 1f);

            // 2. Fruit Sprite
            GameObject fImgObj = new GameObject("FruitImg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fImgObj.transform.SetParent(itemBox.transform, false);
            RectTransform fImgRt = fImgObj.GetComponent<RectTransform>();
            fImgRt.anchoredPosition = new Vector2(0, 8f);
            fImgRt.sizeDelta = new Vector2(imgSize, imgSize);

            Image fImg = fImgObj.GetComponent<Image>();
            if (fruitIdx >= 0 && fruitIdx < fruitSprites.Length)
            {
                fImg.sprite = fruitSprites[fruitIdx];
            }
            fImg.preserveAspect = true;

            // 3. Fruit Name (กล่องสูง 52px และมีระยะห่างจากขอบล่าง 30px สระและวรรณยุกต์ไม่หล่นไม่หลุดกรอบ)
            GameObject nameObj = new GameObject("NameLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(itemBox.transform, false);
            RectTransform nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, -cardH * 0.33f);
            nameRt.sizeDelta = new Vector2(cardW - 8f, 52f);

            TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
            if (thaiFont != null) nameTmp.font = thaiFont;
            nameTmp.text = (fruitIdx >= 0 && fruitIdx < fruitNames.Length) ? fruitNames[fruitIdx] : "";
            nameTmp.fontSize = nameFontSize;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
            nameTmp.overflowMode = TextOverflowModes.Overflow;
            nameTmp.color = new Color(0.18f, 0.22f, 0.30f, 1f);

            // 4. ลูกศรบอกทิศทางเชื่อมไปยังการ์ดถัดไป (จากซ้ายไปขวา)
            if (i < targetSequence.Count - 1)
            {
                GameObject arrowObj = new GameObject($"Arrow_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                arrowObj.transform.SetParent(popupFruitContainer, false);
                RectTransform arrowRt = arrowObj.GetComponent<RectTransform>();
                float arrowSize = (sequenceLength <= 3) ? 44f : ((sequenceLength == 5) ? 34f : 24f);
                arrowRt.sizeDelta = new Vector2(arrowSize, arrowSize);

                Image arrowImg = arrowObj.GetComponent<Image>();
                if (arrowSprite != null)
                {
                    arrowImg.sprite = arrowSprite;
                }
                arrowImg.preserveAspect = true;
                arrowImg.color = new Color(0.90f, 0.50f, 0.15f, 0.95f); // สีส้มอำพัน ชัดเจน สวยงาม
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  เริ่มชุดลำดับผลไม้ใหม่
    // ═══════════════════════════════════════════════════════
    private void StartNewSequence(bool isFirstRound)
    {
        isProcessingRound = false;
        currentStepIndex = 0;
        currentRoundHadMistake = false;

        // สุ่มชุดผลไม้ใหม่
        targetSequence.Clear();
        for (int i = 0; i < sequenceLength; i++)
        {
            int fruit = Random.Range(0, 4);
            targetSequence.Add(fruit);
        }

        // รีเซ็ตช่องตรงกลางให้เป็นช่องว่าง
        ResetCenterStripSlots();

        // นำชุดผลไม้ไปใส่ใน Popup
        PopulateMemorizePopupFruits();

        if (popupTitleText != null)
        {
            popupTitleText.text = isFirstRound ? "จำลำดับผลไม้!" : $"รอบที่ {totalRoundsCompleted + 1}: จำลำดับผลไม้!";
        }

        if (popupSubtitleText != null)
        {
            popupSubtitleText.text = "จำลำดับผลไม้จากซ้ายไปขวา แล้วเหยียบตามลำดับให้ครบนะ!";
        }

        // เปิด Popup ให้ผู้เล่นจำ
        if (memorizePopup != null)
        {
            memorizePopup.SetActive(true);
            memorizePopup.transform.SetAsLastSibling();
        }

        currentPhase = GamePhase.Memorizing;

        if (promptText != null)
        {
            promptText.text = "จำลำดับผลไม้บนหน้าจอ แล้วเหยียบปุ่มใดก็ได้เพื่อเริ่มเล่น!";
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ปิด Popup แล้วเริ่มเหยียบเล่นในรอบนั้น
    // ═══════════════════════════════════════════════════════
    public void StartPlayingRound()
    {
        if (currentPhase != GamePhase.Memorizing) return;

        if (memorizePopup != null)
        {
            memorizePopup.SetActive(false);
        }

        // เริ่ม Session DataLogger หากยังไม่ได้เริ่ม
        if (!dataLoggerStarted && dataLogger != null)
        {
            dataLogger.BeginSession();
            dataLoggerStarted = true;
        }

        currentPhase = GamePhase.Playing;
        stompCooldown = 0.35f; // ป้องกันแรงเหยียบที่กดปิด Popup ซึมเข้ามานับเป็นผลไม้แรก

        if (promptText != null)
        {
            promptText.text = $"เหยียบผลไม้ตามลำดับ <b>(ลำดับที่ 1 / {sequenceLength})</b>";
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ล้างช่องแถบตรงกลางให้เป็นช่องว่างรอรับการเหยียบ
    // ═══════════════════════════════════════════════════════
    private void ResetCenterStripSlots()
    {
        for (int i = 0; i < sequenceLength; i++)
        {
            if (sequenceSlotBgs != null && i < sequenceSlotBgs.Length && sequenceSlotBgs[i] != null)
            {
                sequenceSlotBgs[i].color = defaultSlotBgColor;
            }

            if (sequenceSlotImages != null && i < sequenceSlotImages.Length && sequenceSlotImages[i] != null)
            {
                sequenceSlotImages[i].sprite = null;
                sequenceSlotImages[i].color = Color.clear;
            }

            if (sequenceSlotChecks != null && i < sequenceSlotChecks.Length && sequenceSlotChecks[i] != null)
            {
                sequenceSlotChecks[i].SetActive(false);
            }
        }
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
        if (currentPhase == GamePhase.GameOver)
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

        // ─── ช่วงจำผลไม้ (Memorizing Popup) ───
        if (currentPhase == GamePhase.Memorizing)
        {
            // รอผู้เล่นเหยียบแผ่นเท้าใดก็ได้ หรือกดคีย์บอร์ด / คลิก เพื่อปิด Popup และเริ่มเล่น
            bool anyPad = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.GetFirstPressedPad() != -1);
            bool anyKey = Input.anyKeyDown;
            bool anyClick = Input.GetMouseButtonDown(0);

            if (anyPad || anyKey || anyClick)
            {
                StartPlayingRound();
            }
            return;
        }

        // ─── ช่วงเล่นเกม (Playing) ───
        // 1. นับเวลาถอยหลัง (3 นาที)
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

        // 2. Cooldown หลังปิด Popup
        if (stompCooldown > 0f)
        {
            stompCooldown -= Time.deltaTime;
            return;
        }

        // 3. ตรวจสอบการกดปุ่ม (FSR + คีย์บอร์ด)
        if (!isProcessingRound)
        {
            CheckPlayerInput();
        }

        // ปุ่มลัดเปิดหน้าต่างยืนยันกลับหน้าหลัก (Esc หรือ H)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H))
        {
            OpenExitConfirmPopup();
            return;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Input Handling (FSR 4 แผ่น + Keyboard Fallback)
    // ═══════════════════════════════════════════════════════
    private void CheckPlayerInput()
    {
        // ─── FSR Hardware Stomp ───
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            // เหยียบ BL + BR พร้อมกัน = เปิดหน้าต่างยืนยันกลับหน้าหลัก
            if (SharedSerialReceiver.Instance.IsPadPressed(2) && SharedSerialReceiver.Instance.IsPadPressed(3))
            {
                OpenExitConfirmPopup();
                return;
            }

            // Pad 0: Top-Left (Apple)
            if (SharedSerialReceiver.Instance.IsPadDown(0)) { OnPlayerStomp(0); return; }
            // Pad 1: Top-Right (Papaya)
            if (SharedSerialReceiver.Instance.IsPadDown(1)) { OnPlayerStomp(1); return; }
            // Pad 2: Bottom-Left (Banana)
            if (SharedSerialReceiver.Instance.IsPadDown(2)) { OnPlayerStomp(2); return; }
            // Pad 3: Bottom-Right (Orange)
            if (SharedSerialReceiver.Instance.IsPadDown(3)) { OnPlayerStomp(3); return; }
        }

        // ─── Keyboard Controls ───
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Alpha1)) OnPlayerStomp(0); // Top-Left (Apple)
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Alpha2)) OnPlayerStomp(1); // Top-Right (Papaya)
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Alpha3)) OnPlayerStomp(2); // Bottom-Left (Banana)
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Alpha4)) OnPlayerStomp(3); // Bottom-Right (Orange)
    }

    // ═══════════════════════════════════════════════════════
    //  ผู้เล่นเหยียบแผ่นเท้า (padIndex: 0-3)
    //  โชว์ผลไม้ที่เหยียบลงในแถบตรงกลางทันที
    // ═══════════════════════════════════════════════════════
    public void OnPlayerStomp(int padIndex)
    {
        if (currentPhase != GamePhase.Playing || isProcessingRound || stompCooldown > 0f) return;
        if (padIndex < 0 || padIndex >= 4) return;
        if (currentStepIndex >= targetSequence.Count) return;

        totalStomps++;

        // อ่านแรงเหยียบจาก Load Cell
        float force = 0f;
        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            force = Mathf.Abs(SharedSerialReceiver.Instance.GetLoadCellRaw(padIndex));
        }
        else
        {
            force = Random.Range(35f, 65f); // จำลองน้ำหนักตอนเทสคีย์บอร์ด
        }
        totalKgForce += force;
        if (force > maxSingleForce) maxSingleForce = force;
        if (padIndex >= 0 && padIndex < 4)
        {
            padStomps[padIndex]++;
            padForces[padIndex] += force;
        }

        // แสดงผลไม้ที่เหยียบลงในช่องกลางของลำดับปัจจุบันทันที
        if (sequenceSlotImages != null && currentStepIndex < sequenceSlotImages.Length && sequenceSlotImages[currentStepIndex] != null)
        {
            sequenceSlotImages[currentStepIndex].sprite = fruitSprites[padIndex];
            sequenceSlotImages[currentStepIndex].color = Color.white;
        }

        // ประมวลผลแบบ Errorless Flow (ไม่ขึ้นสีแดง ไม่บอกผิด/ถูก เหยียบต่อเนื่องจนครบชุด)
        StartCoroutine(HandleStompStepRoutine(padIndex));
    }

    // ═══════════════════════════════════════════════════════
    //  ประมวลผลก้าวการเหยียบแบบไร้ผลเชิงลบ (Errorless Flow)
    // ═══════════════════════════════════════════════════════
    private IEnumerator HandleStompStepRoutine(int padIndex)
    {
        // 1. ช่องกลางเปลี่ยนพื้นหลังเป็นสีขาว สว่าง สะอาดตา
        if (sequenceSlotBgs != null && currentStepIndex < sequenceSlotBgs.Length && sequenceSlotBgs[currentStepIndex] != null)
        {
            sequenceSlotBgs[currentStepIndex].color = Color.white;
        }

        // กะพริบการ์ดมุมจอสีสว่างนุ่มนวล
        StartCoroutine(FlashCard(padIndex, new Color(0.95f, 0.95f, 0.95f, 1f), 0.20f));

        // ตรวจสอบเงียบๆ ในเบื้องหลังเพื่อเก็บสถิติ
        int expectedFruit = targetSequence[currentStepIndex];
        if (padIndex != expectedFruit)
        {
            currentRoundHadMistake = true;
            wrongStomps++;
        }

        currentStepIndex++;

        // 2. ถ้ายังไม่ครบตามจำนวนลำดับ
        if (currentStepIndex < targetSequence.Count)
        {
            if (promptText != null)
            {
                promptText.text = $"เหยียบผลไม้ลำดับต่อไป <b>({currentStepIndex + 1} / {sequenceLength})</b>";
            }
        }
        else
        {
            // 3. เหยียบครบตามลำดับของรอบนี้แล้ว!
            isProcessingRound = true;

            // บันทึกคะแนนหลังบ้าน (ถ้าไม่ผิดเลยในรอบนี้ ได้ 1 คะแนน)
            if (!currentRoundHadMistake)
            {
                correctRounds++;
                score++;
            }
            totalRoundsCompleted++;

            if (promptText != null)
            {
                promptText.text = "<b>เหยียบครบชุดแล้ว! เตรียมจำชุดถัดไป</b>";
            }

            // ค้างโชว์ผลไม้ที่เหยียบครบไว้ 0.5 วินาที ตามที่ผู้ใช้และอาจารย์กำหนด
            yield return new WaitForSeconds(0.5f);

            // สุ่มโจทย์ใหม่ และเปิด Popup ขึ้นมาทันที
            StartNewSequence(isFirstRound: false);
        }
    }

    private IEnumerator FlashCard(int padIndex, Color flashColor, float duration)
    {
        if (padCardBackgrounds != null && padIndex < padCardBackgrounds.Length && padCardBackgrounds[padIndex] != null)
        {
            padCardBackgrounds[padIndex].color = flashColor;
            yield return new WaitForSeconds(duration);
            padCardBackgrounds[padIndex].color = normalCardColor;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ตั้งค่า 4 มุมจอ
    // ═══════════════════════════════════════════════════════
    private void SetupCornerCards()
    {
        // 0 = Apple, 1 = Papaya, 2 = Banana, 3 = Orange (เปลี่ยนป้ายเป็นปุ่มเหยียบ ไม่ใช้ QWAS)
        string[] labels = new string[] { "เหยียบ บนซ้าย", "เหยียบ บนขวา", "เหยียบ ล่างซ้าย", "เหยียบ ล่างขวา" };

        for (int i = 0; i < 4; i++)
        {
            if (padFruitImages != null && i < padFruitImages.Length && padFruitImages[i] != null)
            {
                if (i < fruitSprites.Length && fruitSprites[i] != null)
                {
                    padFruitImages[i].sprite = fruitSprites[i];
                }
            }

            if (padCardBackgrounds != null && i < padCardBackgrounds.Length && padCardBackgrounds[i] != null)
            {
                padCardBackgrounds[i].color = normalCardColor;
            }

            if (padLabels != null && i < padLabels.Length && padLabels[i] != null)
            {
                padLabels[i].text = labels[i];
                padLabels[i].fontSize = 24;
                padLabels[i].fontStyle = FontStyles.Bold;
                padLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                padLabels[i].overflowMode = TextOverflowModes.Overflow;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  จบเกม & บันทึกข้อมูล
    // ═══════════════════════════════════════════════════════
    private void EndGame()
    {
        currentPhase = GamePhase.GameOver;

        // ซ่อน Popup ถ้าเปิดอยู่
        if (memorizePopup != null) memorizePopup.SetActive(false);

        // สิ้นสุด DataLogger
        if (dataLogger != null && dataLoggerStarted)
        {
            dataLogger.EndSession();
            dataLoggerStarted = false;
        }

        // บันทึกลง PlayerData
        PlayerData.FinalScore       = score;
        PlayerData.CorrectCount     = correctRounds;
        PlayerData.WrongCount       = wrongStomps;
        PlayerData.TimePlayed       = timePlayed;
        PlayerData.TotalKgForce     = totalKgForce;
        PlayerData.TotalStomps      = totalStomps;
        PlayerData.MaxSingleForce   = maxSingleForce;
        PlayerData.AvgForcePerStomp = totalStomps > 0 ? totalKgForce / totalStomps : 0f;

        // แสดงหน้าจบเกม / Dashboard สรุปผล
        if (gameOverPanel != null)
        {
            GameSummaryUI summaryUI = gameOverPanel.GetComponent<GameSummaryUI>();
            if (summaryUI == null) summaryUI = gameOverPanel.AddComponent<GameSummaryUI>();

            int totalAttempts = correctRounds + wrongStomps;
            float accuracy = totalAttempts > 0 ? ((float)correctRounds / totalAttempts) * 100f : 100f;
            string perfDetail = $"จำสำเร็จ: {correctRounds} ชุด  |  เหยียบผิด: {wrongStomps} ครั้ง\nความแม่นยำ: {accuracy:F0}% (ระดับ {sequenceLength} รูป)";

            summaryUI.ShowSummary(
                gameDisplayName: "เกมจำภาพผลไม้",
                score: score,
                timePlayed: timePlayed,
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

        // ยกปุ่ม Home มาไว้หน้าสุดเพื่อไม่ให้แผงบัง
        if (btnHome != null)
        {
            btnHome.transform.SetAsLastSibling();
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

    private void AutoLoadFruitSprites()
    {
        if (fruitSprites == null || fruitSprites.Length < 4)
        {
            fruitSprites = new Sprite[4];
        }
        if (fruitSprites[0] == null) fruitSprites[0] = Resources.Load<Sprite>("Fruit_Apple");
        if (fruitSprites[1] == null) fruitSprites[1] = Resources.Load<Sprite>("Fruit_Papaya");
        if (fruitSprites[2] == null) fruitSprites[2] = Resources.Load<Sprite>("Fruit_Banana");
        if (fruitSprites[3] == null) fruitSprites[3] = Resources.Load<Sprite>("Fruit_Orange");
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
        if (dataLogger != null && dataLoggerStarted)
        {
            dataLogger.CancelSession();
            dataLoggerStarted = false;
        }
        PlayerData.ResetResults();
        SceneManager.LoadScene("MainMenu");
    }

    private void CreateExitConfirmPopup()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

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
        if (thaiFont != null) titleTmp.font = thaiFont;
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
        if (thaiFont != null) subTmp.font = thaiFont;
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
        if (thaiFont != null) exitTxt.font = thaiFont;
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
        if (thaiFont != null) cancelTxt.font = thaiFont;
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
        if (thaiFont != null) hintTmp.font = thaiFont;
        hintTmp.text = "เหยียบแผ่นเท้าตามคำสั่งบนปุ่ม หรือคลิกเลือกได้เลย";
        hintTmp.fontSize = 22;
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.color = new Color(0.55f, 0.60f, 0.68f, 1f);
    }
}
