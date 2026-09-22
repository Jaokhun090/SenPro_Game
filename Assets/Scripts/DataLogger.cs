// ============================================================
//  DataLogger.cs  (อัพเกรด v2)
//  ─────────────────────────────────────────────────────────────
//  บันทึกข้อมูล FSR + Load Cell ลง CSV
//  ★ เริ่มบันทึกเฉพาะตอนเกมเริ่มจริงๆ (GameManager เรียก BeginSession)
//  ★ บันทึกทุก 50ms คงที่ (ไม่ขึ้นกับ frame rate)
//  ★ ชื่อไฟล์: DD-MM-YYYY_GameXX_NNN_XXXS.csv
//
//  การใช้งาน:
//    DataLogger logger = FindAnyObjectByType<DataLogger>();
//    logger.BeginSession();   // ตอนเกมเริ่ม
//    logger.EndSession();     // ตอนเกมจบ
//
//  ไฟล์ CSV จะอยู่ที่:
//    C:\Users\Asus\Desktop\SeniorProject\GameLogs\
// ============================================================

using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class DataLogger : MonoBehaviour
{
    [Header("ตั้งค่า")]
    [Tooltip("Interval การบันทึก (มิลลิวินาที) — 50ms = 20 แถว/วินาที")]
    public float logIntervalMs = 50f;

    // ── สถานะ (Read-only ใน Inspector) ─────────────────────
    [Header("สถานะ (Read-only)")]
    [SerializeField] private string  logFilePath   = "";
    [SerializeField] private int     rowsWritten   = 0;
    [SerializeField] private bool    isRecording   = false;

    // ── Internal ───────────────────────────────────────────
    private StreamWriter writer;
    private float        intervalSec;       // logIntervalMs แปลงเป็นวินาที
    private float        timeSinceLastLog;  // สะสมเวลาตั้งแต่เขียนแถวล่าสุด
    private int          currentTimestampMs; // timestamp ปัจจุบัน (เพิ่มทีละ interval)

    // ═══════════════════════════════════════════════════════
    //  Game Number Mapping
    // ═══════════════════════════════════════════════════════
    private static string GetGameCode(string sceneName)
    {
        switch (sceneName)
        {
            case "SampleScene":
            case "MoleGame":   return "Game01";
            case "MemoryGame": return "Game02";
            case "MathGame":   return "Game03";
            case "DodgeGame":  return "Game04";
            default:           return "Game00";
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Session Counter — นับครั้งต่อวัน ต่อเกม
    // ═══════════════════════════════════════════════════════
    private static int GetNextSessionNumber(string saveDir, string dateStr, string gameCode)
    {
        // หาไฟล์ที่มี pattern: DD-MM-YYYY_GameXX_NNN
        string pattern = dateStr + "_" + gameCode + "_";
        int maxNum = 0;

        if (Directory.Exists(saveDir))
        {
            string[] files = Directory.GetFiles(saveDir, "*.csv");
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith(pattern))
                {
                    // ดึงส่วน NNN ออกมา
                    // Format: DD-MM-YYYY_GameXX_NNN_XXXS
                    string remaining = name.Substring(pattern.Length);
                    // remaining = "001_060S" → แยกเอา "001"
                    string[] parts = remaining.Split('_');
                    if (parts.Length >= 1 && int.TryParse(parts[0], out int num))
                    {
                        if (num > maxNum) maxNum = num;
                    }
                }
            }
        }

        return maxNum + 1;
    }

    // ═══════════════════════════════════════════════════════
    //  BeginSession — เริ่มบันทึก (GameManager เรียกตอนเกมเริ่ม)
    // ═══════════════════════════════════════════════════════
    public void BeginSession()
    {
        if (isRecording)
        {
            Debug.LogWarning("⚠️ DataLogger: กำลังบันทึกอยู่แล้ว!");
            return;
        }

        // ── สร้างชื่อไฟล์ ──
        string saveDir   = @"C:\Users\Asus\Desktop\SeniorProject\GameLogs";
        Directory.CreateDirectory(saveDir);

        string dateStr   = DateTime.Now.ToString("dd-MM-yyyy");
        string gameCode  = GetGameCode(PlayerData.SelectedGame);
        int    sessionNo = GetNextSessionNumber(saveDir, dateStr, gameCode);
        int    durSec    = Mathf.RoundToInt(PlayerData.GameDuration);

        // Format: DD-MM-YYYY_GameXX_NNN_XXXS.csv
        string fileName = string.Format("{0}_{1}_{2:D3}_{3:D3}S.csv",
            dateStr, gameCode, sessionNo, durSec);

        logFilePath = Path.Combine(saveDir, fileName);

        // ── เปิดไฟล์ + เขียน metadata + header ──
        writer = new StreamWriter(logFilePath, false, Encoding.UTF8);

        // บรรทัดแรก: metadata comment
        writer.WriteLine("# player={0},weight_kg={1:F1},game={2},duration_s={3},date={4}",
            PlayerData.PlayerName,
            PlayerData.PlayerWeight,
            gameCode,
            durSec,
            dateStr);

        // บรรทัดที่สอง: header
        writer.WriteLine("timestamp_ms,fsr_tl,fsr_tr,fsr_bl,fsr_br,lc_tl,lc_tr,lc_bl,lc_br");
        writer.Flush();

        // ── ตั้งค่า interval ──
        intervalSec       = logIntervalMs / 1000f;    // 50ms → 0.05s
        timeSinceLastLog  = 0f;
        currentTimestampMs = 0;
        rowsWritten       = 0;
        isRecording       = true;

        // ── เขียนแถวแรก (ms = 0) ทันที ──
        WriteOneRow();

        Debug.Log("📋 DataLogger เริ่มบันทึก: " + fileName);
    }

    // ═══════════════════════════════════════════════════════
    //  EndSession — หยุดบันทึก (GameManager เรียกตอนเกมจบ)
    // ═══════════════════════════════════════════════════════
    public void EndSession()
    {
        if (!isRecording) return;

        isRecording = false;
        CloseFile();

        Debug.Log("✅ DataLogger จบการบันทึก: " + rowsWritten + " แถว");
        Debug.Log("📁 ไฟล์: " + logFilePath);
    }

    // ═══════════════════════════════════════════════════════
    //  Update — จับเวลาและเขียนทุก interval
    // ═══════════════════════════════════════════════════════
    void Update()
    {
        if (!isRecording || writer == null) return;

        timeSinceLastLog += Time.deltaTime;

        // เขียนทุกๆ interval (อาจต้องเขียนหลายแถวถ้า frame rate ต่ำมาก)
        while (timeSinceLastLog >= intervalSec)
        {
            timeSinceLastLog -= intervalSec;
            currentTimestampMs += Mathf.RoundToInt(logIntervalMs);
            WriteOneRow();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  เขียน 1 แถว CSV
    // ═══════════════════════════════════════════════════════
    private void WriteOneRow()
    {
        // อ่านค่าจาก Hardware (SharedSerialReceiver หรือ HammerController) หรือ Keyboard Fallback
        int[]  fsr = new int[4];
        long[] lc  = new long[4];

        if (SharedSerialReceiver.Instance != null && SharedSerialReceiver.Instance.IsConnected)
        {
            Array.Copy(SharedSerialReceiver.Instance.FsrStates, fsr, 4);
            Array.Copy(SharedSerialReceiver.Instance.LoadCellValues, lc, 4);
        }
        else if (HammerController.FsrStates != null)
        {
            Array.Copy(HammerController.FsrStates, fsr, 4);
            Array.Copy(HammerController.LoadCellValues, lc, 4);

            // ถ้าไม่มี Serial เชื่อมต่ออยู่ และไม่มีการเหยียบ ให้เช็ค Keyboard Fallback
            if (!HammerController.IsConnected && fsr[0] == 0 && fsr[1] == 0 && fsr[2] == 0 && fsr[3] == 0)
            {
                long simLc = (long)(PlayerData.PlayerWeight * 250f);
                if (simLc <= 0) simLc = 15000;

                if (Input.GetKey(KeyCode.Q)) { fsr[0] = 1; lc[0] = simLc; }
                if (Input.GetKey(KeyCode.W)) { fsr[1] = 1; lc[1] = simLc; }
                if (Input.GetKey(KeyCode.A)) { fsr[2] = 1; lc[2] = simLc; }
                if (Input.GetKey(KeyCode.S)) { fsr[3] = 1; lc[3] = simLc; }
            }
        }

        // Format: timestamp_ms, fsr_tl, fsr_tr, fsr_bl, fsr_br, lc_tl, lc_tr, lc_bl, lc_br
        writer.Write(currentTimestampMs);
        writer.Write(','); writer.Write(fsr[0]);
        writer.Write(','); writer.Write(fsr[1]);
        writer.Write(','); writer.Write(fsr[2]);
        writer.Write(','); writer.Write(fsr[3]);
        writer.Write(','); writer.Write(lc[0]);
        writer.Write(','); writer.Write(lc[1]);
        writer.Write(','); writer.Write(lc[2]);
        writer.Write(','); writer.Write(lc[3]);
        writer.WriteLine();

        rowsWritten++;

        // Flush ทุก 100 แถว (กันข้อมูลหายถ้าเกมค้าง)
        if (rowsWritten % 100 == 0)
            writer.Flush();
    }

    // ═══════════════════════════════════════════════════════
    //  Cleanup
    // ═══════════════════════════════════════════════════════
    private void OnApplicationQuit()
    {
        if (isRecording) EndSession();
    }

    private void OnDisable()
    {
        if (isRecording) EndSession();
    }

    private void CloseFile()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════

    /// <summary>กำลังบันทึกอยู่หรือไม่</summary>
    public bool IsRecording => isRecording;

    /// <summary>คืน path ไฟล์ที่กำลัง/เพิ่งบันทึก</summary>
    public string GetLogPath() => logFilePath;

    /// <summary>จำนวนแถวที่เขียนแล้ว</summary>
    public int GetRowCount() => rowsWritten;
}
