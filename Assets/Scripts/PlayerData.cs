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
    public static string UserId       = "";
    public static string UserEmail    = "";
    public static string PlayerName   = "Player";
    public static float  PlayerWeight = 60f;      // กิโลกรัม
    public static int    PlayerAge    = 60;
    public static bool   NeedsWeightUpdate = false;

    // ───── ตั้งค่าเกม ─────
    public static string SelectedGame  = "SampleScene"; // ชื่อ Scene ที่เลือก (Whack-a-Mole)
    public static float  GameDuration  = 60f;           // วินาที (default 1 นาที)
    public static int    GameDifficulty = 1;            // ระดับความยาก (1 = จำ 1, 2 = จำ 2, 3 = จำ 3, 4 = จำ 4)

    // ───── ผลลัพธ์ (เขียนตอนจบเกม → อ่านตอนหน้า Summary) ─────
    public static int    FinalScore         = 0;
    public static float  TotalKgForce       = 0f;
    public static float  TimePlayed         = 0f;     // วินาทีที่เล่นจริง
    public static int    CorrectCount       = 0;
    public static int    WrongCount         = 0;
    public static string GameDisplayName    = "";      // ชื่อเกมที่แสดงผล เช่น "Whack-a-Mole"

    // ───── ข้อมูลไฟล์ CSV Log ล่าสุด ─────
    public static string LastCsvFilePath    = "";      // Path เต็มของไฟล์ CSV ที่เพิ่งบันทึก
    public static string LastCsvFileName    = "";      // ชื่อไฟล์ CSV เช่น 01-11-2026_Game01_001_060S.csv

    // ───── สถิติ Load Cell (เฉลี่ย / สูงสุด) ─────
    public static float  AvgForcePerStomp   = 0f;
    public static float  MaxSingleForce     = 0f;
    public static int    TotalStomps        = 0;

    // ───── ข้อมูลแรงและจำนวนเหยียบแต่ละแผ่น (4-Quadrant Balance) ─────
    public static float  PadForceTL         = 0f;      // บนซ้าย (Pad 0)
    public static float  PadForceTR         = 0f;      // บนขวา (Pad 1)
    public static float  PadForceBL         = 0f;      // ล่างซ้าย (Pad 2)
    public static float  PadForceBR         = 0f;      // ล่างขวา (Pad 3)
    public static int    PadStompsTL        = 0;
    public static int    PadStompsTR        = 0;
    public static int    PadStompsBL        = 0;
    public static int    PadStompsBR        = 0;
    public static string ExtraDetailText    = "";      // ข้อความรายละเอียดเพิ่มเติมของแต่ละเกม

    /// <summary>รีเซ็ตผลลัพธ์ก่อนเริ่มเกมใหม่</summary>
    public static void ResetResults()
    {
        FinalScore         = 0;
        TotalKgForce       = 0f;
        TimePlayed         = 0f;
        CorrectCount       = 0;
        WrongCount         = 0;
        AvgForcePerStomp   = 0f;
        MaxSingleForce     = 0f;
        TotalStomps        = 0;
        LastCsvFilePath    = "";
        LastCsvFileName    = "";
        PadForceTL         = 0f;
        PadForceTR         = 0f;
        PadForceBL         = 0f;
        PadForceBR         = 0f;
        PadStompsTL        = 0;
        PadStompsTR        = 0;
        PadStompsBL        = 0;
        PadStompsBR        = 0;
        ExtraDetailText    = "";
    }
}
