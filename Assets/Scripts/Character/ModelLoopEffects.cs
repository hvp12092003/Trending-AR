using UnityEngine;
using System;

/// <summary>
/// Component điều khiển hiệu ứng vòng lặp trực quan (bay lơ lửng, co giãn/Squash & Stretch) cho mô hình nhân vật.
/// Giúp mô hình chuyển động sinh động, có thể đồng bộ nhịp nhạc toàn cục hoặc cảm ứng theo âm lượng âm nhạc thời gian thực
/// mà không gây xung đột với hệ thống di chuyển hay scale của cha.
/// </summary>
public class ModelLoopEffects : MonoBehaviour
{
    public enum ScaleMode
    {
        ContinuousSine,       // Co giãn tuần hoàn hình sin đều đặn
        SquashAndStretch,     // Hiệu ứng co giãn hoạt hình (Y giãn thì XZ co và ngược lại)
        MusicBeatPulse,       // Phình to/giật co giãn theo nhịp phách nhạc (OnBeat)
        RealtimeAudioLoudness // Co giãn/giật trực tiếp theo âm lượng thực tế của nhạc cụ/bản nhạc (Loudness)
    }

    [Header("Target Transform Settings")]
    [SerializeField]
    [Tooltip("Transform của mô hình con cần áp dụng hiệu ứng (thường là Object chứa Animator/Mesh). Nếu để trống, hệ thống sẽ tự động tìm kiếm Animator ở con.")]
    private Transform m_TargetTransform;

    [Header("Floating / Hovering Settings")]
    [SerializeField]
    [Tooltip("Bật/tắt hiệu ứng bay lơ lửng.")]
    private bool m_EnableFloating = true;

    [SerializeField]
    [Tooltip("Tốc độ/tần số bay lơ lửng.")]
    private float m_FloatingSpeed = 2f;

    [SerializeField]
    [Tooltip("Biên độ di chuyển theo các trục X, Y, Z (mặc định chỉ dao động dọc theo trục Y).")]
    private Vector3 m_FloatingAmplitude = new Vector3(0f, 0.05f, 0f);

    [SerializeField]
    [Tooltip("Độ lệch pha thủ công để căn chỉnh pha dao động của hiệu ứng bay.")]
    private float m_FloatingPhaseOffset = 0f;

    [Header("Scaling / Pulsing Settings")]
    [SerializeField]
    [Tooltip("Bật/tắt hiệu ứng co giãn.")]
    private bool m_EnableScaling = true;

    [SerializeField]
    [Tooltip("Chế độ co giãn của mô hình.")]
    private ScaleMode m_ScaleMode = ScaleMode.ContinuousSine;

    [SerializeField]
    [Tooltip("Tốc độ co giãn tuần hoàn (chỉ dùng cho ContinuousSine và SquashAndStretch).")]
    private float m_ScalingSpeed = 3f;

    [SerializeField]
    [Tooltip("Biên độ thay đổi tỉ lệ scale trên các trục X, Y, Z (cho chế độ Sine và Squash).")]
    private Vector3 m_ScalingAmplitude = new Vector3(0.05f, 0.05f, 0.05f);

    [SerializeField]
    [Tooltip("Nếu bật, sẽ scale đều cả 3 trục theo cùng một tỉ lệ (lấy biên độ trục X làm chuẩn) - áp dụng cho chế độ Sine.")]
    private bool m_UniformScaling = true;

    [Header("Beat Sync Settings (For MusicBeatPulse)")]
    [SerializeField]
    [Tooltip("Lượng scale cộng thêm vào kích thước gốc khi nhận được phách nhạc (Beat).")]
    private Vector3 m_BeatPulseScale = new Vector3(0.1f, 0.15f, 0.1f);

    [SerializeField]
    [Tooltip("Tốc độ phục hồi về trạng thái scale bình thường sau cú giật beat.")]
    private float m_BeatDecaySpeed = 5f;

    [SerializeField]
    [Tooltip("Hệ số bóp méo Squash & Stretch khi nhận beat (X, Z bóp nhỏ lại khi Y phình to ra và ngược lại). Mức độ: 0 (không bóp) đến 1 (bóp mạnh).")]
    [Range(0f, 1f)]
    private float m_BeatSquashFactor = 0.5f;

