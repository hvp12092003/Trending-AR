using System.Collections;
using UnityEngine;

/// <summary>
/// Thêm component này vào Character Prefab để điều khiển di chuyển mượt mà tới vị trí mục tiêu.
/// Hỗ trợ máy trạng thái (State Machine) đồng bộ nhịp nhạc.
/// </summary>
public class Move : MonoBehaviour
{
    public enum CharacterState
    {
        Idle,
        Walking,
        WaitingForSync,
        Dancing
    }

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

    public string IdleStateName
    {
        get => m_IdleStateName;
        set => m_IdleStateName = value;
    }

    public string WalkStateName
    {
        get => m_WalkStateName;
        set => m_WalkStateName = value;
    }

    public string Pose1StateName
    {
        get => m_Pose1StateName;
        set => m_Pose1StateName = value;
    }

    private string m_CurrentStateName;
    private Joystick m_Joystick;

    // Cache variables for Android Optimization
    private int m_SpeedParamHash;
    private int m_MovingParamHash;
    private bool m_HasSpeedParam;
    private bool m_HasMovingParam;

    // Temporary animation control variables
    private Coroutine m_ReturnToIdleCoroutine;
    private bool m_IsPlayingTempAnim = false;

    // State Machine & Sync Variables
    private CharacterState m_State = CharacterState.Idle;
    private float m_IdleTimer = 0f;
    private float m_LastTapTime = 0f;
    private const float DOUBLE_TAP_DELAY = 0.3f;

    public CharacterState CurrentState => m_State;

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
        m_State = CharacterState.Idle;
        m_IdleTimer = 0f;
        PlayIdle();

