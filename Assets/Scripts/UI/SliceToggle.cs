using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Component điều khiển nút gạt bật/tắt dạng slider (thanh trượt).
/// Sử dụng DOTween để tạo hiệu ứng di chuyển handle và đổi màu nền mượt mà.
/// </summary>
public class SliceToggle : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Components")]
    [Tooltip("Ảnh nền của toggle (đổi màu tùy thuộc trạng thái)")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Núm tròn di chuyển (Handle)")]
    [SerializeField] private RectTransform handle;

    [Header("Visual Settings")]
    [Tooltip("Sprite nền khi trạng thái là ON")]
    [SerializeField] private Sprite onBgSprite;

    [Tooltip("Sprite nền khi trạng thái là OFF")]
    [SerializeField] private Sprite offBgSprite;

    [Tooltip("Vị trí X của handle khi ON (Local Anchored Position X)")]
    [SerializeField] private float onPosX = 25f;

    [Tooltip("Vị trí X của handle khi OFF (Local Anchored Position X)")]
    [SerializeField] private float offPosX = -25f;

    [Tooltip("Thời gian trượt của handle")]
    [SerializeField] private float transitionDuration = 0.2f;

    // Sự kiện xảy ra khi trạng thái thay đổi
    public event Action<bool> OnValueChanged;

    private bool _isOn = false;
    public bool IsOn => _isOn;

    /// <summary>
    /// Thiết lập trạng thái của toggle.
    /// </summary>
    /// <param name="state">Trạng thái mới</param>
    /// <param name="animate">Có chạy animation di chuyển handle hay không</param>
    public void SetState(bool state, bool animate = true)
    {
        _isOn = state;

        // Dừng các tween cũ trên handle
        if (handle != null) handle.DOKill();

        float targetX = _isOn ? onPosX : offPosX;
        Sprite targetSprite = _isOn ? onBgSprite : offBgSprite;

        // Gán sprite nền tương ứng trạng thái
        if (backgroundImage != null && targetSprite != null)
        {
            backgroundImage.sprite = targetSprite;
        }

        if (animate)
        {
            if (handle != null)
            {
                handle.DOAnchorPosX(targetX, transitionDuration).SetEase(Ease.OutQuad);
            }
        }
        else
        {
            // Thiết lập giá trị ngay lập tức mà không chạy animation
            if (handle != null)
            {
                Vector2 anchoredPos = handle.anchoredPosition;
                anchoredPos.x = targetX;
                handle.anchoredPosition = anchoredPos;
            }
        }
    }

    /// <summary>
    /// Xử lý sự kiện click chuột/chạm tay vào toggle.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleState();
    }

    private void ToggleState()
    {
        SetState(!_isOn, true);
        OnValueChanged?.Invoke(_isOn);
    }
}
