// ============================================================
//  SummaryController.cs
//  ─────────────────────────────────────────────────────────────
//  หน้าสรุปผลหลังจบเกม — อ่านจาก PlayerData
//  แสดง: ชื่อ, เกม, คะแนน, เวลา, Total kg-force,
//         แรงเฉลี่ยต่อครั้ง, จำนวนครั้งที่เหยียบ
//
//  ★ ใช้ได้ 2 แบบ:
//    1. เป็น Panel ในเกม (เหมือนของเพื่อน) — GameManager เปิด Panel
//    2. เป็น Scene แยก "Summary" — GameManager LoadScene("Summary")
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SummaryController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;         // ชื่อเกม
    public TextMeshProUGUI playerNameText;    // ชื่อผู้เล่น
    public TextMeshProUGUI scoreText;         // คะแนน
    public TextMeshProUGUI timePlayedText;    // เวลาที่เล่น
    public TextMeshProUGUI totalForceText;    // Total kg-force
    public TextMeshProUGUI avgForceText;      // แรงเฉลี่ย/ครั้ง
    public TextMeshProUGUI stompCountText;    // จำนวนครั้งที่เหยียบ
    public TextMeshProUGUI correctWrongText;  // ถูก/ผิด (สำหรับ Memory/Math)

    [Header("Buttons")]
    public Button btnPlayAgain;   // เล่นอีกรอบ (เข้าเกมเดิม)
    public Button btnHome;        // กลับ MainMenu

    // ═══════════════════════════════════════════════════════
    void Start()
    {
        PopulateUI();

        if (btnPlayAgain != null) btnPlayAgain.onClick.AddListener(PlayAgain);
        if (btnHome      != null) btnHome.onClick.AddListener(GoHome);
    }

    // ═══════════════════════════════════════════════════════
    //  ดึงข้อมูลจาก PlayerData มาแสดง
    // ═══════════════════════════════════════════════════════
    public void PopulateUI()
    {
        if (titleText != null)
            titleText.text = PlayerData.GameDisplayName;

        if (playerNameText != null)
            playerNameText.text = "ผู้เล่น: " + PlayerData.PlayerName;

        if (scoreText != null)
            scoreText.text = "คะแนน: " + PlayerData.FinalScore;

        if (timePlayedText != null)
        {
            int mins = Mathf.FloorToInt(PlayerData.TimePlayed / 60f);
            int secs = Mathf.FloorToInt(PlayerData.TimePlayed % 60f);
            timePlayedText.text = string.Format("เวลา: {0:00}:{1:00}", mins, secs);
        }

        if (totalForceText != null)
            totalForceText.text = string.Format("Total Exercise: {0:N0} kg-force", PlayerData.TotalKgForce);

        if (avgForceText != null)
        {
            float avg = PlayerData.TotalStomps > 0
                ? PlayerData.TotalKgForce / PlayerData.TotalStomps
                : 0f;
            avgForceText.text = string.Format("แรงเฉลี่ย: {0:F1} kg/ครั้ง", avg);
        }

        if (stompCountText != null)
            stompCountText.text = "เหยียบทั้งหมด: " + PlayerData.TotalStomps + " ครั้ง";

        if (correctWrongText != null)
        {
            // แสดงเฉพาะเกมที่มีถูก/ผิด (Memory, Math)
            if (PlayerData.CorrectCount > 0 || PlayerData.WrongCount > 0)
            {
                correctWrongText.text = "ถูก: " + PlayerData.CorrectCount +
                                        " | ผิด: " + PlayerData.WrongCount;
                correctWrongText.gameObject.SetActive(true);
            }
            else
            {
                correctWrongText.gameObject.SetActive(false);
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Buttons
    // ═══════════════════════════════════════════════════════
    public void PlayAgain()
    {
        PlayerData.ResetResults();
        SceneManager.LoadScene("PlayerSetup");
    }

    public void GoHome()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
