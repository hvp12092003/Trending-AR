using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gắn component này vào các prefab Cast để định cấu hình tên và danh sách hoạt ảnh tương ứng.
/// </summary>
public class CastPrefab : MonoBehaviour
{
    [Tooltip("Tên của nhân vật (Cast Name)")]
    public string Name;

    [Tooltip("Phân loại nhân vật, hiển thị dòng nhỏ bên dưới tên (ví dụ: Reaction, Comedy, Beat)")]
    public string Category;

    [Tooltip("Ảnh đại diện 2D của nhân vật")]
    public Sprite characterAvatar;

    [Tooltip("Danh sách các hoạt ảnh được phép chọn cho nhân vật này")]
    public List<CastAnimation> animations = new List<CastAnimation>();

    private void OnValidate()
    {
        if (animations == null) return;

        for (int i = 0; i < animations.Count; i++)
        {
            CastAnimation anim = animations[i];
            if (anim.animation != null && string.IsNullOrEmpty(anim.animName))
            {
                anim.animName = anim.animation.name;
                animations[i] = anim;
            }
        }
    }
}
