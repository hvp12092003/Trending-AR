using UnityEngine;

/// <summary>
/// Controls 2 effects for the character model:
/// 1. Floating - position oscillation using Sine wave
/// 2. Audio Reactive - scale pulsing + VFX spawning based on audio volume
/// </summary>
public class ModelLoopEffects : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Transform of the model to apply effects. If empty, auto-grabs the first child object.")]
    public Transform targetTransform;

    // ───────────────────────────────────────────────
    [Header("1. Floating Settings")]
    [Tooltip("Enable or disable floating effect.")]
    public bool enableFloating = true;

    [Tooltip("Oscillation speed (Hz). Higher = floats faster.")]
    [Range(0.1f, 10f)]
    public float floatingSpeed = 2f;

    [Tooltip("Floating amplitude per axis (meters). Usually only Y is used.")]
    public Vector3 floatingAmplitude = new Vector3(0f, 0.05f, 0f);

    // ───────────────────────────────────────────────
    [Header("2. Audio Reactive Settings")]
    [Tooltip("Enable or disable scaling effect based on audio.")]
    public bool enableAudioReactive = true;

    [Tooltip("AudioSource to listen to. If empty, auto-resolves from self or surrounding components.")]
    public AudioSource audioSource;

    [Tooltip("Sensitivity to audio. Higher = pulses more with quiet audio.")]
    [Range(1f, 30f)]
    public float sensitivity = 8f;

    [Tooltip("Maximum scale added to the original scale per axis.\nE.g., (0.3, 0.5, 0.3) = stretches more on Y than X/Z.")]
    public Vector3 maxScaleAdd = new Vector3(0.3f, 0.5f, 0.3f);

    // ───────────────────────────────────────────────
    [Header("VFX Beat Settings")]
    [Tooltip("Particle system prefab to spawn on audio peaks.")]
    public ParticleSystem beatVFXPrefab;

    [Tooltip("Volume threshold to trigger VFX (0 to 1+). Lower = triggers easier.")]
    [Range(0f, 1f)]
    public float vfxLoudnessThreshold = 0.08f;

    [Tooltip("Minimum cooldown (seconds) between VFX triggers. Lower = spawns more frequently.")]
    [Range(0.05f, 2f)]
    public float vfxCooldown = 0.3f;

    [Tooltip("Initial number of VFX instances to spawn in the pool.")]
    public int vfxPoolSize = 5;

    [Tooltip("Parent transform for the VFX pool. If empty, auto-uses self.")]
    public Transform vfxPoolParent;

    // ───────────────────────────────────────────────
    [Header("Advanced Tuning")]
    [Tooltip("Noise threshold (signals below this are ignored).")]
    [Range(0f, 0.05f)]
    public float noiseThreshold = 0.005f;

    [Tooltip("Interpolation speed when volume INCREASES (attack). Higher = more responsive.")]
    [Range(5f, 60f)]
    public float attackSpeed = 30f;

    [Tooltip("Interpolation speed when volume DECREASES (release). Lower = effect lingers longer.")]
    [Range(1f, 30f)]
    public float releaseSpeed = 10f;

    // Compatibility Property to prevent compilation errors in CustomCharacterPanelController
    public AudioSource TargetAudioSource 
    { 
        get => audioSource; 
        set => audioSource = value; 
    }

    // ───────────────────────────────────────────────
    //  Runtime state (private)
    private Vector3 _origPos;
    private Vector3 _origScale;
    private Vector3 _lastPos;
    private Vector3 _lastScale;
    private bool _initialized;

    private float _currentLoudness;
    private float _prevLoudness;
    private float _lastVFXTime;

    private readonly float[] _samples = new float[1024];
    private System.Collections.Generic.Queue<ParticleSystem> _vfxPool
        = new System.Collections.Generic.Queue<ParticleSystem>();

    private AudioSource _overrideSource;
    private AudioSource _originalSource;

    // ───────────────────────────────────────────────
    private void Awake()
    {
        // 1. Target Transform tự lấy object con đầu tiên, nếu không có con thì lấy chính nó
        if (targetTransform == null)
        {
            targetTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        // 2. VFX pool parent tự động lấy chính transform của script này nếu trống
        if (vfxPoolParent == null)
        {
            vfxPoolParent = transform;
        }

        _origPos   = targetTransform.localPosition;
        _origScale = targetTransform.localScale;
        _lastPos   = _origPos;
        _lastScale = _origScale;
        _initialized = true;
    }

    private void Start()
    {
        BuildVFXPool();
        if (audioSource == null) ResolveAudioSource();

        // Auto register to BandAudioManager
        if (BandAudioManager.Instance != null)
        {
            BandAudioManager.Instance.RegisterModelLoopEffect(this);
        }
    }

    private void OnDestroy()
    {
        // Unregister to prevent memory leaks
        if (BandAudioManager.Instance != null)
        {
            BandAudioManager.Instance.UnregisterModelLoopEffect(this);
        }
    }

    private void LateUpdate()
    {
        if (!_initialized || targetTransform == null) return;

        // Detect if other scripts changed our transform externally
        if ((targetTransform.localPosition - _lastPos).sqrMagnitude > 0.000001f)
            _origPos = targetTransform.localPosition;
        if ((targetTransform.localScale - _lastScale).sqrMagnitude > 0.000001f)
            _origScale = targetTransform.localScale;

        // 1. Floating
        if (enableFloating)
        {
            float t = Time.time * floatingSpeed;
            Vector3 offset = new Vector3(
                Mathf.Sin(t) * floatingAmplitude.x,
                Mathf.Sin(t) * floatingAmplitude.y,
                Mathf.Sin(t) * floatingAmplitude.z);
            Vector3 pos = _origPos + offset;
            targetTransform.localPosition = pos;
            _lastPos = pos;
        }
        else
        {
            targetTransform.localPosition = _origPos;
            _lastPos = _origPos;
        }

        // 2. Audio Reactive
        if (enableAudioReactive)
        {
            float loudness = SampleLoudness();

            float speed = loudness > _currentLoudness ? attackSpeed : releaseSpeed;
            _currentLoudness = Mathf.Lerp(_currentLoudness, loudness, Time.deltaTime * speed);

            // Scale
            Vector3 scaleAdd = maxScaleAdd * _currentLoudness;
            Vector3 newScale = _origScale + Vector3.Scale(_origScale, scaleAdd);
            targetTransform.localScale = newScale;
            _lastScale = newScale;

            // VFX - Peak Detection
            bool aboveThreshold = _currentLoudness >= vfxLoudnessThreshold;
            bool isPeak = aboveThreshold && (_currentLoudness < _prevLoudness);
            if (isPeak && Time.time - _lastVFXTime >= vfxCooldown)
            {
                PlayVFX();
                _lastVFXTime = Time.time;
            }
            _prevLoudness = _currentLoudness;
        }
        else
        {
            targetTransform.localScale = _origScale;
            _lastScale = _origScale;
        }
    }

    // ───────────────────────────────────────────────
    private float SampleLoudness()
    {
        AudioSource src = _overrideSource ?? audioSource;
        if (src == null && MusicSyncManager.Instance != null)
            src = MusicSyncManager.Instance.AudioSource;

        if (src == null || !src.isPlaying) return 0f;

        src.GetOutputData(_samples, 0);
        float sum = 0f;
        for (int i = 0; i < _samples.Length; i++)
            sum += _samples[i] * _samples[i];

        float rms = Mathf.Sqrt(sum / _samples.Length) * sensitivity;
        return rms < noiseThreshold ? 0f : rms;
    }

    private void ResolveAudioSource()
    {
        // 1. Tự lấy AudioSource từ chính bản thân trước
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null) return;

        // 2. Nếu không có, tìm trong CastAudioData
        CastAudioData cast = GetComponent<CastAudioData>()
            ?? GetComponentInParent<CastAudioData>()
            ?? GetComponentInChildren<CastAudioData>();
        if (cast != null && cast.preparedSource != null)
        {
            audioSource = cast.preparedSource;
            return;
        }

        // 3. Fallback tìm AudioSource ở cha hoặc con
        audioSource = GetComponentInParent<AudioSource>()
            ?? GetComponentInChildren<AudioSource>();
    }

    // ───────────────────────────────────────────────
    private void BuildVFXPool()
    {
        if (beatVFXPrefab == null) return;
        Transform parent = vfxPoolParent != null ? vfxPoolParent : transform;
        for (int i = 0; i < vfxPoolSize; i++)
        {
            var ps = Instantiate(beatVFXPrefab, parent);
            ps.gameObject.SetActive(false);
            _vfxPool.Enqueue(ps);
        }
    }

    private void PlayVFX()
    {
        if (PlayerPrefs.GetInt("Setting_VFX", 1) == 0) return;
        if (beatVFXPrefab == null) return;

        ParticleSystem ps = null;
        while (_vfxPool.Count > 0)
        {
            ps = _vfxPool.Dequeue();
            if (ps != null) break;
        }
        if (ps == null)
        {
            Transform parent = vfxPoolParent != null ? vfxPoolParent : transform;
            ps = Instantiate(beatVFXPrefab, parent);
        }

        ps.transform.position = targetTransform != null ? targetTransform.position : transform.position;
        ps.transform.localRotation = Quaternion.identity;
        ps.transform.localScale = Vector3.one;

        ps.gameObject.SetActive(true);
        ps.Stop(true);
        ps.Clear(true);
        ps.Play(true);

        StartCoroutine(ReturnToPool(ps));
    }

    private System.Collections.IEnumerator ReturnToPool(ParticleSystem ps)
    {
        yield return new WaitUntil(() => ps == null || !ps.IsAlive(true));
        if (ps != null)
        {
            ps.gameObject.SetActive(false);
            _vfxPool.Enqueue(ps);
        }
    }

    // ───────────────────────────────────────────────
    //  Public API - used by BandAudioManager
    /// <summary>Switches to the full band song AudioSource.</summary>
    public void SetAudioSourceForBandFull(AudioSource fullSongSource)
    {
        if (fullSongSource == null) return;
        if (_overrideSource == null) _originalSource = audioSource;
        _overrideSource = fullSongSource;
    }

    /// <summary>Resets back to the original individual instrument AudioSource.</summary>
    public void ResetAudioSourceToInstrument()
    {
        _overrideSource = null;
        audioSource = _originalSource;
    }

    /// <summary>Re-caches origin position/scale from outside (call after spawning/moving).</summary>
    public void RefreshOrigin()
    {
        if (targetTransform == null) return;
        _origPos   = targetTransform.localPosition;
        _origScale = targetTransform.localScale;
    }
}
