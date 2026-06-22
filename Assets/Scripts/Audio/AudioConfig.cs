using UnityEngine;

/// <summary>
/// Component cấu hình âm thanh cho prefab nhạc cụ.
/// Gắn component này vào các prefab nhạc cụ và thiết lập các trường: Name, Sprite avatar, AudioSource.
/// </summary>
public class AudioConfig : MonoBehaviour
{
    [Tooltip("Tên của nhạc cụ / âm thanh (ví dụ: Guitar, Drum, Piano,...)")]
    public string Name;

    [Tooltip("Ảnh đại diện (avatar) hiển thị trên UI")]
    public Sprite avatar;

    [Tooltip("Nguồn phát âm thanh (AudioSource) chứa clip nhạc của nhạc cụ này")]
    public AudioSource audioSource;

    /// <summary>
    /// Tiện ích lấy nhanh AudioClip từ AudioSource đi kèm.
    /// </summary>
    public AudioClip clip => audioSource != null ? audioSource.clip : null;
}
