using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý giao diện và logic của Custom Character Creator Panel.
/// Bao gồm chuyển tab, ghi âm giọng nói, lưu nhân vật lên Firestore và load scene AR/Non-AR tương ứng.
/// </summary>
public class CustomCharacterPanelController : MonoBehaviour
{
    [Header("Panel Navigation")]
    [SerializeField] private Button backButton;

    [Header("Tab Title Text")]
    [SerializeField] private TextMeshProUGUI titleText; // "NAME", "INSTRUMENT", "ANIMATION"

    [Header("ScrollView Containers")]
    [SerializeField] private GameObject characterScrollView;
    [SerializeField] private GameObject instrumentScrollView;
    [SerializeField] private GameObject animationScrollView;

    [Header("Cast Panel Integration")]
    [SerializeField] private CastPanelController castPanelController;

    [Header("Audio Panel Integration")]
    [SerializeField] private AudioPanelController audioPanelController;

    [Header("Animation Panel Integration")]
    [SerializeField] private AnimPanelController animPanelController;

    [Header("Preview Settings")]
    [SerializeField] private Transform previewContainer;

    [Header("Recording Status")]
    [SerializeField] private TextMeshProUGUI recordingStatusText;

    [Header("Navigation Buttons")]
    [Tooltip("Nút mũi tên TRÁI — về tab trước")]
    [SerializeField] private Button prevButton;
    [Tooltip("Nút mũi tên PHẢI — sang tab tiếp theo")]
    [SerializeField] private Button nextButton;
    [Tooltip("Nút START/SAVE ở tab Animation (chỉ hiện ở tab 3)")]
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI startButtonText;
    [SerializeField] private Button skipButton; // Nút SKIP bỏ qua lưu và vào thẳng AR

    [Header("Studio Popup")]
    [SerializeField] private PopupStudio popupStudio;

    [Header("Progress Bar")]
    [SerializeField] private CustomProgressController progressController;

    // Trạng thái hiện tại của việc thiết kế nhân vật
    private string _selectedPrefabName;
    private string _selectedAudioId;
    private string _selectedAnimationId;

    private GameObject _previewInstance;
    private GameObject _previewInstrumentInstance;
    private AudioSource _previewAudioSource;

    private enum CustomTab { Character, Instrument, Animation }
    private CustomTab _currentTab = CustomTab.Character;

    private bool _isCharacterSaved = false;
    private CastData _savedCastData = null;

    private void MarkAsUnsaved()
    {
        _isCharacterSaved = false;
        _savedCastData = null;
        UpdateStartButtonText();
    }

    /// <summary>
    /// Cập nhật text của nút START:
    /// - Chưa lưu → "SAVE"
    /// - Đã lưu xong → "START"
    /// </summary>
    private void UpdateStartButtonText()
    {
        if (startButtonText == null) return;
        startButtonText.text = _isCharacterSaved ? "START" : "SAVE";
    }

    /// <summary>
    /// Cập nhật trạng thái hiển thị của thanh tiến trình (Process).
    /// </summary>
    private void UpdateProgressBar(bool animate = true)
    {
        if (progressController != null)
        {
            bool hasChar = !string.IsNullOrEmpty(_selectedPrefabName);
            bool hasInstrument = !string.IsNullOrEmpty(_selectedAudioId);

            int currentStep = 1;
            if (_currentTab == CustomTab.Instrument && hasChar)
            {
                currentStep = 2;
            }
            else if (_currentTab == CustomTab.Animation && hasChar && hasInstrument)
            {
                currentStep = 3;
            }

            progressController.SetProgress(currentStep, animate);
        }
    }

    /// <summary>
    /// Cập nhật hiển thị các nút điều hướng dựa theo tab hiện tại.
    /// </summary>
    private void UpdateNavigationButtons()
    {
        // prevButton: ẩn ở tab 1, hiện ở tab 2 và 3
        if (prevButton != null)
            prevButton.gameObject.SetActive(_currentTab != CustomTab.Character);

        // nextButton: hiện ở tab 1 và 2, ẩn ở tab 3
        if (nextButton != null)
            nextButton.gameObject.SetActive(_currentTab != CustomTab.Animation);

        // startButton: chỉ hiện ở tab 3
        if (startButton != null)
            startButton.gameObject.SetActive(_currentTab == CustomTab.Animation);

        UpdateStartButtonText();
    }

    private bool _shouldTransitionAfterSave = false; // Có chuyển scene sau khi lưu xong hay không

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private float slideOffset = 250f;

    [Header("Merged Scene Config")]
    [SerializeField] private string customArSceneName = "Custome AR Scene";
    [SerializeField] private string customNonArSceneName = "Custome NonAR Scene";

    [Header("In-Scene Canvas Groups")]
    [SerializeField] private GameObject uiCustome;
    [SerializeField] private GameObject uiAr;
    [SerializeField] private GameObject previewCamera;
    
    [Header("UI Hide/Show Settings (Merged from UIController)")]
    [SerializeField] private Button hideUiButton;
    [SerializeField] private Button showUiButton;
    [SerializeField] private List<GameObject> arUiPanels = new List<GameObject>(); // Danh sách các UI AR cần ẩn khi quay màn hình

    [Header("Transition Settings (Black Screen Overlay)")]
    [SerializeField] private bool useBlackScreenTransition = false; // Chọn true nếu muốn chớp đen toàn màn hình
    [SerializeField] private Color transitionColor = new Color(0f, 0f, 0f, 1f); // Màu chuyển cảnh (Mặc định là đen)
    [SerializeField] private float fadeOutDuration = 0.2f; // Thời gian chuyển dần sang màu tối
    [SerializeField] private float fadeInDuration = 0.25f; // Thời gian trả về trong suốt

    [Header("Transition Settings (Direct UI Fade & Scale)")]
    [SerializeField] private float uiFadeDuration = 0.35f; // Thời gian mờ/hiện giao diện
    [SerializeField] private Ease uiFadeEase = Ease.OutQuad; // Loại ease khi mờ dần
    [SerializeField] private Ease uiShowEase = Ease.OutBack; // Loại ease khi hiển thị lại (nảy nhẹ)

    private Dictionary<GameObject, bool> m_UiActiveStates = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, Vector3> m_UiOriginalScales = new Dictionary<GameObject, Vector3>();
    private Image m_FadeOverlay;

    private CanvasGroup _characterCG;
    private CanvasGroup _instrumentCG;
    private CanvasGroup _animationCG;

    private RectTransform _characterRect;
    private RectTransform _instrumentRect;
    private RectTransform _animationRect;

    private Vector2 _characterOrigPos;
    private Vector2 _instrumentOrigPos;
    private Vector2 _animationOrigPos;

    private bool _isFirstSwitch = true;

    private void Awake()
    {
        AutoAssignMissingReferences();

        // Tự động tìm kiếm các panel controller (kể cả khi GameObject đang ẩn/inactive) nếu chưa được gán trong Inspector
        if (castPanelController == null)
        {
            castPanelController = FindComponentInScene<CastPanelController>();
        }

        if (audioPanelController == null)
        {
            audioPanelController = FindComponentInScene<AudioPanelController>();
        }

        if (animPanelController == null)
        {
            animPanelController = FindComponentInScene<AnimPanelController>();
        }

        // Khởi tạo CanvasGroup và vị trí gốc cho các panel để phục vụ chuyển tab mượt mà
        InitCanvasGroups();



        // Nút BACK
        if (backButton != null) backButton.onClick.AddListener(ClosePanel);

        // Nút điều hướng mũi tên
        if (prevButton != null) prevButton.onClick.AddListener(OnPrevButtonClicked);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextButtonClicked);

