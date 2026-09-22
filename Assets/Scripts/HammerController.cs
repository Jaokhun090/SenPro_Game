using UnityEngine;
using System.IO.Ports;
using System.Threading;

// ============================================================
//  HammerController.cs
//  โปรเจกต์: Exergame การทรงตัวของผู้สูงอายุ (กลุ่ม 16)
//  ผู้พัฒนา: เจ้าคุณ เกรียงไกรวัฒน  รหัส: 66120501038
// ============================================================
//
//  รับข้อมูลจาก Arduino แบบ format:
//    FSR:1,0,0,1|LC:15230,8120,12400,9870
//
//  ★ แก้ไข: ใช้ Background Thread อ่าน Serial
//    → Main Thread ไม่ถูก block แล้ว เกมลื่นไหล 100%
//  - ค่า FSR (1/0) → ใช้ควบคุมตำแหน่งค้อน (Primary Control)
//  - ค่า LC (raw)  → เก็บไว้ใน LoadCellValues[] ให้ DataLogger.cs ดึงไปบันทึก
// ============================================================

public class HammerController : MonoBehaviour
{
    [Header("Game Systems")]
    public GameManager gameManager;

    [Header("Zone Positions (พิกัด 4 หลุม)")]
    public Vector2 topLeftPos     = new Vector2(-5,  2);
    public Vector2 topRightPos    = new Vector2( 5,  2);
    public Vector2 bottomLeftPos  = new Vector2(-5, -3);
    public Vector2 bottomRightPos = new Vector2( 5, -3);
    public Vector2 centerIdlePos  = new Vector2( 0,  0);

    [Header("Arduino Settings")]
    public string portName = "COM3";   // เช็คพอร์ตใน Device Manager ให้ตรง
    public int    baudRate = 115200;   // ต้องตรงกับ Arduino sketch

    [Header("Debug")]
    [Tooltip("เปิด = พิมพ์ค่า FSR ไปยัง Console ทุกครั้งที่รับข้อมูลใหม่")]
    public bool debugSerial = true;   // ★ เปิดไว้ก่อน ดูค่าใน Console ได้เลย

    // ── Shared Data (DataLogger.cs ดึงค่าไปใช้) ─────────────
    // index: 0=TL, 1=TR, 2=BL, 3=BR
    public static long[]  LoadCellValues = new long[4];   // raw Load Cell
    public static int[]   FsrStates      = new int[4];    // FSR state 0/1

    // ── Serial + Threading ────────────────────────────────────
    public static bool IsConnected { get; private set; } = false;
    private SerialPort serialPort;
    private Thread     serialThread;
    private bool       isThreadRunning = false;

    // ── Thread-safe buffers (thread เขียน, main thread อ่าน) ──
    private readonly object _lock        = new object();
    private int[]           _fsrBuf      = new int[4];
    private long[]          _lcBuf       = new long[4];
    private bool            _newDataReady = false;   // มีข้อมูลใหม่รอ main thread

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        transform.position = centerIdlePos;

        // ตั้งค่า arrays เป็น 0 ทั้งหมดตอนเริ่ม
        for (int i = 0; i < 4; i++) { FsrStates[i] = 0; LoadCellValues[i] = 0; }

        serialPort = new SerialPort(portName, baudRate);
        serialPort.ReadTimeout  = 2000;  // ms — Thread อ่านรอได้นาน ไม่กระทบ main thread
        serialPort.WriteTimeout = 1000;