        // Đăng ký sự kiện beat nhạc toàn cục
        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.OnBeat += OnMusicBeat;
        }
        else
        {
            var syncManager = FindFirstObjectByType<MusicSyncManager>();
            if (syncManager != null)
            {
                syncManager.OnBeat += OnMusicBeat;
            }
        }
    }

    private void OnDestroy()
    {
        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.OnBeat -= OnMusicBeat;
        }
    }

    private void Update()
    {
        // 0. Nếu đang chạy hoạt ảnh tạm thời (chạy 1 lần rồi về Idle), kiểm tra xem có ngắt bằng di chuyển không
        if (m_IsPlayingTempAnim)
        {
            Vector2 joystickDir = Vector2.zero;
            if (m_Joystick != null) joystickDir = m_Joystick.Direction;
            else
            {
                m_Joystick = FindFirstObjectByType<Joystick>();
                if (m_Joystick != null) joystickDir = m_Joystick.Direction;
            }

            if (joystickDir.sqrMagnitude > 0.001f)
            {
                if (m_ReturnToIdleCoroutine != null)
                {
                    StopCoroutine(m_ReturnToIdleCoroutine);
                }
                m_IsPlayingTempAnim = false;
            }
            else
            {
                return; // Đợi hoạt ảnh tạm thời kết thúc
            }
        }

        // 1. Chỉ nhận đầu vào và di chuyển nếu nhân vật này đang được chọn để điều khiển
        if (CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter != gameObject)
        {
            // Nếu không được chọn điều khiển:
            if (m_State == CharacterState.Walking)
            {
                m_State = CharacterState.Idle;
                m_IdleTimer = 0f;
                PlayIdle();
                UpdateAnimatorParams(0f);
            }

            // Vẫn thực hiện đếm thời gian Idle để nhảy đồng bộ ngay cả khi không được chọn
            if (m_State == CharacterState.Idle)
            {
                m_IdleTimer += Time.deltaTime;
                if (m_IdleTimer >= 2f)
                {
                    m_State = CharacterState.WaitingForSync;
                }
            }
            return;
        }

        // 2. Kiểm tra chạm đúp để reset nhạc & nhảy khớp phách
        if (DetectDoubleTap())
        {
            ResetToDance();
            return;
        }

        // 3. Đọc dữ liệu hướng đi từ Joystick ảo
        Vector2 joystickInput = Vector2.zero;
        if (m_Joystick != null)
        {
            joystickInput = m_Joystick.Direction;
        }
        else
        {
            m_Joystick = FindFirstObjectByType<Joystick>();
            if (m_Joystick != null)
            {
                joystickInput = m_Joystick.Direction;
            }
        }

        // 4. Di chuyển dựa trên Joystick
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
                moveDirection = new Vector3(joystickInput.x, 0f, joystickInput.y).normalized;
            }

            if (moveDirection != Vector3.zero)
            {
                float currentSpeed = m_MoveSpeed * joystickInput.magnitude;
                transform.position += moveDirection * currentSpeed * Time.deltaTime;

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotationSpeed * Time.deltaTime);

                m_State = CharacterState.Walking;
                m_IdleTimer = 0f;
                PlayWalk();
                UpdateAnimatorParams(currentSpeed);
            }
        }
        else
        {
            // Không có di chuyển
            if (m_State == CharacterState.Walking)
            {
                m_State = CharacterState.Idle;
                m_IdleTimer = 0f;
                PlayIdle();
                UpdateAnimatorParams(0f);
            }
            else if (m_State == CharacterState.Idle)
            {
                m_IdleTimer += Time.deltaTime;
                if (m_IdleTimer >= 2f)
                {
                    m_State = CharacterState.WaitingForSync;
                    Debug.Log($"[Move] {gameObject.name}: Chờ đồng bộ nhịp nhạc...");
                }
                PlayIdle();
                UpdateAnimatorParams(0f);
            }
            else if (m_State == CharacterState.WaitingForSync)
            {
                PlayIdle();
                UpdateAnimatorParams(0f);
            }
            // Nếu ở trạng thái Dancing, giữ nguyên animation nhảy không bị đè bởi Idle
        }
    }

    private void OnMusicBeat()
    {
        if (m_State == CharacterState.WaitingForSync)
        {
            Debug.Log($"[Move] {gameObject.name}: Nhận Beat! Bắt đầu nhảy.");
            m_State = CharacterState.Dancing;
            PlayPose1();
        }
    }

    private bool DetectDoubleTap()
    {
        if (UnityEngine.InputSystem.Touchscreen.current != null)
        {
            var touches = UnityEngine.InputSystem.Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.press.wasPressedThisFrame)
                {
                    float timeSinceLastTap = Time.time - m_LastTapTime;
                    m_LastTapTime = Time.time;
                    if (timeSinceLastTap < DOUBLE_TAP_DELAY)
                    {
                        return true;
                    }
                }
            }
        }

        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            float timeSinceLastTap = Time.time - m_LastTapTime;
            m_LastTapTime = Time.time;
            if (timeSinceLastTap < DOUBLE_TAP_DELAY)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reset điệu nhảy của nhân vật về đầu và đồng bộ hóa lại nhạc toàn cục.
    /// </summary>
    public void ResetToDance()
    {
        Debug.Log($"[Move] {gameObject.name}: Reset điệu nhảy và nhạc.");
        
        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.ResetPlayback();
        }

        m_State = CharacterState.Dancing;
        m_IdleTimer = 0f;

        if (m_Animator != null)
        {
            m_Animator.Play(m_Pose1StateName, 0, 0f);
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

        // Chuẩn hóa tên state (thay thế dấu chấm bằng dấu gạch dưới vì Unity tự động chuyển đổi tên clip .fbx khi tạo State)
        string sanitizedStateName = stateName.Replace(".", "_");

        // Tránh chạy lại animation nếu đang chạy đúng animation đó
        if (m_CurrentStateName == sanitizedStateName) return;

        m_Animator.CrossFade(sanitizedStateName, transitionDuration);
        m_CurrentStateName = sanitizedStateName;
    }

    /// <summary>
    /// Phát một animation một lần duy nhất, sau đó tự động quay về trạng thái Idle.
    /// </summary>
    /// <param name="stateName">Tên State trong Animator Controller.</param>
    /// <param name="transitionDuration">Thời gian chuyển cảnh mượt mà (mặc định 0.15 giây).</param>
    public void PlayAnimationOnce(string stateName, float transitionDuration = 0.15f)
    {
        if (m_Animator == null || string.IsNullOrEmpty(stateName)) return;

        if (m_ReturnToIdleCoroutine != null)
        {
            StopCoroutine(m_ReturnToIdleCoroutine);
        }

        m_IsPlayingTempAnim = true;
        PlayAnimation(stateName, transitionDuration);
        m_ReturnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterPlay(stateName, transitionDuration));
    }

    private IEnumerator ReturnToIdleAfterPlay(string stateName, float transitionDuration)
    {
        // Đợi 1 frame để Animator bắt đầu chuyển sang trạng thái mới
        yield return null;

        // Lấy thông tin trạng thái hoạt ảnh hiện tại trên layer 0
        var stateInfo = m_Animator.GetCurrentAnimatorStateInfo(0);
        float duration = stateInfo.length;

        // Đợi cho đến khi hoạt ảnh phát xong (trừ đi khoảng thời gian transition sang Idle để mượt mà)
        float waitTime = Mathf.Max(0f, duration - transitionDuration);
        yield return new WaitForSeconds(waitTime);

        // Quay lại trạng thái Idle
        m_IsPlayingTempAnim = false;
        m_State = CharacterState.Idle;
        m_IdleTimer = 0f;
        PlayIdle();
    }

    /// <summary>
    /// Chạy animation Đứng yên (Idle).
    /// </summary>
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

