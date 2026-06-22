using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Quản lý giao diện của một ô nút nhân vật (Cast Button) trong ScrollView của Studio.
/// Tự động cập nhật ảnh (avatar), tên (name), ID nhạc cụ, và các ID animation nhảy đi kèm.
/// </summary>
public class CastButtonUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Ảnh đại diện của nhân vật")]
    [SerializeField] private Image avatarImage;

    [Tooltip("Text hiển thị tên nhân vật")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Interactivity")]
    [Tooltip("Nút bấm chính để chọn cast này")]
    [SerializeField] private Button button;

    [Tooltip("Nút bấm để xóa cast này khỏi thư viện")]
    [SerializeField] private Button deleteButton;

    public Button DeleteButton => deleteButton;

    // ─────────────────────────────────────────────────────────────────────────
    // Dữ liệu bổ sung đi kèm nhân vật khi khởi tạo
    // ─────────────────────────────────────────────────────────────────────────
    
    /// <summary>
    /// Dữ liệu thông tin của Cast (nhân vật).
    /// </summary>
    public CastData Cast { get; private set; }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    /// <summary>
    /// Khởi tạo và cập nhật giao diện cho Cast Button.
    /// </summary>
    /// <param name="castData">Dữ liệu của Cast.</param>
    /// <param name="avatar">Sprite ảnh đại diện.</param>
    public void Setup(CastData castData, Sprite avatar)
    {
        Cast = castData;

        // Cập nhật lên UI
        if (nameText != null && Cast != null)
        {
            nameText.text = Cast.name;
        }

        if (avatarImage != null)
        {
            if (avatar != null)
            {
                avatarImage.sprite = avatar;
                avatarImage.enabled = true;
            }
            else
            {
                // Nếu không có ảnh, ẩn hình ảnh đi hoặc hiển thị ảnh mặc định
                avatarImage.enabled = false;
            }
        }
    }

    /// <summary>
    /// Cập nhật riêng lẻ ID nhạc cụ / audio sau khi khởi tạo.
    /// </summary>
    public void SetAudio(string audioId)
    {
        if (Cast != null)
        {
            Cast.audioId = audioId;
        }
    }

    /// <summary>
    /// Cập nhật riêng lẻ ID animation nhảy đang chọn sau khi khởi tạo.
    /// </summary>
    public void SetDanceAnimation(string danceAnimId)
    {
        if (Cast != null)
        {
            Cast.danceAnimId = danceAnimId;
            if (!Cast.danceAnimIds.Contains(danceAnimId))
            {
                Cast.danceAnimIds.Add(danceAnimId);
            }
        }
    }

    private void OnButtonClicked()
    {
        if (Cast != null)
        {
            Debug.Log($"[CastButtonUI] Đã chọn cast: {Cast.name} | Audio: {Cast.audioId} | Anim nhảy hiện tại: {Cast.danceAnimId}");
        }
        
        // Bạn có thể kích hoạt event hoặc gọi sang CharacterManager khi bấm vào nút này ở đây.
    }
}