    [Header("Audio Reactive Settings (For RealtimeAudioLoudness)")]
    [SerializeField]
    [Tooltip("Nguồn phát âm thanh để cảm ứng. Nếu trống, hệ thống tự động tìm kiếm trên Object hiện tại, Object cha, hoặc tìm MusicSyncManager.")]
    private AudioSource m_AudioSource;

    [SerializeField]
    [Tooltip("Vector xác định các trục sẽ giật theo âm lượng nhạc và biên độ giật tối đa (Ví dụ: Loa giật trục Z đặt (0, 0, 0.5), kèn giật trục X đặt (0.5, 0, 0)).")]
    private Vector3 m_AudioReactiveScaleAxis = new Vector3(0f, 0f, 0.5f);

    [SerializeField]
    [Tooltip("Độ nhạy của cảm ứng âm thanh (nhân với âm lượng thực tế thu được). Mặc định là 2.")]
    private float m_AudioSensitivity = 2f;

    [SerializeField]
    [Tooltip("Ngưỡng lọc âm lượng nhỏ để tránh rung nhiễu khi nhạc nhỏ hoặc không phát.")]
    private float m_AudioMinThreshold = 0.01f;

    [SerializeField]
    [Tooltip("Tốc độ làm mượt dao động co giãn của cảm ứng (Lerp speed).")]
    private float m_AudioSmoothSpeed = 15f;

    [Header("VFX Beat Settings")]
    [SerializeField]
    [Tooltip("VFX Particle System Prefab sẽ phát mỗi khi có nhịp nhạc (beat).")]
    private ParticleSystem m_BeatVFXPrefab;

    [SerializeField]
    [Tooltip("Số lượng VFX khởi tạo trước trong Object Pool.")]
    private int m_InitialPoolSize = 5;

    [SerializeField]
    [Tooltip("Transform chứa các đối tượng VFX trong Pool (để tránh bị ảnh hưởng bởi scale của Target Transform). Thường là cha của Target Transform.")]
    private Transform m_PoolParent;

    // Hàng đợi quản lý Object Pool của VFX
    private System.Collections.Generic.Queue<ParticleSystem> m_VFXPool = new System.Collections.Generic.Queue<ParticleSystem>();
    private bool m_RegisteredToBeat = false;

    [Header("Loudness VFX Settings")]
    [SerializeField]
    [Tooltip("Ngưỡng âm lượng để kích hoạt VFX khi ở chế độ RealtimeAudioLoudness.")]
    private float m_LoudnessVFXThreshold = 0.15f;

    [SerializeField]
    [Tooltip("Thời gian giãn cách tối thiểu giữa 2 lần phát VFX ở chế độ RealtimeAudioLoudness.")]
    private float m_LoudnessVFXCooldown = 0.15f;

    private bool m_WasAboveLoudnessThreshold = false;
    private float m_LastLoudnessVFXTime = 0f;

    // Cache các giá trị ban đầu để hiệu ứng chạy tương đối
    private Vector3 m_OriginalLocalPosition;
    private Vector3 m_OriginalLocalScale;
    private Vector3 m_LastAppliedPosition;
    private Vector3 m_LastAppliedScale;
    private bool m_HasInitialized = false;

    // Quản lý trạng thái xung beat nhạc và cảm ứng
    private float m_BeatPulseIntensity = 0f;
    private float m_CurrentLoudness = 0f;
    private float[] m_AudioSamples = new float[128];

    // Thuộc tính công khai để tinh chỉnh từ code hoặc Editor Script khác nếu cần
    public Transform TargetTransform { get => m_TargetTransform; set => m_TargetTransform = value; }
    public bool EnableFloating { get => m_EnableFloating; set => m_EnableFloating = value; }
    public float FloatingSpeed { get => m_FloatingSpeed; set => m_FloatingSpeed = value; }
    public Vector3 FloatingAmplitude { get => m_FloatingAmplitude; set => m_FloatingAmplitude = value; }
    public float FloatingPhaseOffset { get => m_FloatingPhaseOffset; set => m_FloatingPhaseOffset = value; }
    
    public bool EnableScaling { get => m_EnableScaling; set => m_EnableScaling = value; }
    public ScaleMode CurrentScaleMode { get => m_ScaleMode; set => m_ScaleMode = value; }
    public float ScalingSpeed { get => m_ScalingSpeed; set => m_ScalingSpeed = value; }
    public Vector3 ScalingAmplitude { get => m_ScalingAmplitude; set => m_ScalingAmplitude = value; }
    public bool UniformScaling { get => m_UniformScaling; set => m_UniformScaling = value; }

