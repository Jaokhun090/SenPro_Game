// ============================================================
//  SharedSerialReceiver.cs
//  ─────────────────────────────────────────────────────────────
//  DontDestroyOnLoad Serial Reader — สร้างครั้งเดียวใน MainMenu
//  แล้วคงอยู่ตลอดทุก Scene  ทุก GameManager เรียก Instance ได้
//
//  รองรับ Format จาก Arduino ของเรา:
//      FSR:1,0,0,1|LC:15230,8120,12400,9870
//
//  การใช้งานใน Script อื่น:
//      SharedSerialReceiver.Instance.FsrStates[0]  // 0 หรือ 1
//      SharedSerialReceiver.Instance.LoadCellValues[2]  // ค่าดิบ LC
// ============================================================

using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class SharedSerialReceiver : MonoBehaviour
{
    // ═══════ Singleton ═══════
    public static SharedSerialReceiver Instance { get; private set; }

    // ═══════ Settings (ตั้งค่าได้ใน Inspector) ═══════
    [Header("Serial Settings")]
    public string portName = "COM3";
    public int baudRate = 115200;

    [Header("Debug")]
    public bool debugSerial = false;

    // ═══════ Public Data (อ่านจาก Script อื่นได้) ═══════
    [HideInInspector] public int[]  FsrStates      = new int[4];
    [HideInInspector] public long[] LoadCellValues  = new long[4];
    [HideInInspector] public bool   IsConnected     = false;

    // ═══════ Private ═══════
    private SerialPort serialPort;
    private Thread     readThread;
    private bool       isThreadRunning = false;

    // Double buffer — Thread เขียน buf → Main Thread คัดลอก
    private readonly object _lock      = new object();
    private int[]  _fsrBuf             = new int[4];
    private long[] _lcBuf              = new long[4];
    private bool   _newDataReady       = false;

    // ═══════════════════════════════════════════════════════
    //  Awake — Singleton + DontDestroyOnLoad
    // ═══════════════════════════════════════════════════════
    void Awake()
    {
        // ถ้ามี Instance อยู่แล้ว (เช่น กลับมา MainMenu) → ทำลายตัวซ้ำทิ้ง
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ═══════════════════════════════════════════════════════
    //  Start — เปิด Serial Port
    // ═══════════════════════════════════════════════════════
    void Start()
    {
        OpenConnection();
    }

    void OpenConnection()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 100;
            serialPort.DtrEnable   = true;
            serialPort.Open();

            isThreadRunning = true;
            readThread = new Thread(ReadDataThread);
            readThread.IsBackground = true;
            readThread.Start();

            IsConnected = true;
            Debug.Log("✅ SharedSerialReceiver: เปิด " + portName + " สำเร็จ");
        }
        catch (System.Exception e)
        {
            IsConnected = false;
            Debug.LogWarning("⚠️ SharedSerialReceiver: เปิด Serial ไม่ได้ (ใช้ Keyboard แทน): " + e.Message);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Background Thread — อ่าน Serial ไม่หยุด
    // ═══════════════════════════════════════════════════════
    void ReadDataThread()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string line = serialPort.ReadLine().Trim();

                // Parse format: FSR:1,0,0,1|LC:15230,8120,12400,9870
                if (line.Contains("FSR:") && line.Contains("|LC:"))
                {
                    string[] halves = line.Split('|');
                    if (halves.Length == 2)
                    {
                        int[]  tmpFsr = new int[4];
                        long[] tmpLc  = new long[4];

                        if (TryParseFSR(halves[0], ref tmpFsr) &&
                            TryParseLC(halves[1], ref tmpLc))
                        {
                            lock (_lock)
                            {
                                System.Array.Copy(tmpFsr, _fsrBuf, 4);
                                System.Array.Copy(tmpLc,  _lcBuf,  4);
                                _newDataReady = true;
                            }
                        }
                    }
                }
            }
            catch (System.TimeoutException) { }
            catch (System.Exception) { break; }

            Thread.Sleep(5);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Update — คัดลอก buffer → public arrays (Main Thread)
    // ═══════════════════════════════════════════════════════
    void Update()
    {
        if (!IsConnected) return;

        lock (_lock)
        {
            if (_newDataReady)
            {
                System.Array.Copy(_fsrBuf, FsrStates,     4);
                System.Array.Copy(_lcBuf,  LoadCellValues, 4);
                _newDataReady = false;
            }
        }

        if (debugSerial)
        {
            Debug.Log($"[Serial] FSR: {FsrStates[0]},{FsrStates[1]},{FsrStates[2]},{FsrStates[3]} " +
                      $"| LC: {LoadCellValues[0]},{LoadCellValues[1]},{LoadCellValues[2]},{LoadCellValues[3]}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Parse Helpers
    // ═══════════════════════════════════════════════════════
    private bool TryParseFSR(string section, ref int[] result)
    {
        string values = section.Replace("FSR:", "").Trim();
        string[] parts = values.Split(',');
        if (parts.Length != 4) return false;
        for (int i = 0; i < 4; i++)
            if (!int.TryParse(parts[i].Trim(), out result[i])) return false;
        return true;
    }

    private bool TryParseLC(string section, ref long[] result)
    {
        string values = section.Replace("LC:", "").Trim();
        string[] parts = values.Split(',');
        if (parts.Length != 4) return false;
        for (int i = 0; i < 4; i++)
            if (!long.TryParse(parts[i].Trim(), out result[i])) return false;
        return true;
    }

    // ═══════════════════════════════════════════════════════
    //  Public API — ให้ GameManager เรียกใช้ง่ายๆ
    // ═══════════════════════════════════════════════════════

    /// <summary>เช็คว่า FSR ตัวที่ index กำลังถูกเหยียบอยู่หรือไม่</summary>
    public bool IsPadPressed(int index)
    {
        if (index < 0 || index >= 4) return false;
        return FsrStates[index] == 1;
    }

    /// <summary>หาว่าแผ่นเหยียบไหนถูกกดอยู่ (ตัวแรกที่เจอ) หรือ -1 ถ้าไม่มี</summary>
    public int GetFirstPressedPad()
    {
        for (int i = 0; i < 4; i++)
            if (FsrStates[i] == 1) return i;
        return -1;
    }

    /// <summary>คืนค่าน้ำหนักดิบจาก Load Cell ตัวที่ index</summary>
    public long GetLoadCellRaw(int index)
    {
        if (index < 0 || index >= 4) return 0;
        return LoadCellValues[index];
    }

    // ═══════════════════════════════════════════════════════
    //  Cleanup
    // ═══════════════════════════════════════════════════════
    void OnApplicationQuit()
    {
        StopSerial();
    }

    void OnDestroy()
    {
        // เฉพาะเมื่อ Instance ตัวจริงถูกทำลาย (ปิดแอป)
        if (Instance == this)
        {
            StopSerial();
            Instance = null;
        }
    }

    private void StopSerial()
    {
        isThreadRunning = false;

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(500);
            readThread = null;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("🔌 SharedSerialReceiver: ปิด Serial Port แล้ว");
        }
    }
}
