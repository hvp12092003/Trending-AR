using System;
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
    private string m_IdleStateName = "Idle";

    [SerializeField]
    [Tooltip("Tên State trong Animator cho trạng thái chạy/đi bộ.")]
    private string m_WalkStateName = "Run";

    [SerializeField]
    [Tooltip("Tên State trong Animator cho trạng thái nhảy mặc định (Dance1–Dance8).")]
    private string m_Pose1StateName = "Dance1";

    [Header("Animation Settings (Optional Parameters)")]
    [SerializeField]
    [Tooltip("Tên tham số Float trong Animator để điều khiển tốc độ chạy/đi bộ (nếu sử dụng parameter).")]
    private string m_AnimatorSpeedParam = "Speed";

    [SerializeField]
    [Tooltip("Tên tham số Bool trong Animator để bật/tắt trạng thái di chuyển (nếu sử dụng parameter).")]
    private string m_AnimatorMovingParam = "IsMoving";

    [Header("Audio Reactivity for Auto Dance")]
    [SerializeField]
    [Tooltip("Ngưỡng lọc khoảng lặng âm thanh (độ nhạy). Giá trị càng nhỏ càng nhạy (ví dụ 0.001), nhân vật sẽ nhảy kể cả với tiếng nhạc cụ rất nhỏ. Mặc định là 0.002.")]
    private float m_AudioNoiseThreshold = 0.002f;

    [SerializeField]
    [Tooltip("Thời gian chờ duy trì trạng thái nhảy sau khi hết nhạc (giây). Mặc định là 0.5.")]
    private float m_DanceKeepAliveTime = 0.5f;

    private float m_DanceKeepAliveTimer = 0f;

    public Animator m_Animator;

    [Header("Animation Root Lock")]
    [SerializeField]
    [Tooltip("Locks the visual skeleton root in place so imported clips cannot make the Cast float or sink.")]
    private bool m_LockAnimationRootLocalPosition = true;

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
    private Camera m_MainCamera;
    private CastPlacementState m_PlacementState;
    private CastAudioData m_CastAudio;
    private Transform m_AnimationRootLockTarget;
    private Vector3 m_AnimationRootBaseLocalPosition;
    private bool m_HasAnimationRootLock;

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
        // Đọc độ nhạy lọc khoảng lặng từ CastPrefab (tránh việc component Move được add lúc runtime)
        CastPrefab castPrefab = GetComponent<CastPrefab>();
        if (castPrefab != null)
        {
            m_AudioNoiseThreshold = castPrefab.audioNoiseThreshold;
            m_DanceKeepAliveTime = castPrefab.danceKeepAliveTime;
        }

        // Lấy Animator để điều khiển animation chạy/đi bộ nếu có
        m_MainCamera = Camera.main;
        m_PlacementState = GetComponent<CastPlacementState>();
        m_CastAudio = GetComponent<CastAudioData>();

        m_Animator = GetComponentInChildren<Animator>();
        if (m_Animator != null)
        {
            m_Animator.applyRootMotion = false;
            ConfigureAnimationRootLock();
        }

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

        // Bắt đầu với Animation Idle mặc định nếu chưa ở trạng thái Dancing
        if (m_State != CharacterState.Dancing)
        {
            m_State = CharacterState.Idle;
            m_IdleTimer = 0f;
            PlayIdle();
        }

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

    private void LateUpdate()
    {
        RestoreAnimationRootLock();
    }

    private void Update()
    {
        // 0. Nếu đang chạy hoạt ảnh tạm thời (chạy 1 lần rồi về Idle), kiểm tra xem có ngắt bằng di chuyển không
        if (m_IsPlayingTempAnim)
        {
            Vector2 joystickDir = Vector2.zero;
            bool isControlledChar = CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter == gameObject;
            if (isControlledChar)
            {
                if (m_Joystick != null) joystickDir = m_Joystick.Direction;
                else
                {
                    m_Joystick = FindFirstObjectByType<Joystick>();
                    if (m_Joystick != null) joystickDir = m_Joystick.Direction;
                }
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

        // 1. Kiểm tra xem nhân vật có được chọn để điều khiển hay không
        bool isControlled = CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter == gameObject;

        // 2. Đọc dữ liệu hướng đi từ Joystick ảo (chỉ khi được điều khiển)
        Vector2 joystickInput = Vector2.zero;
        if (isControlled)
        {
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
        }

        // 3. Di chuyển dựa trên Joystick
        if (joystickInput.sqrMagnitude > 0.001f)
        {
            // Tắt vòng chỉ báo chọn khi di chuyển và reset bộ đếm thời gian tự động ẩn UI
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.SetIndicatorActive(false);
                CharacterManager.Instance.ResetAutoHideTimer();
            }

            Vector3 moveDirection = Vector3.zero;
            Camera mainCam = GetMainCamera();

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

                // Nếu đang ở trạng thái Dancing, reset cache tên state để buộc
                // animation Walk được phát (tránh bị chặn do tên state giống Dance)
                if (m_State == CharacterState.Dancing)
                {
                    m_CurrentStateName = null;
                }

                m_State = CharacterState.Walking;
                m_IdleTimer = 0f;
                PlayWalk();
                UpdateAnimatorParams(currentSpeed);
            }
        }
        else
        {
            // Không có di chuyển (hoặc không được điều khiển)
            if (m_State == CharacterState.Walking)
            {
                UpdateAnimatorParams(0f);
                // Sau khi di chuyển xong, chuyển sang nhảy nếu có nhạc (hoặc trong thời gian duy trì), ngược lại về Idle
                if (IsMusicPlaying())
                {
                    m_DanceKeepAliveTimer = m_DanceKeepAliveTime;
                    PlayDance(0.15f);
                }
                else if (m_DanceKeepAliveTimer > 0f)
                {
                    PlayDance(0.15f);
                }
                else
                {
                    m_State = CharacterState.Idle;
                    PlayIdle(0.15f);
                }
            }
            else
            {
                // Tự động kiểm tra nhạc và chuyển đổi trạng thái khi không di chuyển
                if (!m_IsPlayingTempAnim)
                {
                    bool musicPlaying = IsMusicPlaying();
                    if (musicPlaying)
                    {
                        // Reset timer duy trì trạng thái nhảy khi phát hiện tiếng nhạc
                        m_DanceKeepAliveTimer = m_DanceKeepAliveTime;

                        if (m_State != CharacterState.Dancing)
                        {
                            // Có nhạc -> chuyển sang nhảy nhanh, khớp nhịp
                            PlayDance(0.1f);
                        }
                    }
                    else
                    {
                        if (m_State == CharacterState.Dancing || m_State == CharacterState.WaitingForSync)
                        {
                            // Giảm timer duy trì trạng thái nhảy khi nhạc tắt
                            m_DanceKeepAliveTimer -= Time.deltaTime;

                            if (m_DanceKeepAliveTimer <= 0f)
                            {
                                // Hết thời gian duy trì -> chuyển dần về idle
                                m_State = CharacterState.Idle;
                                PlayIdle(0.15f);
                            }
                        }
                    }
                }

                // Nếu ở Idle và không có nhạc phát (và hết thời gian duy trì), đảm bảo chạy animation Idle
                if (m_State == CharacterState.Idle && m_DanceKeepAliveTimer <= 0f)
                {
                    PlayIdle();
                    UpdateAnimatorParams(0f);
                }
            }
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

    private Camera GetMainCamera()
    {
        if (m_MainCamera == null)
        {
            m_MainCamera = Camera.main;
        }
        return m_MainCamera;
    }

    private bool IsPlaced()
    {
        if (m_PlacementState == null)
        {
            m_PlacementState = GetComponent<CastPlacementState>();
        }

        return m_PlacementState != null ? m_PlacementState.IsPlaced : transform.parent == null;
    }

    /// <summary>
    /// Reset điệu nhảy của tất cả nhân vật về đầu và đồng bộ hóa lại nhạc toàn cục.
    /// </summary>
    public void ResetToDance()
    {
        Debug.Log($"[Move] {gameObject.name}: Reset điệu nhảy và nhạc cho tất cả nhân vật.");
        
        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.ResetPlayback();
        }

        // Tìm tất cả các đối tượng Move hoạt động trong Scene và reset animation của chúng nếu đã được thả ra
        Move[] allCharacters = FindObjectsByType<Move>(FindObjectsSortMode.None);
        foreach (var character in allCharacters)
        {
            if (character != null && character.transform.parent == null)
            {
                character.ResetLocalDanceState();
            }
        }
    }

    /// <summary>
    /// Chỉ reset trạng thái nhảy cục bộ của nhân vật này (không reset nhạc toàn cục).
    /// </summary>
    public void ResetLocalDanceState()
    {
        m_State = CharacterState.Dancing;
        m_IdleTimer = 0f;

        if (m_Animator != null)
        {
            m_Animator.Play(m_Pose1StateName, 0, 0f);
            RestoreAnimationRootLock();
        }
    }

    /// <summary>
    /// Phát điệu nhảy được chọn và chuyển trạng thái thành Dancing để tránh bị đè bởi Idle.
    /// </summary>
    public void PlayDance(float transitionDuration = 0.15f)
    {
        m_State = CharacterState.Dancing;
        m_IdleTimer = 0f;
        PlayAnimation(m_Pose1StateName, transitionDuration, true);
    }

    /// <summary>
    /// Phát một animation bất kỳ bằng tên State với hiệu ứng chuyển cảnh mượt mà (CrossFadeInFixedTime) và đồng bộ nhịp nhạc.
    /// </summary>
    /// <param name="stateName">Tên State trong Animator Controller.</param>
    /// <param name="transitionDuration">Thời gian chuyển cảnh mượt mà (mặc định 0.15 giây).</param>
    /// <param name="syncPhase">Nếu true, đồng bộ pha của animation với nhịp beat nhạc hiện tại.</param>
    public void PlayAnimation(string stateName, float transitionDuration = 0.15f, bool syncPhase = false)
    {
        if (m_Animator == null || string.IsNullOrEmpty(stateName)) return;

        // Chuẩn hóa tên state (thay thế dấu chấm bằng dấu gạch dưới vì Unity tự động chuyển đổi tên clip .fbx khi tạo State)
        string sanitizedStateName = stateName.Replace(".", "_");

        // Tránh chạy lại animation nếu đang chạy đúng animation đó
        if (m_CurrentStateName == sanitizedStateName) return;

        float normalizedTimeOffset = 0f;
        if (syncPhase && MusicSyncManager.Instance != null && MusicSyncManager.Instance.BeatInterval > 0f)
        {
            // Tính toán pha hiện tại trong beat để nhảy khớp ngay lập tức
            normalizedTimeOffset = MusicSyncManager.Instance.BeatTimer / MusicSyncManager.Instance.BeatInterval;
            normalizedTimeOffset = Mathf.Clamp01(normalizedTimeOffset);
        }

        // Sử dụng CrossFadeInFixedTime để chuyển đổi nhanh và độc lập với độ dài của clip cũ
        m_Animator.CrossFadeInFixedTime(sanitizedStateName, transitionDuration, 0, normalizedTimeOffset);
        RestoreAnimationRootLock();

        // Đồng bộ tốc độ hoạt ảnh của Animator dựa trên BPM hiện tại so với BPM chuẩn (ví dụ: 120 BPM)
        if (MusicSyncManager.Instance != null)
        {
            m_Animator.speed = MusicSyncManager.Instance.Bpm / 120f;
        }

        m_CurrentStateName = sanitizedStateName;
    }

    private void ConfigureAnimationRootLock()
    {
        m_HasAnimationRootLock = false;
        m_AnimationRootLockTarget = null;

        if (!m_LockAnimationRootLocalPosition || m_Animator == null)
        {
            return;
        }

        m_AnimationRootLockTarget = AnimationRootLockUtility.ResolveLockTarget(m_Animator, transform);
        if (m_AnimationRootLockTarget == null)
        {
            return;
        }

        m_AnimationRootBaseLocalPosition = m_AnimationRootLockTarget.localPosition;
        m_HasAnimationRootLock = true;
    }

    private void RestoreAnimationRootLock()
    {
        if (!m_HasAnimationRootLock || m_AnimationRootLockTarget == null)
        {
            return;
        }

        AnimationRootLockUtility.RestoreLocalPosition(m_AnimationRootLockTarget, m_AnimationRootBaseLocalPosition);
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

    private readonly float[] m_AudioSamples = new float[128];

    /// <summary>
    /// Kiểm tra xem AudioSource có thực sự đang phát ra âm thanh hay không (vượt qua ngưỡng lọc nhiễu khoảng lặng).
    /// </summary>
    private bool IsAudioSourceActuallyMakingSound(AudioSource src)
    {
        if (src == null || !src.isPlaying || src.mute || src.volume <= 0.005f)
        {
            return false;
        }

        // Lấy mẫu tín hiệu âm thanh hiện tại
        src.GetOutputData(m_AudioSamples, 0);

        float sum = 0f;
        for (int i = 0; i < m_AudioSamples.Length; i++)
        {
            sum += m_AudioSamples[i] * m_AudioSamples[i];
        }

        // Tính Root Mean Square (RMS) đại diện cho biên độ trung bình
        float rms = Mathf.Sqrt(sum / m_AudioSamples.Length);

        // Ngưỡng lọc khoảng lặng (Inspector configurable)
        return rms > m_AudioNoiseThreshold;
    }

    /// <summary>
    /// Kiểm tra xem nhạc của nhạc cụ cá nhân hoặc nhạc tổng có đang thực sự phát ra âm thanh hay không.
    /// </summary>
    public bool IsMusicPlaying()
    {
        if (!IsPlaced()) return false;

        // 1. Kiểm tra xem nhạc tổng (Full Song) ở chế độ Band có đang phát thực tế không
        if (BandAudioManager.Instance != null)
        {
            AudioSource fullSongSource = BandAudioManager.Instance.GetFullSongSource();
            if (fullSongSource != null && fullSongSource.isPlaying && !fullSongSource.mute && fullSongSource.volume > 0.01f)
            {
                // Khi chơi nhạc tổng, ta kiểm tra độ lớn thực tế phát ra từ Full Song
                return IsAudioSourceActuallyMakingSound(fullSongSource);
            }
        }

        // 2. Kiểm tra nhạc cụ riêng lẻ của Cast
        if (m_CastAudio != null && m_CastAudio.preparedSource != null)
        {
            return IsAudioSourceActuallyMakingSound(m_CastAudio.preparedSource);
        }

        return false;
    }

    /// <summary>
    /// Chạy animation Đứng yên (Idle).
    /// </summary>
    public void PlayIdle(float transitionDuration = 0.15f)
    {
        PlayAnimation(m_IdleStateName, transitionDuration);
    }

    /// <summary>
    /// Chạy animation Đi bộ (Walk).
    /// </summary>
    public void PlayWalk(float transitionDuration = 0.15f)
    {
        PlayAnimation(m_WalkStateName, transitionDuration);
    }

    /// <summary>
    /// Chạy animation Tạo dáng (Pose 1).
    /// </summary>
    public void PlayPose1()
    {
        Stop(); // Dừng di chuyển nếu đang đi
        PlayAnimation(m_Pose1StateName, 0.15f, true);
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

internal static class AnimationRootLockUtility
{
    private static readonly string[] RootCandidateNames =
    {
        "Armature",
        "Root",
        "root",
        "Rig",
        "rig"
    };

    public static Transform ResolveLockTarget(Animator animator, Transform gameplayRoot)
    {
        if (animator == null)
        {
            return null;
        }

        Transform animatorTransform = animator.transform;
        Transform namedRoot = FindImmediateChildByName(animatorTransform);
        if (IsSafeTarget(namedRoot, gameplayRoot))
        {
            return namedRoot;
        }

        Transform hips = null;
        if (animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        Transform topmostBoneRoot = GetTopmostChildUnder(animatorTransform, hips);
        if (IsSafeTarget(topmostBoneRoot, gameplayRoot))
        {
            return topmostBoneRoot;
        }

        // If Animator is already on a visual child, locking it will not fight gameplay movement.
        if (IsSafeTarget(animatorTransform, gameplayRoot))
        {
            return animatorTransform;
        }

        return null;
    }

    public static void RestoreLocalPosition(Transform target, Vector3 localPosition)
    {
        if (target != null && target.localPosition != localPosition)
        {
            target.localPosition = localPosition;
        }
    }

    private static Transform FindImmediateChildByName(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            for (int j = 0; j < RootCandidateNames.Length; j++)
            {
                if (string.Equals(child.name, RootCandidateNames[j], StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static Transform GetTopmostChildUnder(Transform root, Transform child)
    {
        if (root == null || child == null || child == root || !child.IsChildOf(root))
        {
            return null;
        }

        Transform current = child;
        while (current.parent != null && current.parent != root)
        {
            current = current.parent;
        }

        return current.parent == root ? current : null;
    }

    private static bool IsSafeTarget(Transform target, Transform gameplayRoot)
    {
        return target != null && target != gameplayRoot;
    }
}
