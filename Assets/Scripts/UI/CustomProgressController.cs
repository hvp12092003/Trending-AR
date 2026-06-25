using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý hiệu ứng thanh tiến trình (Process) của Custom Character Creator Panel.
/// Chạy tuần tự: Đầy Line -> Đầy Vòng tròn -> Đổi màu số sang đen.
/// Hỗ trợ chạy ngược lại khi hủy lựa chọn / quay lui.
/// </summary>
public class CustomProgressController : MonoBehaviour
{
    [Header("Lines")]
    [SerializeField] private Image filledLine1;
    [SerializeField] private Image filledLine2;

    [Header("Step Images (Circles)")]
    [SerializeField] private Image filledImageStep2;
    [SerializeField] private Image filledImageStep3;

    [Header("Step Texts (Numbers)")]
    [SerializeField] private TextMeshProUGUI step1Text;
    [SerializeField] private TextMeshProUGUI step2Text;
    [SerializeField] private TextMeshProUGUI step3Text;

    [Header("Step GameObjects (Optional for animations)")]
    [SerializeField] private GameObject step3Parent;

    [Header("Color Config")]
    [SerializeField] private Color defaultTextColor = Color.white;
    [SerializeField] private Color activeTextColor = Color.black;

    [Header("Tween Config")]
    [SerializeField] private float fillDuration = 0.3f;
    [SerializeField] private Ease fillEase = Ease.OutQuad;

    // Trạng thái hiện tại để so sánh khi cập nhật
    private int _currentStep = 1;

    private Sequence _activeSequence;

    private void Awake()
    {
        // Khởi tạo trạng thái ban đầu của Bước 1 (luôn kích hoạt)
        if (step1Text != null)
        {
            step1Text.color = activeTextColor;
        }

        // Đảm bảo ban đầu Bước 2 và Bước 3 ở màu mặc định nếu chưa gán
        if (step2Text != null) step2Text.color = defaultTextColor;
        if (step3Text != null) step3Text.color = defaultTextColor;
    }

    /// <summary>
    /// Cập nhật tiến độ thanh tiến trình theo bước hiện tại.
    /// </summary>
    /// <param name="step">Bước hiện tại (1, 2, 3)</param>
    /// <param name="animate">Có chạy hiệu ứng chuyển động hay không</param>
    public Sequence SetProgress(int step, bool animate = true)
    {
        // Xác định các giá trị lượng đầy (fill amount) mục tiêu theo bước hiện tại:
        // - Bước 1: Line 1 = 0, Image 2 = 0, Line 2 = 0, Image 3 = 0, Text 2 & 3 rỗng (màu mặc định).
        // - Bước 2: Line 1 = 1, Image 2 = 1, Line 2 = 0, Image 3 = 0, Text 2 màu active, Text 3 màu mặc định.
        // - Bước 3: Line 1 = 1, Image 2 = 1, Line 2 = 1, Image 3 = 1, Text 2 & 3 màu active.
        
        float targetLine1 = (step >= 2) ? 1f : 0f;
        float targetStep2 = (step >= 2) ? 1f : 0f;
        float targetLine2 = (step >= 3) ? 1f : 0f;
        float targetStep3 = (step >= 3) ? 1f : 0f;

        Color targetColor2 = (step >= 2) ? activeTextColor : defaultTextColor;
        Color targetColor3 = (step >= 3) ? activeTextColor : defaultTextColor;

        // Dừng sequence đang chạy dở
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }

