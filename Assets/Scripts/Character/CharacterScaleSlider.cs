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
    private bool m_IsSubscribed = false;
    private BandARSpawner m_BandSpawner;

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

        SubscribeToManager();
    }

    private void OnEnable()
    {
        SubscribeToManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }

    private void Update()
    {
        // Fallback phòng trường hợp CharacterManager.Instance chưa sẵn sàng trong lúc Awake/Start/OnEnable
        if (!m_IsSubscribed && CharacterManager.Instance != null)
        {
            SubscribeToManager();
        }
    }

    private void SubscribeToManager()
    {
        if (m_IsSubscribed) return;
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterSelected += UpdateSliderFromCharacter;
            m_IsSubscribed = true;
            UpdateSliderFromCharacter(CharacterManager.Instance.SelectedCharacter);
        }
    }

    private void UnsubscribeFromManager()
    {
        if (!m_IsSubscribed) return;
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterSelected -= UpdateSliderFromCharacter;
        }
        m_IsSubscribed = false;
    }

    private void OnDestroy()
    {
        // Huỷ đăng ký sự kiện tránh rò rỉ bộ nhớ
        if (m_ScaleSlider != null)
        {
            m_ScaleSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
        UnsubscribeFromManager();
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
            
            // Thay đổi tỷ lệ scale trực tiếp để phản hồi ngay lập tức
            selected.transform.localScale = Vector3.one * value;

            // Kiểm tra xem nhân vật có đang đứng trên bệ của BandARSpawner hay không
            BandARSpawner spawner = GetBandSpawner();
            if (spawner != null && spawner.Pedestals != null && selected.transform.parent != null)
            {
                if (spawner.Pedestals.Contains(selected.transform.parent.gameObject))
                {
                    // Công thức tỷ lệ thuận: vị trí cục bộ mới = vị trí mặc định * (scale mới / scale mặc định)
                    float defaultScaleX = spawner.PedestalLocalScale.x;
                    if (defaultScaleX > 0.0001f)
                    {
                        Vector3 targetPos = spawner.PedestalLocalPosition * (value / defaultScaleX);
                        selected.transform.localPosition = targetPos;
                    }
                }
            }

            // Reset bộ đếm thời gian tự động ẩn UI
            CharacterManager.Instance.ResetAutoHideTimer();
        }
    }

    private void UpdateSliderFromCharacter(GameObject character)
    {
        if (m_ScaleSlider == null) return;

        if (character != null)
        {
            // Hiện Slider và bật tương tác khi chọn được nhân vật
            m_ScaleSlider.gameObject.SetActive(true);
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
            // Ẩn Slider và vô hiệu hóa khi không có nhân vật nào được chọn
            m_ScaleSlider.gameObject.SetActive(false);
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

    private BandARSpawner GetBandSpawner()
    {
        if (m_BandSpawner == null)
        {
            m_BandSpawner = FindFirstObjectByType<BandARSpawner>();
        }
        return m_BandSpawner;
    }
}