    public Vector3 BeatPulseScale { get => m_BeatPulseScale; set => m_BeatPulseScale = value; }
    public float BeatDecaySpeed { get => m_BeatDecaySpeed; set => m_BeatDecaySpeed = value; }
    public float BeatSquashFactor { get => m_BeatSquashFactor; set => m_BeatSquashFactor = value; }

    public AudioSource TargetAudioSource { get => m_AudioSource; set => m_AudioSource = value; }
    public Vector3 AudioReactiveScaleAxis { get => m_AudioReactiveScaleAxis; set => m_AudioReactiveScaleAxis = value; }
    public float AudioSensitivity { get => m_AudioSensitivity; set => m_AudioSensitivity = value; }
    public float AudioMinThreshold { get => m_AudioMinThreshold; set => m_AudioMinThreshold = value; }
    public float AudioSmoothSpeed { get => m_AudioSmoothSpeed; set => m_AudioSmoothSpeed = value; }
    public ParticleSystem BeatVFXPrefab { get => m_BeatVFXPrefab; set => m_BeatVFXPrefab = value; }
    public int InitialPoolSize { get => m_InitialPoolSize; set => m_InitialPoolSize = value; }
    public Transform PoolParent { get => m_PoolParent; set => m_PoolParent = value; }
    public float LoudnessVFXThreshold { get => m_LoudnessVFXThreshold; set => m_LoudnessVFXThreshold = value; }
    public float LoudnessVFXCooldown { get => m_LoudnessVFXCooldown; set => m_LoudnessVFXCooldown = value; }

    private void Awake()
    {
        InitializeTarget();
    }

