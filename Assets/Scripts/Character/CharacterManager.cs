using UnityEngine;
using System;
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
    [SerializeField] private Vector3 m_IndicatorLocalPosition = new Vector3(0f, -0.6f, 0f);
    [SerializeField] private Vector3 m_IndicatorLocalRotation = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 m_IndicatorLocalScale = new Vector3(0.6f, 0.6f, 0.6f);

    private GameObject m_ActiveIndicator;

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Chọn một nhân vật để bắt đầu điều khiển.
    /// </summary>
    /// <param name="character">GameObject của nhân vật.</param>
    public void SelectCharacter(GameObject character)
    {
        if (character == null) return;

        m_SelectedCharacter = character;
        Debug.Log($"CharacterManager: Đã chọn nhân vật: {character.name}");

        // Tạo hiệu ứng vòng tròn chọn dưới chân nhân vật (nếu được cấu hình)
        UpdateSelectionIndicator();

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
            Destroy(m_ActiveIndicator);
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

        if (m_ActiveIndicator != null)
        {
            Destroy(m_ActiveIndicator);
        }

        // Tạo vòng chỉ báo và gán làm con của nhân vật để nó di chuyển theo
        m_ActiveIndicator = Instantiate(m_SelectionIndicatorPrefab, m_SelectedCharacter.transform);
        m_ActiveIndicator.transform.localPosition = m_IndicatorLocalPosition; // Đặt đúng dưới chân nhân vật
        m_ActiveIndicator.transform.localRotation = Quaternion.Euler(m_IndicatorLocalRotation); // Xoay nằm ngang
        
        // Hoạt ảnh xuất hiện vòng tròn chọn dùng DOTween
        Vector3 targetScale = m_IndicatorLocalScale;
        m_ActiveIndicator.transform.localScale = Vector3.zero;
        m_ActiveIndicator.transform.DOKill();
        m_ActiveIndicator.transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack);
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
}
