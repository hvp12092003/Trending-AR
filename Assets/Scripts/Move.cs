using UnityEngine;

/// <summary>
/// Thêm component này vào Character Prefab để điều khiển di chuyển mượt mà tới vị trí mục tiêu.
/// </summary>
public class Move : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]
    [Tooltip("Tốc độ di chuyển của nhân vật.")]
    private float m_MoveSpeed = 2f;

    [SerializeField]
    [Tooltip("Tốc độ xoay của nhân vật hướng về phía di chuyển.")]
    private float m_RotationSpeed = 10f;



    [Header("Animation State Names (Direct Play)")]
    [SerializeField]
    [Tooltip("Tên State trong Animator cho trạng thái đứng yên.")]
    private string m_IdleStateName = "root_Girl_Idle";

    [SerializeField]
    [Tooltip("Tên State trong Animator cho trạng thái đi bộ.")]
    private string m_WalkStateName = "root_Girl_walk";

    [SerializeField]
    [Tooltip("Tên State trong Animator cho trạng thái tạo dáng (Pose 1).")]
    private string m_Pose1StateName = "root_Girl_pose1";

    [Header("Animation Settings (Optional Parameters)")]
    [SerializeField]
    [Tooltip("Tên tham số Float trong Animator để điều khiển tốc độ chạy/đi bộ (nếu sử dụng parameter).")]
    private string m_AnimatorSpeedParam = "Speed";

    [SerializeField]
    [Tooltip("Tên tham số Bool trong Animator để bật/tắt trạng thái di chuyển (nếu sử dụng parameter).")]
    private string m_AnimatorMovingParam = "IsMoving";

    public Animator m_Animator;
    private string m_CurrentStateName;
    private Joystick m_Joystick;

    // Cache variables for Android Optimization
    private int m_SpeedParamHash;
    private int m_MovingParamHash;
    private bool m_HasSpeedParam;
    private bool m_HasMovingParam;

    private void Start()
    {
        // Lấy Animator để điều khiển animation chạy/đi bộ nếu có
        m_Animator = GetComponentInChildren<Animator>();

        // Tìm kiếm Joystick từ Joystick Pack trong Scene
        m_Joystick = FindFirstObjectByType<Joystick>();

        // Tự động kiểm tra xem tham số có tồn tại trong Animator không để tránh spam cảnh báo
        m_HasSpeedParam = false;
        m_HasMovingParam = false;

        if (m_Animator != null && m_Animator.runtimeAnimatorController != null)
        {
            foreach (var param in m_Animator.parameters)
            {
                if (!string.IsNullOrEmpty(m_AnimatorSpeedParam) && param.name == m_AnimatorSpeedParam)
                {
                    m_HasSpeedParam = true;
                    m_SpeedParamHash = Animator.StringToHash(m_AnimatorSpeedParam);
                }
                if (!string.IsNullOrEmpty(m_AnimatorMovingParam) && param.name == m_AnimatorMovingParam)
                {
                    m_HasMovingParam = true;
                    m_MovingParamHash = Animator.StringToHash(m_AnimatorMovingParam);
                }
            }
        }

        // Bắt đầu với Animation Idle mặc định
        PlayIdle();
    }

    private void Update()
    {
        // Chỉ nhận đầu vào và di chuyển nếu nhân vật này đang được chọn để điều khiển
        if (CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter != gameObject)
        {
            PlayIdle();
            UpdateAnimatorParams(0f);
            return;
        }

        // 1. Đọc dữ liệu hướng đi từ Joystick ảo (nếu có)
        Vector2 joystickInput = Vector2.zero;
        if (m_Joystick != null)
        {
            joystickInput = m_Joystick.Direction;
        }
        else
        {
            // Dự phòng: Thử tìm lại Joystick trong scene nếu chưa tham chiếu được
            m_Joystick = FindFirstObjectByType<Joystick>();
            if (m_Joystick != null)
            {
                joystickInput = m_Joystick.Direction;
            }
        }

        // 2. Nếu có đầu vào từ Joystick, điều khiển di chuyển trực tiếp
        if (joystickInput.sqrMagnitude > 0.001f)
        {
            // Tắt vòng chỉ báo chọn khi di chuyển
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.SetIndicatorActive(false);
            }

            Vector3 moveDirection = Vector3.zero;
            Camera mainCam = Camera.main;

            if (mainCam != null)
            {
                // Tính toán hướng di chuyển tương đối theo Camera để điều khiển trực quan trên di động
                Vector3 camForward = mainCam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = mainCam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                moveDirection = (camForward * joystickInput.y + camRight * joystickInput.x).normalized;
            }
            else
            {
                // Fallback nếu không tìm thấy Camera chính
                moveDirection = new Vector3(joystickInput.x, 0f, joystickInput.y).normalized;
            }

            if (moveDirection != Vector3.zero)
            {
                // Di chuyển nhân vật dựa trên hướng đã tính
                float currentSpeed = m_MoveSpeed * joystickInput.magnitude;
                transform.position += moveDirection * currentSpeed * Time.deltaTime;

                // Xoay nhân vật hướng về phía di chuyển (chỉ xoay theo trục Y)
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed * Time.deltaTime);

                // Cập nhật Animation Đi bộ (Walk)
                PlayWalk();
                UpdateAnimatorParams(currentSpeed);
            }
        }
        else
        {
            // Đứng yên
            PlayIdle();
            UpdateAnimatorParams(0f);
        }
    }

    /// <summary>
    /// Phát một animation bất kỳ bằng tên State với hiệu ứng chuyển cảnh mượt mà (CrossFade).
    /// </summary>
    /// <param name="stateName">Tên State trong Animator Controller.</param>
    /// <param name="transitionDuration">Thời gian chuyển cảnh mượt mà (mặc định 0.15 giây).</param>
    public void PlayAnimation(string stateName, float transitionDuration = 0.15f)
    {
        if (m_Animator == null || string.IsNullOrEmpty(stateName)) return;

        // Tránh chạy lại animation nếu đang chạy đúng animation đó
        if (m_CurrentStateName == stateName) return;

        m_Animator.CrossFade(stateName, transitionDuration);
        m_CurrentStateName = stateName;
    }

    /// <summary>
    /// Chạy animation Đứng yên (Idle).
    /// </summary>
    /// <param name="stateName">Tên State trong Animator.</param>
    public void PlayIdle()
    {
        PlayAnimation(m_IdleStateName);
    }

    /// <summary>
    /// Chạy animation Đi bộ (Walk).
    /// </summary>
    public void PlayWalk()
    {
        PlayAnimation(m_WalkStateName);
    }

    /// <summary>
    /// Chạy animation Tạo dáng (Pose 1).
    /// </summary>
    public void PlayPose1()
    {
        Stop(); // Dừng di chuyển nếu đang đi
        PlayAnimation(m_Pose1StateName);
    }

    /// <summary>
    /// Cập nhật các tham số Parameter của Animator (sử dụng Hash ID tối ưu cho Android).
    /// </summary>
    private void UpdateAnimatorParams(float speed)
    {
        if (m_Animator == null) return;

        if (m_HasSpeedParam)
        {
            m_Animator.SetFloat(m_SpeedParamHash, speed);
        }

        if (m_HasMovingParam)
        {
            m_Animator.SetBool(m_MovingParamHash, speed > 0f);
        }
    }

    /// <summary>
    /// Dừng di chuyển ngay lập tức.
    /// </summary>
    public void Stop()
    {
        PlayIdle();
        UpdateAnimatorParams(0f);
    }
}
