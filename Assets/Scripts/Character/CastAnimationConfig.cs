using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct CastAnimationItem
{
    [Tooltip("Animation clip thực tế")]
    public AnimationClip animation;

    [Tooltip("Ảnh đại diện (avatar) của animation hiển thị trên UI")]
    public Sprite sprite;

    [Tooltip("Tên hiển thị của animation trên UI")]
    public string animName;
}

/// <summary>
/// Gắn component này vào các prefab Cast để định cấu hình tên và danh sách hoạt ảnh tương ứng.
/// </summary>
public class CastAnimationConfig : MonoBehaviour
{
    [Tooltip("Tên của nhân vật (Cast Name)")]
    public string Name;

    [Tooltip("Ảnh đại diện 2D của nhân vật")]
    public Sprite characterAvatar;

    [Tooltip("Danh sách các hoạt ảnh được phép chọn cho nhân vật này")]
    public List<CastAnimationItem> animations = new List<CastAnimationItem>();
}
