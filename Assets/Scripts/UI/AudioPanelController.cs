using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AudioPanelController quản lý việc hiển thị danh sách âm thanh / nhạc cụ trong ScrollView.
/// Tự động lấy thông tin từ danh sách prefab nhạc cụ (chứa AudioConfig) để sinh các button tương ứng.
/// Hỗ trợ thêm chức năng ghi âm (Record) giọng nói/âm thanh tùy chỉnh.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPanelController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Transform Content của ScrollView để chứa các nút chọn âm thanh")]
    [SerializeField] private Transform scrollViewContent;

    [Tooltip("Prefab của nút chọn âm thanh (hỗ trợ CustomCharacterItemUI hoặc Button tiêu chuẩn)")]
    [SerializeField] private GameObject audioButtonPrefab;

    [Tooltip("Danh sách prefab nhạc cụ chứa thông tin cấu hình AudioConfig local làm fallback")]
    [SerializeField] private List<GameObject> localAudioPrefabs = new List<GameObject>();

    private List<GameObject> audioPrefabs
    {
        get
        {
            if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.InstrumentPrefabs != null && MainMenuDataManager.Instance.InstrumentPrefabs.Count > 0)
            {
                return MainMenuDataManager.Instance.InstrumentPrefabs;
            }
            return localAudioPrefabs;
        }
    }

    private IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> audioEntries
    {
        get
        {
            if (MainMenuDataManager.Instance != null)
            {
                return MainMenuDataManager.Instance.InstrumentEntries;
            }

            return MainMenuPrefabCatalog.CreateRuntimeEntries(localAudioPrefabs);
        }
    }

    [Header("Aesthetic Fallbacks (Optional)")]
    [Tooltip("Sprite nền của nút khi được chọn (dành cho Button tiêu chuẩn)")]
    [SerializeField] private Sprite selectedButtonSprite;

    [Tooltip("Sprite nền của nút khi không được chọn (dành cho Button tiêu chuẩn)")]
    [SerializeField] private Sprite unselectedButtonSprite;

    [Header("Initialization Settings")]
    [Tooltip("Tự động khởi tạo và sinh nút khi Start")]
    [SerializeField] private bool initializeOnStart = true;

    [Tooltip("Tự động chọn âm thanh đầu tiên sau khi khởi tạo thành công")]
    [SerializeField] private bool selectFirstOnStart = true;

    [Header("Audio Recording Optionals")]
    [Tooltip("Cho phép hiển thị tính năng ghi âm trong ScrollView")]
    [SerializeField] private bool enableRecording = true;

    [Tooltip("Prefab của nút ghi âm (chứa component CustomAudioRecordItemUI)")]
    [SerializeField] private GameObject audioRecordButtonPrefab;

    [Tooltip("Button record dat san trong Content cua ScrollView")]
    [SerializeField] private CustomAudioRecordItemUI recordItemButton;

    [Tooltip("Text hiển thị trạng thái đang ghi âm / tải dữ liệu")]
    [SerializeField] private TextMeshProUGUI recordingStatusText;


    [Tooltip("Nút ghi âm từ bên ngoài truyền vào")]
    [SerializeField] private Button recordButton;

    [Tooltip("Ảnh tiến trình dạng filled 360 để ghi âm")]
    [SerializeField] private Image recordProgressImage;

    /// <summary>
    /// Lưu ID bản ghi âm mới tạo trong phiên hiện tại để dọn dẹp khi cần.
    /// </summary>
    public string TempRecordedAudioId { get; set; }

    /// <summary>
    /// ID của bản ghi âm tự thu hiện có của nhân vật.
    /// </summary>
    public string ExistingCustomAudioId { get; set; }

    [Header("Events")]
    [Tooltip("Sự kiện kích hoạt khi một âm thanh nhạc cụ được chọn (truyền vào prefab gốc)")]
    public UnityEngine.Events.UnityEvent<GameObject> onAudioSelectedEvent;

    [Tooltip("Sự kiện kích hoạt khi một âm thanh nhạc cụ được chọn (truyền vào component AudioConfig tương ứng)")]
    public UnityEngine.Events.UnityEvent<AudioConfig> onAudioConfigSelectedEvent;

    [Tooltip("Sự kiện kích hoạt khi một bản ghi âm tự thu được chọn (truyền vào ID bản ghi)")]
    public UnityEngine.Events.UnityEvent<string> onCustomAudioSelectedEvent;

    // Sự kiện C# Actions để dễ đăng ký từ mã nguồn khác
    public event Action<GameObject> OnAudioSelected;
    public event Action<AudioConfig> OnAudioConfigSelected;
    public event Action<string> OnCustomAudioSelected;

    // Trạng thái lưu trữ các nút đã sinh
    private List<GameObject> _instantiatedButtons = new List<GameObject>();
    private AudioSource _previewAudioSource;
    private CustomAudioRecordItemUI _resolvedRecordItemButton;
    private int _selectionRequestVersion = 0;
    private string _selectedAudioDisplayName;

    // Trạng thái thu âm
    private string _microphoneName;
    private AudioClip _recordingClip;
    private bool _isRecording = false;
    private float _recordingStartTime;
    private const int MaxRecordingDuration = 10; // Tối đa 10 giây

    // Thuộc tính công khai để truy xuất dữ liệu đang chọn
    public GameObject SelectedPrefab { get; private set; }
    public AudioConfig SelectedAudioConfig { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public string SelectedCustomAudioId { get; private set; }
    private bool ShouldShowRecordButton => enableRecording && ResolveRecordItemUI() != null;
    private bool ShouldInstantiateRecordButton => false;

    /// <summary>
    /// Cho phép bật/tắt tính năng ghi âm từ bên ngoài.
    /// </summary>
    public bool EnableRecording
    {
        get => enableRecording;
        set => enableRecording = value;
    }

    /// <summary>
    /// Chọn âm thanh / nhạc cụ dựa trên ID (tên nhạc cụ hoặc ID bản ghi âm rec_).
    /// </summary>
    public void SelectAudioById(string audioId)
    {
        if (string.IsNullOrEmpty(audioId)) return;

        if (audioId.StartsWith("rec_"))
        {
            SelectedCustomAudioId = audioId;
            ExistingCustomAudioId = audioId;
            SelectedIndex = -1;
            SelectedPrefab = null;
            SelectedAudioConfig = null;
            _selectedAudioDisplayName = null;
        }
        else
        {
            SelectedCustomAudioId = null;
            SelectedIndex = -1;
            SelectedPrefab = null;
            SelectedAudioConfig = null;
            _selectedAudioDisplayName = null;

            IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> entries = audioEntries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                MainMenuPrefabCatalog.PrefabAssetEntry entry = entries[i];
                if (entry != null && entry.Matches(audioId))
                {
                    SelectedIndex = i;
                    SelectedPrefab = entry.DirectPrefab;
                    SelectedAudioConfig = SelectedPrefab != null ? SelectedPrefab.GetComponent<AudioConfig>() : null;
                    _selectedAudioDisplayName = entry.DisplayName;
                    break;
                }
            }
        }
    }

    private void Awake()
    {
        _previewAudioSource = GetComponent<AudioSource>();
        if (_previewAudioSource == null)
        {
            _previewAudioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (recordButton != null)
        {
            recordButton.onClick.RemoveAllListeners();
            recordButton.onClick.AddListener(OnRecordButtonClicked);
        }

        ResetRecordProgressImage();

        if (initializeOnStart)
        {
            InitializePanel();
        }
    }

    private void OnRecordButtonClicked()
    {
        if (!string.IsNullOrEmpty(ExistingCustomAudioId))
        {
            SelectCustomRecording(ExistingCustomAudioId);
            if (string.IsNullOrEmpty(ExistingCustomAudioId))
            ShowStatusText("Đã có bản ghi âm cho nhân vật. Vui lòng xóa đi để ghi âm lại!", 3f);
            return;
        }

        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording(GetRecordItemUI());
        }
    }

    /// <summary>
    /// Thiết lập ID bản ghi âm tùy chỉnh hiện tại (ví dụ khi load nhân vật có sẵn).
    /// </summary>
    public void SetCustomRecordingId(string recordingId)
    {
        SelectedCustomAudioId = recordingId;
        ExistingCustomAudioId = recordingId;
    }

    /// <summary>
    /// Thực hiện xóa các nút cũ và sinh lại các nút mới dựa trên danh sách audioPrefabs và trạng thái ghi âm.
    /// </summary>
    public void InitializePanel()
    {
        ClearPanel();

        if (scrollViewContent == null)
        {
            Debug.LogError("[AudioPanelController] Chưa gán scrollViewContent!");
            return;
        }

        // 1. Tạo nút Ghi âm ở đầu danh sách nếu tính năng được bật VÀ nhân vật đã có bản ghi âm
        SetupRecordItemButton();

        if (ShouldInstantiateRecordButton && ShouldShowRecordButton && !string.IsNullOrEmpty(ExistingCustomAudioId))
        {
            GameObject recordBtnObj = Instantiate(audioRecordButtonPrefab, scrollViewContent);
            _instantiatedButtons.Add(recordBtnObj);

            CustomAudioRecordItemUI recordItemUI = recordBtnObj.GetComponent<CustomAudioRecordItemUI>();
            if (recordItemUI != null)
            {
                recordBtnObj.name = "Btn_Audio_Record_Item";
                // Đã có ghi âm -> Hiển thị thẻ âm thanh đã ghi âm
                recordItemUI.SetupRecordedItem("Record", null, () =>
                {
                    SelectCustomRecording(ExistingCustomAudioId);
                }, () =>
                {
                    DeleteCustomRecording(ExistingCustomAudioId);
                });
            }
        }

        // 2. Tạo các nút cho các prefab nhạc cụ mặc định
        if (audioButtonPrefab == null)
        {
            Debug.LogError("[AudioPanelController] Chưa gán audioButtonPrefab!");
            return;
        }

        IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> entries = audioEntries;
        if (entries != null && entries.Count > 0)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MainMenuPrefabCatalog.PrefabAssetEntry entry = entries[i];
                if (entry == null) continue;

                // Lấy thông tin cấu hình AudioConfig từ prefab nhạc cụ
                string displayName = entry.DisplayName;
                Sprite avatar = entry.Avatar;

                // Nếu avatar trống, thử load từ Resources làm dự phòng
                if (avatar == null)
                {
                    avatar = Resources.Load<Sprite>("Avatars/" + entry.Id);
                }

                GameObject buttonObj = Instantiate(audioButtonPrefab, scrollViewContent);
                _instantiatedButtons.Add(buttonObj);
                buttonObj.name = $"Btn_Audio_{displayName}";

                // Thiết lập hiển thị và gán sự kiện click
                SetupButtonComponent(buttonObj, displayName, avatar, i);
            }
        }

        // 3. Tự động chọn mặc định
        if (selectFirstOnStart)
        {
            if (!string.IsNullOrEmpty(SelectedCustomAudioId))
            {
                // Ưu tiên chọn bản ghi âm tự thu trước nếu có
                SelectCustomRecording(SelectedCustomAudioId);
            }
            else if (entries != null && entries.Count > 0)
            {
                // Ngược lại chọn prefab nhạc cụ đầu tiên
                SelectAudio(0);
            }
        }
    }

    /// <summary>
    /// Xóa toàn bộ các nút đã được sinh ra trong ScrollView.
    /// </summary>
    public void ClearPanel()
    {
        if (_previewAudioSource != null)
        {
            _previewAudioSource.Stop();
        }

        // Reset AudioSource trên MusicSyncManager để tránh theo dõi AudioSource đã dừng phát
        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.SetAudioSource(null);
        }

        if (scrollViewContent != null)
        {
            for (int i = _instantiatedButtons.Count - 1; i >= 0; i--)
            {
                if (_instantiatedButtons[i] != null)
                {
                    Destroy(_instantiatedButtons[i]);
                }
            }
        }

        _instantiatedButtons.Clear();
        SelectedPrefab = null;
        SelectedAudioConfig = null;
        SelectedIndex = -1;
        _selectedAudioDisplayName = null;
        // Chú ý: Không xóa SelectedCustomAudioId ở đây để bảo toàn ID âm thanh khi reload UI panel
    }

    /// <summary>
    /// Chọn âm thanh nhạc cụ theo Index trong danh sách.
    /// </summary>
    public void SelectAudio(int index)
    {
        SelectAudioAsync(index);
    }

    private async void SelectAudioAsync(int index)
    {
        IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> entries = audioEntries;
        if (index < 0 || entries == null || index >= entries.Count)
        {
            Debug.LogWarning($"[AudioPanelController] Index {index} vượt quá giới hạn danh sách audio.");
            return;
        }

        MainMenuPrefabCatalog.PrefabAssetEntry selectedEntry = entries[index];
        if (selectedEntry == null) return;

        int requestVersion = ++_selectionRequestVersion;
        SelectedIndex = index;
        SelectedPrefab = null;
        SelectedAudioConfig = null;
        SelectedCustomAudioId = null; // Bỏ chọn âm thanh tự thu

        // Cập nhật trạng thái hiển thị được chọn/không được chọn trên UI
        _selectedAudioDisplayName = selectedEntry.DisplayName;
        UpdateSelectionVisuals();

        // Phát thử âm thanh (Preview)
        GameObject selectedPrefab = null;
        if (MainMenuDataManager.Instance != null)
        {
            selectedPrefab = await MainMenuDataManager.Instance.LoadInstrumentPrefabAsync(selectedEntry.Id);
        }
        else
        {
            selectedPrefab = selectedEntry.DirectPrefab;
        }

        if (requestVersion != _selectionRequestVersion)
        {
            return;
        }

        if (selectedPrefab == null)
        {
            Debug.LogWarning($"[AudioPanelController] Khong load duoc prefab nhac cu: {selectedEntry.Id}");
            return;
        }

        SelectedPrefab = selectedPrefab;
        SelectedAudioConfig = selectedPrefab.GetComponent<AudioConfig>();

        PlayPreviewSound();

        // Kích hoạt các sự kiện thông báo
        OnAudioSelected?.Invoke(SelectedPrefab);
        onAudioSelectedEvent?.Invoke(SelectedPrefab);

        if (SelectedAudioConfig != null)
        {
            OnAudioConfigSelected?.Invoke(SelectedAudioConfig);
            onAudioConfigSelectedEvent?.Invoke(SelectedAudioConfig);
        }

        Debug.Log($"[AudioPanelController] Đã chọn nhạc cụ/âm thanh: {displayNameForSelected()} (Index: {SelectedIndex})");
    }

    /// <summary>
    /// Chọn âm thanh tự thu (Custom Audio) của người dùng.
    /// </summary>
    public void SelectCustomRecording(string recordingId)
    {
        if (string.IsNullOrEmpty(recordingId)) return;

        SelectedCustomAudioId = recordingId;
        _selectionRequestVersion++;
        SelectedIndex = -1;
        SelectedPrefab = null;
        SelectedAudioConfig = null;
        _selectedAudioDisplayName = null;

        // Cập nhật hiển thị các nút
        UpdateSelectionVisuals();

        // Preview âm thanh tự thu cục bộ
        PlayPreviewSound();

        // Kích hoạt sự kiện
        OnCustomAudioSelected?.Invoke(recordingId);
        onCustomAudioSelectedEvent?.Invoke(recordingId);

        // Kích hoạt sự kiện với prefab/config là null để thông báo chọn custom audio
        OnAudioSelected?.Invoke(null);
        onAudioSelectedEvent?.Invoke(null);
        OnAudioConfigSelected?.Invoke(null);
        onAudioConfigSelectedEvent?.Invoke(null);

        Debug.Log($"[AudioPanelController] Đã chọn Audio tự thu: {recordingId}");
    }

    /// <summary>
    /// Phát thử âm thanh của nhạc cụ hoặc bản ghi âm đang được chọn.
    /// </summary>
    private async void PlayPreviewSound()
    {
        if (_previewAudioSource == null) return;
        _previewAudioSource.Stop();

        // TH 1: Phát âm thanh tự thu (Tải & giải mã cục bộ)
        if (!string.IsNullOrEmpty(SelectedCustomAudioId))
        {
            ShowStatusText("Loading audio...");

            try
            {
                var recordings = await MainMenuDataManager.Instance.GetRecordingsAsync();
                var targetRec = recordings.Find(r => r.recordingId == SelectedCustomAudioId);

                HideStatusText();

                if (targetRec != null && !string.IsNullOrEmpty(targetRec.audioBase64))
                {
                    byte[] wavBytes = Convert.FromBase64String(targetRec.audioBase64);

                    // Giải mã AES
                    try
                    {
                        wavBytes = AudioEncryption.Decrypt(wavBytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[AudioPanelController] Giải mã âm thanh cũ không thành công, phát trực tiếp: " + ex.Message);
                    }

                    AudioClip clip = WavUtility.ToAudioClip(wavBytes, targetRec.name);
                    if (clip != null)
                    {
                        _previewAudioSource.clip = clip;
                        _previewAudioSource.Play();
                        // Thông báo MusicSyncManager phát VFX theo âm thanh preview này
                        if (MusicSyncManager.Instance != null)
                            MusicSyncManager.Instance.SetAudioSource(_previewAudioSource);
                        StartCoroutine(DestroyClipAfterPlay(clip));
                    }
                }
                else
                {
                    Debug.LogWarning($"[AudioPanelController] Không tìm thấy bản ghi ID {SelectedCustomAudioId} cục bộ.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AudioPanelController] Lỗi khi tải preview bản thu âm: " + ex.Message);
                HideStatusText();
            }
        }
        // TH 2: Phát âm thanh từ Prefab nhạc cụ
        // Nhạc cụ preset được phát trực tiếp từ AudioSource trên model nhạc cụ (SpawnInstrumentPreview)
        // nên AudioPanelController không cần phát duplicate — chỉ dừng audio cũ nếu có
        else if (SelectedAudioConfig != null)
        {
            // AudioSource trên model đã phát (xem SpawnInstrumentPreview), không cần phát thêm
            Debug.Log($"[AudioPanelController] Nhạc cụ preset '{SelectedPrefab?.name}' đang phát từ AudioSource của model.");
        }
    }

    private IEnumerator DestroyClipAfterPlay(AudioClip clip)
    {
        yield return new WaitForSeconds(clip.length + 0.5f);
        if (_previewAudioSource.clip == clip)
        {
            _previewAudioSource.clip = null;
        }
        Destroy(clip);
        GC.Collect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Microphone Recording Logic
    // ─────────────────────────────────────────────────────────────────────────

    private CustomAudioRecordItemUI ResolveRecordItemUI()
    {
        if (recordItemButton != null)
        {
            _resolvedRecordItemButton = recordItemButton;
            return _resolvedRecordItemButton;
        }

        if (_resolvedRecordItemButton != null)
        {
            return _resolvedRecordItemButton;
        }

        if (scrollViewContent != null)
        {
            _resolvedRecordItemButton = scrollViewContent.GetComponentInChildren<CustomAudioRecordItemUI>(true);
        }

        return _resolvedRecordItemButton;
    }

    private void SetupRecordItemButton()
    {
        CustomAudioRecordItemUI recordItem = ResolveRecordItemUI();
        if (recordItem == null) return;

        if (!enableRecording)
        {
            recordItem.gameObject.SetActive(false);
            return;
        }

        recordItem.gameObject.SetActive(true);
        recordItem.transform.SetAsFirstSibling();
        recordItem.SetupRecordButton(
            !string.IsNullOrEmpty(ExistingCustomAudioId),
            OnRecordButtonClicked,
            () => DeleteCustomRecording(ExistingCustomAudioId)
        );
    }

    private CustomAudioRecordItemUI GetRecordItemUI()
    {
        return ResolveRecordItemUI();
    }

    private void ConfigureRecordProgressImage()
    {
        if (recordProgressImage == null) return;

        recordProgressImage.type = Image.Type.Filled;
        recordProgressImage.fillMethod = Image.FillMethod.Horizontal;
        recordProgressImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        recordProgressImage.fillClockwise = true;
    }

    private void SetRecordProgress(float progress, bool visible)
    {
        if (recordProgressImage == null) return;

        ConfigureRecordProgressImage();
        recordProgressImage.gameObject.SetActive(visible);
        recordProgressImage.fillAmount = Mathf.Clamp01(progress);
    }

    private void ResetRecordProgressImage()
    {
        SetRecordProgress(0f, false);
    }

    private void StopAllAudioBeforeRecording()
    {
        if (_previewAudioSource != null)
        {
            _previewAudioSource.Stop();
        }

        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.SetAudioSource(null);
        }

        AudioSource[] audioSources = Resources.FindObjectsOfTypeAll<AudioSource>();
        foreach (AudioSource source in audioSources)
        {
            if (source == null || source.gameObject == null)
            {
                continue;
            }

            if (!source.gameObject.scene.IsValid())
            {
                continue;
            }

            source.Stop();
        }
    }

    private void StartRecording(CustomAudioRecordItemUI recordItem)
    {
        if (recordItem == null)
        {
            recordItem = GetRecordItemUI();
        }

        StopAllAudioBeforeRecording();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[AudioPanelController] Không tìm thấy thiết bị Microphone nào!");
            ShowStatusText("No Microphone Found!", 2f);
            return;
        }

        _microphoneName = Microphone.devices[0];
        _recordingClip = Microphone.Start(_microphoneName, false, MaxRecordingDuration, 44100);
        if (_recordingClip == null)
        {
            Debug.LogError("[AudioPanelController] Microphone.Start failed.");
            ResetRecordProgressImage();
            ShowStatusText("Recording Failed!", 2f);
            InitializePanel();
            return;
        }

        _isRecording = true;
        _recordingStartTime = Time.time;

        ShowStatusText("Recording: 0s");

        // Vô hiệu hóa panel trong khi ghi âm để tránh click loạn
        SetPanelInteractive(false);
        SetRecordProgress(0f, true);

        StartCoroutine(RecordingRoutine(recordItem));
    }

    private void SetPanelInteractive(bool interactive)
    {
        // Vô hiệu hóa click của tất cả các button trong ScrollView
        CustomAudioRecordItemUI recordItem = ResolveRecordItemUI();
        if (recordItem != null)
        {
            Button recordBtn = recordItem.GetComponent<Button>();
            if (recordBtn != null) recordBtn.interactable = interactive;
        }

        foreach (var btnObj in _instantiatedButtons)
        {
            var btn = btnObj.GetComponent<Button>();
            if (btn != null) btn.interactable = interactive;

            var customUI = btnObj.GetComponent<CustomCharacterItemUI>();
            if (customUI != null)
            {
                var b = customUI.GetComponent<Button>();
                if (b != null) b.interactable = interactive;
            }
        }
    }

    private IEnumerator RecordingRoutine(CustomAudioRecordItemUI recordItem)
    {
        float duration = (float)MaxRecordingDuration;
        float elapsed = 0f;

        if (recordItem != null)
        {
            recordItem.SetRecordingState(true);
        }

        SetRecordProgress(0f, true);

        while (elapsed < duration && _isRecording)
        {
            yield return null;
            elapsed = Time.time - _recordingStartTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (recordItem != null)
            {
                recordItem.SetRadialFill(progress);
            }

            SetRecordProgress(progress, true);

            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Recording: {(int)elapsed}s";
            }
        }

        if (recordItem != null)
        {
            recordItem.SetRadialFill(1f);
        }

        SetRecordProgress(1f, true);

        StopRecording();
    }

    private async void StopRecording()
    {
        if (!_isRecording) return;

        int lastSamplePos = Microphone.GetPosition(_microphoneName);
        Microphone.End(_microphoneName);
        _isRecording = false;

        SetPanelInteractive(true);

        ShowStatusText("Saving audio...");

        if (lastSamplePos <= 0)
        {
            Debug.LogWarning("[AudioPanelController] Ghi âm trống!");
            HideStatusText();
            if (_recordingClip != null) Destroy(_recordingClip);
            ResetRecordProgressImage();
            InitializePanel();
            return;
        }

        int channels = _recordingClip.channels;
        int frequency = _recordingClip.frequency;
        float[] samples = new float[lastSamplePos * channels];
        _recordingClip.GetData(samples, 0);

        AudioClip trimmedClip = AudioClip.Create("TempTrimmed", lastSamplePos, channels, frequency, false);
        trimmedClip.SetData(samples, 0);

        byte[] wavBytes = WavUtility.FromAudioClip(trimmedClip);

        Destroy(_recordingClip);
        Destroy(trimmedClip);

        if (wavBytes == null)
        {
            Debug.LogError("[AudioPanelController] Lỗi mã hóa WAV bytes!");
            HideStatusText();
            ResetRecordProgressImage();
            InitializePanel();
            return;
        }

        // Mã hóa AES
        byte[] encryptedBytes;
        try
        {
            encryptedBytes = AudioEncryption.Encrypt(wavBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError("[AudioPanelController] Lỗi mã hóa âm thanh: " + ex.Message);
            encryptedBytes = wavBytes; // Fallback
        }

        string base64 = Convert.ToBase64String(encryptedBytes);
        wavBytes = null;
        encryptedBytes = null;
        GC.Collect();

        string recId = "rec_" + DateTime.UtcNow.Ticks;
        string recName = "Rec " + DateTime.Now.ToString("HH:mm:ss");

        bool success = await MainMenuDataManager.Instance.SaveRecordingAsync(recId, recName, base64);
        base64 = null;
        GC.Collect();

        ResetRecordProgressImage();

        if (success)
        {
            ShowStatusText("Saved successfully!", 1.5f);
            SelectedCustomAudioId = recId;
            ExistingCustomAudioId = recId;
            TempRecordedAudioId = recId;
            InitializePanel(); // Tải lại để hiển thị Recorded Item
            SelectCustomRecording(recId); // Tự động chọn bản ghi âm vừa thu
        }
        else
        {
            ShowStatusText("Save Failed!", 1.5f);
            InitializePanel();
        }
    }

    private async void DeleteCustomRecording(string recordingId)
    {
        if (string.IsNullOrEmpty(recordingId)) return;

        ShowStatusText("Deleting recording...");

        bool success = await MainMenuDataManager.Instance.DeleteRecordingAsync(recordingId);

        if (success)
        {
            ShowStatusText("Deleted!", 1.5f);
            SelectedCustomAudioId = null;
            ExistingCustomAudioId = null;
            if (TempRecordedAudioId == recordingId)
            {
                TempRecordedAudioId = null;
            }

            // Nếu âm thanh đang chọn là bản ghi âm bị xóa, tự động chọn nhạc cụ mặc định đầu tiên
            IReadOnlyList<MainMenuPrefabCatalog.PrefabAssetEntry> entries = audioEntries;
            if (entries != null && entries.Count > 0)
            {
                SelectAudio(0);
            }
            else
            {
                SelectedIndex = -1;
                SelectedPrefab = null;
                SelectedAudioConfig = null;
                OnAudioSelected?.Invoke(null);
            }
            InitializePanel();
        }
        else
        {
            ShowStatusText("Delete Failed!", 1.5f);
            InitializePanel();
        }
    }

    /// <summary>
    /// Xóa bản ghi âm một cách âm thầm không hiển thị status UI hay cập nhật panel
    /// </summary>
    public void DeleteCustomRecordingSilently(string recordingId)
    {
        if (string.IsNullOrEmpty(recordingId)) return;
        _ = MainMenuDataManager.Instance.DeleteRecordingAsync(recordingId);
        ExistingCustomAudioId = null;
        if (TempRecordedAudioId == recordingId)
        {
            TempRecordedAudioId = null;
        }
    }

    private void ShowStatusText(string message, float duration = 0f)
    {
        if (recordingStatusText != null)
        {
            recordingStatusText.text = message;
            recordingStatusText.gameObject.SetActive(true);

            if (duration > 0f)
            {
                CancelInvoke(nameof(HideStatusText));
                Invoke(nameof(HideStatusText), duration);
            }
        }
    }

    private void HideStatusText()
    {
        if (recordingStatusText != null)
        {
            recordingStatusText.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thiết lập hiển thị và sự kiện click cho nút nhạc cụ mặc định.
    /// </summary>
    private void SetupButtonComponent(GameObject buttonObj, string displayName, Sprite avatar, int index)
    {
        // TH 1: Sử dụng CustomCharacterItemUI (khuyên dùng vì đồng bộ giao diện)
        var customUI = buttonObj.GetComponent<CustomCharacterItemUI>();
        if (customUI != null)
        {
            customUI.Setup(displayName, avatar, () => SelectAudio(index));
            return;
        }

        // TH 2: Sử dụng Button tiêu chuẩn của Unity
        Button standardBtn = buttonObj.GetComponent<Button>();
        if (standardBtn != null)
        {
            standardBtn.onClick.RemoveAllListeners();
            standardBtn.onClick.AddListener(() => SelectAudio(index));

            // Tìm và gán text tên nhạc cụ
            var nameText = buttonObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameText != null)
            {
                nameText.text = displayName;
            }
            else
            {
                var legacyText = buttonObj.GetComponentInChildren<Text>(true);
                if (legacyText != null)
                {
                    legacyText.text = displayName;
                }
            }

            // Tìm và gán ảnh đại diện (bỏ qua Image của chính nút)
            Image[] images = buttonObj.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != buttonObj)
                {
                    img.sprite = avatar;
                    img.gameObject.SetActive(avatar != null);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Cập nhật trạng thái hiển thị của các nút trong danh sách.
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        CustomAudioRecordItemUI recordItem = ResolveRecordItemUI();
        if (recordItem != null)
        {
            recordItem.SetSelected(!string.IsNullOrEmpty(SelectedCustomAudioId));
        }

        for (int i = 0; i < _instantiatedButtons.Count; i++)
        {
            GameObject buttonObj = _instantiatedButtons[i];
            if (buttonObj == null) continue;

            // TH 1: CustomCharacterItemUI (cho các prefab nhạc cụ thông thường)
            var customUI = buttonObj.GetComponent<CustomCharacterItemUI>();
            if (customUI != null)
            {
                int prefabOffset = 0;
                int prefabIndex = i - prefabOffset;

                bool isSelected = (prefabIndex == SelectedIndex);
                customUI.SetSelected(isSelected);
                continue;
            }

            // TH 2: CustomAudioRecordItemUI (cho nút ghi âm ở đầu danh sách)
            var recordItemUI = buttonObj.GetComponent<CustomAudioRecordItemUI>();
            if (recordItemUI != null)
            {
                bool isSelected = !string.IsNullOrEmpty(SelectedCustomAudioId);
                recordItemUI.SetSelected(isSelected);
                continue;
            }

            // TH 3: Button tiêu chuẩn (dự phòng)
            Button standardBtn = buttonObj.GetComponent<Button>();
            if (standardBtn != null)
            {
                int prefabOffset = 0;
                int prefabIndex = i - prefabOffset;
                bool isSelected = (prefabIndex == SelectedIndex);

                Image btnImage = standardBtn.image != null ? standardBtn.image : standardBtn.GetComponent<Image>();
                if (btnImage != null)
                {
                    Sprite targetSprite = isSelected ? selectedButtonSprite : unselectedButtonSprite;
                    if (targetSprite != null)
                    {
                        btnImage.sprite = targetSprite;
                    }
                }
            }
        }
    }

    private string displayNameForSelected()
    {
        if (!string.IsNullOrEmpty(SelectedCustomAudioId))
        {
            return "Custom Audio Recording";
        }
        if (SelectedAudioConfig != null && !string.IsNullOrEmpty(SelectedAudioConfig.Name))
        {
            return SelectedAudioConfig.Name;
        }
        if (!string.IsNullOrEmpty(_selectedAudioDisplayName))
        {
            return _selectedAudioDisplayName;
        }
        return SelectedPrefab != null ? SelectedPrefab.name : "None";
    }
}
