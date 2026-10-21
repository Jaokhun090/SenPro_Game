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

    // ═══════ FSR state tracking (ป้องกันนับซ้ำ) ═══════
    private bool[] prevFsr = new bool[4];

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        // ตั้งเวลาจาก PlayerData (ถ้ามาจาก PlayerSetup)
        currentTime = PlayerData.GameDuration;
        if (currentTime <= 0f) currentTime = 60f;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // แสดง Instruction Panel ก่อน (ถ้ามี)
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
            btnHome.onClick.RemoveListener(GoHome);
            btnHome.onClick.AddListener(GoHome);
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
    //  เริ่มเกมหลังอ่านคำอธิบาย
    // ═══════════════════════════════════════════════════════
    public void StartGameAfterInstructions()
    {
        if (instructionPanel != null) instructionPanel.SetActive(false);

        // ★ เริ่มบันทึก CSV ตอนเกมเริ่มจริงๆ
        if (dataLogger != null) dataLogger.BeginSession();

        isGameActive = true;
        isWaitingForNextMole = false;
        SpawnRandomMole();
    }

    // ═══════════════════════════════════════════════════════
    void Update()
    {
        if (!isGameActive) return;

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

        // ★ หยุดบันทึก CSV
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

        // ═══ แสดง Game Over Panel ═══
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (finalScoreText != null)
                finalScoreText.text = "คะแนน: " + score;

            if (summaryDetailText != null)
            {
                int mins = Mathf.FloorToInt(totalTimePlayed / 60f);
                int secs = Mathf.FloorToInt(totalTimePlayed % 60f);

                summaryDetailText.text =
                    $"ผู้เล่น: {PlayerData.PlayerName}\n" +
                    $"เวลาเล่น: {mins:00}:{secs:00}\n" +
                    $"เหยียบทั้งหมด: {totalStomps} ครั้ง\n" +
                    $"Total Exercise: {totalKgForce:N0} kg-force\n" +
                    $"แรงเฉลี่ย: {PlayerData.AvgForcePerStomp:F1} kg/ครั้ง";
            }
        }

        // ═══ บันทึกผลเกมขึ้น Cloud Firestore อัตโนมัติ ═══
        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SaveGameResult((success, msg) =>
            {
                Debug.Log($"[GameManager] Save to Firestore: {success} ({msg})");
            });
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
}