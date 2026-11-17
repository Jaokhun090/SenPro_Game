// ============================================================
//  GameSummaryUI.cs
//  ─────────────────────────────────────────────────────────────
//  หน้าสรุปผล / Dashboard หลังเล่นจบเกมสำหรับผู้สูงอายุ (Elderly-Friendly)
//  - ออกแบบให้อ่านง่าย ตัวหนังสือขนาดใหญ่ คอนทราสต์ชัดเจน ไม่มี emoji ใดๆ
//  - แสดง 3 การ์ดหลัก: ประสิทธิภาพเกม, กายภาพ/แรงเหยียบ, สมดุล 4 แผ่นเหยียบ
//  - ดึงข้อมูลจาก Cloud Firestore แบบ Real-time เพื่อเทียบสถิติ
//  - รองรับการส่งรายงานสรุปผลและไฟล์ CSV ไปยังอีเมลของผู้เล่น
//  - รองรับการควบคุมด้วยการเหยียบแผ่นเท้า (Foot Stomp) ทุกปุ่ม
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections;

public class GameSummaryUI : MonoBehaviour
{
    private TMP_FontAsset thaiFont;
    private Sprite cardSprite;

    // References ของ UI ภายใน Panel
    private GameObject mainCardObj;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;

    // Card 1: ประสิทธิภาพเกม
    private TextMeshProUGUI scoreValText;
    private TextMeshProUGUI perfDetailText;

    // Card 2: กายภาพ / แรงเหยียบ
    private TextMeshProUGUI totalForceValText;
    private TextMeshProUGUI biomechDetailText;

    // Card 3: สมดุล 4 แผ่นเหยียบ
    private TextMeshProUGUI[] padGridTexts = new TextMeshProUGUI[4];
    private TextMeshProUGUI balanceEvalText;

    // Cloud Status Bar
    private TextMeshProUGUI cloudStatusText;

    // Buttons
    private Button btnPlayAgain;
    private Button btnSendEmail;
    private Button btnGoHome;
    private TextMeshProUGUI emailStatusText;

    // Callbacks
    private UnityAction onPlayAgainCallback;
    private UnityAction onGoHomeCallback;
    private bool isSummaryActive = false;
    private bool isSendingEmail = false;

