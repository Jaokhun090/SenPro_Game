// ============================================================
//  PlayerData.cs
//  ─────────────────────────────────────────────────────────────
//  Static class เก็บข้อมูลผู้เล่นให้ใช้ข้าม Scene ได้
//  ไม่ต้อง attach กับ GameObject ใดๆ  เรียกใช้ตรงๆ เช่น:
//      PlayerData.PlayerName = "สมชาย";
//      float t = PlayerData.GameDuration;
// ============================================================

public static class PlayerData
{
    // ───── ข้อมูลผู้เล่น ─────
    public static string PlayerName   = "Player";
    public static float  PlayerWeight = 60f;      // กิโลกรัม

    // ───── ตั้งค่าเกม ─────
    public static string SelectedGame  = "SampleScene"; // ชื่อ Scene ที่เลือก (Whack-a-Mole)
    public static float  GameDuration  = 60f;           // วินาที (default 1 นาที)

    // ───── ผลลัพธ์ (เขียนตอนจบเกม → อ่านตอนหน้า Summary) ─────
    public static int    FinalScore         = 0;
    public static float  TotalKgForce       = 0f;
    public static float  TimePlayed         = 0f;     // วินาทีที่เล่นจริง
    public static int    CorrectCount       = 0;
    public static int    WrongCount         = 0;
    public static string GameDisplayName    = "";      // ชื่อเกมที่แสดงผล เช่น "Whack-a-Mole"

    // ───── สถิติ Load Cell (เฉลี่ย / สูงสุด) ─────
    public static float  AvgForcePerStomp   = 0f;
    public static float  MaxSingleForce     = 0f;
    public static int    TotalStomps        = 0;

    /// <summary>รีเซ็ตผลลัพธ์ก่อนเริ่มเกมใหม่</summary>
    public static void ResetResults()
    {
        FinalScore       = 0;
        TotalKgForce     = 0f;
        TimePlayed       = 0f;
        CorrectCount     = 0;
        WrongCount       = 0;
        AvgForcePerStomp = 0f;
        MaxSingleForce   = 0f;
        TotalStomps      = 0;
    }
}