    private void Start()
    {
        InitializeVFXPool();
        TryRegisterBeatEvent();

        // Tự động tìm AudioSource trên chính nhân vật/Object cha nếu chưa được gán
        if (m_AudioSource == null)
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = GetComponentInParent<AudioSource>();
            }
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký tránh rò rỉ bộ nhớ
        if (m_RegisteredToBeat && MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.OnBeat -= OnMusicBeat;
            m_RegisteredToBeat = false;
        }
    }

    /// <summary>
    /// Tìm kiếm và lưu trữ thông tin Transform đích và các giá trị gốc.
    /// </summary>
    public void InitializeTarget()
    {
        if (m_HasInitialized) return;

        if (m_TargetTransform == null)
        {
            m_TargetTransform = this.transform;
            Debug.Log($"[ModelLoopEffects] {gameObject.name}: Tự động nhận chính Transform hiện tại làm Target Transform.");
        }

        if (m_TargetTransform != null)
        {
            m_OriginalLocalPosition = m_TargetTransform.localPosition;
            m_OriginalLocalScale = m_TargetTransform.localScale;
            m_LastAppliedPosition = m_OriginalLocalPosition;
            m_LastAppliedScale = m_OriginalLocalScale;
            m_HasInitialized = true;
        }
    }

    private void InitializeVFXPool()
    {
        if (m_BeatVFXPrefab == null) return;

        m_VFXPool.Clear();
        Transform parentTransform = m_PoolParent != null ? m_PoolParent : this.transform;

        for (int i = 0; i < m_InitialPoolSize; i++)
        {
            ParticleSystem vfxInstance = Instantiate(m_BeatVFXPrefab, parentTransform);
            vfxInstance.gameObject.SetActive(false);
            m_VFXPool.Enqueue(vfxInstance);
        }
    }

    private void TryRegisterBeatEvent()
    {
        if (m_RegisteredToBeat) return;

        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.OnBeat += OnMusicBeat;
            m_RegisteredToBeat = true;
            Debug.Log($"[ModelLoopEffects] {gameObject.name}: Đã đăng ký sự kiện nhịp nhạc (OnBeat) thành công.");
        }
    }

    private void OnMusicBeat()
    {
        // Khi nhận được beat nhạc, kích hoạt cường độ xung lên tối đa (1.0)
        if (m_EnableScaling && m_ScaleMode == ScaleMode.MusicBeatPulse)
        {
            m_BeatPulseIntensity = 1f;
        }

        // Phát VFX từ Object Pool
        PlayBeatVFX();
    }

    private void PlayBeatVFX()
    {
        if (m_BeatVFXPrefab == null) return;

        ParticleSystem vfxInstance = null;

        // Thử lấy từ pool
        while (m_VFXPool.Count > 0)
        {
            vfxInstance = m_VFXPool.Dequeue();
            if (vfxInstance != null)
            {
                break;
            }
        }

        // Nếu pool trống hoặc chứa phần tử null, sinh mới một instance
        if (vfxInstance == null)
        {
            Transform parentTransform = m_PoolParent != null ? m_PoolParent : this.transform;
            vfxInstance = Instantiate(m_BeatVFXPrefab, parentTransform);
        }

        // Đặt tọa độ thế giới của VFX để trùng khớp vị trí nhân vật (tránh ảnh hưởng bởi scale của Target Transform)
        vfxInstance.transform.position = m_TargetTransform != null ? m_TargetTransform.position : this.transform.position;
        vfxInstance.transform.localRotation = Quaternion.identity;
        vfxInstance.transform.localScale = Vector3.one;

        // Bật GameObject, dọn dẹp các hạt cũ và phát
        vfxInstance.gameObject.SetActive(true);
        vfxInstance.Stop(true);
        vfxInstance.Clear(true);
        vfxInstance.Play(true);

        // Khởi chạy Coroutine để thu hồi về pool khi phát xong
        StartCoroutine(ReturnVFXToPoolAfterPlay(vfxInstance));
    }

    private System.Collections.IEnumerator ReturnVFXToPoolAfterPlay(ParticleSystem vfxInstance)
    {
        // Chờ cho đến khi toàn bộ các hạt của VFX phát xong hoàn toàn
        yield return new WaitUntil(() => vfxInstance == null || !vfxInstance.IsAlive(true));

        if (vfxInstance != null)
        {
            vfxInstance.gameObject.SetActive(false);
            m_VFXPool.Enqueue(vfxInstance);
        }
    }

    private void LateUpdate()
    {
        if (!m_HasInitialized || m_TargetTransform == null) return;

        // Đảm bảo đăng ký sự kiện nếu chưa đăng ký thành công trước đó
        if (!m_RegisteredToBeat)
        {
            TryRegisterBeatEvent();
        }

        // Kiểm tra xem có thay đổi từ các script bên ngoài hoặc DOTween hay không
        if ((m_TargetTransform.localPosition - m_LastAppliedPosition).sqrMagnitude > 0.000001f)
        {
            m_OriginalLocalPosition = m_TargetTransform.localPosition;
        }
        if ((m_TargetTransform.localScale - m_LastAppliedScale).sqrMagnitude > 0.000001f)
        {
            m_OriginalLocalScale = m_TargetTransform.localScale;
        }

        // 1. Xử lý hiệu ứng bay lơ lửng (Floating)
        if (m_EnableFloating)
        {
            float floatTime = Time.time * m_FloatingSpeed + m_FloatingPhaseOffset;
            Vector3 floatOffset = new Vector3(
                Mathf.Sin(floatTime) * m_FloatingAmplitude.x,
                Mathf.Sin(floatTime) * m_FloatingAmplitude.y,
                Mathf.Sin(floatTime) * m_FloatingAmplitude.z
            );
            Vector3 targetPos = m_OriginalLocalPosition + floatOffset;
            m_TargetTransform.localPosition = targetPos;
            m_LastAppliedPosition = targetPos;
        }
        else
        {
            // Trả về vị trí cục bộ gốc nếu tắt
            m_TargetTransform.localPosition = m_OriginalLocalPosition;
            m_LastAppliedPosition = m_OriginalLocalPosition;
        }

        // 2. Xử lý hiệu ứng co giãn (Scaling)
        if (m_EnableScaling)
        {
            Vector3 scaleMultiplier = Vector3.one;

            switch (m_ScaleMode)
            {
                case ScaleMode.ContinuousSine:
                    float sineTime = Time.time * m_ScalingSpeed;
                    if (m_UniformScaling)
                    {
                        float uniformOffset = Mathf.Sin(sineTime) * m_ScalingAmplitude.x;
                        scaleMultiplier = Vector3.one * (1f + uniformOffset);
                    }
                    else
                    {
                        scaleMultiplier.x = 1f + Mathf.Sin(sineTime) * m_ScalingAmplitude.x;
                        scaleMultiplier.y = 1f + Mathf.Sin(sineTime) * m_ScalingAmplitude.y;
                        scaleMultiplier.z = 1f + Mathf.Sin(sineTime) * m_ScalingAmplitude.z;
                    }
                    break;

                case ScaleMode.SquashAndStretch:
                    float squashTime = Time.time * m_ScalingSpeed;
                    float wave = Mathf.Sin(squashTime); // Giá trị chạy từ -1 đến 1

                    // Y co giãn theo trục dọc
                    scaleMultiplier.y = 1f + wave * m_ScalingAmplitude.y;
                    // X và Z co giãn ngược chiều với trục dọc Y
                    scaleMultiplier.x = 1f - wave * m_ScalingAmplitude.x;
                    scaleMultiplier.z = 1f - wave * m_ScalingAmplitude.z;
                    break;

                case ScaleMode.MusicBeatPulse:
                    // Giảm dần cường độ xung phách nhạc theo thời gian trôi qua (Delta Time)
                    m_BeatPulseIntensity = Mathf.Max(0f, m_BeatPulseIntensity - Time.deltaTime * m_BeatDecaySpeed);

                    if (m_BeatPulseIntensity > 0f)
                    {
                        // Phình to trục dọc Y
                        scaleMultiplier.y = 1f + m_BeatPulseScale.y * m_BeatPulseIntensity;
                        // Co trục ngang X và Z tỉ lệ nghịch với hệ số SquashFactor
                        scaleMultiplier.x = 1f - m_BeatPulseScale.x * m_BeatPulseIntensity * m_BeatSquashFactor;
                        scaleMultiplier.z = 1f - m_BeatPulseScale.z * m_BeatPulseIntensity * m_BeatSquashFactor;
                    }
                    break;

                case ScaleMode.RealtimeAudioLoudness:
                    float targetLoudness = 0f;
                    AudioSource activeSource = m_AudioSource;

                    // Nếu không tự gán cục bộ, lấy nguồn phát chính từ MusicSyncManager
                    if (activeSource == null && MusicSyncManager.Instance != null)
                    {
                        activeSource = MusicSyncManager.Instance.AudioSource;
                    }

                    if (activeSource != null && activeSource.isPlaying)
                    {
                        activeSource.GetOutputData(m_AudioSamples, 0);
                        float sum = 0f;
                        for (int i = 0; i < m_AudioSamples.Length; i++)
                        {
                            sum += m_AudioSamples[i] * m_AudioSamples[i];
                        }
                        float rms = Mathf.Sqrt(sum / m_AudioSamples.Length);
                        targetLoudness = rms * m_AudioSensitivity;
                        if (targetLoudness < m_AudioMinThreshold)
                        {
                            targetLoudness = 0f;
                        }
                    }

                    // Làm mượt để tránh nhấp nháy/giật gián đoạn quá nhanh
                    m_CurrentLoudness = Mathf.Lerp(m_CurrentLoudness, targetLoudness, Time.deltaTime * m_AudioSmoothSpeed);

                    // Áp dụng lượng giật co giãn cộng thêm tương ứng vào từng trục
                    scaleMultiplier.x = 1f + m_AudioReactiveScaleAxis.x * m_CurrentLoudness;
                    scaleMultiplier.y = 1f + m_AudioReactiveScaleAxis.y * m_CurrentLoudness;
                    scaleMultiplier.z = 1f + m_AudioReactiveScaleAxis.z * m_CurrentLoudness;

                    // Kiểm tra kích hoạt VFX theo ngưỡng âm lượng (Edge Trigger)
                    bool isAboveLoudness = m_CurrentLoudness >= m_LoudnessVFXThreshold;
                    if (isAboveLoudness && !m_WasAboveLoudnessThreshold)
                    {
                        if (Time.time - m_LastLoudnessVFXTime >= m_LoudnessVFXCooldown)
                        {
                            PlayBeatVFX();
                            m_LastLoudnessVFXTime = Time.time;
                        }
                    }
                    m_WasAboveLoudnessThreshold = isAboveLoudness;
                    break;
            }

            // Áp dụng scaleMultiplier lên scale gốc đã cache
            Vector3 targetScale = Vector3.Scale(m_OriginalLocalScale, scaleMultiplier);
            m_TargetTransform.localScale = targetScale;
            m_LastAppliedScale = targetScale;
        }
        else
        {
            // Trả về tỉ lệ scale gốc nếu tắt
            m_TargetTransform.localScale = m_OriginalLocalScale;
            m_LastAppliedScale = m_OriginalLocalScale;
        }
    }
}
