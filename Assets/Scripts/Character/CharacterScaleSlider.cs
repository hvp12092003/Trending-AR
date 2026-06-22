using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;

/// <summary>
/// Quản lý việc thay đổi scale của nhân vật đang được chọn thông qua UI Slider.
/// </summary>
public class CharacterScaleSlider : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField]
    [Tooltip("Thanh Slider UI dùng để điều chỉnh scale. Nếu để trống, script sẽ tự động tìm kiếm trên GameObject này.")]
    private Slider m_ScaleSlider;

    [Header("Scale Range")]
    [SerializeField]
    [Tooltip("Tỷ lệ scale nhỏ nhất (mặc định là 0.2).")]
    private float m_MinScale = 0.2f;

    [SerializeField]
    [Tooltip("Tỷ lệ scale lớn nhất (mặc định là 10.0).")]
    private float m_MaxScale = 10f;

    // Biến cờ ngăn chặn vòng lặp cập nhật đệ quy giữa Slider và code thay đổi scale
    private bool m_IsUpdatingSliderValue = false;

    private void Awake()
    {
        // Tự động tìm component Slider nếu chưa gán
        if (m_ScaleSlider == null)
        {
            m_ScaleSlider = GetComponent<Slider>();
        }
    }

    private void Start()
    {
        if (m_ScaleSlider != null)
        {
            // Thiết lập giới hạn cho Slider theo đúng yêu cầu
            m_ScaleSlider.minValue = m_MinScale;
            m_ScaleSlider.maxValue = m_MaxScale;

            // Đăng ký sự kiện khi người dùng kéo Slider
            m_ScaleSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
        else
        {
            Debug.LogError("CharacterScaleSlider: Không tìm thấy component Slider! Vui lòng gán hoặc thêm Slider vào GameObject này.", this);
        }

        // Đăng ký nhận sự kiện khi chọn nhân vật mới từ CharacterManager
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterSelected += UpdateSliderFromCharacter;

            // Cập nhật giá trị ban đầu nếu game đã có sẵn nhân vật đang được chọn
            if (CharacterManager.Instance.SelectedCharacter != null)
            {
                UpdateSliderFromCharacter(CharacterManager.Instance.SelectedCharacter);
            }
            else
            {
                // Vô hiệu hóa slider tạm thời nếu chưa chọn nhân vật nào
                SetSliderInteractable(false);
            }
        }
        else
        {
            Debug.LogWarning("CharacterScaleSlider: CharacterManager.Instance chưa sẵn sàng trong Start.");
        }
    }

    private void OnDestroy()
    {
        // Huỷ đăng ký sự kiện tránh rò rỉ bộ nhớ
        if (m_ScaleSlider != null)
        {
            m_ScaleSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterSelected -= UpdateSliderFromCharacter;
        }
    }

    /// <summary>
    /// Gọi khi người dùng kéo thanh Slider.
    /// </summary>
    private void OnSliderValueChanged(float value)
    {
        // Nếu thay đổi này được gọi từ code (hàm UpdateSliderFromCharacter) thì không áp dụng ngược lại nhân vật
        if (m_IsUpdatingSliderValue) return;

        if (CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter != null)
        {
            GameObject selected = CharacterManager.Instance.SelectedCharacter;
            
            // Thay đổi tỷ lệ scale đều trên cả 3 trục (X, Y, Z) của nhân vật đang chọn bằng DOTween mượt mà
            selected.transform.DOKill();
            selected.transform.DOScale(Vector3.one * value, 0.15f).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// Đồng bộ giá trị Slider tương ứng khi chọn một nhân vật mới.
    /// </summary>
    private void UpdateSliderFromCharacter(GameObject character)
    {
        if (m_ScaleSlider == null) return;

        if (character != null)
        {
            // Bật tương tác slider khi chọn được nhân vật
            SetSliderInteractable(true);

            // Lấy scale hiện tại của nhân vật theo trục X làm chuẩn đại diện
            float currentScale = character.transform.localScale.x;

            // Bật cờ đánh dấu bắt đầu cập nhật slider từ code
            m_IsUpdatingSliderValue = true;
            
            // Gán giá trị vào slider, clamp trong giới hạn min/max
            m_ScaleSlider.value = Mathf.Clamp(currentScale, m_MinScale, m_MaxScale);
            
            // Tắt cờ đánh dấu
            m_IsUpdatingSliderValue = false;
        }
        else
        {
            // Vô hiệu hóa slider khi không có nhân vật nào được chọn
            SetSliderInteractable(false);
        }
    }

    /// <summary>
    /// Bật/Tắt tính năng tương tác của Slider UI.
    /// </summary>
    private void SetSliderInteractable(bool interactable)
    {
        if (m_ScaleSlider != null)
        {
            m_ScaleSlider.interactable = interactable;
        }
    }
}
