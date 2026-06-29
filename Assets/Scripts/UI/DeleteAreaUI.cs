using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Giao diện vùng xóa Cast tự thiết lập thủ công trong Scene.
/// Tự động lấy component Image trên đối tượng được gắn để đổi màu và co giãn khi hover kéo đè.
/// Hỗ trợ hoạt ảnh ẩn/hiện và chuyển màu mượt mà bằng DOTween.
/// </summary>
public class DeleteAreaUI : MonoBehaviour
{
    public static DeleteAreaUI Instance { get; set; }

    [Header("Color Settings")]
    [SerializeField] [Tooltip("Màu sắc của Image khi kéo Cast đè lên vùng xóa")]
    private Color m_HoverColor = Color.red;

    private RectTransform m_RectTransform;
    private Image m_Image;
    private Color m_OriginalColor;
    private bool m_IsHovered = false;

    private void Awake()
    {
        Instance = this;
        m_RectTransform = GetComponent<RectTransform>();
        m_Image = GetComponent<Image>();
        
        if (m_Image != null)
        {
            m_OriginalColor = m_Image.color;
        }
    }

    private void Start()
    {
        // Luôn thu nhỏ về 0 và ẩn đi khi bắt đầu game
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Hiển thị UI xóa bằng hoạt ảnh scale lớn dần mượt mà.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        m_IsHovered = false;
        
        if (m_Image != null)
        {
            m_Image.DOKill();
            m_Image.color = m_OriginalColor;
        }

        transform.DOKill();
        transform.localScale = Vector3.zero;
        // Hiệu ứng phóng to nảy nhẹ OutBack
        transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Ẩn UI xóa bằng hoạt ảnh scale nhỏ lại về 0 mượt mà.
    /// </summary>
    public void Hide()
    {
        transform.DOKill();
        transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// Kiểm tra xem vị trí trỏ chuột / chạm có nằm trong khu vực Rect của UI xóa không.
    /// </summary>
    public bool IsPointerInsideDeleteArea(Vector2 screenPosition)
    {
        if (!gameObject.activeInHierarchy) return false;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera checkCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(m_RectTransform, screenPosition, checkCam);
    }

    /// <summary>
    /// Cập nhật hiệu ứng đổi màu và phóng to mượt mà khi kéo đè (hover) qua vùng xóa.
    /// </summary>
    public void UpdateHoverState(Vector2 screenPosition)
    {
        bool inside = IsPointerInsideDeleteArea(screenPosition);
        if (inside && !m_IsHovered)
        {
            m_IsHovered = true;
            
            if (m_Image != null)
            {
                m_Image.DOKill();
                m_Image.DOColor(m_HoverColor, 0.15f);
            }
            transform.DOKill();
            transform.DOScale(new Vector3(1.15f, 1.15f, 1.15f), 0.15f).SetEase(Ease.OutQuad);

            // Phản hồi xúc giác (rung điện thoại nhẹ khi hover vào vùng xóa)
            AndroidUtils.Vibrate(40);
        }
        else if (!inside && m_IsHovered)
        {
            m_IsHovered = false;
            
            if (m_Image != null)
            {
                m_Image.DOKill();
                m_Image.DOColor(m_OriginalColor, 0.15f);
            }
            transform.DOKill();
            transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
        }
    }
}
