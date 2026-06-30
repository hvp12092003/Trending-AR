using UnityEngine;
using System;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Quản lý nhân vật đang được chọn và điều khiển trong Scene.
/// </summary>
public class CharacterManager : MonoBehaviour
{
    // Singleton Instance để dễ dàng truy cập từ các script khác
    public static CharacterManager Instance { get; private set; }

    [Header("Selection Settings")]
    [SerializeField]
    [Tooltip("Nhân vật hiện tại đang được chọn để điều khiển.")]
    private GameObject m_SelectedCharacter;

    /// <summary>
    /// Nhân vật hiện tại đang được chọn để điều khiển.
    /// </summary>
    public GameObject SelectedCharacter => m_SelectedCharacter;

    /// <summary>
    /// Sự kiện kích hoạt khi nhân vật được chọn thay đổi.
    /// </summary>
    public event System.Action<GameObject> OnCharacterSelected;

    [Header("Visual Indicators (Optional)")]
    [SerializeField]
    [Tooltip("Prefab hiệu ứng vòng tròn lựa chọn dưới chân nhân vật (nếu có).")]
    private GameObject m_SelectionIndicatorPrefab;

    [Header("Selection Indicator Offset Settings")]
    [SerializeField] private Vector3 m_IndicatorLocalPosition = new Vector3(0f, -0.2f, 0f);
    [SerializeField] private Vector3 m_IndicatorLocalRotation = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 m_IndicatorLocalScale = new Vector3(0.6f, 0.6f, 0.6f);

    private GameObject m_ActiveIndicator;
    private Coroutine m_AutoHideCoroutine;

    [Header("Auto-Hide Settings")]
    [SerializeField]
    [Tooltip("Thời gian (giây) trước khi indicator tự ẩn sau khi nhân vật được chọn. 0 = không tự ẩn.")]
    private float m_AutoHideDelay = 3f;

    [Header("Control UI References")]
    [SerializeField] private GameObject m_JoystickObject;
    [SerializeField] private GameObject m_ScaleSliderObject;

