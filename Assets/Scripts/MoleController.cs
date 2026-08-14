using UnityEngine;

public class MoleController : MonoBehaviour
{
    public Sprite normalSprite; 
    public Sprite deadSprite;   
    
    private SpriteRenderer sr;
    
    // 🔴 เปลี่ยนบรรทัดนี้ จาก BoxCollider2D เป็น CircleCollider2D ครับ
    private CircleCollider2D col; 

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        
        // 🔴 และเปลี่ยนบรรทัดนี้ด้วยครับ
        col = GetComponent<CircleCollider2D>(); 
    }

    public void ShowMole()
    {
        sr.sprite = normalSprite; 
        sr.enabled = true;        
        col.enabled = true;       
    }

    public void ForceHide()
    {
        sr.enabled = false;       
        col.enabled = false;      
    }

    public void Hit()
    {
        sr.sprite = deadSprite;   
        col.enabled = false;      
        Invoke("ForceHide", 0.5f); 
    }
}