        if (animate)
        {
            _activeSequence = DOTween.Sequence();

            // Kiểm tra xem là tiến lên hay lùi lại để sắp xếp thứ tự chạy animation
            bool isForward = step > _currentStep;

            if (isForward)
            {
                // TIẾN LÊN:
                // Nếu tiến lên bước 2 hoặc hơn và trước đó đang ở bước nhỏ hơn 2
                if (step >= 2 && _currentStep < 2)
                {
                    if (filledLine1 != null) _activeSequence.Append(filledLine1.DOFillAmount(targetLine1, fillDuration).SetEase(fillEase));
                    if (filledImageStep2 != null) _activeSequence.Append(filledImageStep2.DOFillAmount(targetStep2, fillDuration).SetEase(fillEase));
                    if (step2Text != null) _activeSequence.Join(step2Text.DOColor(targetColor2, fillDuration));
                }

                // Nếu tiến lên bước 3 và trước đó đang ở bước nhỏ hơn 3
                if (step >= 3 && _currentStep < 3)
                {
                    if (filledLine2 != null) _activeSequence.Append(filledLine2.DOFillAmount(targetLine2, fillDuration).SetEase(fillEase));
                    if (filledImageStep3 != null) _activeSequence.Append(filledImageStep3.DOFillAmount(targetStep3, fillDuration).SetEase(fillEase));
                    if (step3Text != null) _activeSequence.Join(step3Text.DOColor(targetColor3, fillDuration));
                }
            }
            else
            {
                // LÙI LẠI (Chạy ngược):
                // Nếu lùi về bước nhỏ hơn 3 và trước đó đang ở bước 3
                if (step < 3 && _currentStep >= 3)
                {
                    if (step3Text != null) _activeSequence.Append(step3Text.DOColor(targetColor3, fillDuration));
                    if (filledImageStep3 != null) _activeSequence.Append(filledImageStep3.DOFillAmount(targetStep3, fillDuration).SetEase(fillEase));
                    if (filledLine2 != null) _activeSequence.Append(filledLine2.DOFillAmount(targetLine2, fillDuration).SetEase(fillEase));
                }

                // Nếu lùi về bước nhỏ hơn 2 và trước đó đang ở bước 2 hoặc hơn
                if (step < 2 && _currentStep >= 2)
                {
                    if (step2Text != null) _activeSequence.Append(step2Text.DOColor(targetColor2, fillDuration));
                    if (filledImageStep2 != null) _activeSequence.Append(filledImageStep2.DOFillAmount(targetStep2, fillDuration).SetEase(fillEase));
                    if (filledLine1 != null) _activeSequence.Append(filledLine1.DOFillAmount(targetLine1, fillDuration).SetEase(fillEase));
                }
            }

            // Hiệu ứng hoàn thành toàn bộ (punch scale step 3) khi tiến lên bước 3
            if (step >= 3 && _currentStep < 3)
            {
                _activeSequence.OnComplete(() =>
                {
                    if (step3Parent != null)
                    {
                        step3Parent.transform.DOKill();
                        step3Parent.transform.localScale = Vector3.one;
                        step3Parent.transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 10, 1f);
                    }
                });
            }

            _activeSequence.Play();
            _currentStep = step;
            return _activeSequence;
        }
        else
        {
            // Thiết lập giá trị tĩnh ngay lập tức (không chạy hiệu ứng)
            if (filledLine1 != null) filledLine1.fillAmount = targetLine1;
            if (filledImageStep2 != null) filledImageStep2.fillAmount = targetStep2;
            if (step2Text != null) step2Text.color = targetColor2;

            if (filledLine2 != null) filledLine2.fillAmount = targetLine2;
            if (filledImageStep3 != null) filledImageStep3.fillAmount = targetStep3;
            if (step3Text != null) step3Text.color = targetColor3;

            if (step3Parent != null)
            {
                step3Parent.transform.DOKill();
                step3Parent.transform.localScale = Vector3.one;
            }

            _currentStep = step;
            return null;
        }
    }

    /// <summary>
    /// Cập nhật tiến độ thanh tiến trình dựa trên trạng thái chọn của các bước (Tương thích ngược).
    /// </summary>
    public Sequence SetProgress(bool hasCharacter, bool hasInstrument, bool hasAnimation, bool animate = true)
    {
        int step = 1;
        if (hasCharacter && hasInstrument && hasAnimation)
        {
            step = 3;
        }
        else if (hasCharacter && hasInstrument)
        {
            step = 2;
        }
        return SetProgress(step, animate);
    }

    private void OnDestroy()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }
    }
}
