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
    private bool _currentHasCharacter = false;
    private bool _currentHasInstrument = false;
    private bool _currentHasAnimation = false;

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
    /// Cập nhật tiến độ thanh tiến trình.
    /// </summary>
    /// <param name="hasCharacter">Đã chọn nhân vật (Hoàn thành bước 1)</param>
    /// <param name="hasInstrument">Đã chọn nhạc cụ/ghi âm (Hoàn thành bước 2)</param>
    /// <param name="hasAnimation">Đã chọn hoạt ảnh (Hoàn thành bước 3)</param>
    /// <param name="animate">Có chạy hiệu ứng chuyển động hay không</param>
    public void SetProgress(bool hasCharacter, bool hasInstrument, bool hasAnimation, bool animate = true)
    {
        // Xác định các giá trị lượng đầy (fill amount) mục tiêu theo yêu cầu của user:
        // - Bước 1 (NAME): Không điền đầy line hay image nào (bằng 0).
        // - Bước 2 (SOUND): Line 1 đầy -> Image 2 đầy -> Màu text 2 đen.
        // - Bước 3 (ANIM): Line 2 đầy -> Image 3 đầy -> Màu text 3 đen.
        
        float targetLine1 = (hasCharacter && hasInstrument) ? 1f : 0f;
        float targetStep2 = (hasCharacter && hasInstrument) ? 1f : 0f;
        float targetLine2 = (hasCharacter && hasInstrument && hasAnimation) ? 1f : 0f;
        float targetStep3 = (hasCharacter && hasInstrument && hasAnimation) ? 1f : 0f;

        Color targetColor2 = (hasCharacter && hasInstrument) ? activeTextColor : defaultTextColor;
        Color targetColor3 = (hasCharacter && hasInstrument && hasAnimation) ? activeTextColor : defaultTextColor;

        // Dừng sequence đang chạy dở
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }

        if (animate)
        {
            _activeSequence = DOTween.Sequence();

            // Kiểm tra xem là tiến lên hay lùi lại để sắp xếp thứ tự chạy animation
            bool isForward = (hasInstrument && !_currentHasInstrument) || (hasAnimation && !_currentHasAnimation);

            if (isForward)
            {
                // TIẾN LÊN:
                // Bước 2 (SOUND): Line 1 đầy -> Image 2 đầy -> Màu text 2 đen
                if (hasInstrument != _currentHasInstrument && hasInstrument)
                {
                    if (filledLine1 != null) _activeSequence.Append(filledLine1.DOFillAmount(targetLine1, fillDuration).SetEase(fillEase));
                    if (filledImageStep2 != null) _activeSequence.Append(filledImageStep2.DOFillAmount(targetStep2, fillDuration).SetEase(fillEase));
                    if (step2Text != null) _activeSequence.Join(step2Text.DOColor(targetColor2, fillDuration));
                }

                // Bước 3 (ANIM): Line 2 đầy -> Image 3 đầy -> Màu text 3 đen
                if (hasAnimation != _currentHasAnimation && hasAnimation)
                {
                    if (filledLine2 != null) _activeSequence.Append(filledLine2.DOFillAmount(targetLine2, fillDuration).SetEase(fillEase));
                    if (filledImageStep3 != null) _activeSequence.Append(filledImageStep3.DOFillAmount(targetStep3, fillDuration).SetEase(fillEase));
                    if (step3Text != null) _activeSequence.Join(step3Text.DOColor(targetColor3, fillDuration));
                }
            }
            else
            {
                // LÙI LẠI (Chạy ngược):
                // Bước 3 rỗng trước: Màu text 3 về mặc định -> Image 3 rỗng -> Line 2 rỗng
                if (hasAnimation != _currentHasAnimation && !hasAnimation)
                {
                    if (step3Text != null) _activeSequence.Append(step3Text.DOColor(targetColor3, fillDuration));
                    if (filledImageStep3 != null) _activeSequence.Append(filledImageStep3.DOFillAmount(targetStep3, fillDuration).SetEase(fillEase));
                    if (filledLine2 != null) _activeSequence.Append(filledLine2.DOFillAmount(targetLine2, fillDuration).SetEase(fillEase));
                }

                // Bước 2 rỗng sau: Màu text 2 về mặc định -> Image 2 rỗng -> Line 1 rỗng
                if (hasInstrument != _currentHasInstrument && !hasInstrument)
                {
                    if (step2Text != null) _activeSequence.Append(step2Text.DOColor(targetColor2, fillDuration));
                    if (filledImageStep2 != null) _activeSequence.Append(filledImageStep2.DOFillAmount(targetStep2, fillDuration).SetEase(fillEase));
                    if (filledLine1 != null) _activeSequence.Append(filledLine1.DOFillAmount(targetLine1, fillDuration).SetEase(fillEase));
                }
            }

            // Hiệu ứng hoàn thành toàn bộ (punch scale step 3)
            if (hasCharacter && hasInstrument && hasAnimation && (hasAnimation != _currentHasAnimation))
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
        }

        // Lưu lại trạng thái
        _currentHasCharacter = hasCharacter;
        _currentHasInstrument = hasInstrument;
        _currentHasAnimation = hasAnimation;
    }

    private void OnDestroy()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }
    }
}