    [Header("Persistence Settings")]
    [SerializeField]
    [Tooltip("Nếu true, đối tượng này sẽ không bị hủy khi chuyển scene.")]
    private bool m_DontDestroyOnLoad = false;
    private BandPanelController m_BandPanel;
    private CustomCharacterPanelController m_CustomPanel;

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null)
        {
            Instance = this;
            if (m_DontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (m_JoystickObject == null)
        {
            Joystick joystick = FindFirstObjectByType<Joystick>(FindObjectsInactive.Include);
            if (joystick != null)
            {
                m_JoystickObject = joystick.gameObject;
            }
        }

        if (m_ScaleSliderObject == null)
        {
            CharacterScaleSlider scaleSlider = FindFirstObjectByType<CharacterScaleSlider>(FindObjectsInactive.Include);
            if (scaleSlider != null)
            {
                m_ScaleSliderObject = scaleSlider.gameObject;
            }
        }

        // Mặc định ẩn UI điều khiển lúc khởi đầu khi chưa chọn nhân vật
        UpdateControlUI(false, true);
    }

    /// <summary>
    /// Bật/Tắt hiển thị của Joystick và Scale Slider.
    /// </summary>
    private void UpdateControlUI(bool show, bool immediate = false)
    {
        // Luôn tìm kiếm lại nếu tham chiếu bị thiếu/null (ví dụ khi chuyển scene)
        if (m_JoystickObject == null)
        {
            Joystick joystick = FindFirstObjectByType<Joystick>(FindObjectsInactive.Include);
            if (joystick != null)
            {
                m_JoystickObject = joystick.gameObject;
            }
        }

        if (m_ScaleSliderObject == null)
        {
            CharacterScaleSlider scaleSlider = FindFirstObjectByType<CharacterScaleSlider>(FindObjectsInactive.Include);
            if (scaleSlider != null)
            {
                m_ScaleSliderObject = scaleSlider.gameObject;
            }
        }

        if (m_JoystickObject != null)
        {
            AnimateUIElement(m_JoystickObject, show, immediate);
        }
        if (m_ScaleSliderObject != null)
        {
            AnimateUIElement(m_ScaleSliderObject, show, immediate);
        }
    }

    /// <summary>
    /// Thực hiện hoạt ảnh ẩn hiện mượt mà (Fade + Scale) bằng DOTween.
    /// </summary>
    private void AnimateUIElement(GameObject obj, bool show, bool immediate)
    {
        if (obj == null) return;

        obj.transform.DOKill();
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = obj.AddComponent<CanvasGroup>();
        }
        cg.DOKill();

        if (immediate)
        {
            cg.alpha = show ? 1f : 0f;
            obj.transform.localScale = show ? Vector3.one : Vector3.one * 0.5f;
            obj.SetActive(show);
            return;
        }

        if (show)
        {
            obj.SetActive(true);
            
            // Nếu UI đang hoàn toàn ẩn hoặc bị thu nhỏ, reset trạng thái để chạy hoạt ảnh
            if (obj.transform.localScale.sqrMagnitude < 0.001f || cg.alpha < 0.01f)
            {
                obj.transform.localScale = Vector3.one * 0.5f;
                cg.alpha = 0f;
            }

            obj.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            cg.DOFade(1f, 0.2f).SetUpdate(true);
        }
        else
        {
            if (obj.activeSelf)
            {
                cg.DOFade(0f, 0.2f).SetUpdate(true);
                obj.transform.DOScale(Vector3.one * 0.5f, 0.2f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
                {
                    obj.SetActive(false);
                });
            }
        }
    }

    /// <summary>
    /// Khởi động lại bộ đếm thời gian tự động ẩn UI điều khiển và vòng tròn chỉ báo.
    /// Gọi mỗi khi có tương tác của người dùng.
    /// </summary>
    public void ResetAutoHideTimer()
    {
        if (m_SelectedCharacter == null) return;

        if (m_AutoHideCoroutine != null)
        {
            StopCoroutine(m_AutoHideCoroutine);
        }

        // Khôi phục hiển thị nếu đã bị ẩn
        bool isUiHidden = IsGameplayUiHidden();

        if (!isUiHidden)
        {
            SetIndicatorActive(true);
            UpdateControlUI(true);
        }

        if (m_AutoHideDelay > 0f)
        {
            m_AutoHideCoroutine = StartCoroutine(AutoHideIndicatorAfterDelay(m_AutoHideDelay));
        }
    }

    /// <summary>
    /// Chọn một nhân vật để bắt đầu điều khiển.
    /// </summary>
    /// <param name="character">GameObject của nhân vật.</param>
    public void SelectCharacter(GameObject character)
    {
        if (character == null) return;

        // Kiểm tra xem UI có đang ẩn hay không
        bool isUiHidden = IsGameplayUiHidden();

        if (isUiHidden)
        {
            // Nếu chạm lại vào nhân vật đang được chọn/highlight thì hủy chọn (toggle off)
            if (m_SelectedCharacter == character)
            {
                DeselectCharacter();
                return;
            }
        }

        m_SelectedCharacter = character;
        Debug.Log($"CharacterManager: Đã chọn nhân vật: {character.name}");

        // Tạo hiệu ứng vòng tròn chọn dưới chân nhân vật (nếu được cấu hình)
        UpdateSelectionIndicator();

        // Hiển thị UI điều khiển di chuyển và scale
        if (!isUiHidden)
        {
            UpdateControlUI(true);
        }

        // Kích hoạt sự kiện để thông báo cho các component khác (như UI Slider)
        OnCharacterSelected?.Invoke(m_SelectedCharacter);
    }

    /// <summary>
    /// Huỷ chọn nhân vật hiện tại.
    /// </summary>
    public void DeselectCharacter()
    {
        m_SelectedCharacter = null;
        if (m_ActiveIndicator != null)
        {
            m_ActiveIndicator.transform.DOKill();
            m_ActiveIndicator.SetActive(false);
            m_ActiveIndicator.transform.SetParent(transform, false);
        }

        // Ẩn UI điều khiển di chuyển và scale
        UpdateControlUI(false);

        if (m_AutoHideCoroutine != null)
        {
            StopCoroutine(m_AutoHideCoroutine);
            m_AutoHideCoroutine = null;
        }

        // Kích hoạt sự kiện với tham số null
        OnCharacterSelected?.Invoke(null);
    }



    /// <summary>
    /// Cho nhân vật đang chọn thực hiện Pose 1 (tạo dáng).
    /// </summary>
    public void PlayPose1OnSelected()
    {
        if (m_SelectedCharacter == null) return;

        Move moveScript = m_SelectedCharacter.GetComponent<Move>();
        if (moveScript != null)
        {
            moveScript.PlayPose1();
        }
    }

    /// <summary>
    /// Cho nhân vật đang chọn chạy một animation bất kỳ bằng tên State.
    /// </summary>
    /// <param name="stateName">Tên State trong Animator.</param>
    /// <param name="crossFadeTime">Thời gian chuyển cảnh.</param>
    public void PlayCustomAnimationOnSelected(string stateName, float crossFadeTime = 0.15f)
    {
        if (m_SelectedCharacter == null) return;

        Move moveScript = m_SelectedCharacter.GetComponent<Move>();
        if (moveScript != null)
        {
            moveScript.PlayAnimation(stateName, crossFadeTime);
        }
        else
        {
            // Fallback: Phát hoạt ảnh trực tiếp trên Animator nếu thiếu Move
            Animator animator = m_SelectedCharacter.GetComponent<Animator>();
            if (animator == null)
            {
                animator = m_SelectedCharacter.GetComponentInChildren<Animator>();
            }

            if (animator != null && !string.IsNullOrEmpty(stateName))
            {
                animator.applyRootMotion = false;
                string sanitizedStateName = stateName.Replace(".", "_");
                animator.CrossFade(sanitizedStateName, crossFadeTime);
            }
        }
    }

    /// <summary>
    /// Hàm gọi từ UI Button để chạy animation pose bất kỳ bằng tên State (chỉ nhận 1 tham số string để hiển thị trong Unity UI Button onClick).
    /// </summary>
    /// <param name="stateName">Tên State trong Animator.</param>
    public void PlayPoseByName(string stateName)
    {
        PlayCustomAnimationOnSelected(stateName);
    }

    /// <summary>
    /// Cập nhật vị trí của vòng tròn chỉ báo lựa chọn dưới chân nhân vật.
    /// </summary>
    private void UpdateSelectionIndicator()
    {
        if (m_SelectionIndicatorPrefab == null || m_SelectedCharacter == null) return;

        // Không hiển thị vòng chỉ báo chọn dưới chân nhân vật khi UI đang ẩn để quay video sạch
        bool isUiHidden = IsGameplayUiHidden();

        if (isUiHidden)
        {
            if (m_ActiveIndicator != null)
            {
                m_ActiveIndicator.SetActive(false);
            }
            return;
        }

        if (m_ActiveIndicator != null)
        {
            m_ActiveIndicator.transform.DOKill();
        }
        else
        {
            m_ActiveIndicator = Instantiate(m_SelectionIndicatorPrefab);
        }

        bool alreadyAttached = m_ActiveIndicator.transform.parent == m_SelectedCharacter.transform &&
                               m_ActiveIndicator.activeSelf;

        // Reuse one indicator instance and re-parent it to the selected character.
        m_ActiveIndicator.transform.SetParent(m_SelectedCharacter.transform, false);
        m_ActiveIndicator.transform.localPosition = m_IndicatorLocalPosition;
        m_ActiveIndicator.transform.localRotation = Quaternion.Euler(m_IndicatorLocalRotation);

        if (alreadyAttached)
        {
            m_ActiveIndicator.transform.localScale = m_IndicatorLocalScale;
            return;
        }

        // Hoạt ảnh xuất hiện vòng tròn chọn dùng DOTween
        Vector3 targetScale = m_IndicatorLocalScale;
        m_ActiveIndicator.SetActive(true);
        m_ActiveIndicator.transform.localScale = Vector3.zero;
        m_ActiveIndicator.transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack);

        // Tự động ẩn indicator sau m_AutoHideDelay giây
        if (m_AutoHideDelay > 0f)
        {
            if (m_AutoHideCoroutine != null)
            {
                StopCoroutine(m_AutoHideCoroutine);
            }
            m_AutoHideCoroutine = StartCoroutine(AutoHideIndicatorAfterDelay(m_AutoHideDelay));
        }
    }

    /// <summary>
    /// Coroutine tự động ẩn indicator sau một khoảng thời gian bằng cách huỷ chọn nhân vật.
    /// </summary>
    private IEnumerator AutoHideIndicatorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DeselectCharacter();
        m_AutoHideCoroutine = null;
    }

    /// <summary>
    /// Bật/Tắt hiển thị vòng chỉ báo chọn với hiệu ứng scale mượt mà.
    /// </summary>
    public void SetIndicatorActive(bool isActive)
    {
        if (m_ActiveIndicator != null)
        {
            if (isActive)
            {
                if (!m_ActiveIndicator.activeSelf)
                {
                    m_ActiveIndicator.SetActive(true);
                    Vector3 targetScale = m_IndicatorLocalScale;
                    m_ActiveIndicator.transform.localScale = Vector3.zero;
                    m_ActiveIndicator.transform.DOKill();
                    m_ActiveIndicator.transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack);
                }
            }
            else
            {
                if (m_ActiveIndicator.activeSelf)
                {
                    m_ActiveIndicator.transform.DOKill();
                    m_ActiveIndicator.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        m_ActiveIndicator.SetActive(false);
                    });
                }
            }
        }
    }

    private bool IsGameplayUiHidden()
    {
        if (m_BandPanel == null)
        {
            m_BandPanel = FindFirstObjectByType<BandPanelController>();
        }
        if (m_BandPanel != null && m_BandPanel.IsUiHidden)
        {
            return true;
        }

        if (m_CustomPanel == null)
        {
            m_CustomPanel = FindFirstObjectByType<CustomCharacterPanelController>();
        }
        return m_CustomPanel != null && m_CustomPanel.IsUiHidden;
    }

    private void OnDestroy()
    {
        if (m_ActiveIndicator != null)
        {
            m_ActiveIndicator.transform.DOKill();
            Destroy(m_ActiveIndicator);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
