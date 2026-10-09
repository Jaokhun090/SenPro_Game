using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    //  GAME RESULTS LOGGING (CLOUD FIRESTORE)
    // ============================================================

    /// <summary>
    /// บันทึกผลลัพธ์การเล่นเกมจาก PlayerData ขึ้น Cloud Firestore
    /// บันทึกเป็น subcollection: users/{uid}/game_history/{sessionId}
    /// </summary>
    public void SaveGameResult(Action<bool, string> onComplete = null)
    {
        if (string.IsNullOrEmpty(currentUserId) || db == null)
        {
            Debug.LogWarning("[FirebaseManager] Cannot save game result: User not logged in or DB not ready.");
            onComplete?.Invoke(false, "ผู้ใช้ไม่ได้เข้าสู่ระบบ");
            return;
        }

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
            { "playedAt", FieldValue.ServerTimestamp }
        };

        // บันทึกลง users/{uid}/game_history
        db.Collection("users").Document(currentUserId)
          .Collection("game_history").Document()
          .SetAsync(sessionData).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string errMsg = task.Exception?.GetBaseException()?.Message ?? "บันทึกผลเกมไม่สำเร็จ";
                Debug.LogError($"[FirebaseManager] Save game error: {errMsg}");
                onComplete?.Invoke(false, errMsg);
                return;
            }

            Debug.Log("[FirebaseManager] Game session saved to Firestore successfully!");
            onComplete?.Invoke(true, "บันทึกผลการเล่นเรียบร้อยแล้ว");
        });
    }
}
