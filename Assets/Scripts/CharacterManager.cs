using UnityEngine;
using System;

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
        m_ActiveIndicator.transform.localPosition = new Vector3(0, 0.01f, 0); // Đặt hơi cao hơn chân một chút tránh z-fighting
        m_ActiveIndicator.transform.localRotation = m_SelectionIndicatorPrefab.transform.localRotation;
    }

    /// <summary>
    /// Bật/Tắt hiển thị vòng chỉ báo chọn.
    /// </summary>
    public void SetIndicatorActive(bool isActive)
    {
        if (m_ActiveIndicator != null && m_ActiveIndicator.activeSelf != isActive)
        {
            m_ActiveIndicator.SetActive(isActive);
        }
    }
}