        // Nút START/SAVE
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);

        // Nút SKIP
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipButtonClicked);

        // Khởi tạo AudioSource cho preview
        _previewAudioSource = gameObject.AddComponent<AudioSource>();

        // Đăng ký sự kiện từ AudioPanelController để đồng bộ hóa lựa chọn âm thanh
        if (audioPanelController != null)
        {
            audioPanelController.OnAudioSelected += (selectedPrefab) =>
            {
                if (selectedPrefab != null)
                {
                    AudioConfig config = selectedPrefab.GetComponent<AudioConfig>();
                    _selectedAudioId = (config != null && !string.IsNullOrEmpty(config.Name)) ? config.Name : selectedPrefab.name;
                    MarkAsUnsaved();
                }
                SpawnInstrumentPreview(selectedPrefab);
                UpdateProgressBar(true);
            };
            audioPanelController.OnCustomAudioSelected += (recordingId) =>
            {
                _selectedAudioId = recordingId;
                MarkAsUnsaved();
                SpawnInstrumentPreview(null);
                UpdateProgressBar(true);
            };
        }

        // Đăng ký sự kiện từ CastPanelController để đồng bộ hóa lựa chọn nhân vật
        if (castPanelController != null)
        {
            castPanelController.OnCharacterSelected += (characterPrefab) =>
            {
                if (characterPrefab != null)
                {
                    _selectedPrefabName = characterPrefab.name;
                    _selectedAudioId = "";
                    _selectedAnimationId = "";

                    // Reset trạng thái chọn của audio panel
                    if (audioPanelController != null)
                    {
                        audioPanelController.SetCustomRecordingId(null);
                        audioPanelController.SelectAudioById("");
                    }

                    SpawnCharacterPreview(_selectedPrefabName);
                    MarkAsUnsaved();
                    UpdateProgressBar(true);
                }
            };
        }

        // Đăng ký sự kiện từ AnimPanelController để đồng bộ hóa lựa chọn hoạt ảnh
        if (animPanelController != null)
        {
            animPanelController.OnAnimationSelected += (animId) =>
            {
                _selectedAnimationId = animId;
                PlayPreviewAnimation(_selectedAnimationId);
                MarkAsUnsaved();
                UpdateProgressBar(true);
            };
        }
    }

    private void AutoAssignMissingReferences()
    {
        if (startButton == null)
        {
            startButton = FindSceneComponentByName<Button>("Start Button", "StartButton", "Save Button");
        }

        if (startButtonText == null && startButton != null)
        {
            startButtonText = startButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (prevButton == null)
        {
            prevButton = FindSceneComponentByName<Button>("Prev Button", "PrevButton", "Back Arrow");
        }

        if (nextButton == null)
        {
            nextButton = FindSceneComponentByName<Button>("Next Button", "NextButton", "Forward Arrow");
        }

        if (uiCustome == null)
        {
            Transform canvasTransform = transform.parent;
            if (canvasTransform != null)
            {
                Transform customeTrans = canvasTransform.Find("UI Custome");
                if (customeTrans != null) uiCustome = customeTrans.gameObject;
            }
            if (uiCustome == null) uiCustome = this.gameObject;
        }

        if (uiAr == null)
        {
            Transform canvasTransform = transform.parent;
            if (canvasTransform != null)
            {
                Transform arTrans = canvasTransform.Find("UI AR");
                if (arTrans != null) uiAr = arTrans.gameObject;
            }
        }

        if (previewCamera == null)
        {
            previewCamera = GameObject.Find("PreviewCamera");
            if (previewCamera == null) previewCamera = GameObject.Find("Preview Camera");
        }

        if (popupStudio == null)
        {
            popupStudio = FindComponentInScene<PopupStudio>();
        }
    }

    private void Start()
    {
        // Ẩn các popup khi bắt đầu
        if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);

        // Đổ dữ liệu ban đầu
        PopulateAnimations();

        // Khởi tạo và thiết lập các sự kiện cho nút Hide/Show UI
        if (useBlackScreenTransition)
        {
            CreateFadeOverlay();
        }

        AutoAssignUIReferences();

        if (hideUiButton != null)
        {
            hideUiButton.onClick.RemoveListener(HideUI);
            hideUiButton.onClick.AddListener(HideUI);
            Debug.Log("[CustomCharacterPanelController] Đã gắn sự kiện HideUI cho nút: " + hideUiButton.gameObject.name);
        }

        if (showUiButton != null)
        {
            showUiButton.onClick.RemoveListener(ShowUI);
            showUiButton.onClick.AddListener(ShowUI);
            showUiButton.gameObject.SetActive(false);
            
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();
            cgShow.alpha = 0f;
            showUiButton.transform.localScale = Vector3.zero;

            Debug.Log("[CustomCharacterPanelController] Đã gắn sự kiện ShowUI cho nút: " + showUiButton.gameObject.name);
        }
    }

    private void OnEnable()
    {
        // Khi bật panel, mặc định chọn tab đầu tiên và không chạy hiệu ứng chuyển động
        SwitchTab(CustomTab.Character, false);

        // Đảm bảo bật lại Preview Container và PreviewCamera khi Panel Creator hoạt động
        if (previewContainer != null) previewContainer.gameObject.SetActive(true);
        if (previewCamera != null) previewCamera.SetActive(true);

        HideArPedestalsForCreator();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chuyển đổi Tab & Cập nhật UI
    // ─────────────────────────────────────────────────────────────────────────

    private void SwitchTab(CustomTab tab, bool animate = true)
    {
        if (tab == _currentTab && !_isFirstSwitch) return;

        CustomTab oldTab = _currentTab;
        _currentTab = tab;
        _isFirstSwitch = false;



        // 2. Cập nhật tiêu đề theo đúng thiết kế: NAME / INSTRUMENT / ANIMATION
        if (titleText != null)
        {
            string newTitle = tab switch
            {
                CustomTab.Character  => "CAST",
                CustomTab.Instrument => "INSTRUMENT",
                _                    => "ANIMATION",
            };

            if (titleText.text != newTitle)
            {
                if (animate)
                {
                    titleText.transform.DOKill();
                    titleText.DOFade(0f, 0.12f).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        titleText.text = newTitle;
                        titleText.DOFade(1f, 0.12f).SetEase(Ease.OutQuad);
                        titleText.transform.localScale = Vector3.one * 0.95f;
                        titleText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
                    });
                }
                else
                {
                    titleText.text = newTitle;
                    titleText.alpha = 1f;
                }
            }
        }

        // 3. Bật/Tắt và di chuyển các ScrollView có hiệu ứng slide & fade
        AnimateTabTransition(oldTab, tab, animate);

        // 4. Cập nhật nút điều hướng (prev/next/start)
        UpdateNavigationButtons();

        if (tab == CustomTab.Instrument)
        {
            PopulateInstruments();
        }
        else if (tab == CustomTab.Animation)
        {
            PopulateAnimations();
        }
        else if (tab == CustomTab.Character)
        {
            ClearInstrumentPreview();
        }

        // Điều khiển hoạt ảnh tương ứng với Tab
        if (tab == CustomTab.Animation)
        {
            if (!string.IsNullOrEmpty(_selectedAnimationId))
            {
                PlayPreviewAnimation(_selectedAnimationId);
            }
        }
        else
        {
            PlayIdleAnimation();
        }

        // Cập nhật thanh tiến trình tương ứng với tab hiện tại
        UpdateProgressBar(animate);
    }

    private void InitCanvasGroups()
    {
        if (characterScrollView != null)
        {
            _characterCG = characterScrollView.GetComponent<CanvasGroup>();
            if (_characterCG == null) _characterCG = characterScrollView.AddComponent<CanvasGroup>();
            _characterRect = characterScrollView.GetComponent<RectTransform>();
            if (_characterRect != null) _characterOrigPos = _characterRect.anchoredPosition;
        }
        if (instrumentScrollView != null)
        {
            _instrumentCG = instrumentScrollView.GetComponent<CanvasGroup>();
            if (_instrumentCG == null) _instrumentCG = instrumentScrollView.AddComponent<CanvasGroup>();
            _instrumentRect = instrumentScrollView.GetComponent<RectTransform>();
            if (_instrumentRect != null) _instrumentOrigPos = _instrumentRect.anchoredPosition;
        }
        if (animationScrollView != null)
        {
            _animationCG = animationScrollView.GetComponent<CanvasGroup>();
            if (_animationCG == null) _animationCG = animationScrollView.AddComponent<CanvasGroup>();
            _animationRect = animationScrollView.GetComponent<RectTransform>();
            if (_animationRect != null) _animationOrigPos = _animationRect.anchoredPosition;
        }
    }


    private void AnimateTabTransition(CustomTab oldTab, CustomTab newTab, bool animate)
    {
        bool slideRight = (int)newTab < (int)oldTab;

        TransitionPanel(characterScrollView, _characterCG, _characterRect, _characterOrigPos, newTab == CustomTab.Character, animate, slideRight);
        TransitionPanel(instrumentScrollView, _instrumentCG, _instrumentRect, _instrumentOrigPos, newTab == CustomTab.Instrument, animate, slideRight);
        TransitionPanel(animationScrollView, _animationCG, _animationRect, _animationOrigPos, newTab == CustomTab.Animation, animate, slideRight);
    }

    private void TransitionPanel(GameObject panelObj, CanvasGroup cg, RectTransform rect, Vector2 origPos, bool show, bool animate, bool slideRight)
    {
        if (panelObj == null || cg == null || rect == null) return;

        rect.DOKill();
        cg.DOKill();

        if (show)
        {
            panelObj.SetActive(true);
            cg.interactable = true;
            cg.blocksRaycasts = true;

            if (animate)
            {
                float startX = slideRight ? origPos.x - slideOffset : origPos.x + slideOffset;
                rect.anchoredPosition = new Vector2(startX, origPos.y);
                cg.alpha = 0f;

                rect.DOAnchorPos(origPos, transitionDuration).SetEase(Ease.OutCubic);
                cg.DOFade(1f, transitionDuration).SetEase(Ease.OutQuad);
            }
            else
            {
                rect.anchoredPosition = origPos;
                cg.alpha = 1f;
            }
        }
        else
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;

            if (animate && panelObj.activeSelf)
            {
                float targetX = slideRight ? origPos.x + slideOffset : origPos.x - slideOffset;
                Vector2 targetPos = new Vector2(targetX, origPos.y);

                rect.DOAnchorPos(targetPos, transitionDuration).SetEase(Ease.OutCubic);
                cg.DOFade(0f, transitionDuration).SetEase(Ease.OutQuad).OnComplete(() => panelObj.SetActive(false));
            }
            else
            {
                rect.anchoredPosition = origPos;
                cg.alpha = 0f;
                panelObj.SetActive(false);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Đổ dữ liệu vào các ScrollView
    // ─────────────────────────────────────────────────────────────────────────



    private void PopulateInstruments()
    {
        if (audioPanelController != null)
        {
            // 1. Kiểm tra tính năng thu âm của nhân vật đang chọn
            bool characterCanRecord = true;

            // Cập nhật trạng thái cho phép thu âm và ID đang chọn
            audioPanelController.EnableRecording = characterCanRecord;

            audioPanelController.SelectAudioById(_selectedAudioId);
            audioPanelController.InitializePanel();

            // Đồng bộ lại _selectedAudioId từ trạng thái của audioPanelController
            if (!string.IsNullOrEmpty(audioPanelController.SelectedCustomAudioId))
            {
                _selectedAudioId = audioPanelController.SelectedCustomAudioId;
            }
            else if (audioPanelController.SelectedAudioConfig != null)
            {
                _selectedAudioId = !string.IsNullOrEmpty(audioPanelController.SelectedAudioConfig.Name)
                    ? audioPanelController.SelectedAudioConfig.Name
                    : audioPanelController.SelectedPrefab.name;
            }
        }
    }

    private void PopulateAnimations()
    {
        if (animPanelController != null)
        {
            GameObject selectedPrefab = GetCharacterPrefab(_selectedPrefabName);
            animPanelController.InitializePanel(selectedPrefab);
            
            // Đồng bộ _selectedAnimationId
            if (!string.IsNullOrEmpty(animPanelController.SelectedAnimationId))
            {
                _selectedAnimationId = animPanelController.SelectedAnimationId;
                PlayPreviewAnimation(_selectedAnimationId);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xử lý Preview Nhân vật & Hoạt ảnh
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnCharacterPreview(string prefabName)
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
        }

        GameObject prefab = GetCharacterPrefab(prefabName);
        if (prefab != null && previewContainer != null)
        {
            _previewInstance = Instantiate(prefab, previewContainer.position, previewContainer.rotation, previewContainer);
            _previewInstance.transform.localPosition = Vector3.zero;
            _previewInstance.transform.localScale = Vector3.one;

            // Đồng bộ layer của nhân vật và các object con theo layer của previewContainer
            SetLayerRecursively(_previewInstance, previewContainer.gameObject.layer);

            // Gán target động cho UICharacterRotator xoay 3D preview
#if UNITY_2023_1_OR_NEWER
            UICharacterRotator rotator = FindAnyObjectByType<UICharacterRotator>();
#else
            UICharacterRotator rotator = FindObjectOfType<UICharacterRotator>();
#endif
            if (rotator == null)
            {
                // Thử tìm GameObject "Cast Preview" (RawImage) để gắn động rotator
                GameObject rawImageObj = GameObject.Find("Cast Preview");
                if (rawImageObj != null)
                {
                    rotator = rawImageObj.AddComponent<UICharacterRotator>();
                    Debug.Log("[CustomCharacterPanelController] Tự động gắn component UICharacterRotator vào GameObject 'Cast Preview'.");
                }
            }

            if (rotator != null)
            {
                rotator.SetTarget(_previewInstance.transform);
                rotator.ResetRotation();
            }

            // Chạy Idle mặc định khi spawn nhân vật
            PlayIdleAnimation();

            // Nếu đã chọn hoạt ảnh trước đó (chỉ chạy khi ở tab Animation), chạy luôn hoạt ảnh trên model preview mới
            if (_currentTab == CustomTab.Animation && !string.IsNullOrEmpty(_selectedAnimationId))
            {
                PlayPreviewAnimation(_selectedAnimationId);
            }

            // Tự động spawn nhạc cụ đã chọn đứng cạnh nhân vật mới khi không ở tab Cast
            if (_currentTab != CustomTab.Character && audioPanelController != null && audioPanelController.SelectedPrefab != null)
            {
                SpawnInstrumentPreview(audioPanelController.SelectedPrefab);
            }
        }
    }

    private void SpawnInstrumentPreview(GameObject instrumentPrefab)
    {
        ClearInstrumentPreview();

        if (instrumentPrefab != null && previewContainer != null)
        {
            _previewInstrumentInstance = Instantiate(instrumentPrefab, previewContainer.position, previewContainer.rotation, previewContainer);
            _previewInstrumentInstance.transform.localPosition = new Vector3(-1f, 0.5f, 0f);
            _previewInstrumentInstance.transform.localRotation = Quaternion.identity;
            _previewInstrumentInstance.transform.localScale = Vector3.one;

            SetLayerRecursively(_previewInstrumentInstance, previewContainer.gameObject.layer);

            // Lấy AudioConfig để lấy clip và AudioSource của model nhạc cụ
            AudioConfig audioConfig = _previewInstrumentInstance.GetComponentInChildren<AudioConfig>(true);
            AudioSource instrumentAudioSource = null;

            if (audioConfig != null && audioConfig.audioSource != null)
            {
                instrumentAudioSource = audioConfig.audioSource;
            }
            else
            {
                // Fallback: lấy AudioSource bất kỳ trên model
                instrumentAudioSource = _previewInstrumentInstance.GetComponentInChildren<AudioSource>(true);
            }

            // Phát nhạc từ AudioSource của model nhạc cụ để ModelLoopEffects nhận tín hiệu âm lượng
            if (instrumentAudioSource != null)
            {
                instrumentAudioSource.enabled = true;
                instrumentAudioSource.loop = true;
                instrumentAudioSource.spatialBlend = 0f; // 2D để nghe rõ trong preview
                if (instrumentAudioSource.clip != null)
                {
                    instrumentAudioSource.Play();
                    Debug.Log($"[CustomCharacterPanelController] Đang phát nhạc preview từ AudioSource của model: {instrumentPrefab.name}");
                }
                else
                {
                    // Thử lấy clip từ AudioSource trên prefab gốc (trước khi instantiate)
                    AudioConfig srcConfig = instrumentPrefab.GetComponentInChildren<AudioConfig>(true);
                    if (srcConfig != null && srcConfig.audioSource != null && srcConfig.audioSource.clip != null)
                    {
                        instrumentAudioSource.clip = srcConfig.audioSource.clip;
                        instrumentAudioSource.Play();
                        Debug.Log($"[CustomCharacterPanelController] Đang phát nhạc preview (từ prefab config): {instrumentPrefab.name}");
                    }
                }

                // Gán AudioSource vào ModelLoopEffects để VFX phản ứng theo âm lượng nhạc
                ModelLoopEffects[] loopEffects = _previewInstrumentInstance.GetComponentsInChildren<ModelLoopEffects>(true);
                foreach (var fx in loopEffects)
                {
                    fx.TargetAudioSource = instrumentAudioSource;
                    Debug.Log($"[CustomCharacterPanelController] Gán AudioSource vào ModelLoopEffects: {fx.gameObject.name}");
                }

                // Cũng cập nhật MusicSyncManager để các effect khác (như nhân vật chính) cũng nhảy theo
                if (MusicSyncManager.Instance != null)
                {
                    MusicSyncManager.Instance.SetAudioSource(instrumentAudioSource);
                }
            }
            else
            {
                Debug.LogWarning($"[CustomCharacterPanelController] Không tìm thấy AudioSource trên model nhạc cụ: {instrumentPrefab.name}");
            }

            Debug.Log($"[CustomCharacterPanelController] Spawned 3D instrument preview: {instrumentPrefab.name}");
        }
        else if (instrumentPrefab == null)
        {
            // Khi không có instrument (custom recording), dừng nhạc và reset MusicSyncManager
            if (MusicSyncManager.Instance != null)
            {
                MusicSyncManager.Instance.SetAudioSource(null);
            }
        }
    }

    private void ClearInstrumentPreview()
    {
        if (_previewInstrumentInstance != null)
        {
            AudioSource oldSrc = _previewInstrumentInstance.GetComponentInChildren<AudioSource>(true);
            if (oldSrc != null) oldSrc.Stop();
            Destroy(_previewInstrumentInstance);
            _previewInstrumentInstance = null;
        }

        if (MusicSyncManager.Instance != null)
        {
            MusicSyncManager.Instance.SetAudioSource(null);
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void PlayIdleAnimation()
    {
        if (_previewInstance == null) return;

        // 1. Thử qua component Move
        Move move = _previewInstance.GetComponent<Move>();
        if (move == null)
        {
            move = _previewInstance.GetComponentInChildren<Move>();
        }

        if (move != null)
        {
            move.PlayIdle();
            Debug.Log("[CustomCharacterPanelController] PlayIdleAnimation: Đã chạy PlayIdle() qua component Move.");
            return;
        }

        // 2. Thử trực tiếp qua Animator
        Animator animator = _previewInstance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = _previewInstance.GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.CrossFade("root_Girl_Idle", 0.15f);
            Debug.Log("[CustomCharacterPanelController] PlayIdleAnimation: Đã chạy root_Girl_Idle trên Animator.");
        }
    }

    private void PlayPreviewAnimation(string animId)
    {
        if (_previewInstance == null || string.IsNullOrEmpty(animId)) return;

        // Nếu không ở tab Animation, bắt buộc chạy Idle
        if (_currentTab != CustomTab.Animation)
        {
            PlayIdleAnimation();
            return;
        }

        // Chuẩn hóa tên state (thay thế dấu chấm bằng dấu gạch dưới vì Unity tự động chuyển đổi tên clip .fbx khi tạo State)
        string sanitizedStateName = animId.Replace(".", "_");

        // Tìm Animator trên chính preview instance hoặc các con của nó
        Animator animator = _previewInstance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = _previewInstance.GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.CrossFade(sanitizedStateName, 0.15f);
            Debug.Log($"[CustomCharacterPanelController] PlayPreviewAnimation: Đã chạy {sanitizedStateName} trên Animator.");
        }
        else
        {
            // Fallback sang Move nếu có
            Move move = _previewInstance.GetComponent<Move>();
            if (move != null)
            {
                move.PlayAnimationOnce(animId, 0.15f);
            }
        }
    }

    private GameObject GetCharacterPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        if (castPanelController != null && castPanelController.CharacterPrefabs != null)
        {
            foreach (var prefab in castPanelController.CharacterPrefabs)
            {
                if (prefab != null && prefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

        if (MainMenuDataManager.Instance != null)
        {
            GameObject prefab = MainMenuDataManager.Instance.GetCharacterPrefab(prefabName);
            if (prefab != null)
            {
                return prefab;
            }
        }

        GameObject resPrefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(prefabName);
        return resPrefab;
    }

    private void SyncCharacterPrefabsToDataManager(MainMenuDataManager dataManager)
    {
        if (dataManager == null) return;


        if (castPanelController != null)
        {
            dataManager.AddCharacterPrefabs(castPanelController.CharacterPrefabs);
        }
    }

    private Sprite GetAvatarSprite(string prefabName)
    {
        // Thử lấy avatar từ component CastPrefab của prefab trước
        GameObject prefab = GetCharacterPrefab(prefabName);
        if (prefab != null)
        {
            var config = prefab.GetComponent<CastPrefab>();
            if (config != null && config.characterAvatar != null)
            {
                return config.characterAvatar;
            }
        }


        return Resources.Load<Sprite>("Avatars/" + prefabName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ghi âm âm thanh (Microphone) & Lưu Cục Bộ
    // ─────────────────────────────────────────────────────────────────────────



    private void HideRecordingStatus()
    {
        if (recordingStatusText != null)
        {
            recordingStatusText.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phát âm thanh Preview
    // ─────────────────────────────────────────────────────────────────────────

    private async void PreviewAudio(string audioId)
    {
        if (_previewAudioSource == null) return;
        _previewAudioSource.Stop();

        if (string.IsNullOrEmpty(audioId)) return;

        if (audioId.StartsWith("rec_"))
        {
            // Âm thanh tự thu: Tải động từ bộ nhớ cục bộ
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "Loading audio...";
                recordingStatusText.gameObject.SetActive(true);
            }

            var recordings = await MainMenuDataManager.Instance.GetRecordingsAsync();
            var targetRec = recordings.Find(r => r.recordingId == audioId);

            if (recordingStatusText != null)
            {
                recordingStatusText.gameObject.SetActive(false);
            }

            if (targetRec != null && !string.IsNullOrEmpty(targetRec.audioBase64))
            {
                byte[] wavBytes = Convert.FromBase64String(targetRec.audioBase64);
                
                // --- GIẢI MÃ AES ---
                try
                {
                    wavBytes = AudioEncryption.Decrypt(wavBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CustomCharacterPanel] Giải mã âm thanh cũ không thành công, thử phát trực tiếp: " + ex.Message);
                }

                // Giải mã mảng byte WAV thành AudioClip trong RAM
                AudioClip clip = WavUtility.ToAudioClip(wavBytes, targetRec.name);
                if (clip != null)
                {
                    _previewAudioSource.clip = clip;
                    _previewAudioSource.Play();
                    // Tự động giải phóng bộ nhớ clip sau khi phát xong
                    StartCoroutine(DestroyClipAfterPlay(clip));
                }
            }
        }
        else
        {
            // Nhạc cụ mặc định: Tải từ Resources
            AudioClip clip = Resources.Load<AudioClip>("Audios/" + audioId);
            if (clip == null) clip = Resources.Load<AudioClip>(audioId);

            if (clip != null)
            {
                _previewAudioSource.clip = clip;
                _previewAudioSource.Play();
            }
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
    // Chức năng Lưu Nhân vật (Local)
    // ─────────────────────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task<bool> SaveCharacter()
    {
        string charName = !string.IsNullOrEmpty(_selectedPrefabName) ? _selectedPrefabName : "Custom Character";
        CastData savedCast = null;

        MainMenuDataManager dataManager = GetOrCreateMainMenuDataManager();
        if (dataManager != null && dataManager.GetSavedCastsCount() >= 7)
        {
            if (popupStudio != null)
            {
                popupStudio.OnCancel -= OnPopupCancel;
                popupStudio.OnStart -= OnPopupStart;

                popupStudio.OnCancel += OnPopupCancel;
                popupStudio.OnStart += OnPopupStart;

                popupStudio.OpenPopup();
            }
            else
            {
                if (recordingStatusText != null)
                {
                    recordingStatusText.text = "Limit of 7 characters reached. Please delete an old character in Studio to save a new one!";
                    recordingStatusText.gameObject.SetActive(true);
                }
                Debug.LogWarning("[CustomCharacterPanel] Limit of 7 characters reached and popupStudio is null.");
                Invoke("HideRecordingStatus", 4f);
            }
            return false;
        }

        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Saving Character...";
            recordingStatusText.gameObject.SetActive(true);
        }

        // Tạo danh sách animation nhảy sở hữu (mặc định lấy animation hiện tại)
        List<string> animIds = new List<string>();
        if (!string.IsNullOrEmpty(_selectedAnimationId))
        {
            animIds.Add(_selectedAnimationId);
        }

        bool success = false;

        if (dataManager != null)
        {
            try
            {
                // Lưu cấu hình nhân vật cục bộ thông qua API có sẵn
                SyncCharacterPrefabsToDataManager(dataManager);

                GameObject selectedPrefab = GetCharacterPrefab(_selectedPrefabName);
                savedCast = selectedPrefab != null
                    ? dataManager.CreateCastDataFromPrefab(selectedPrefab, _selectedAudioId, _selectedAnimationId)
                    : dataManager.CreateCastDataFromPrefabName(_selectedPrefabName, _selectedAudioId, _selectedAnimationId);

                if (savedCast != null)
                {
                    charName = savedCast.name;
                    success = await dataManager.CreateCharacterAsync(savedCast);
                }
                else
                {
                    success = await dataManager.CreateCharacterAsync(
                        charName,
                        _selectedPrefabName,
                        _selectedAudioId,
                        _selectedAnimationId,
                        animIds
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[CustomCharacterPanel] SaveCharacter failed: " + ex.Message);
            }
        }
        else
        {
            Debug.LogError("[CustomCharacterPanel] Không thể tạo MainMenuDataManager để lưu nhân vật.");
        }

        if (recordingStatusText != null)
        {
            recordingStatusText.text = success ? "Saved Successfully!" : "Save Failed!";
        }

        if (success)
        {
            _isCharacterSaved = true;
            _savedCastData = savedCast;
            UpdateStartButtonText(); // text đổi thành "START" sau khi lưu xong

            // Tự động xóa bản ghi âm dư thừa nếu không chọn nó làm âm thanh chính của nhân vật
            if (audioPanelController != null)
            {
                string recordedId = audioPanelController.TempRecordedAudioId;
                if (!string.IsNullOrEmpty(recordedId) && _selectedAudioId != recordedId)
                {
                    Debug.Log($"[SaveCharacter] Tự động xóa bản ghi âm không chọn: {recordedId}");
                    audioPanelController.DeleteCustomRecordingSilently(recordedId);
                }
            }
        }

        if (success && _shouldTransitionAfterSave)
        {
            StartCoroutine(TransitionAfterDelayRoutine(savedCast));
        }
        else
        {
            Invoke("HideRecordingStatus", 2f);
        }

        return success;
    }

    private IEnumerator TransitionAfterDelayRoutine(CastData cast)
    {
        yield return new WaitForSeconds(1.0f);
        if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);

        // Lưu dữ liệu castData để mang qua Scene AR
        if (cast == null)
        {
            MainMenuDataManager dataManager = GetOrCreateMainMenuDataManager();
            if (dataManager != null)
            {
                SyncCharacterPrefabsToDataManager(dataManager);
                cast = dataManager.CreateCastDataFromPrefabName(_selectedPrefabName, _selectedAudioId, _selectedAnimationId);
            }
        }
        
        // Vào chế độ Custom Character: clear Band Mode selection
        BandSelectionManager.ClearSelection();
        
        StartCoroutine(CheckARCoreAndTransition(cast));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chuyển Scene AR/Non-AR theo cấu hình ARCore
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    // Điều hướng bằng mũi tên Trái / Phải / START
    // ─────────────────────────────────────────────────────────────────────────

    private void OnPrevButtonClicked()
    {
        switch (_currentTab)
        {
            case CustomTab.Instrument: SwitchTab(CustomTab.Character, true); break;
            case CustomTab.Animation:  SwitchTab(CustomTab.Instrument, true); break;
        }
    }

    private void OnNextButtonClicked()
    {
        if (_currentTab == CustomTab.Character)
        {
            // Kiểm tra xem đã chọn nhân vật chưa
            if (string.IsNullOrEmpty(_selectedPrefabName))
            {
                Debug.LogWarning("[CustomCharacterPanelController] Chưa chọn Cast!");
                return;
            }

            if (progressController != null)
            {
                SetNavigationButtonsInteractable(false);
                var seq = progressController.SetProgress(2, true);
                if (seq != null)
                {
                    seq.OnComplete(() =>
                    {
                        SetNavigationButtonsInteractable(true);
                        SwitchTab(CustomTab.Instrument, true);
                    });
                }
                else
                {
                    SetNavigationButtonsInteractable(true);
                    SwitchTab(CustomTab.Instrument, true);
                }
            }
            else
            {
                SwitchTab(CustomTab.Instrument, true);
            }
        }
        else if (_currentTab == CustomTab.Instrument)
        {
            // Kiểm tra xem đã chọn nhạc cụ chưa
            if (string.IsNullOrEmpty(_selectedAudioId))
            {
                Debug.LogWarning("[CustomCharacterPanelController] Chưa chọn nhạc cụ!");
                return;
            }

            if (progressController != null)
            {
                SetNavigationButtonsInteractable(false);
                var seq = progressController.SetProgress(3, true);
                if (seq != null)
                {
                    seq.OnComplete(() =>
                    {
                        SetNavigationButtonsInteractable(true);
                        SwitchTab(CustomTab.Animation, true);
                    });
                }
                else
                {
                    SetNavigationButtonsInteractable(true);
                    SwitchTab(CustomTab.Animation, true);
                }
            }
            else
            {
                SwitchTab(CustomTab.Animation, true);
            }
        }
    }

    private void SetNavigationButtonsInteractable(bool interactable)
    {
        if (nextButton != null) nextButton.interactable = interactable;
        if (prevButton != null) prevButton.interactable = interactable;
        if (backButton != null) backButton.interactable = interactable;
        if (startButton != null) startButton.interactable = interactable;
        if (skipButton != null) skipButton.interactable = interactable;
    }

    /// <summary>
    /// Nút START / SAVE ở tab Animation:
    /// - Chưa lưu → thực hiện SAVE, đổi text thành START
    /// - Đã lưu → chuyển sang AR
    /// </summary>
    private async void OnStartButtonClicked()
    {
        if (_isCharacterSaved)
        {
            // Đã lưu thành công → START: vào AR
            if (_savedCastData == null)
            {
                MainMenuDataManager dataManager = GetOrCreateMainMenuDataManager();
                if (dataManager != null)
                {
                    _savedCastData = dataManager.CreateCastDataFromPrefabName(_selectedPrefabName, _selectedAudioId, _selectedAnimationId);
                }
            }

            // Vào chế độ Custom Character: clear Band Mode selection
            BandSelectionManager.ClearSelection();
            StartCoroutine(CheckARCoreAndTransition(_savedCastData));
        }
        else
        {
            // Chưa lưu → SAVE
            _shouldTransitionAfterSave = false;
            await SaveCharacter();
            // UpdateStartButtonText() đã được gọi trong SaveCharacter → _isCharacterSaved = true
        }
    }

    private void OnSkipButtonClicked()
    {
        if (_previewAudioSource != null) _previewAudioSource.Stop();

        CastData tempCast = null;
        MainMenuDataManager dataManager = GetOrCreateMainMenuDataManager();
        if (dataManager != null)
        {
            SyncCharacterPrefabsToDataManager(dataManager);
            tempCast = dataManager.CreateCastDataFromPrefabName(_selectedPrefabName, _selectedAudioId, _selectedAnimationId);
        }

        // Vào chế độ Custom Character: clear Band Mode selection
        BandSelectionManager.ClearSelection();

        StartCoroutine(CheckARCoreAndTransition(tempCast));
    }

    private IEnumerator CheckARCoreAndTransition(CastData cast)
    {
        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Checking Device AR Support...";
            recordingStatusText.gameObject.SetActive(true);
        }

        // Gọi API ARFoundation để kiểm tra tính tương thích của thiết bị
        yield return ARSession.CheckAvailability();

        if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);

        // Tự động tìm kiếm UI Custome và UI AR nếu chưa được gán
        if (uiCustome == null)
        {
            Transform canvasTransform = transform.parent;
            if (canvasTransform != null)
            {
                Transform customeTrans = canvasTransform.Find("UI Custome");
                if (customeTrans != null) uiCustome = customeTrans.gameObject;
            }
            if (uiCustome == null) uiCustome = this.gameObject;
        }

        if (uiAr == null)
        {
            Transform canvasTransform = transform.parent;
            if (canvasTransform != null)
            {
                Transform arTrans = canvasTransform.Find("UI AR");
                if (arTrans != null) uiAr = arTrans.gameObject;
            }
        }

        if (previewCamera == null)
        {
            previewCamera = GameObject.Find("PreviewCamera");
            if (previewCamera == null) previewCamera = GameObject.Find("Preview Camera");
        }

        // Tạm thời vô hiệu hóa AR theo yêu cầu, luôn định tuyến sang Non-AR
        bool isARCoreSupported = false;
        string targetSceneName = isARCoreSupported ? customArSceneName : customNonArSceneName;
        string activeSceneName = SceneManager.GetActiveScene().name;

        // Gán biến castData vào MainMenuDataManager
        MainMenuDataManager dataManager = GetOrCreateMainMenuDataManager();
        if (dataManager != null)
        {
            dataManager.castData = cast;
        }
        else
        {
            Debug.LogError("[CustomCharacterPanel] Không thể lưu castData trước khi chuyển scene.");
            yield break;
        }

        // Nếu có UI AR trong scene, tức là scene đã được gộp. Ta chuyển đổi trực tiếp mà không cần load scene mới.
        if (uiAr != null)
        {
            TransitionToArModeDirectly("[CustomCharacterPanel] Phát hiện UI AR trong Scene (Scene đã gộp). Thực hiện chuyển đổi UI trực tiếp.");
            yield break;
        }

        if (activeSceneName == targetSceneName)
        {
            TransitionToArModeDirectly($"[CustomCharacterPanel] Đang ở trong đúng scene mục tiêu '{targetSceneName}'. Tiến hành ẩn Panel Creator và spawn nhân vật trực tiếp.");
            yield break;
        }

        Debug.Log($"[CustomCharacterPanel] Thiết bị hỗ trợ ARCore: {isARCoreSupported}. Loading scene: {targetSceneName}");

        // Chuyển scene mượt mà bằng SceneTransitionManager
        ARFallbackManager.RequestAROnNextSceneLoad();

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(targetSceneName);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    /// <summary>
    /// Thực hiện chuyển đổi giao diện mượt mà từ Custom Creator sang AR Mode bằng DOTween fade & scale.
    /// </summary>
    private void TransitionToArModeDirectly(string logMessage)
    {
        Debug.Log(logMessage);
        ARFallbackManager.ResumeForCurrentScene();

        // 1. Lấy hoặc thêm CanvasGroup cho uiCustome và uiAr để chạy fade mượt mà
        CanvasGroup cgCustome = null;
        if (uiCustome != null)
        {
            cgCustome = uiCustome.GetComponent<CanvasGroup>();
            if (cgCustome == null) cgCustome = uiCustome.AddComponent<CanvasGroup>();
        }

        CanvasGroup cgAr = null;
        if (uiAr != null)
        {
            cgAr = uiAr.GetComponent<CanvasGroup>();
            if (cgAr == null) cgAr = uiAr.AddComponent<CanvasGroup>();
        }

        // Vô hiệu hóa tương tác trong khi chuyển cảnh
        if (cgCustome != null)
        {
            cgCustome.interactable = false;
            cgCustome.blocksRaycasts = false;
        }

        if (cgAr != null)
        {
            cgAr.interactable = false;
            cgAr.blocksRaycasts = false;
            uiAr.SetActive(true);
            cgAr.alpha = 0f;
            uiAr.transform.localScale = Vector3.one * 0.95f;

            cgAr.DOKill();
            uiAr.transform.DOKill();
            cgAr.DOFade(1f, 0.45f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                cgAr.interactable = true;
                cgAr.blocksRaycasts = true;
            });
            uiAr.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack);
        }

        // Chạy hiệu ứng Fade Out của uiCustome
        if (cgCustome != null)
        {
            cgCustome.DOKill();
            uiCustome.transform.DOKill();
            uiCustome.transform.DOScale(0.95f, 0.35f).SetEase(Ease.InQuad);
            cgCustome.DOFade(0f, 0.35f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                uiCustome.SetActive(false);
            });
        }
        else
        {
            gameObject.SetActive(false);
        }

        // Tắt Preview Container và PreviewCamera
        if (previewContainer != null) previewContainer.gameObject.SetActive(false);
        if (previewCamera != null) previewCamera.SetActive(false);

        // Dọn dẹp các đối tượng tạm thời của preview
        if (_previewAudioSource != null) _previewAudioSource.Stop();
        if (_previewInstance != null) Destroy(_previewInstance);
        if (_previewInstrumentInstance != null) Destroy(_previewInstrumentInstance);

        // Tìm Spawner và tiến hành spawn nhân vật
        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
        if (spawner != null)
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (Camera.main != null)
            {
                Transform camTrans = Camera.main.transform;
                Vector3 camForwardHorizontal = camTrans.forward;
                camForwardHorizontal.y = 0f;
                camForwardHorizontal.Normalize();
                
                spawnPos = camTrans.position + camForwardHorizontal * 3f; // cách camera 3 mét
                spawnRot = Quaternion.LookRotation(-camForwardHorizontal, Vector3.up);
            }
            else
            {
                spawnPos = spawner.transform.position;
                spawnRot = spawner.transform.rotation;
            }

            spawner.SpawnBand(spawnPos, spawnRot);
        }
        else
        {
            Debug.LogWarning("[CustomCharacterPanel] Không tìm thấy BandARSpawner trong scene hiện hành để spawn nhân vật!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích dọn dẹp
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsARCoreSupportedForRouting(ARSessionState state)
    {
        return state != ARSessionState.Unsupported &&
               state != ARSessionState.None &&
               state != ARSessionState.CheckingAvailability;
    }

    private MainMenuDataManager GetOrCreateMainMenuDataManager()
    {
        if (MainMenuDataManager.Instance != null)
        {
            return MainMenuDataManager.Instance;
        }

        GameObject managerObject = new GameObject("MainMenuDataManager (Runtime)");
        return managerObject.AddComponent<MainMenuDataManager>();
    }

    private void ClosePanel()
    {
        // ── Phân biệt ngữ cảnh: đang ở Custom UI hay đang trải nghiệm AR? ──
        bool isInArMode = (uiAr != null && uiAr.activeSelf) ||
                          (uiCustome != null && !uiCustome.activeSelf);

        if (isInArMode)
        {
            // Đang ở chế độ AR → Back-from-AR: dọn Cast + hiện lại UI Custom
            BackFromArToCustomUI();
        }
        else
        {
            // Đang ở màn hình Custom UI → Về Main Menu như cũ
            ARFallbackManager.ReleaseDeviceCamera();

            if (_previewAudioSource != null) _previewAudioSource.Stop();
            if (_previewInstance != null) Destroy(_previewInstance);
            if (_previewInstrumentInstance != null) Destroy(_previewInstrumentInstance);

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("Main Menu Scene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu Scene");
            }
        }
    }

    /// <summary>
    /// Xử lý Back từ chế độ AR về lại giao diện Custom Creator trong cùng Scene:
    /// 1. Dọn sạch Cast đã spawn trong AR và reset các bệ đứng.
    /// 2. Ẩn UI AR, hiện lại UI Custom để người dùng tạo nhân vật mới.
    /// 3. Bật lại Preview Camera và reset trạng thái panel về tab đầu tiên.
    /// </summary>
    private void BackFromArToCustomUI()
    {
        Debug.Log("[CustomCharacterPanel] Back-from-AR: Dọn Cast và hiện lại UI Custom.");
        ARFallbackManager.ReleaseDeviceCamera();

        // 1. Dọn sạch Cast trong AR và reset bệ đứng
        BandARSpawner spawner = null;
#if UNITY_2023_1_OR_NEWER
        spawner = FindAnyObjectByType<BandARSpawner>();
#else
        spawner = FindObjectOfType<BandARSpawner>();
#endif
        if (spawner != null)
        {
            spawner.DestroyAllAndResetPedestals();
            HideArPedestalsForCreator(spawner);
        }

        // 2. Ẩn UI AR với hiệu ứng fade out
        if (uiAr != null)
        {
            CanvasGroup cgAr = uiAr.GetComponent<CanvasGroup>();
            if (cgAr == null) cgAr = uiAr.AddComponent<CanvasGroup>();

            cgAr.DOKill();
            uiAr.transform.DOKill();
            cgAr.interactable = false;
            cgAr.blocksRaycasts = false;
            cgAr.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase).OnComplete(() =>
            {
                uiAr.SetActive(false);
            });
        }

        // 3. Hiện lại UI Custom với hiệu ứng fade in
        if (uiCustome != null)
        {
            CanvasGroup cgCustome = uiCustome.GetComponent<CanvasGroup>();
            if (cgCustome == null) cgCustome = uiCustome.AddComponent<CanvasGroup>();

            uiCustome.SetActive(true);
            cgCustome.alpha = 0f;
            uiCustome.transform.localScale = Vector3.one * 0.95f;
            cgCustome.interactable = false;
            cgCustome.blocksRaycasts = false;

            cgCustome.DOKill();
            uiCustome.transform.DOKill();
            cgCustome.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
            uiCustome.transform.DOScale(1f, uiFadeDuration + 0.05f).SetEase(uiShowEase).OnComplete(() =>
            {
                cgCustome.interactable = true;
                cgCustome.blocksRaycasts = true;
            });
        }

        // 4. Bật lại Preview Camera
        if (previewCamera != null) previewCamera.SetActive(true);
        if (previewContainer != null) previewContainer.gameObject.SetActive(true);

        // 5. Reset trạng thái panel về tab đầu tiên, xóa lựa chọn cũ
        _isCharacterSaved = false;
        _savedCastData = null;
        _shouldTransitionAfterSave = false;

        SwitchTab(CustomTab.Character, false);
        RestoreFirstCharacterPreview();
        UpdateStartButtonText();
    }

    private void RestoreFirstCharacterPreview()
    {
        if (castPanelController == null)
        {
            castPanelController = FindComponentInScene<CastPanelController>();
        }

        if (castPanelController != null && castPanelController.CharacterPrefabs.Count > 0)
        {
            castPanelController.SelectCharacter(0);
            return;
        }

        if (!string.IsNullOrEmpty(_selectedPrefabName))
        {
            SpawnCharacterPreview(_selectedPrefabName);
        }
    }

    private void HideArPedestalsForCreator(BandARSpawner spawner = null)
    {
        if (spawner == null)
        {
#if UNITY_2023_1_OR_NEWER
            spawner = FindAnyObjectByType<BandARSpawner>();
#else
            spawner = FindObjectOfType<BandARSpawner>();
#endif
        }

        if (spawner != null)
        {
            spawner.SetPedestalsAndUnplacedCastsActive(false);
        }
    }

    private void ClearChildren(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Tìm kiếm component trong scene, kể cả khi GameObject đang ẩn (inactive).
    /// </summary>
    private T FindComponentInScene<T>() where T : Component
    {
#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<T>(FindObjectsInactive.Include);
#else
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        foreach (var obj in objects)
        {
            // Kiểm tra xem đối tượng có thuộc về một scene đang hoạt động hay không (loại bỏ các assets prefab trong Project)
            if (obj != null && obj.gameObject != null && obj.gameObject.scene.IsValid() && !string.IsNullOrEmpty(obj.gameObject.scene.name))
            {
                return obj;
            }
        }
        return null;
#endif
    }

    private T FindSceneComponentByName<T>(params string[] objectNames) where T : Component
    {
        if (objectNames == null || objectNames.Length == 0) return null;

        T[] objects = Resources.FindObjectsOfTypeAll<T>();

        foreach (string objectName in objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName)) continue;

            foreach (T obj in objects)
            {
                if (!IsSceneComponent(obj)) continue;

                if (string.Equals(obj.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return obj;
                }
            }
        }

        foreach (string objectName in objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName)) continue;

            foreach (T obj in objects)
            {
                if (!IsSceneComponent(obj)) continue;

                if (obj.gameObject.name.IndexOf(objectName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return obj;
                }
            }
        }

        return null;
    }

    private bool IsSceneComponent(Component component)
    {
        return component != null &&
               component.gameObject != null &&
               component.gameObject.scene.IsValid() &&
               !string.IsNullOrEmpty(component.gameObject.scene.name);
    }

    private async void OnPopupStart()
    {
        if (popupStudio != null)
        {
            popupStudio.OnCancel -= OnPopupCancel;
            popupStudio.OnStart -= OnPopupStart;
        }

        Debug.Log("[CustomCharacterPanel] Nhấn Start từ Popup Studio. Tiến hành lưu tiếp...");
        bool success = await SaveCharacter();
        if (success && _isCharacterSaved && _savedCastData != null)
        {
            // Tự động chuyển cảnh sau khi lưu thành công
            BandSelectionManager.ClearSelection();
            StartCoroutine(CheckARCoreAndTransition(_savedCastData));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Giao diện Ẩn/Hiện UI (Gộp từ UIController)
    // ─────────────────────────────────────────────────────────────────────────

    public void HideUI()
    {
        if (useBlackScreenTransition)
        {
            RunTransition(HideUI_Internal);
        }
        else
        {
            HideUI_Internal();
        }
    }

    private void HideUI_Internal()
    {
        if (arUiPanels == null || arUiPanels.Count == 0)
        {
            Debug.LogWarning("[CustomCharacterPanelController] Không có phần tử UI nào được thiết lập để ẩn!");
            return;
        }

        m_UiActiveStates.Clear();

        foreach (var uiObj in arUiPanels)
        {
            if (uiObj == null) continue;

            // Bỏ qua nút showUiButton để nó có thể hiển thị độc lập
            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            // Lưu scale ban đầu nếu chưa lưu (chỉ lưu 1 lần duy nhất để giữ đúng scale gốc)
            if (!m_UiOriginalScales.ContainsKey(uiObj))
            {
                m_UiOriginalScales[uiObj] = uiObj.transform.localScale;
            }

            // Lưu trạng thái hoạt động hiện tại
            m_UiActiveStates[uiObj] = uiObj.activeSelf;
            
            if (uiObj.activeSelf)
            {
                if (useBlackScreenTransition)
                {
                    // Nếu dùng chớp đen, ẩn đột ngột vì đã có màn đen che phủ
                    uiObj.SetActive(false);
                }
                else
                {
                    // Chạy hiệu ứng làm mờ dần và thu nhỏ nhẹ trước khi tắt
                    CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                    cg.DOKill();
                    uiObj.transform.DOKill();
                    cg.interactable = false;
                    cg.blocksRaycasts = false;

                    GameObject targetObj = uiObj; // Tránh biến đóng kín (closure) thay đổi giá trị
                    Vector3 targetScale = m_UiOriginalScales[uiObj] * 0.95f; // Thu nhỏ 5% scale gốc
                    cg.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase);
                    uiObj.transform.DOScale(targetScale, uiFadeDuration).SetEase(uiFadeEase).OnComplete(() =>
                    {
                        targetObj.SetActive(false);
                    });
                }
            }
        }

        // Hiện nút Show UI lên với hiệu ứng nảy nhẹ
        if (showUiButton != null)
        {
            showUiButton.gameObject.SetActive(true);
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();

            cgShow.DOKill();
            showUiButton.transform.DOKill();
            cgShow.interactable = false;
            cgShow.blocksRaycasts = false;

            if (useBlackScreenTransition)
            {
                cgShow.alpha = 1f;
                showUiButton.transform.localScale = Vector3.one;
                cgShow.interactable = true;
                cgShow.blocksRaycasts = true;
            }
            else
            {
                cgShow.alpha = 0f;
                showUiButton.transform.localScale = Vector3.zero;
                cgShow.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                showUiButton.transform.DOScale(1f, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                {
                    cgShow.interactable = true;
                    cgShow.blocksRaycasts = true;
                });
            }
        }

    }

    public void ShowUI()
    {
        if (useBlackScreenTransition)
        {
            RunTransition(ShowUI_Internal);
        }
        else
        {
            ShowUI_Internal();
        }
    }

    private void ShowUI_Internal()
    {
        if (arUiPanels == null || arUiPanels.Count == 0) return;

        // Ẩn nút Show UI đi với hiệu ứng thu nhỏ
        if (showUiButton != null)
        {
            var cgShow = showUiButton.GetComponent<CanvasGroup>();
            if (cgShow == null) cgShow = showUiButton.gameObject.AddComponent<CanvasGroup>();

            cgShow.DOKill();
            showUiButton.transform.DOKill();
            cgShow.interactable = false;
            cgShow.blocksRaycasts = false;

            if (useBlackScreenTransition)
            {
                showUiButton.gameObject.SetActive(false);
            }
            else
            {
                cgShow.DOFade(0f, uiFadeDuration).SetEase(uiFadeEase);
                showUiButton.transform.DOScale(0f, uiFadeDuration).SetEase(Ease.InBack).OnComplete(() =>
                {
                    showUiButton.gameObject.SetActive(false);
                });
            }
        }

        // Khôi phục trạng thái hoạt động của các phần tử
        foreach (var uiObj in arUiPanels)
        {
            if (uiObj == null) continue;

            if (showUiButton != null && uiObj == showUiButton.gameObject)
            {
                continue;
            }

            // Lấy scale ban đầu của đối tượng (hoặc lưu lại nếu chưa có)
            Vector3 originalScale = Vector3.one;
            if (m_UiOriginalScales.TryGetValue(uiObj, out Vector3 savedScale))
            {
                originalScale = savedScale;
            }
            else
            {
                originalScale = uiObj.transform.localScale;
                m_UiOriginalScales[uiObj] = originalScale;
            }

            if (m_UiActiveStates.TryGetValue(uiObj, out bool wasActive))
            {
                if (wasActive)
                {
                    if (useBlackScreenTransition)
                    {
                        uiObj.SetActive(true);
                        uiObj.transform.localScale = originalScale;
                    }
                    else
                    {
                        uiObj.SetActive(true);
                        CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                        if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                        cg.DOKill();
                        uiObj.transform.DOKill();
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                        cg.alpha = 0f;
                        uiObj.transform.localScale = originalScale * 0.95f;

                        cg.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                        uiObj.transform.DOScale(originalScale, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                        {
                            cg.interactable = true;
                            cg.blocksRaycasts = true;
                        });
                    }
                }
                else
                {
                    uiObj.SetActive(false);
                }
            }
            else
            {
                if (useBlackScreenTransition)
                {
                    uiObj.SetActive(true);
                    uiObj.transform.localScale = originalScale;
                }
                else
                {
                    uiObj.SetActive(true);
                    CanvasGroup cg = uiObj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = uiObj.AddComponent<CanvasGroup>();

                    cg.DOKill();
                    uiObj.transform.DOKill();
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                    cg.alpha = 0f;
                    uiObj.transform.localScale = originalScale * 0.95f;

                    cg.DOFade(1f, uiFadeDuration).SetEase(uiFadeEase);
                    uiObj.transform.DOScale(originalScale, uiFadeDuration + 0.1f).SetEase(uiShowEase).OnComplete(() =>
                    {
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    });
                }
            }
        }

    }

    private void CreateFadeOverlay()
    {
        Canvas canvas = null;
#if UNITY_2023_1_OR_NEWER
        canvas = FindAnyObjectByType<Canvas>();
#else
        canvas = FindObjectOfType<Canvas>();
#endif
        if (canvas != null)
        {
            GameObject overlayObj = new GameObject("UI_FadeOverlay_Dynamic");
            overlayObj.transform.SetParent(canvas.transform, false);
            overlayObj.transform.SetAsLastSibling();

            RectTransform rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            m_FadeOverlay = overlayObj.AddComponent<Image>();
            m_FadeOverlay.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f);
            m_FadeOverlay.raycastTarget = false;
        }
    }

    private void RunTransition(System.Action middleAction)
    {
        if (m_FadeOverlay == null)
        {
            middleAction?.Invoke();
            return;
        }

        m_FadeOverlay.raycastTarget = true;
        m_FadeOverlay.transform.SetAsLastSibling();
        m_FadeOverlay.DOKill();
        
        m_FadeOverlay.DOColor(transitionColor, fadeOutDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                middleAction?.Invoke();

                m_FadeOverlay.DOColor(new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f), fadeInDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        m_FadeOverlay.raycastTarget = false;
                    });
            });
    }

    private void AutoAssignUIReferences()
    {
        if (hideUiButton == null)
        {
            GameObject hideObj = GameObject.Find("Hiden UI");
            if (hideObj != null)
            {
                hideUiButton = hideObj.GetComponent<Button>();
            }
            else
            {
                Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
                foreach (var btn in allButtons)
                {
                    if (btn.gameObject.name == "Hiden UI" || btn.gameObject.name.Contains("HidenUI"))
                    {
                        hideUiButton = btn;
                        break;
                    }
                }
            }
        }

        if (showUiButton == null)
        {
            Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
            foreach (var btn in allButtons)
            {
                if (btn.gameObject.name == "Show UI Button" || btn.gameObject.name == "Show UI" || btn.gameObject.name.Contains("ShowUI"))
                {
                    showUiButton = btn;
                    break;
                }
            }
        }

        if (arUiPanels == null || arUiPanels.Count == 0)
        {
            arUiPanels = new List<GameObject>();
            GameObject mainUiAr = GameObject.Find("UI AR");
            if (mainUiAr == null)
            {
                Canvas canvas = null;
#if UNITY_2023_1_OR_NEWER
                canvas = FindAnyObjectByType<Canvas>();
#else
                canvas = FindObjectOfType<Canvas>();
#endif
                if (canvas != null)
                {
                    Transform arTrans = canvas.transform.Find("UI AR");
                    if (arTrans != null)
                    {
                        mainUiAr = arTrans.gameObject;
                    }
                }
            }

            if (mainUiAr != null)
            {
                for (int i = 0; i < mainUiAr.transform.childCount; i++)
                {
                    Transform child = mainUiAr.transform.GetChild(i);
                    if (showUiButton != null && child == showUiButton.transform)
                    {
                        continue;
                    }
                    arUiPanels.Add(child.gameObject);
                }
                Debug.Log($"[CustomCharacterPanelController] Tự động thêm {arUiPanels.Count} phần tử con của 'UI AR' vào danh sách ẩn.");
            }
            else if (hideUiButton != null && hideUiButton.transform.parent != null)
            {
                Transform parentTrans = hideUiButton.transform.parent;
                for (int i = 0; i < parentTrans.childCount; i++)
                {
                    Transform child = parentTrans.GetChild(i);
                    if (showUiButton != null && child == showUiButton.transform)
                    {
                        continue;
                    }
                    arUiPanels.Add(child.gameObject);
                }
                Debug.Log($"[CustomCharacterPanelController] Fallback: Tự động thêm {arUiPanels.Count} phần tử con của parent vào danh sách ẩn.");
            }
        }
    }

    private void OnPopupCancel()
    {
        if (popupStudio != null)
        {
            popupStudio.OnCancel -= OnPopupCancel;
            popupStudio.OnStart -= OnPopupStart;
        }
        Debug.Log("[CustomCharacterPanel] Người dùng đóng popup và hủy lưu.");
    }
}
