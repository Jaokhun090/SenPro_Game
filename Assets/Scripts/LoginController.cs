using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// ============================================================
//  LoginController.cs
//  ─────────────────────────────────────────────────────────────
//  ควบคุมหน้าจอ LoginScene:
//  - สลับระหว่างหน้า Login และ Register
//  - เรียกใช้งาน FirebaseManager
//  - ตรวจสอบและแสดงหน้าต่างเตือนอัปเดตน้ำหนัก (เมื่อครบ 7 วัน)
//  - เปลี่ยน Scene ไปยัง MainMenu เมื่อเข้าสู่ระบบสำเร็จ
// ============================================================

public class LoginController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject weightUpdatePanel;

    [Header("Login Fields")]
    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;
    public Button btnLogin;
    public Button btnSwitchToRegister;

    [Header("Register Fields")]
    public TMP_InputField regNameInput;
    public TMP_InputField regAgeInput;
    public TMP_InputField regWeightInput;
    public TMP_InputField regEmailInput;
    public TMP_InputField regPasswordInput;
    public TMP_InputField regConfirmPasswordInput;
    public Button btnRegister;
    public Button btnSwitchToLogin;

    [Header("Weight Update Popup (แจ้งเตือน 7 วัน)")]
    public TMP_Text weightPopupNoticeText;
    public TMP_InputField newWeightInput;
    public Button btnConfirmWeight;
    public Button btnSkipWeight;

    [Header("Status Feedback")]
    public TMP_Text statusText;

    private void Start()
    {
        // ค่าเริ่มต้น: แสดงหน้า Login ซ่อนหน้า Register & Weight Popup
        ShowLoginPanel();

        if (weightUpdatePanel != null)
            weightUpdatePanel.SetActive(false);

        SetStatus("");

        // ผูก Event ให้ปุ่มต่างๆ
        if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
        if (btnSwitchToRegister != null) btnSwitchToRegister.onClick.AddListener(ShowRegisterPanel);
        if (btnRegister != null) btnRegister.onClick.AddListener(OnRegisterClicked);
        if (btnSwitchToLogin != null) btnSwitchToLogin.onClick.AddListener(ShowLoginPanel);
        if (btnConfirmWeight != null) btnConfirmWeight.onClick.AddListener(OnConfirmWeightClicked);
        if (btnSkipWeight != null) btnSkipWeight.onClick.AddListener(OnSkipWeightClicked);

        // ตรวจสอบความพร้อมของ Firebase
        if (FirebaseManager.Instance != null && !FirebaseManager.Instance.isFirebaseReady)
        {
            SetStatus("กำลังเชื่อมต่อระบบ...");
            FirebaseManager.Instance.InitializeFirebase(ready =>
            {
                if (ready) SetStatus("");
                else SetStatus("เชื่อมต่อเซิร์ฟเวอร์ไม่สำเร็จ ตรวจสอบอินเทอร์เน็ต");
            });
        }
    }

    // ============================================================
    //  PANEL SWITCHING
    // ============================================================

    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        SetStatus("");
    }

    public void ShowRegisterPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);
        SetStatus("");
    }

    // ============================================================
    //  LOGIN LOGIC
    // ============================================================

    public void OnLoginClicked()
    {
        string email = loginEmailInput != null ? loginEmailInput.text.Trim() : "";
        string password = loginPasswordInput != null ? loginPasswordInput.text.Trim() : "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SetStatus("กรุณากรอกอีเมลและรหัสผ่านให้ครบถ้วน");
            return;
        }

        SetInteractable(false);
        SetStatus("กำลังเข้าสู่ระบบ...");

        FirebaseManager.Instance.LoginUser(email, password, (success, message, needsWeightUpdate) =>
        {
            SetInteractable(true);

            if (!success)
            {
                SetStatus($"เข้าสู่ระบบไม่สำเร็จ: {message}");
                return;
            }

            SetStatus("เข้าสู่ระบบสำเร็จ!");

            // ตรวจสอบว่าต้องอัปเดตน้ำหนักประจำสัปดาห์หรือไม่
            if (needsWeightUpdate && weightUpdatePanel != null)
            {
                ShowWeightUpdatePopup();
            }
            else
            {
                // เข้าสู่เมนูหลักทันที
                ProceedToMainMenu();
            }
        });
    }

    // ============================================================
    //  REGISTER LOGIC
    // ============================================================

    public void OnRegisterClicked()
    {
        string name = regNameInput != null ? regNameInput.text.Trim() : "";
        string ageStr = regAgeInput != null ? regAgeInput.text.Trim() : "";
        string weightStr = regWeightInput != null ? regWeightInput.text.Trim() : "";
        string email = regEmailInput != null ? regEmailInput.text.Trim() : "";
        string password = regPasswordInput != null ? regPasswordInput.text.Trim() : "";
        string confirmPass = regConfirmPasswordInput != null ? regConfirmPasswordInput.text.Trim() : "";

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SetStatus("กรุณากรอกข้อมูลสำคัญให้ครบถ้วน (ชื่อ, อีเมล, รหัสผ่าน)");
            return;
        }

        if (password.Length < 6)
        {
            SetStatus("รหัสผ่านต้องมีความยาวอย่างน้อย 6 ตัวอักษร");
            return;
        }

        if (password != confirmPass)
        {
            SetStatus("รหัสผ่านทั้งสองช่องไม่ตรงกัน");
            return;
        }

        int.TryParse(ageStr, out int age);
        if (age <= 0) age = 60; // default สำหรับผู้สูงอายุ

        float.TryParse(weightStr, out float weight);
        if (weight <= 0) weight = 60f; // default weight

        SetInteractable(false);
        SetStatus("กำลังสมัครสมาชิกและสร้างข้อมูล...");

        FirebaseManager.Instance.RegisterUser(email, password, name, weight, age, (success, message) =>
        {
            SetInteractable(true);

            if (!success)
            {
                SetStatus($"สมัครไม่สำเร็จ: {message}");
                return;
            }

            SetStatus("สมัครสมาชิกสำเร็จ กำลังเข้าสู่ระบบ...");
            ProceedToMainMenu();
        });
    }

    // ============================================================
    //  WEEKLY WEIGHT UPDATE POPUP
    // ============================================================

    private void ShowWeightUpdatePopup()
    {
        if (weightUpdatePanel == null)
        {
            ProceedToMainMenu();
            return;
        }

        weightUpdatePanel.SetActive(true);

        if (weightPopupNoticeText != null)
        {
            weightPopupNoticeText.text = $"สวัสดีคุณ {PlayerData.PlayerName}!\n" +
                                         $"ผ่านไป 1 สัปดาห์แล้วจากน้ำหนักเดิม ({PlayerData.PlayerWeight:F1} kg)\n" +
                                         "กรุณาชั่งน้ำหนักเพื่อความแม่นยำในการคำนวณแรง";
        }

        if (newWeightInput != null)
        {
            newWeightInput.text = PlayerData.PlayerWeight.ToString("F1");
        }
    }

    public void OnConfirmWeightClicked()
    {
        if (newWeightInput == null)
        {
            ProceedToMainMenu();
            return;
        }

        if (float.TryParse(newWeightInput.text.Trim(), out float newWeight) && newWeight > 0)
        {
            SetStatus("กำลังอัปเดตน้ำหนัก...");
            FirebaseManager.Instance.UpdateWeight(newWeight, (success, msg) =>
            {
                if (weightUpdatePanel != null) weightUpdatePanel.SetActive(false);
                ProceedToMainMenu();
            });
        }
        else
        {
            SetStatus("กรุณาระบุน้ำหนักที่ถูกต้อง");
        }
    }

    public void OnSkipWeightClicked()
    {
        if (weightUpdatePanel != null)
            weightUpdatePanel.SetActive(false);

        ProceedToMainMenu();
    }

    // ============================================================
    //  NAVIGATION & HELPERS
    // ============================================================

    private void ProceedToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }
        Debug.Log($"[LoginController] {msg}");
    }

    private void SetInteractable(bool interactable)
    {
        if (btnLogin != null) btnLogin.interactable = interactable;
        if (btnRegister != null) btnRegister.interactable = interactable;
        if (btnSwitchToRegister != null) btnSwitchToRegister.interactable = interactable;
        if (btnSwitchToLogin != null) btnSwitchToLogin.interactable = interactable;
    }
}
