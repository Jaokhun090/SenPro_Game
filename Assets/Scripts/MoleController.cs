using UnityEngine;

// ============================================================
//  MoleController.cs
//  ─────────────────────────────────────────────────────────────
//  ควบคุมการแสดงผลและสถานะของตุ่นแต่ละตัว:
//  - เช็คสถานะ IsAlive ป้องกันการตีซ้ำและคะแนนเด้งเกิน
//  - แสดงภาพปกติ (normalSprite) / ภาพตอนโดนตี (deadSprite)
//  - ปิด Collider และซ่อนตัวเมื่อตายตามเวลาที่กำหนด
// ============================================================

public class MoleController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite normalSprite; 
    public Sprite deadSprite;   
    
    [Header("Settings")]
    [Tooltip("ระยะเวลาแสดงภาพตอนโดนตีก่อนมุดลงหลุม (วินาที)")]
    public float deadDuration = 0.35f;

    private SpriteRenderer sr;
    private CircleCollider2D col; 

    // สถานะว่าตุ่นยังมีชีวิตอยู่ให้ตีได้หรือไม่
    public bool IsAlive { get; private set; } = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>(); 
    }

    /// <summary>
    /// สั่งให้ตุ่นโผล่ขึ้นมาพร้อมให้ตี
    /// </summary>
    public void ShowMole()
    {
        CancelInvoke();
        IsAlive = true;

        if (sr != null)
        {
            sr.sprite = normalSprite; 
            sr.enabled = true;
        }

        if (col != null)
        {
            col.enabled = true;
        }
    }

    /// <summary>
    /// สั่งซ่อนตุ่นลงหลุมทันที
    /// </summary>
    public void ForceHide()
    {
        CancelInvoke();
        IsAlive = false;

        if (sr != null) sr.enabled = false;       
        if (col != null) col.enabled = false;      
    }

    /// <summary>
    /// ถูกตีโดน — คืนค่า true เฉพาะการตีครั้งแรกที่ถูกต้องเท่านั้น (ถ้าตีซ้ำขณะตายแล้วจะ return false)
    /// </summary>
    public bool Hit()
    {
        // ถ้าตุ่นตายไปแล้ว หรือยังไม่โผล่ ปฏิเสธการตีทันที (1 ตุ่น = 1 คะแนน)
        if (!IsAlive) return false;

        IsAlive = false;

        // ปิด Collider ทันที เพื่อไม่ให้เกิด Trigger ซ้ำซ้อน
        if (col != null) col.enabled = false;

        // เปลี่ยนเป็นรูปตาย
        if (sr != null) sr.sprite = deadSprite;

        // แสดงรูปตายตามเวลา deadDuration แล้วมุดลงหลุม (ไม่รีเซ็ตเวลาซ้ำ)
        CancelInvoke();
        Invoke(nameof(ForceHide), deadDuration);

        return true;
    }
}