    // ═══════════════════════════════════════════════════════
    //  ShowSummary — เรียกเมื่อเกมหมดเวลา
    // ═══════════════════════════════════════════════════════
    public void ShowSummary(
        string gameDisplayName,
        int score,
        float timePlayed,
        float totalForce,
        int totalStomps,
        float maxForce,
        float[] padForces,
        int[] padStomps,
        string extraPerfDetail,
        UnityAction onPlayAgain,
        UnityAction onGoHome)
    {
        onPlayAgainCallback = onPlayAgain;
        onGoHomeCallback = onGoHome;

        // บันทึกลง PlayerData เผื่อไว้
        PlayerData.GameDisplayName  = gameDisplayName;
        PlayerData.FinalScore       = score;
        PlayerData.TimePlayed       = timePlayed;
        PlayerData.TotalKgForce     = totalForce;
        PlayerData.TotalStomps      = totalStomps;
        PlayerData.MaxSingleForce   = maxForce;
        PlayerData.AvgForcePerStomp = totalStomps > 0 ? totalForce / totalStomps : 0f;

        if (padForces != null && padForces.Length >= 4)
        {
            PlayerData.PadForceTL = padForces[0];
            PlayerData.PadForceTR = padForces[1];
            PlayerData.PadForceBL = padForces[2];
            PlayerData.PadForceBR = padForces[3];
        }
        if (padStomps != null && padStomps.Length >= 4)
        {
            PlayerData.PadStompsTL = padStomps[0];
            PlayerData.PadStompsTR = padStomps[1];
            PlayerData.PadStompsBL = padStomps[2];
            PlayerData.PadStompsBR = padStomps[3];
        }
        PlayerData.ExtraDetailText = extraPerfDetail;

        // โหลดฟอนต์และรูป
        thaiFont = Resources.Load<TMP_FontAsset>("LeelawadeeUI SDF");
        cardSprite = Resources.Load<Sprite>("Card_Popup");

        // สร้างหรือรีเซ็ตโครงสร้าง UI
        BuildUI();

        // ใส่ข้อมูล
        PopulateData(gameDisplayName, score, timePlayed, totalForce, totalStomps, maxForce, padForces, padStomps, extraPerfDetail);

        // แสดง Panel
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        isSummaryActive = true;

        // บันทึกขึ้น Cloud Firestore (ถ้ายังไม่ได้บันทึก)
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.isFirebaseReady)
        {
            if (cloudStatusText != null)
            {
                cloudStatusText.text = "สถานะ: กำลังบันทึกข้อมูลขึ้น Cloud Firestore...";
            }

            FirebaseManager.Instance.SaveGameResult((success, msg) =>
            {
                if (success)
                {
                    if (cloudStatusText != null)
                    {
                        cloudStatusText.text = "สถานะ: บันทึกขึ้น Cloud สำเร็จ (Real-time) | กำลังเชื่อมต่อสถิติเดิม...";
                    }

                    // ดึงสถิติเดิมของผู้เล่นจาก Cloud เพื่อพิสูจน์การซิงค์แบบ Real-time
                    FirebaseManager.Instance.FetchUserGameStats(gameDisplayName, (fetchOk, sessions, highScore, fetchMsg) =>
                    {
                        if (fetchOk && cloudStatusText != null)
                        {
                            cloudStatusText.text = $"สถานะ: ซิงค์ Cloud เรียบร้อย | สถิติสูงสุดบน Cloud: {highScore} คะแนน | เล่นสะสม: {sessions} รอบ";
                        }
                        else if (cloudStatusText != null)
                        {
                            cloudStatusText.text = "สถานะ: บันทึกขึ้น Cloud สำเร็จ (Real-time)";
                        }
                    });
                }
                else
                {
                    if (cloudStatusText != null)
                    {
                        cloudStatusText.text = "สถานะ: บันทึกในเครื่องเรียบร้อย (ออฟไลน์)";
                    }
                }
            });
        }
        else
        {
            if (cloudStatusText != null)
            {
                cloudStatusText.text = "สถานะ: บันทึกข้อมูลในเครื่องเรียบร้อย (ออฟไลน์)";
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  BuildUI — สร้างหน้าจอ Dashboard อัตโนมัติ
    // ═══════════════════════════════════════════════════════
    private void BuildUI()
    {
        // ล้างคอมโพเนนต์เก่าภายใน ถ้ามีลูกเดิมที่สร้างไว้แล้วแต่เป็นของเก่า
        Transform existingCard = transform.Find("SummaryMainCard");
        if (existingCard != null)
        {
            Destroy(existingCard.gameObject);
        }

        // 1. Overlay พื้นหลังมืด
        Image panelImg = GetComponent<Image>();
        if (panelImg == null) panelImg = gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.10f, 0.15f, 0.85f);

        // 2. การ์ดหลักตรงกลาง (ขนาด 1240 x 790)
        mainCardObj = new GameObject("SummaryMainCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mainCardObj.transform.SetParent(transform, false);
        RectTransform cardRt = mainCardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot     = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1240, 790);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = mainCardObj.GetComponent<Image>();
        if (cardSprite != null)
        {
            cardImg.sprite = cardSprite;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.96f, 0.94f, 0.90f, 1f); // สีครีมอบอุ่น อ่อนโยนต่อสายตา
        }
        else
        {
            cardImg.color = new Color(0.96f, 0.94f, 0.90f, 1f);
        }

        // ── 3. Header Section (ชื่อเกม, ผู้เล่น, เวลา) ──
        GameObject titleObj = CreateTMP(mainCardObj.transform, "TitleText", new Vector2(0, 335), new Vector2(1160, 60), 42, FontStyles.Bold, new Color(0.24f, 0.15f, 0.08f, 1f), TextAlignmentOptions.Center);
        titleText = titleObj.GetComponent<TextMeshProUGUI>();

        GameObject subObj = CreateTMP(mainCardObj.transform, "SubtitleText", new Vector2(0, 282), new Vector2(1160, 40), 24, FontStyles.Normal, new Color(0.40f, 0.32f, 0.25f, 1f), TextAlignmentOptions.Center);
        subtitleText = subObj.GetComponent<TextMeshProUGUI>();

        // ── 4. Middle Section (3 Cards) ──
        float cardY = 55f;
        float cardW = 370f;
        float cardH = 365f;

        // Card 1: ประสิทธิภาพเกม (ซ้าย)
        GameObject c1 = CreateSubCard(mainCardObj.transform, "CardPerformance", new Vector2(-390, cardY), new Vector2(cardW, cardH));
        CreateTMP(c1.transform, "H1", new Vector2(0, 140), new Vector2(340, 40), 26, FontStyles.Bold, new Color(0.18f, 0.24f, 0.32f, 1f), TextAlignmentOptions.Center, "ประสิทธิภาพการเล่น");
        GameObject sObj = CreateTMP(c1.transform, "ScoreVal", new Vector2(0, 70), new Vector2(340, 80), 62, FontStyles.Bold, new Color(0.85f, 0.35f, 0.05f, 1f), TextAlignmentOptions.Center, "0");
        scoreValText = sObj.GetComponent<TextMeshProUGUI>();
        CreateTMP(c1.transform, "ScoreSub", new Vector2(0, 25), new Vector2(340, 30), 20, FontStyles.Normal, new Color(0.50f, 0.55f, 0.62f, 1f), TextAlignmentOptions.Center, "คะแนนรวม");
        CreateDivider(c1.transform, new Vector2(0, 0), 320);
        GameObject pDetObj = CreateTMP(c1.transform, "PerfDetail", new Vector2(0, -75), new Vector2(340, 130), 22, FontStyles.Normal, new Color(0.25f, 0.28f, 0.35f, 1f), TextAlignmentOptions.Center, "");
        perfDetailText = pDetObj.GetComponent<TextMeshProUGUI>();

        // Card 2: กายภาพ / แรงเหยียบ (กลาง)
        GameObject c2 = CreateSubCard(mainCardObj.transform, "CardBiomechanical", new Vector2(0, cardY), new Vector2(cardW, cardH));
        CreateTMP(c2.transform, "H2", new Vector2(0, 140), new Vector2(340, 40), 26, FontStyles.Bold, new Color(0.18f, 0.24f, 0.32f, 1f), TextAlignmentOptions.Center, "พลังงานและการออกแรง");
        GameObject fObj = CreateTMP(c2.transform, "ForceVal", new Vector2(0, 70), new Vector2(340, 80), 46, FontStyles.Bold, new Color(0.15f, 0.62f, 0.35f, 1f), TextAlignmentOptions.Center, "0 กก.");
        totalForceValText = fObj.GetComponent<TextMeshProUGUI>();
        CreateTMP(c2.transform, "ForceSub", new Vector2(0, 25), new Vector2(340, 30), 20, FontStyles.Normal, new Color(0.50f, 0.55f, 0.62f, 1f), TextAlignmentOptions.Center, "Total Exercise (แรงสะสม)");
        CreateDivider(c2.transform, new Vector2(0, 0), 320);
        GameObject bDetObj = CreateTMP(c2.transform, "BiomechDetail", new Vector2(0, -75), new Vector2(340, 130), 22, FontStyles.Normal, new Color(0.25f, 0.28f, 0.35f, 1f), TextAlignmentOptions.Center, "");
        biomechDetailText = bDetObj.GetComponent<TextMeshProUGUI>();

        // Card 3: สมดุล 4 แผ่นเหยียบ (ขวา)
        GameObject c3 = CreateSubCard(mainCardObj.transform, "CardBalance", new Vector2(390, cardY), new Vector2(cardW, cardH));
        CreateTMP(c3.transform, "H3", new Vector2(0, 140), new Vector2(340, 40), 26, FontStyles.Bold, new Color(0.18f, 0.24f, 0.32f, 1f), TextAlignmentOptions.Center, "สมดุลการลงน้ำหนัก 4 จุด");

        // 2x2 Grid จำลอง 4 แผ่นเหยียบ
        padGridTexts[0] = CreatePadMiniBox(c3.transform, new Vector2(-75, 60), "บนซ้าย");
        padGridTexts[1] = CreatePadMiniBox(c3.transform, new Vector2(75, 60),  "บนขวา");
        padGridTexts[2] = CreatePadMiniBox(c3.transform, new Vector2(-75, -20), "ล่างซ้าย");
        padGridTexts[3] = CreatePadMiniBox(c3.transform, new Vector2(75, -20),  "ล่างขวา");

        CreateDivider(c3.transform, new Vector2(0, -65), 320);
        GameObject bEvalObj = CreateTMP(c3.transform, "BalanceEval", new Vector2(0, -115), new Vector2(340, 80), 20, FontStyles.Bold, new Color(0.20f, 0.25f, 0.35f, 1f), TextAlignmentOptions.Center, "");
        balanceEvalText = bEvalObj.GetComponent<TextMeshProUGUI>();

        // ── 5. Cloud Status Bar (ล่างการ์ด) ──
        GameObject cloudBarObj = new GameObject("CloudStatusBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cloudBarObj.transform.SetParent(mainCardObj.transform, false);
        RectTransform cBarRt = cloudBarObj.GetComponent<RectTransform>();
        cBarRt.anchoredPosition = new Vector2(0, -165);
        cBarRt.sizeDelta = new Vector2(1150, 48);
        Image cBarImg = cloudBarObj.GetComponent<Image>();
        cBarImg.color = new Color(0.90f, 0.96f, 0.94f, 1f); // เขียวมิ้นต์อ่อน สะอาดตา

        GameObject cTxtObj = CreateTMP(cloudBarObj.transform, "CloudStatusText", Vector2.zero, new Vector2(1130, 44), 20, FontStyles.Bold, new Color(0.12f, 0.45f, 0.30f, 1f), TextAlignmentOptions.Center, "สถานะ: บันทึกข้อมูลขึ้น Cloud สำเร็จ (Real-time)");
        cloudStatusText = cTxtObj.GetComponent<TextMeshProUGUI>();

        // ── 6. Bottom Buttons (3 ปุ่มขนาดใหญ่ คอนทราสต์สูง) ──
        float btnY = -255f;
        float btnW = 360f;
        float btnH = 95f;

        // ปุ่ม 1: เล่นอีกครั้ง (เขียวมรกต)
        btnPlayAgain = CreateActionButton(mainCardObj.transform, "BtnPlayAgain", new Vector2(-390, btnY), new Vector2(btnW, btnH),
            new Color(0.15f, 0.65f, 0.35f, 1f), "เล่นอีกครั้ง\n<size=75%>(เหยียบ บนซ้าย)</size>", () =>
            {
                isSummaryActive = false;
                onPlayAgainCallback?.Invoke();
            });

        // ปุ่ม 2: ส่งรายงานเข้าอีเมล (น้ำเงินมหาสมุทร)
        btnSendEmail = CreateActionButton(mainCardObj.transform, "BtnSendEmail", new Vector2(0, btnY), new Vector2(btnW, btnH),
            new Color(0.18f, 0.50f, 0.78f, 1f), "ส่งรายงานเข้าอีเมล\n<size=75%>(เหยียบ ล่างซ้าย)</size>", SendReportEmail);

        // ปุ่ม 3: กลับหน้าหลัก (น้ำตาลเข้ม)
        btnGoHome = CreateActionButton(mainCardObj.transform, "BtnGoHome", new Vector2(390, btnY), new Vector2(btnW, btnH),
            new Color(0.48f, 0.35f, 0.25f, 1f), "กลับหน้าหลัก\n<size=75%>(เหยียบ ล่างขวา)</size>", () =>
            {
                isSummaryActive = false;
                onGoHomeCallback?.Invoke();
            });

        // ข้อความสถานะการส่งอีเมล
        GameObject emStatObj = CreateTMP(mainCardObj.transform, "EmailStatusText", new Vector2(0, -340), new Vector2(1150, 40), 20, FontStyles.Normal, new Color(0.40f, 0.45f, 0.55f, 1f), TextAlignmentOptions.Center, "");
        emailStatusText = emStatObj.GetComponent<TextMeshProUGUI>();
    }

    // ═══════════════════════════════════════════════════════
    //  PopulateData — แสดงข้อมูลสถิติและการคำนวณสมดุล
    // ═══════════════════════════════════════════════════════
    private void PopulateData(
        string gameDisplayName,
        int score,
        float timePlayed,
        float totalForce,
        int totalStomps,
        float maxForce,
        float[] padForces,
        int[] padStomps,
        string extraPerfDetail)
    {
        int mins = Mathf.FloorToInt(timePlayed / 60f);
        int secs = Mathf.FloorToInt(timePlayed % 60f);

        if (titleText != null)
            titleText.text = "ผลการทดสอบ: " + gameDisplayName;

        if (subtitleText != null)
            subtitleText.text = $"ผู้เล่น: {PlayerData.PlayerName}   |   น้ำหนัก: {PlayerData.PlayerWeight:F1} กก.   |   เวลาที่เล่น: {mins:00}:{secs:00} นาที";

        if (scoreValText != null)
            scoreValText.text = score.ToString();

        if (perfDetailText != null)
        {
            if (!string.IsNullOrEmpty(extraPerfDetail))
            {
                perfDetailText.text = extraPerfDetail;
            }
            else
            {
                perfDetailText.text = $"เหยียบสำเร็จ: {score} ครั้ง\nเวลาเล่น: {mins:00}:{secs:00} นาที";
            }
        }

        if (totalForceValText != null)
            totalForceValText.text = string.Format("{0:N0} กก.", totalForce);

        float avgForce = totalStomps > 0 ? totalForce / totalStomps : 0f;
        if (biomechDetailText != null)
        {
            biomechDetailText.text =
                $"แรงเหยียบเฉลี่ย: {avgForce:F1} กก./ครั้ง\n" +
                $"แรงสูงสุด (Peak): {maxForce:F1} กก.\n" +
                $"จำนวนการเหยียบ: {totalStomps} ก้าว";
        }

        // คำนวณสมดุล 4 แผ่นเหยียบ
        float[] f = (padForces != null && padForces.Length >= 4) ? padForces : new float[4];
        int[] s = (padStomps != null && padStomps.Length >= 4) ? padStomps : new int[4];
        float sumForce = f[0] + f[1] + f[2] + f[3];

        string[] padNames = new string[] { "บนซ้าย", "บนขวา", "ล่างซ้าย", "ล่างขวา" };
        for (int i = 0; i < 4; i++)
        {
            if (padGridTexts[i] != null)
            {
                float pct = sumForce > 0f ? (f[i] / sumForce) * 100f : 25f;
                padGridTexts[i].text = $"<b>{padNames[i]}</b>\n{pct:F0}% ({f[i]:F0} กก.)\n<size=75%>{s[i]} ครั้ง</size>";
            }
        }

        // คำนวณสมดุลซ้าย vs ขวา
        float leftForce = f[0] + f[2];
        float rightForce = f[1] + f[3];
        float totalLR = leftForce + rightForce;
        float leftPct = totalLR > 0f ? (leftForce / totalLR) * 100f : 50f;
        float rightPct = totalLR > 0f ? (rightForce / totalLR) * 100f : 50f;

        string evalMessage;
        if (Mathf.Abs(leftPct - rightPct) <= 10f)
        {
            evalMessage = "การลงน้ำหนักซ้ายและขวาสมดุลดีมาก";
        }
        else if (leftPct > rightPct)
        {
            evalMessage = "มีการลงน้ำหนักไปทางซีกซ้ายมากกว่า";
        }
        else
        {
            evalMessage = "มีการลงน้ำหนักไปทางซีกขวามากกว่า";
        }

        if (balanceEvalText != null)
        {
            balanceEvalText.text = $"สมดุล: ซ้าย {leftPct:F0}%  |  ขวา {rightPct:F0}%\n<size=85%><color=#445566>{evalMessage}</color></size>";
        }
    }

    // ═══════════════════════════════════════════════════════
    //  SendReportEmail — ส่งรายงานเข้าอีเมลผู้เล่น
    // ═══════════════════════════════════════════════════════
    private void SendReportEmail()
    {
        if (isSendingEmail) return;

        string targetEmail = !string.IsNullOrEmpty(PlayerData.UserEmail) ? PlayerData.UserEmail : "";
        if (string.IsNullOrEmpty(targetEmail))
        {
            if (emailStatusText != null)
            {
                emailStatusText.text = "<color=#C0392B>ไม่พบอีเมลผู้เล่นในระบบ กรุณาเข้าสู่ระบบก่อน</color>";
            }
            return;
        }

        isSendingEmail = true;
        if (emailStatusText != null)
        {
            emailStatusText.text = $"<color=#2980B9>กำลังจัดส่งรายงานและลิงก์ CSV ไปยัง {targetEmail}...</color>";
        }

        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SendReportEmail((success, msg) =>
            {
                isSendingEmail = false;
                if (emailStatusText != null)
                {
                    if (success)
                    {
                        emailStatusText.text = $"<color=#27AE60>ส่งรายงานไปยัง {targetEmail} เรียบร้อยแล้ว</color>";
                    }
                    else
                    {
                        emailStatusText.text = $"<color=#C0392B>{msg}</color>";
                    }
                }
            });
        }
        else
        {
            isSendingEmail = false;
            if (emailStatusText != null)
            {
                emailStatusText.text = "<color=#C0392B>ระบบ Cloud ไม่พร้อมใช้งานในขณะนี้</color>";
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Input Handling — รองรับการเหยียบแผ่นเท้า (Foot Stomp)
    // ═══════════════════════════════════════════════════════
    void Update()
    {
        if (!isSummaryActive) return;

        // 1. บนซ้าย (TL / Pad 0 / Q / 1) -> เล่นอีกครั้ง
        bool tlStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(0));
        bool tlKey = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Alpha1);
        if (tlStomp || tlKey)
        {
            isSummaryActive = false;
            onPlayAgainCallback?.Invoke();
            return;
        }

        // 2. ล่างซ้าย + ล่างขวา พร้อมกัน -> กลับหน้าหลัก
        bool blStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(2));
        bool brStomp = (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected && SharedSerialReceiver.Instance.IsPadDown(3));
        if (blStomp && brStomp)
        {
            isSummaryActive = false;
            onGoHomeCallback?.Invoke();
            return;
        }

        // 3. ล่างซ้าย (BL / Pad 2 / A / 3) -> ส่งรายงานเข้าอีเมล
        bool blKey = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Alpha3);
        if (blStomp || blKey)
        {
            SendReportEmail();
            return;
        }

