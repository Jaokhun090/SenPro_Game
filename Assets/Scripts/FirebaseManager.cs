using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

// ============================================================
//  FirebaseManager.cs
//  ─────────────────────────────────────────────────────────────
//  Singleton ควบคุมระบบ Firebase ทั้งหมด:
//  - ตรวจสอบ Dependency และเริ่มต้น Firebase อัตโนมัติ
//  - Authentication: สมัครสมาชิก, เข้าสู่ระบบ, ออกจากระบบ
//  - Firestore: บันทึก/ดึงข้อมูลประวัติผู้ป่วย, เช็คน้ำหนักทุก 7 วัน, บันทึกผลเกม
// ============================================================

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    [Header("Status")]
    public bool isFirebaseReady = false;
    public string currentUserId = "";
    public string latestCsvDownloadUrl = "";

    // Firebase References
    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private FirebaseUser user;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ตรวจสอบและเริ่มต้น Firebase
    /// </summary>
    public void InitializeFirebase(Action<bool> onInitialized = null)
    {
        if (isFirebaseReady)
        {
            onInitialized?.Invoke(true);
            return;
        }

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.DefaultInstance;
                isFirebaseReady = true;
                Debug.Log("[FirebaseManager] Firebase initialized successfully!");
                onInitialized?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[FirebaseManager] Could not resolve Firebase dependencies: {dependencyStatus}");
                isFirebaseReady = false;
                onInitialized?.Invoke(false);
            }
        });
    }

    // ============================================================
    //  AUTHENTICATION & USER PROFILE
    // ============================================================

    /// <summary>
    /// สมัครสมาชิกใหม่ พร้อมบันทึก Profile ผู้ป่วยลง Firestore
    /// </summary>
    public void RegisterUser(string email, string password, string fullName, float weight, int age, Action<bool, string> onComplete)
    {
        if (!isFirebaseReady)
        {
            onComplete?.Invoke(false, "Firebase ยังไม่พร้อมใช้งาน กรุณาลองใหม่อีกครั้ง");
            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                onComplete?.Invoke(false, "การสมัครสมาชิกล้มเหลว (ถูกยกเลิก)");
                return;
            }
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "เกิดข้อผิดพลาดในการสมัคร";
                Debug.LogError($"[FirebaseManager] Register error: {errMsg}");
                onComplete?.Invoke(false, errMsg);
                return;
            }

            // สมัคร Auth สำเร็จ -> ได้ User ID
            AuthResult authResult = task.Result;
            user = authResult.User;
            currentUserId = user.UserId;

            // บันทึก Profile ผู้ป่วยลง Firestore
            SaveInitialUserProfile(currentUserId, email, fullName, weight, age, onComplete);
        });
    }

    /// <summary>
    /// สร้างเอกสารผู้ใช้เริ่มต้นใน Firestore
    /// </summary>
    private void SaveInitialUserProfile(string uid, string email, string fullName, float weight, int age, Action<bool, string> onComplete)
    {
        DocumentReference userDoc = db.Collection("users").Document(uid);

        Dictionary<string, object> profileData = new Dictionary<string, object>
        {
            { "fullName", fullName },
            { "email", email },
            { "weight", weight },
            { "age", age },
            { "lastWeightUpdate", FieldValue.ServerTimestamp },
            { "createdAt", FieldValue.ServerTimestamp }
        };

        userDoc.SetAsync(profileData).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "บันทึกโปรไฟล์ไม่สำเร็จ";
                onComplete?.Invoke(false, errMsg);
                return;
            }

            // ซิงค์เข้า PlayerData Static
            PlayerData.UserId = uid;
            PlayerData.UserEmail = email;
            PlayerData.PlayerName = fullName;
            PlayerData.PlayerWeight = weight;
            PlayerData.PlayerAge = age;
            PlayerData.NeedsWeightUpdate = false;

            Debug.Log($"[FirebaseManager] User created & profile saved: {fullName}");
            onComplete?.Invoke(true, "สมัครสมาชิกสำเร็จ!");
        });
    }

    /// <summary>
    /// เข้าสู่ระบบ และดึงข้อมูล Profile มาใส่ PlayerData
    /// onComplete: (isSuccess, message, needsWeightUpdate)
    /// </summary>
    public void LoginUser(string email, string password, Action<bool, string, bool> onComplete)
    {
        if (!isFirebaseReady)
        {
            onComplete?.Invoke(false, "Firebase ยังไม่พร้อมใช้งาน", false);
            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                onComplete?.Invoke(false, "การเข้าสู่ระบบถูกยกเลิก", false);
                return;
            }
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "อีเมลหรือรหัสผ่านไม่ถูกต้อง";
                Debug.LogError($"[FirebaseManager] Login error: {errMsg}");
                onComplete?.Invoke(false, errMsg, false);
                return;
            }

            AuthResult authResult = task.Result;
            user = authResult.User;
            currentUserId = user.UserId;

            // ดึงข้อมูลผู้ป่วยจาก Firestore
            FetchUserProfile(currentUserId, onComplete);
        });
    }

    /// <summary>
    /// ส่งอีเมลสำหรับรีเซ็ตรหัสผ่าน (Forgot Password)
    /// </summary>
    public void SendPasswordResetEmail(string email, Action<bool, string> onComplete)
    {
        if (!isFirebaseReady)
        {
            onComplete?.Invoke(false, "Firebase ยังไม่พร้อมใช้งาน");
            return;
        }

        if (string.IsNullOrEmpty(email))
        {
            onComplete?.Invoke(false, "กรุณากรอกอีเมล");
            return;
        }

        auth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                onComplete?.Invoke(false, "การส่งคำขอถูกยกเลิก");
                return;
            }
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "เกิดข้อผิดพลาดในการส่งอีเมลรีเซ็ตรหัสผ่าน";
                Debug.LogError($"[FirebaseManager] Reset password error: {errMsg}");

                if (errMsg.Contains("user-not-found") || errMsg.Contains("There is no user record"))
                {
                    errMsg = "ไม่พบบัญชีผู้ใช้นี้ในระบบ กรุณาตรวจสอบอีเมล";
                }
                else if (errMsg.Contains("invalid-email") || errMsg.Contains("badly formatted"))
                {
                    errMsg = "รูปแบบอีเมลไม่ถูกต้อง";
                }

                onComplete?.Invoke(false, errMsg);
                return;
            }

            Debug.Log($"[FirebaseManager] Password reset email sent to: {email}");
            onComplete?.Invoke(true, "ส่งลิงก์เปลี่ยนรหัสผ่านไปยังอีเมลของคุณเรียบร้อยแล้ว!\nกรุณาเปิดอีเมลเพื่อตั้งรหัสผ่านใหม่ (อย่าลืมเช็คใน Junk/Spam)");
        });
    }

    /// <summary>
    /// ดึงข้อมูล Profile ของผู้ป่วยจาก Firestore และตรวจสอบรอบอัปเดตน้ำหนัก 7 วัน
    /// </summary>
    private void FetchUserProfile(string uid, Action<bool, string, bool> onComplete)
    {
        DocumentReference userDoc = db.Collection("users").Document(uid);

        userDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || !task.Result.Exists)
            {
                // เข้าสู่ระบบได้แต่ไม่มีข้อมูล profile (หรือดึงไม่สำเร็จ)
                PlayerData.UserId = uid;
                PlayerData.UserEmail = user.Email;
                PlayerData.PlayerName = user.Email;
                PlayerData.PlayerWeight = 60f;
                onComplete?.Invoke(true, "เข้าสู่ระบบสำเร็จ (ใช้ค่าเริ่มต้น)", false);
                return;
            }

            DocumentSnapshot snapshot = task.Result;
            Dictionary<string, object> data = snapshot.ToDictionary();

            string fullName = data.ContainsKey("fullName") ? data["fullName"]?.ToString() : "Player";
            float weight = 60f;
            if (data.ContainsKey("weight"))
            {
                float.TryParse(data["weight"]?.ToString(), out weight);
            }
            int age = 60;
            if (data.ContainsKey("age"))
            {
                int.TryParse(data["age"]?.ToString(), out age);
            }

            // ซิงค์เข้า PlayerData
            PlayerData.UserId = uid;
            PlayerData.UserEmail = user.Email;
            PlayerData.PlayerName = fullName;
            PlayerData.PlayerWeight = weight > 0 ? weight : 60f;
            PlayerData.PlayerAge = age;

            // ตรวจสอบว่าน้ำหนักเกิน 7 วันแล้วหรือไม่
            bool needsUpdate = CheckIfWeightUpdateNeeded(snapshot);
            PlayerData.NeedsWeightUpdate = needsUpdate;

            Debug.Log($"[FirebaseManager] Loaded profile: {fullName}, Weight: {weight}kg, NeedsWeightUpdate: {needsUpdate}");
            onComplete?.Invoke(true, "เข้าสู่ระบบสำเร็จ!", needsUpdate);
        });
    }

    /// <summary>
    /// ตรวจสอบว่าผ่านไปมากกว่า 7 วันนับจากการชั่งน้ำหนักครั้งล่าสุดหรือไม่
    /// </summary>
    private bool CheckIfWeightUpdateNeeded(DocumentSnapshot snapshot)
    {
        if (!snapshot.ContainsField("lastWeightUpdate"))
            return true;

        try
        {
            Timestamp lastTimestamp = snapshot.GetValue<Timestamp>("lastWeightUpdate");
            DateTime lastDate = lastTimestamp.ToDateTime();
            TimeSpan diff = DateTime.UtcNow - lastDate;
            return diff.TotalDays >= 7.0;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirebaseManager] Error checking lastWeightUpdate: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// อัปเดตค่าน้ำหนักใหม่ของผู้ป่วย (บันทึกลง Firestore และ PlayerData)
    /// </summary>
    public void UpdateWeight(float newWeight, Action<bool, string> onComplete)
    {
        if (string.IsNullOrEmpty(currentUserId) || db == null)
        {
            PlayerData.PlayerWeight = newWeight;
            onComplete?.Invoke(true, "อัปเดตน้ำหนักในเครื่องเรียบร้อย");
            return;
        }

        DocumentReference userDoc = db.Collection("users").Document(currentUserId);
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "weight", newWeight },
            { "lastWeightUpdate", FieldValue.ServerTimestamp }
        };

        userDoc.UpdateAsync(updates).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "อัปเดตน้ำหนักไม่สำเร็จ";
                onComplete?.Invoke(false, errMsg);
                return;
            }

            PlayerData.PlayerWeight = newWeight;
            PlayerData.NeedsWeightUpdate = false;
            Debug.Log($"[FirebaseManager] Weight updated to: {newWeight} kg");
            onComplete?.Invoke(true, "อัปเดตน้ำหนักเรียบร้อยแล้ว");
        });
    }

    /// <summary>
    /// ออกจากระบบ
    /// </summary>
    public void SignOut()
    {
        if (auth != null)
        {
            auth.SignOut();
        }
        user = null;
        currentUserId = "";
        PlayerData.UserId = "";
        PlayerData.UserEmail = "";
        PlayerData.PlayerName = "Guest";
        PlayerData.PlayerWeight = 60f;
        PlayerData.NeedsWeightUpdate = false;
        Debug.Log("[FirebaseManager] Signed out.");
    }

    // ============================================================
    //  GAME RESULTS LOGGING (CLOUD FIRESTORE & STORAGE)
    // ============================================================

    /// <summary>
    /// ค้นหาไฟล์ CSV บันทึกเซนเซอร์ล่าสุดที่เพิ่งบันทึกเสร็จ
    /// </summary>
    private string GetLatestCsvPath()
    {
        if (!string.IsNullOrEmpty(PlayerData.LastCsvFilePath) && File.Exists(PlayerData.LastCsvFilePath))
        {
            return PlayerData.LastCsvFilePath;
        }

        string saveDir = @"C:\Users\Asus\Desktop\SeniorProject\GameLogs";
        if (Directory.Exists(saveDir))
        {
            DirectoryInfo dir = new DirectoryInfo(saveDir);
            FileInfo[] files = dir.GetFiles("*.csv");
            if (files != null && files.Length > 0)
            {
                Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                // ตรวจสอบว่าไฟล์ถูกแก้ไขไม่เกิน 5 นาทีที่ผ่านมา
                if ((DateTime.Now - files[0].LastWriteTime).TotalMinutes < 5)
                {
                    return files[0].FullName;
                }
            }
        }
        return "";
    }

    /// <summary>
    /// บันทึกผลลัพธ์การเล่นเกมจาก PlayerData ขึ้น Cloud Firestore
    /// พร้อมแนบข้อมูลไฟล์ตาราง CSV Log (Raw Sensor Data) ขึ้น Cloud
    /// </summary>
    public void SaveGameResult(Action<bool, string> onComplete = null)
    {
        if (string.IsNullOrEmpty(currentUserId) || db == null)
        {
            Debug.LogWarning("[FirebaseManager] Cannot save game result: User not logged in or DB not ready.");
            onComplete?.Invoke(false, "ผู้ใช้ไม่ได้เข้าสู่ระบบ");
            return;
        }

        float leftF = PlayerData.PadForceTL + PlayerData.PadForceBL;
        float rightF = PlayerData.PadForceTR + PlayerData.PadForceBR;
        float totalF = leftF + rightF;
        float leftPct = totalF > 0 ? (leftF / totalF) * 100f : 50f;
        float rightPct = totalF > 0 ? (rightF / totalF) * 100f : 50f;

        Dictionary<string, object> sessionData = new Dictionary<string, object>
        {
            { "gameName", string.IsNullOrEmpty(PlayerData.GameDisplayName) ? PlayerData.SelectedGame : PlayerData.GameDisplayName },
            { "score", PlayerData.FinalScore },
            { "timePlayedSeconds", PlayerData.TimePlayed },
            { "durationSettingSeconds", PlayerData.GameDuration },
            { "correctCount", PlayerData.CorrectCount },
            { "wrongCount", PlayerData.WrongCount },
            { "playerWeightKg", PlayerData.PlayerWeight },
            { "totalKgForce", PlayerData.TotalKgForce },
            { "avgForcePerStomp", PlayerData.AvgForcePerStomp },
            { "maxSingleForce", PlayerData.MaxSingleForce },
            { "totalStomps", PlayerData.TotalStomps },
            { "padForceTL", PlayerData.PadForceTL },
            { "padForceTR", PlayerData.PadForceTR },
            { "padForceBL", PlayerData.PadForceBL },
            { "padForceBR", PlayerData.PadForceBR },
            { "padStompsTL", PlayerData.PadStompsTL },
            { "padStompsTR", PlayerData.PadStompsTR },
            { "padStompsBL", PlayerData.PadStompsBL },
            { "padStompsBR", PlayerData.PadStompsBR },
            { "balanceLeftPercent", Mathf.Round(leftPct) },
            { "balanceRightPercent", Mathf.Round(rightPct) },
            { "playedAt", FieldValue.ServerTimestamp }
        };

        // แนบข้อมูลไฟล์ CSV Log
        string csvPath = GetLatestCsvPath();
        if (!string.IsNullOrEmpty(csvPath) && File.Exists(csvPath))
        {
            try
            {
                FileInfo fi = new FileInfo(csvPath);
                sessionData["csvFileName"] = fi.Name;
                sessionData["csvFileSizeBytes"] = fi.Length;
                sessionData["hasCsvLog"] = true;

                // หากขนาดไฟล์ไม่เกิน 850 KB บันทึกเนื้อหาตาราง CSV ลงใน Firestore โดยตรง
                // เพื่อให้ดูตาราง CSV ได้ทันทีผ่านหน้าเว็บ Firebase Console
                if (fi.Length <= 850000)
                {
                    string csvContent = File.ReadAllText(csvPath, Encoding.UTF8);
                    sessionData["csvContent"] = csvContent;
                    sessionData["csvRowCount"] = File.ReadAllLines(csvPath).Length;
                }
                else
                {
                    sessionData["csvContentNotice"] = "ไฟล์มีขนาดใหญ่กว่า 850KB ถูกบันทึกไว้ในเครื่อง Host";
                }

                Debug.Log($"[FirebaseManager] Attached CSV to session: {fi.Name} ({fi.Length} bytes)");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirebaseManager] Error reading CSV: {ex.Message}");
            }
        }

        // บันทึกลง users/{uid}/game_history/{documentId}
        DocumentReference newDoc = db.Collection("users").Document(currentUserId)
          .Collection("game_history").Document();

        string docId = newDoc.Id;

        newDoc.SetAsync(sessionData).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "บันทึกผลเกมไม่สำเร็จ";
                Debug.LogError($"[FirebaseManager] Save game error: {errMsg}");
                onComplete?.Invoke(false, errMsg);
                return;
            }

            Debug.Log("[FirebaseManager] Game session & CSV saved to Firestore successfully!");
            onComplete?.Invoke(true, "บันทึกผลการเล่นและข้อมูลตาราง CSV เรียบร้อยแล้ว");

            // อัปโหลดไฟล์ตัวจริงขึ้น Firebase Cloud Storage ในเบื้องหลัง (ถ้าเปิดใช้งาน)
            if (!string.IsNullOrEmpty(csvPath) && File.Exists(csvPath))
            {
                StartCoroutine(UploadCsvToStorageCoroutine(csvPath, docId));
            }
        });
    }

    /// <summary>
    /// อัปโหลดไฟล์ .csv ไปยัง Firebase Cloud Storage (Background Coroutine)
    /// </summary>
    private IEnumerator UploadCsvToStorageCoroutine(string filePath, string documentId)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) yield break;
        if (string.IsNullOrEmpty(currentUserId) || user == null) yield break;

        string idToken = "";
        var tokenTask = user.TokenAsync(false);
        while (!tokenTask.IsCompleted) yield return null;
        if (!tokenTask.IsFaulted && !string.IsNullOrEmpty(tokenTask.Result))
        {
            idToken = tokenTask.Result;
        }

        string fileName = Path.GetFileName(filePath);
        string bucket = "senpro-24a41.firebasestorage.app";
        string objectPath = UnityWebRequest.EscapeURL($"users/{currentUserId}/game_logs/{fileName}");
        string uploadUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o?uploadType=media&name={objectPath}";

        byte[] fileBytes = File.ReadAllBytes(filePath);

        using (UnityWebRequest req = new UnityWebRequest(uploadUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(fileBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "text/csv");
            if (!string.IsNullOrEmpty(idToken))
            {
                req.SetRequestHeader("Authorization", "Firebase " + idToken);
            }

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FirebaseManager] CSV uploaded to Storage successfully: {fileName}");
                try
                {
                    string respJson = req.downloadHandler.text;
                    string downloadUrl = "";
                    int tokenIdx = respJson.IndexOf("\"downloadTokens\":");
                    if (tokenIdx > 0)
                    {
                        int start = respJson.IndexOf("\"", tokenIdx + 17) + 1;
                        int end = respJson.IndexOf("\"", start);
                        if (start > 0 && end > start)
                        {
                            string token = respJson.Substring(start, end - start);
                            downloadUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{objectPath}?alt=media&token={token}";
                        }
                    }

                    if (db != null && !string.IsNullOrEmpty(documentId))
                    {
                        var updates = new Dictionary<string, object>
                        {
                            { "csvStoragePath", $"users/{currentUserId}/game_logs/{fileName}" }
                        };
                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            updates["csvDownloadUrl"] = downloadUrl;
                            latestCsvDownloadUrl = downloadUrl;
                        }

                        db.Collection("users").Document(currentUserId)
                          .Collection("game_history").Document(documentId)
                          .UpdateAsync(updates);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FirebaseManager] Error parsing storage response: {e.Message}");
                }
            }
            else
            {
                // ถ้ายังไม่ได้กดเปิดใช้งาน Cloud Storage ใน Console (HTTP 404) จะขึ้นเป็นข้อมูลประกอบ ไม่รบกวนการเล่น
                Debug.Log($"[FirebaseManager] Cloud Storage upload notice ({req.responseCode}): {req.error}");
            }
        }
    }

    /// <summary>
    /// ดึงสถิติประวัติการเล่นจริงจาก Cloud Firestore (Real-time Fetch)
    /// คืนค่า (success, จำนวนรอบที่เล่น, คะแนนสูงสุดบน Cloud, ข้อความแจ้งเตือน)
    /// </summary>
    public void FetchUserGameStats(string gameName, Action<bool, int, int, string> onComplete)
    {
        if (string.IsNullOrEmpty(currentUserId) || db == null)
        {
            onComplete?.Invoke(false, 0, 0, "ผู้ใช้ไม่ได้เข้าสู่ระบบหรือระบบ Cloud ยังไม่พร้อม");
            return;
        }

        db.Collection("users").Document(currentUserId)
          .Collection("game_history").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "ไม่สามารถดึงข้อมูลจาก Cloud ได้";
                Debug.LogWarning($"[FirebaseManager] Fetch stats error: {errMsg}");
                onComplete?.Invoke(false, 0, 0, errMsg);
                return;
            }

            QuerySnapshot snapshot = task.Result;
            int totalSessions = 0;
            int highestScore = 0;

            if (snapshot != null)
            {
                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;

                    var dict = doc.ToDictionary();
                    if (dict.ContainsKey("gameName"))
                    {
                        string gName = dict["gameName"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(gameName) && !gName.Contains(gameName) && !gameName.Contains(gName))
                        {
                            continue;
                        }
                    }

                    totalSessions++;

                    if (dict.ContainsKey("score"))
                    {
                        int s = Convert.ToInt32(dict["score"]);
                        if (s > highestScore) highestScore = s;
                    }
                }
            }

            Debug.Log($"[FirebaseManager] Fetched stats for '{gameName}': Sessions={totalSessions}, HighScore={highestScore}");
            onComplete?.Invoke(true, totalSessions, highestScore, "ดึงข้อมูลจาก Cloud สำเร็จ");
        });
    }

    /// <summary>
    /// ส่งรายงานผลการฝึกและข้อมูลสุขภาพไปยังอีเมลของผู้เล่นผ่าน Firebase Cloud
    /// </summary>
    public void SendReportEmail(Action<bool, string> onComplete)
    {
        if (db == null)
        {
            onComplete?.Invoke(false, "ระบบ Cloud ยังไม่พร้อมใช้งาน");
            return;
        }

        string targetEmail = !string.IsNullOrEmpty(PlayerData.UserEmail) 
            ? PlayerData.UserEmail 
            : (auth != null && auth.CurrentUser != null ? auth.CurrentUser.Email : "");

        if (string.IsNullOrEmpty(targetEmail))
        {
            onComplete?.Invoke(false, "ไม่พบอีเมลผู้เล่นในระบบ");
            return;
        }

        float leftForce = PlayerData.PadForceTL + PlayerData.PadForceBL;
        float rightForce = PlayerData.PadForceTR + PlayerData.PadForceBR;
        float totalF = leftForce + rightForce;
        float leftPct = totalF > 0 ? (leftForce / totalF) * 100f : 50f;
        float rightPct = totalF > 0 ? (rightForce / totalF) * 100f : 50f;

        string gameName = string.IsNullOrEmpty(PlayerData.GameDisplayName) ? PlayerData.SelectedGame : PlayerData.GameDisplayName;
        string timestampStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        string textBody = 
            $"รายงานผลการฝึกสมรรถภาพและการรับรู้ - โครงการ SenPro\n\n" +
            $"ผู้เล่น: {PlayerData.PlayerName}\n" +
            $"เกมที่เล่น: {gameName}\n" +
            $"วันที่และเวลา: {timestampStr}\n" +
            $"เวลาที่เล่น: {PlayerData.TimePlayed:F0} วินาที\n" +
            $"คะแนนที่ได้: {PlayerData.FinalScore} คะแนน\n\n" +
            $"--- ข้อมูลทางกายภาพและแรงเหยียบ ---\n" +
            $"พลังงานรวมที่ออกแรง (Total Exercise): {PlayerData.TotalKgForce:N0} kg-force\n" +
            $"แรงเหยียบเฉลี่ย: {PlayerData.AvgForcePerStomp:F1} kg/ครั้ง\n" +
            $"แรงเหยียบสูงสุด: {PlayerData.MaxSingleForce:F1} kg\n" +
            $"จำนวนก้าว/ครั้งที่เหยียบ: {PlayerData.TotalStomps} ครั้ง\n\n" +
            $"--- สมดุลการลงน้ำหนัก 4 จุด ---\n" +
            $"บนซ้าย: {PlayerData.PadForceTL:F1} kg  |  บนขวา: {PlayerData.PadForceTR:F1} kg\n" +
            $"ล่างซ้าย: {PlayerData.PadForceBL:F1} kg  |  ล่างขวา: {PlayerData.PadForceBR:F1} kg\n" +
            $"สมดุล ซีกซ้าย {leftPct:F0}%  vs  ซีกขวา {rightPct:F0}%\n";

        if (!string.IsNullOrEmpty(latestCsvDownloadUrl))
        {
            textBody += $"\nดาวน์โหลดไฟล์ข้อมูลดิบ (CSV Log): {latestCsvDownloadUrl}\n";
        }

        string htmlBody = 
            $"<div style='font-family: Arial, sans-serif; max-width: 600px; padding: 20px; border: 1px solid #e0e0e0; border-radius: 10px;'>" +
            $"<h2 style='color: #2c3e50;'>รายงานผลการฝึกสมรรถภาพ (SenPro)</h2>" +
            $"<p><strong>ผู้เล่น:</strong> {PlayerData.PlayerName} (น้ำหนัก {PlayerData.PlayerWeight} กก.)</p>" +
            $"<p><strong>เกมที่ทดสอบ:</strong> {gameName}</p>" +
            $"<p><strong>คะแนน:</strong> <span style='font-size: 24px; color: #d35400; font-weight: bold;'>{PlayerData.FinalScore}</span></p>" +
            $"<p><strong>เวลาที่เล่น:</strong> {PlayerData.TimePlayed:F0} วินาที</p>" +
            $"<hr style='border: none; border-top: 1px solid #eee; margin: 15px 0;'/>" +
            $"<h3 style='color: #27ae60;'>ข้อมูลการออกแรงและการทรงตัว</h3>" +
            $"<ul>" +
            $"<li><strong>Total Exercise:</strong> {PlayerData.TotalKgForce:N0} kg-force</li>" +
            $"<li><strong>แรงเฉลี่ย:</strong> {PlayerData.AvgForcePerStomp:F1} kg/ครั้ง</li>" +
            $"<li><strong>แรงเหยียบสูงสุด:</strong> {PlayerData.MaxSingleForce:F1} kg</li>" +
            $"<li><strong>เหยียบทั้งหมด:</strong> {PlayerData.TotalStomps} ครั้ง</li>" +
            $"<li><strong>สมดุลร่างกาย:</strong> ซีกซ้าย {leftPct:F0}% | ซีกขวา {rightPct:F0}%</li>" +
            $"</ul>";

        if (!string.IsNullOrEmpty(latestCsvDownloadUrl))
        {
            htmlBody += $"<p><a href='{latestCsvDownloadUrl}' style='display:inline-block; padding:10px 18px; background-color:#2980b9; color:#fff; text-decoration:none; border-radius:6px;'>ดาวน์โหลดไฟล์ตารางข้อมูลดิบ (CSV)</a></p>";
        }

        htmlBody += $"<p style='color: #888; font-size: 12px; margin-top: 25px;'>ระบบบันทึกและส่งรายงานอัตโนมัติ Senior Project SenPro Platform</p></div>";

        var mailDoc = new Dictionary<string, object>
        {
            { "to", targetEmail },
            { "message", new Dictionary<string, object>
                {
                    { "subject", $"[SenPro] รายงานผลการฝึก: {gameName} - คุณ {PlayerData.PlayerName} ({timestampStr})" },
                    { "text", textBody },
                    { "html", htmlBody }
                }
            },
            { "userId", currentUserId },
            { "createdAt", FieldValue.ServerTimestamp }
        };

        db.Collection("mail").AddAsync(mailDoc).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string err = task.Exception?.GetBaseException()?.Message ?? "ส่งไม่สำเร็จ";
                Debug.LogError($"[FirebaseManager] Error sending email via mail collection: {err}");
                onComplete?.Invoke(false, "ส่งไม่สำเร็จ: " + err);
            }
            else
            {
                Debug.Log($"[FirebaseManager] Mail request queued for {targetEmail}");
                onComplete?.Invoke(true, $"ส่งรายงานไปยัง {targetEmail} เรียบร้อยแล้ว");
            }
        });
    }
}