        try
        {
            serialPort.Open();
            IsConnected = true;
            Debug.Log("✅ เชื่อมต่อ Arduino สำเร็จ! Port: " + portName);

            // เริ่ม Background Thread อ่าน Serial
            isThreadRunning = true;
            serialThread = new Thread(SerialReadThread);
            serialThread.IsBackground = true;   // Thread จะถูกปิดพร้อมแอปอัตโนมัติ
            serialThread.Start();
            Debug.Log("🔄 Serial Thread เริ่มทำงาน");
        }
        catch (System.Exception e)
        {
            IsConnected = false;
            Debug.LogError("❌ เปิดพอร์ตไม่ได้: " + e.Message);
            Debug.LogWarning("⚠️ Keyboard fallback พร้อมใช้ (Q=TL, W=TR, A=BL, S=BR)");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  BACKGROUND THREAD — อ่าน Serial ตลอดเวลา
    //  ★ ฟังก์ชันนี้รันใน Thread แยก ไม่ใช่ Main Thread
    // ─────────────────────────────────────────────────────────
    private void SerialReadThread()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // ★ แก้ lag สะสม: ถ้า buffer มีข้อมูลค้างเกิน ~3 บรรทัด (150 bytes)
                //   แปลว่า Unity ช้ากว่า Arduino → ทิ้งข้อมูลเก่าทิ้ง อ่านแค่ล่าสุด
                if (serialPort.BytesToRead > 150)
                {
                    serialPort.DiscardInBuffer();
                    continue;  // วน loop ใหม่ รออ่านบรรทัดสด
                }

                string raw = serialPort.ReadLine().Trim();
                // raw คือ เช่น "FSR:1,0,0,1|LC:15230,8120,12400,9870"

                string[] sections = raw.Split('|');
                if (sections.Length == 2)
                {
                    int[]  fsrTmp = new int[4];
                    long[] lcTmp  = new long[4];

                    bool fsrOk = TryParseFSR(sections[0], ref fsrTmp);
                    bool lcOk  = TryParseLC (sections[1], ref lcTmp);

                    if (fsrOk && lcOk)
                    {
                        // lock — เขียนค่าอย่างปลอดภัย
                        lock (_lock)
                        {
                            System.Array.Copy(fsrTmp, _fsrBuf, 4);
                            System.Array.Copy(lcTmp,  _lcBuf,  4);
                            _newDataReady = true;
                        }

                        // Debug: พิมพ์ค่า FSR ให้ดูใน Console (ปิดเมื่อไม่ใช้)
                        if (debugSerial)
                        {
                            UnityEngine.Debug.Log(string.Format(
                                "[FSR] TL={0} TR={1} BL={2} BR={3}  |  raw: {4}",
                                fsrTmp[0], fsrTmp[1], fsrTmp[2], fsrTmp[3], raw));
                        }
                    }
                }
            }
            catch (System.TimeoutException)
            {
                // ปกติ — รอต่อไป
            }
            catch (System.Exception e)
            {
                // serial ปิด หรือ error ร้ายแรง
                if (isThreadRunning)
                    Debug.LogWarning("[SerialThread] " + e.Message);
            }
        }
        Debug.Log("🛑 Serial Thread หยุดทำงาน");
    }

    // ─────────────────────────────────────────────────────────
    void Update()
    {
        // ══════════════════════════════════════════════════════
        //  [A] Main Thread — รับข้อมูลจาก Serial Thread
        // ══════════════════════════════════════════════════════
        bool serialConnected = (serialPort != null && serialPort.IsOpen);

        if (serialConnected)
        {
            // ดึงค่าจาก buffer อย่างปลอดภัย (แค่ถ้ามีข้อมูลใหม่)
            lock (_lock)
            {
                if (_newDataReady)
                {
                    System.Array.Copy(_fsrBuf, FsrStates,      4);
                    System.Array.Copy(_lcBuf,  LoadCellValues,  4);
                    _newDataReady = false;
                }
            }

            // ══════════════════════════════════════════════════
            //  [B] ควบคุมค้อนด้วยค่า FSR (Serial mode)
            // ══════════════════════════════════════════════════
            if (FsrStates[0] == 0 && FsrStates[1] == 0 &&
                FsrStates[2] == 0 && FsrStates[3] == 0)
            {
                transform.position = centerIdlePos;
            }
            else
            {
                if      (FsrStates[0] == 1) transform.position = topLeftPos;
                else if (FsrStates[1] == 1) transform.position = topRightPos;
                else if (FsrStates[2] == 1) transform.position = bottomLeftPos;
                else if (FsrStates[3] == 1) transform.position = bottomRightPos;
            }
        }
        else
        {
            // ══════════════════════════════════════════════════
            //  [C] Keyboard Fallback (ใช้เฉพาะตอนไม่ได้ต่อ Arduino)
            // ══════════════════════════════════════════════════
            if      (Input.GetKey(KeyCode.Q)) transform.position = topLeftPos;
            else if (Input.GetKey(KeyCode.W)) transform.position = topRightPos;
            else if (Input.GetKey(KeyCode.A)) transform.position = bottomLeftPos;
            else if (Input.GetKey(KeyCode.S)) transform.position = bottomRightPos;
            else                              transform.position = centerIdlePos;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  PARSE FSR  "FSR:1,0,0,1"  → int[4]
    // ─────────────────────────────────────────────────────────
    private bool TryParseFSR(string section, ref int[] result)
    {
        string values = section.Replace("FSR:", "").Trim();
        string[] parts = values.Split(',');
        if (parts.Length != 4) return false;
        for (int i = 0; i < 4; i++)
            if (!int.TryParse(parts[i].Trim(), out result[i])) return false;
        return true;
    }

    // ─────────────────────────────────────────────────────────
    //  PARSE LC  "LC:15230,8120,12400,9870"  → long[4]
    // ─────────────────────────────────────────────────────────
    private bool TryParseLC(string section, ref long[] result)
    {
        string values = section.Replace("LC:", "").Trim();
        string[] parts = values.Split(',');
        if (parts.Length != 4) return false;
        for (int i = 0; i < 4; i++)
            if (!long.TryParse(parts[i].Trim(), out result[i])) return false;
        return true;
    }

    // ─────────────────────────────────────────────────────────
    //  COLLISION — ตีโดนตุ่น
    // ─────────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Mole"))
        {
            if (gameManager != null)
                gameManager.AddScore(1);

            MoleController mole = collision.gameObject.GetComponent<MoleController>();
            if (mole != null)
                mole.Hit();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  CLEANUP — หยุด Thread และปิดพอร์ตเมื่อออกจากเกม
    // ─────────────────────────────────────────────────────────
    private void OnApplicationQuit()
    {
        StopSerialThread();
    }

    private void OnDestroy()
    {
        StopSerialThread();
    }

    private void StopSerialThread()
    {
        isThreadRunning = false;
        IsConnected = false;

        if (serialThread != null && serialThread.IsAlive)
        {
            serialThread.Join(500);   // รอ Thread จบ max 500ms
            serialThread = null;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("🔌 ปิด Serial Port เรียบร้อย");
        }
    }
}