        // 4. ล่างขวา (BR / Pad 3 / S / 4 / Esc) -> กลับหน้าหลัก
        bool brKey = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Escape);
        if (brStomp || brKey)
        {
            isSummaryActive = false;
            onGoHomeCallback?.Invoke();
            return;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  UI Helper Methods
    // ═══════════════════════════════════════════════════════
    private GameObject CreateSubCard(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = obj.GetComponent<Image>();
        img.color = Color.white;
        return obj;
    }

    private TextMeshProUGUI CreatePadMiniBox(Transform parent, Vector2 pos, string defaultName)
    {
        GameObject box = new GameObject("PadBox_" + defaultName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(parent, false);
        RectTransform rt = box.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(140, 72);

        Image img = box.GetComponent<Image>();
        img.color = new Color(0.93f, 0.95f, 0.98f, 1f); // ฟ้าอมเทาจาง สะอาด

        GameObject txtObj = CreateTMP(box.transform, "Text", Vector2.zero, new Vector2(136, 68), 18, FontStyles.Normal, new Color(0.20f, 0.25f, 0.35f, 1f), TextAlignmentOptions.Center, defaultName);
        return txtObj.GetComponent<TextMeshProUGUI>();
    }

    private Button CreateActionButton(Transform parent, string name, Vector2 pos, Vector2 size, Color bgColor, string text, UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txtObj = CreateTMP(btnObj.transform, "BtnText", Vector2.zero, size, 24, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, text);
        return btn;
    }

    private void CreateDivider(Transform parent, Vector2 pos, float width)
    {
        GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        div.transform.SetParent(parent, false);
        RectTransform rt = div.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 2);

        Image img = div.GetComponent<Image>();
        img.color = new Color(0.88f, 0.90f, 0.92f, 1f);
    }

    private GameObject CreateTMP(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color, TextAlignmentOptions align, string text = "")
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        if (thaiFont != null) tmp.font = thaiFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return obj;
    }
}

