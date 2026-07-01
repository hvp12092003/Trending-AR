using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gắn component này vào các prefab Cast để định cấu hình tên và danh sách hoạt ảnh tương ứng.
/// </summary>
public class CastPrefab : MonoBehaviour
{
    [Tooltip("Tên của nhân vật (Cast Name)")]
    public string Name;

    [Tooltip("Ngưỡng lọc khoảng lặng âm thanh (độ nhạy). Giá trị càng nhỏ càng nhạy (mặc định là 0.002).")]
    public float audioNoiseThreshold = 0.002f;

    [Tooltip("Thời gian chờ duy trì trạng thái nhảy (giây) sau khi tiếng nhạc tắt. Giúp tránh việc nhân vật bị giật giật với các nhạc cụ ngắt quãng như Bass/Drum. Mặc định là 0.5 giây.")]
    public float danceKeepAliveTime = 0.5f;


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
