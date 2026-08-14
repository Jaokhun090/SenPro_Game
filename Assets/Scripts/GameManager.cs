using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("UI Settings")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;

    [Header("ระบบสุ่มตุ่น (Mole Spawner)")]
    public MoleController[] moles; // อาเรย์สำหรับเก็บตุ่นทั้ง 4 หลุม
    public float moleActiveTime = 2f; // เวลาที่ตุ่นจะโผล่ค้างไว้ก่อนหนีลงดิน (หน่วยเป็นวินาที)
    
    private float spawnTimer;
    private MoleController currentMole; // เก็บข้อมูลว่าตอนนี้ตุ่นโผล่ที่หลุมไหน

    private float currentTime;
    private int score = 0;
    private bool isGameActive = true; 

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        ParseInitialTime();
        score = 0;
        UpdateScoreUI();

        // 1. ซ่อนตุ่นทุกตัวตอนเริ่มเกม
        foreach (MoleController mole in moles) mole.ForceHide();

        // 2. สุ่มให้ตัวแรกโผล่ขึ้นมา
        SpawnRandomMole();
    }

    private void ParseInitialTime()
    {
        float defaultTime = 60f; 
        if (timeText != null && !string.IsNullOrEmpty(timeText.text))
        {
            string textValue = timeText.text.Replace("Time:", "").Replace("Time: ", "").Trim();
            if (float.TryParse(textValue, out float parsedTime)) currentTime = parsedTime;
            else currentTime = defaultTime;
        }
        else currentTime = defaultTime;
    }

    void Update()
    {
        if (isGameActive)
        {
            // นับเวลาถอยหลังของเกม
            currentTime -= Time.deltaTime;
            if (timeText != null) timeText.text = "Time: " + Mathf.CeilToInt(currentTime).ToString();

            // ระบบจับเวลาของตัวตุ่น (ถ้านับจนเหลือ 0 แปลว่าตีไม่ทัน ตุ่นจะหนี)
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                if (currentMole != null) currentMole.ForceHide(); // ตัวเก่ามุดลงไป
                SpawnRandomMole(); // สุ่มตัวใหม่ขึ้นมา
            }

            // ถ้าเวลาเกมหมด
            if (currentTime <= 0)
            {
                currentTime = 0;
                GameOver();
            }
        }
    }

    // ฟังก์ชันสำหรับสุ่มหลุมตุ่น
    public void SpawnRandomMole()
    {
        if (moles.Length == 0) return; // ถ้าลืมใส่ตุ่นใน Unity จะได้ไม่พัง

        int randomIndex = Random.Range(0, moles.Length); // สุ่มเลข 0 ถึง 3
        
        // ป้องกันไม่ให้สุ่มได้หลุมเดิมซ้ำติดกัน (จะได้ต้องขยับตัวไปตีหลุมอื่น)
        while (moles[randomIndex] == currentMole)
        {
            randomIndex = Random.Range(0, moles.Length);
        }

        // บันทึกตัวใหม่ สั่งให้โผล่ และรีเซ็ตเวลา
        currentMole = moles[randomIndex];
        currentMole.ShowMole();
        spawnTimer = moleActiveTime; 
    }

    public void AddScore(int amount)
    {
        if (!isGameActive) return; 
        score += amount;
        UpdateScoreUI();

        // ทริคสำคัญ: พอตีโดนปุ๊บ เราสั่งให้เวลาเกิดเหลือ 0 เพื่อให้ Update() รีบสุ่มตัวใหม่ขึ้นมาทันที!
        spawnTimer = 1f; 
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }

    private void GameOver()
    {
        isGameActive = false; 
        if (timeText != null) timeText.text = "Time: 0";
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (finalScoreText != null) finalScoreText.text = "Total Score: " + score;
        }

        foreach (MoleController mole in moles) mole.ForceHide();
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}