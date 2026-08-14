using UnityEngine;
using System;
using System.IO;
using System.Text;

// ============================================================
//  DataLogger.cs
//  โปรเจกต์: Exergame การทรงตัวของผู้สูงอายุ (กลุ่ม 16)
//  ผู้พัฒนา: เจ้าคุณ เกรียงไกรวัฒน  รหัส: 66120501038
// ============================================================
//
//  Script นี้ทำหน้าที่บันทึกข้อมูลจาก Load Cell และ FSR
//  ลงไฟล์ CSV ขณะเล่นเกม เพื่อนำไปวิเคราะห์การทรงตัวภายหลัง
//
//  ★ ไฟล์ CSV จะถูกบันทึกที่:
//    C:\Users\Asus\Desktop\SeniorProject\GameLogs\
//    (เปิดจาก Explorer ได้เลย ใช้ Excel หรือ Python pandas)
//
//  Format ของแต่ละแถวใน CSV:
//    timestamp_ms, lc_tl, lc_tr, lc_bl, lc_br,
//    fsr_tl, fsr_tr, fsr_bl, fsr_br
// ============================================================

public class DataLogger : MonoBehaviour
{
    [Header("การตั้งค่าการบันทึก")]
    [Tooltip("เปิด/ปิดการบันทึกข้อมูล — สามารถ toggle ได้ขณะ Play Mode")]
    public bool isLogging = true;

    [Tooltip("ชื่อไฟล์ CSV (ไม่ต้องใส่นามสกุล)")]
    public string fileName = "balance_log";

    [Tooltip("บันทึกทุกกี่ frame (1 = ทุก frame, 2 = ทุก 2 frame ฯลฯ)")]
    [Range(1, 10)]
    public int logEveryNFrames = 1;

    // ── ข้อมูล Inspector แบบ Read-only (ดูสถานะ) ────────────
    [Header("สถานะ (Read-only)")]
    [SerializeField] private string  logFilePath   = "";
    [SerializeField] private int     rowsWritten   = 0;

    private StreamWriter writer;
    private int          frameCounter = 0;
    private long         startTimeMs;

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        if (!isLogging) return;

        // สร้างชื่อไฟล์พร้อม timestamp (กันซ้ำ)
        string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fullName   = fileName + "_" + timestamp + ".csv";

        // บันทึกลงโฟลเดอร์ GameLogs ใน SeniorProject (เปิดดูจาก Explorer ได้เลย)
        string saveDir  = @"C:\Users\Asus\Desktop\SeniorProject\GameLogs";
        Directory.CreateDirectory(saveDir);   // สร้างโฟลเดอร์ถ้ายังไม่มี (safe)
        logFilePath     = Path.Combine(saveDir, fullName);

        // เปิดไฟล์และเขียน Header
        writer      = new StreamWriter(logFilePath, false, Encoding.UTF8);
        startTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        writer.WriteLine("timestamp_ms,lc_tl,lc_tr,lc_bl,lc_br,fsr_tl,fsr_tr,fsr_bl,fsr_br");
        writer.Flush();

        Debug.Log("📋 DataLogger เริ่มบันทึกที่: " + logFilePath);
    }

    // ─────────────────────────────────────────────────────────
    void Update()
    {
        if (!isLogging || writer == null) return;

        // บันทึกทุก N frame ตามที่ตั้งไว้
        frameCounter++;
        if (frameCounter < logEveryNFrames) return;
        frameCounter = 0;

        // คำนวณ timestamp (ms) นับจากเริ่ม session
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTimeMs;

        // ดึงค่าจาก HammerController (static arrays)
        long[] lc  = HammerController.LoadCellValues;  // long[4]
        int[]  fsr = HammerController.FsrStates;        // int[4]

        // เขียน 1 แถว CSV
        // Format: timestamp_ms, lc_tl, lc_tr, lc_bl, lc_br, fsr_tl, fsr_tr, fsr_bl, fsr_br
        string row = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
            nowMs,
            lc[0], lc[1], lc[2], lc[3],
            fsr[0], fsr[1], fsr[2], fsr[3]);

        writer.WriteLine(row);
        rowsWritten++;

        // Flush ทุก 100 แถว (กันข้อมูลหายถ้าเกมค้าง)
        if (rowsWritten % 100 == 0)
            writer.Flush();
    }

    // ─────────────────────────────────────────────────────────
    //  ปิดไฟล์เมื่อออกจากเกม (สำคัญมาก ไม่งั้นไฟล์ไม่สมบูรณ์)
    // ─────────────────────────────────────────────────────────
    private void OnApplicationQuit()
    {
        CloseLog();
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void CloseLog()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
            Debug.Log("✅ DataLogger ปิดไฟล์เรียบร้อย บันทึกทั้งหมด " + rowsWritten + " แถว");
            Debug.Log("📁 ไฟล์อยู่ที่: " + logFilePath);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Public API — เรียกจาก Script อื่นได้ถ้าต้องการ
    // ─────────────────────────────────────────────────────────

    /// <summary>เปิดการบันทึก (ถ้าปิดอยู่)</summary>
    public void StartLogging()  => isLogging = true;

    /// <summary>หยุดการบันทึก (ไฟล์ยังคงอยู่)</summary>
    public void StopLogging()   => isLogging = false;

    /// <summary>คืน path ของไฟล์ที่กำลังบันทึก</summary>
    public string GetLogPath()  => logFilePath;
}
