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

    [Header("Tab Toggle Buttons")]
    [SerializeField] private Button characterTabButton;
    [SerializeField] private Button instrumentTabButton;
    [SerializeField] private Button animationTabButton;

    [Header("Tab Images & Text")]
    [SerializeField] private TextMeshProUGUI titleText; // "NAME", "INSTRUMENT", "ANIMATION"

    [System.Serializable]
    public struct TabSpriteGroup
    {
        public Sprite selectedSprite;
        public Sprite unselectedSprite;
    }

    [Header("Tab Sprites")]
    [SerializeField] private TabSpriteGroup characterTabSprites;
    [SerializeField] private TabSpriteGroup instrumentTabSprites;
    [SerializeField] private TabSpriteGroup animationTabSprites;

    [Header("ScrollView Containers")]
    [SerializeField] private GameObject characterScrollView;
    [SerializeField] private GameObject instrumentScrollView;
    [SerializeField] private GameObject animationScrollView;

    [SerializeField] private Transform instrumentContent;
    [SerializeField] private Transform animationContent;

    [Header("Prefabs")]
    [SerializeField] private GameObject audioRecordButtonPrefab;
    [SerializeField] private GameObject audioPresetButtonPrefab;
    [SerializeField] private GameObject animationButtonPrefab;
    [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

    [Header("Cast Panel Integration")]
    [SerializeField] private CastPanelController castPanelController;

    [Header("Audio Panel Integration")]
    [SerializeField] private AudioPanelController audioPanelController;

    [Header("Animation Panel Integration")]
    [SerializeField] private AnimPanelController animPanelController;

    [Header("Preview Settings")]
    [SerializeField] private Transform previewContainer;
    [SerializeField] private List<Sprite> avatarSprites = new List<Sprite>();

    [Header("Microphone Recording")]
    [SerializeField] private Button micButton; // Nút ghi âm nổi bên phải
    [SerializeField] private TextMeshProUGUI recordingStatusText;

    [Header("Action Buttons")]
    [SerializeField] private Button actionButton; // Nút SELECT hoặc START ở dưới cùng
    [SerializeField] private TextMeshProUGUI actionText;
    [SerializeField] private Button saveButton; // Nút SAVE nhân vật ở góc trên/dưới



    [Header("Presets Configuration")]
    [SerializeField] private List<InstrumentPreset> instrumentPresets = new List<InstrumentPreset>();
    [SerializeField] private List<AnimationPreset> animationPresets = new List<AnimationPreset>();

    // Trạng thái hiện tại của việc thiết kế nhân vật
    private string _selectedPrefabName;
    private string _selectedAudioId;
    private string _selectedAnimationId;

    private GameObject _previewInstance;
    private AudioSource _previewAudioSource;

    private enum CustomTab { Character, Instrument, Animation }
    private CustomTab _currentTab = CustomTab.Character;

    // Ghi âm
    private string _microphoneName;
    private AudioClip _recordingClip;
    private bool _isRecording = false;
    private float _recordingStartTime;
    private const int MaxRecordingDuration = 10; // Tối đa 10 giây
    private string _customRecordingId; // Lưu ID của ghi âm tùy chỉnh trong phiên này
    private bool _shouldTransitionAfterSave = false; // Có chuyển scene sau khi lưu xong hay không



    [System.Serializable]
    public struct InstrumentPreset
    {
        public string displayName;
        public string instrumentId;
        public Sprite icon;
    }

    [System.Serializable]
    public struct AnimationPreset
    {
        public string displayName;
        public string animationId;
    }

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private float slideOffset = 250f;

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

        // Gán sự kiện cho các nút chuyển tab
        if (characterTabButton != null) characterTabButton.onClick.AddListener(() => SwitchTab(CustomTab.Character, true));
        if (instrumentTabButton != null) instrumentTabButton.onClick.AddListener(() => SwitchTab(CustomTab.Instrument, true));
        if (animationTabButton != null) animationTabButton.onClick.AddListener(() => SwitchTab(CustomTab.Animation, true));

        // Nút BACK
        if (backButton != null) backButton.onClick.AddListener(ClosePanel);

        // Nút Microphone
        if (micButton != null) micButton.onClick.AddListener(OnMicButtonClicked);

        // Nút SELECT/START
        if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);

        // Nút SAVE
        if (saveButton != null) saveButton.onClick.AddListener(() => {
            _shouldTransitionAfterSave = false;
            SaveCharacter();
        });

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
                    
                    // Hiện lại nút mic nổi vì đang chọn preset nhạc cụ
                    if (micButton != null && _currentTab == CustomTab.Instrument)
                    {
                        micButton.gameObject.SetActive(true);
                    }
                }
            };
            audioPanelController.OnCustomAudioSelected += (recordingId) =>
            {
                _selectedAudioId = recordingId;
                
                // Ẩn nút mic nổi vì đã chọn bản thu âm
                if (micButton != null)
                {
                    micButton.gameObject.SetActive(false);
                }
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
                    SpawnCharacterPreview(_selectedPrefabName);
                    SelectDefaultAnimationForCurrentCharacter();
                    PopulateAnimations();
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
            };
        }
    }

    private void Start()
    {
        // Ẩn các popup khi bắt đầu
        if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);

        // Đổ dữ liệu ban đầu
        PopulateAnimations();
    }

    private void OnEnable()
    {
        // Khi bật panel, mặc định chọn tab đầu tiên và không chạy hiệu ứng chuyển động
        SwitchTab(CustomTab.Character, false);
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

        // 1. Cập nhật Sprite và hiệu ứng các nút Tab
        UpdateTabButtonVisuals(characterTabButton, (tab == CustomTab.Character) ? characterTabSprites.selectedSprite : characterTabSprites.unselectedSprite, tab == CustomTab.Character, animate);
        UpdateTabButtonVisuals(instrumentTabButton, (tab == CustomTab.Instrument) ? instrumentTabSprites.selectedSprite : instrumentTabSprites.unselectedSprite, tab == CustomTab.Instrument, animate);
        UpdateTabButtonVisuals(animationTabButton, (tab == CustomTab.Animation) ? animationTabSprites.selectedSprite : animationTabSprites.unselectedSprite, tab == CustomTab.Animation, animate);

        // 2. Cập nhật tiêu đề tương ứng với thiết kế Figma kèm hiệu ứng fade & scale nhẹ
        if (titleText != null)
        {
            string newTitle = "";
            switch (tab)
            {
                case CustomTab.Character: newTitle = "NAME"; break;
                case CustomTab.Instrument: newTitle = "INSTRUMENT"; break;
                case CustomTab.Animation: 
                    var config = GetSelectedCastConfig();
                    if (config != null && !string.IsNullOrEmpty(config.Name))
                    {
                        newTitle = $"ANIMATION - {config.Name.ToUpper()} ({config.animations.Count})";
                    }
                    else
                    {
                        newTitle = "ANIMATION";
                    }
                    break;
            }

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

        // 4. Hiển thị nút Microphone nổi nếu ở tab Instrument và chưa có bản ghi âm nào cho nhân vật
        if (micButton != null)
        {
            bool hasRecording = !string.IsNullOrEmpty(_customRecordingId) || 
                                (!string.IsNullOrEmpty(_selectedAudioId) && _selectedAudioId.StartsWith("rec_"));
            micButton.gameObject.SetActive(tab == CustomTab.Instrument && !hasRecording);
        }

        // 5. Cập nhật nội dung Action Button
        if (actionText != null)
        {
            actionText.text = (tab == CustomTab.Animation) ? "START" : "SELECT";
        }

        // Làm mới danh sách Nhạc cụ (vì có thể có bản ghi âm mới tải từ Firebase)
        if (tab == CustomTab.Instrument)
        {
            PopulateInstruments();
        }
        else if (tab == CustomTab.Animation)
        {
            PopulateAnimations();
        }
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

    private void UpdateTabButtonVisuals(Button button, Sprite targetSprite, bool isSelected, bool animate)
    {
        if (button != null && button.image != null)
        {
            button.image.sprite = targetSprite;
        }

        if (button != null)
        {
            if (animate)
            {
                button.transform.DOKill();
                if (isSelected)
                {
                    button.transform.localScale = Vector3.one;
                    button.transform.DOScale(1.1f, 0.2f).SetEase(Ease.OutQuad)
                        .OnComplete(() => button.transform.DOScale(1.0f, 0.15f).SetEase(Ease.InQuad));
                }
                else
                {
                    button.transform.DOScale(1.0f, 0.2f).SetEase(Ease.OutQuad);
                }
            }
            else
            {
                button.transform.localScale = Vector3.one;
            }
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
                _selectedAudioId = !string.IsNullOrEmpty(audioPanelController.SelectedAudioConfig.Name) ? audioPanelController.SelectedAudioConfig.Name : audioPanelController.SelectedPrefab.name;
            }
        }
        else
        {
            ClearChildren(instrumentContent);

            bool characterCanRecord = true;

            // Chỉ tạo nút bản thu âm đã ghi khi đã có ghi âm cho nhân vật
            if (characterCanRecord && !string.IsNullOrEmpty(_customRecordingId))
            {
                GameObject obj = Instantiate(audioRecordButtonPrefab, instrumentContent);
                var itemUI = obj.GetComponent<CustomAudioRecordItemUI>();
                if (itemUI != null)
                {
                    Sprite audioSprite = null;
                    if (micButton != null && micButton.image != null) audioSprite = micButton.image.sprite;
                    itemUI.SetupRecordedItem("Record", audioSprite, () =>
                    {
                        _selectedAudioId = _customRecordingId;
                        PreviewAudio(_selectedAudioId);
                        UpdateSelectionVisuals(instrumentContent, _customRecordingId);
                    }, () =>
                    {
                        DeleteCustomRecording(_customRecordingId);
                    });
                }
            }

            if (instrumentPresets.Count == 0)
            {
                instrumentPresets.Add(new InstrumentPreset { displayName = "Drum", instrumentId = "drum" });
                instrumentPresets.Add(new InstrumentPreset { displayName = "Saxophone", instrumentId = "saxophone" });
            }

            foreach (var preset in instrumentPresets)
            {
                GameObject obj = Instantiate(audioPresetButtonPrefab, instrumentContent);
                var itemUI = obj.GetComponent<CustomCharacterItemUI>();
                if (itemUI != null)
                {
                    itemUI.Setup(preset.displayName, preset.icon, () =>
                    {
                        _selectedAudioId = preset.instrumentId;
                        PreviewAudio(_selectedAudioId);
                        UpdateSelectionVisuals(instrumentContent, preset.instrumentId);
                    });
                }
                else
                {
                    Button standardBtn = obj.GetComponent<Button>();
                    if (standardBtn != null)
                    {
                        standardBtn.onClick.RemoveAllListeners();
                        standardBtn.onClick.AddListener(() =>
                        {
                            _selectedAudioId = preset.instrumentId;
                            PreviewAudio(_selectedAudioId);
                            UpdateSelectionVisuals(instrumentContent, preset.instrumentId);
                        });

                        var nameText = obj.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (nameText != null) nameText.text = preset.displayName;

                        Image[] images = obj.GetComponentsInChildren<Image>(true);
                        foreach (var img in images)
                        {
                            if (img.gameObject != obj)
                            {
                                img.sprite = preset.icon;
                                img.gameObject.SetActive(preset.icon != null);
                                break;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(_selectedAudioId))
            {
                UpdateSelectionVisuals(instrumentContent, _selectedAudioId);
            }
        }
    }

    private CastAnimationConfig GetSelectedCastConfig()
    {
        CastAnimationConfig config = null;
        if (_previewInstance != null)
        {
            config = _previewInstance.GetComponent<CastAnimationConfig>();
        }

        if (config == null && !string.IsNullOrEmpty(_selectedPrefabName))
        {
            GameObject prefab = GetCharacterPrefab(_selectedPrefabName);
            if (prefab != null)
            {
                config = prefab.GetComponent<CastAnimationConfig>();
            }
        }
        return config;
    }

    private void SelectDefaultAnimationForCurrentCharacter()
    {
        if (animPanelController != null)
        {
            return;
        }

        CastAnimationConfig config = GetSelectedCastConfig();
        if (config != null && config.animations != null && config.animations.Count > 0)
        {
            if (config.animations[0].animation != null)
            {
                _selectedAnimationId = config.animations[0].animation.name;
                PlayPreviewAnimation(_selectedAnimationId);
            }
        }
        else
        {
            if (animationPresets.Count > 0)
            {
                _selectedAnimationId = animationPresets[0].animationId;
                PlayPreviewAnimation(_selectedAnimationId);
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
        else
        {
            ClearChildren(animationContent);

            CastAnimationConfig config = GetSelectedCastConfig();

            if (config != null && config.animations != null && config.animations.Count > 0)
            {
                foreach (var animInfo in config.animations)
                {
                    if (animInfo.animation == null) continue;

                    GameObject obj = Instantiate(animationButtonPrefab, animationContent);
                    var itemUI = obj.GetComponent<CustomCharacterItemUI>();
                    if (itemUI != null)
                    {
                        string animId = animInfo.animation.name;
                        Sprite animAvatar = animInfo.sprite;
                        string animDisplayName = string.IsNullOrEmpty(animInfo.animName) ? animInfo.animation.name : animInfo.animName;

                        itemUI.Setup(animDisplayName, animAvatar, () =>
                        {
                            _selectedAnimationId = animId;
                            PlayPreviewAnimation(_selectedAnimationId);
                            UpdateSelectionVisuals(animationContent, animDisplayName);
                        });
                    }
                    else
                    {
                        string animId = animInfo.animation.name;
                        Sprite animAvatar = animInfo.sprite;
                        string animDisplayName = string.IsNullOrEmpty(animInfo.animName) ? animInfo.animation.name : animInfo.animName;

                        Button standardBtn = obj.GetComponent<Button>();
                        if (standardBtn != null)
                        {
                            standardBtn.onClick.RemoveAllListeners();
                            standardBtn.onClick.AddListener(() =>
                            {
                                _selectedAnimationId = animId;
                                PlayPreviewAnimation(_selectedAnimationId);
                                UpdateSelectionVisuals(animationContent, animDisplayName);
                            });

                            var nameText = obj.GetComponentInChildren<TextMeshProUGUI>(true);
                            if (nameText != null) nameText.text = animDisplayName;

                            Image[] images = obj.GetComponentsInChildren<Image>(true);
                            foreach (var img in images)
                            {
                                if (img.gameObject != obj)
                                {
                                    img.sprite = animAvatar;
                                    img.gameObject.SetActive(animAvatar != null);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Chọn mặc định animation đầu tiên
                if (config.animations.Count > 0 && config.animations[0].animation != null)
                {
                    string firstAnimId = config.animations[0].animation.name;
                    string firstAnimDisplayName = string.IsNullOrEmpty(config.animations[0].animName) ? firstAnimId : config.animations[0].animName;
                    _selectedAnimationId = firstAnimId;
                    PlayPreviewAnimation(_selectedAnimationId);
                    UpdateSelectionVisuals(animationContent, firstAnimDisplayName);
                }
            }
            else
            {
                if (animationPresets.Count == 0)
                {
                    animationPresets.Add(new AnimationPreset { displayName = "Jump", animationId = "jump" });
                    animationPresets.Add(new AnimationPreset { displayName = "Dance 1", animationId = "dance1" });
                    animationPresets.Add(new AnimationPreset { displayName = "Dance 2", animationId = "dance2" });
                }

                foreach (var preset in animationPresets)
                {
                    GameObject obj = Instantiate(animationButtonPrefab, animationContent);
                    var itemUI = obj.GetComponent<CustomCharacterItemUI>();
                    if (itemUI != null)
                    {
                        itemUI.Setup(preset.displayName, null, () =>
                        {
                            _selectedAnimationId = preset.animationId;
                            PlayPreviewAnimation(_selectedAnimationId);
                            UpdateSelectionVisuals(animationContent, preset.displayName);
                        });
                    }
                    else
                    {
                        Button standardBtn = obj.GetComponent<Button>();
                        if (standardBtn != null)
                        {
                            standardBtn.onClick.RemoveAllListeners();
                            standardBtn.onClick.AddListener(() =>
                            {
                                _selectedAnimationId = preset.animationId;
                                PlayPreviewAnimation(_selectedAnimationId);
                                UpdateSelectionVisuals(animationContent, preset.displayName);
                            });

                            var nameText = obj.GetComponentInChildren<TextMeshProUGUI>(true);
                            if (nameText != null) nameText.text = preset.displayName;

                            Image[] images = obj.GetComponentsInChildren<Image>(true);
                            foreach (var img in images)
                            {
                                if (img.gameObject != obj)
                                {
                                    img.gameObject.SetActive(false);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (animationPresets.Count > 0)
                {
                    _selectedAnimationId = animationPresets[0].animationId;
                    PlayPreviewAnimation(_selectedAnimationId);
                    UpdateSelectionVisuals(animationContent, animationPresets[0].displayName);
                }
            }
        }
    }

    private void UpdateSelectionVisuals(Transform container, string targetId)
    {
        foreach (Transform child in container)
        {
            var itemUI = child.GetComponent<CustomCharacterItemUI>();
            if (itemUI != null)
            {
                // So sánh tên hiển thị hoặc ID hoặc ItemId để highlight
                bool isSelected = itemUI.GetTitle() == targetId || itemUI.GetTitle().Equals(targetId, StringComparison.OrdinalIgnoreCase) || itemUI.ItemId == targetId;
                
                // Hỗ trợ thêm việc so sánh với _selectedAnimationId (tên clip thực tế)
                if (!isSelected && container == animationContent)
                {
                    isSelected = _selectedAnimationId == itemUI.ItemId || _selectedAnimationId == itemUI.GetTitle();
                }

                // Fallback nếu targetId là ID nhạc cụ/hoạt ảnh thay vì tên hiển thị
                if (targetId.StartsWith("rec_"))
                {
                    isSelected = itemUI.GetTitle().Contains(targetId) || _selectedAudioId == targetId;
                }

                itemUI.SetSelected(isSelected);
            }
            else
            {
                var recordItemUI = child.GetComponent<CustomAudioRecordItemUI>();
                if (recordItemUI != null)
                {
                    // So sánh tên hiển thị hoặc ID hoặc ItemId để highlight
                    bool isSelected = recordItemUI.GetTitle() == targetId || recordItemUI.GetTitle().Equals(targetId, StringComparison.OrdinalIgnoreCase) || recordItemUI.ItemId == targetId;
                    
                    // Fallback nếu targetId là ID nhạc cụ/hoạt ảnh thay vì tên hiển thị
                    if (targetId.StartsWith("rec_"))
                    {
                        isSelected = recordItemUI.GetTitle().Contains(targetId) || _selectedAudioId == targetId;
                    }

                    recordItemUI.SetSelected(isSelected);
                }
                else
                {
                    Button standardBtn = child.GetComponent<Button>();
                    if (standardBtn != null)
                    {
                        var nameText = child.GetComponentInChildren<TextMeshProUGUI>(true);
                        string nameVal = nameText != null ? nameText.text : "";
                        bool isSelected = nameVal == targetId || nameVal.Equals(targetId, StringComparison.OrdinalIgnoreCase);

                        if (!isSelected && container == animationContent)
                        {
                            isSelected = _selectedAnimationId == nameVal;
                        }

                        Image btnImage = standardBtn.image != null ? standardBtn.image : standardBtn.GetComponent<Image>();
                        if (btnImage != null)
                        {
                            btnImage.color = isSelected ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
                        }
                    }
                }
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
            if (rotator != null)
            {
                rotator.SetTarget(_previewInstance.transform);
                rotator.ResetRotation();
            }

            // Nếu đã chọn hoạt ảnh trước đó, chạy luôn hoạt ảnh trên model preview mới
            if (!string.IsNullOrEmpty(_selectedAnimationId))
            {
                PlayPreviewAnimation(_selectedAnimationId);
            }
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

    private void PlayPreviewAnimation(string animId)
    {
        if (_previewInstance == null || string.IsNullOrEmpty(animId)) return;
        Move move = _previewInstance.GetComponent<Move>();
        if (move != null)
        {
            move.PlayAnimationOnce(animId, 0.15f);
        }
    }

    private GameObject GetCharacterPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        if (characterPrefabs != null)
        {
            foreach (var prefab in characterPrefabs)
            {
                if (prefab != null && prefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

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

        GameObject resPrefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(prefabName);
        return resPrefab;
    }

    private Sprite GetAvatarSprite(string prefabName)
    {
        // Thử lấy avatar từ component CastAnimationConfig của prefab trước
        GameObject prefab = GetCharacterPrefab(prefabName);
        if (prefab != null)
        {
            var config = prefab.GetComponent<CastAnimationConfig>();
            if (config != null && config.characterAvatar != null)
            {
                return config.characterAvatar;
            }
        }

        if (avatarSprites != null)
        {
            foreach (var sprite in avatarSprites)
            {
                if (sprite != null && sprite.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }
            }
        }
        return Resources.Load<Sprite>("Avatars/" + prefabName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ghi âm âm thanh (Microphone) & Tải lên Firebase Firestore
    // ─────────────────────────────────────────────────────────────────────────

    private void OnMicButtonClicked()
    {
        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartFloatingRecording();
        }
    }

    private void StartFloatingRecording()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[CustomCharacterPanel] Không tìm thấy thiết bị Microphone nào!");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "No Microphone Found!";
                recordingStatusText.gameObject.SetActive(true);
                Invoke("HideRecordingStatus", 2f);
            }
            return;
        }

        _microphoneName = Microphone.devices[0];
        _recordingClip = Microphone.Start(_microphoneName, false, MaxRecordingDuration, 44100);
        _isRecording = true;
        _recordingStartTime = Time.time;

        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Recording: 0s";
            recordingStatusText.gameObject.SetActive(true);
        }

        SetPanelInteractive(false);

        StartCoroutine(FloatingRecordingRoutine());
    }

    private IEnumerator FloatingRecordingRoutine()
    {
        float duration = (float)MaxRecordingDuration;
        float elapsed = 0f;

        while (elapsed < duration && _isRecording)
        {
            yield return null;
            elapsed = Time.time - _recordingStartTime;

            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Recording: {(int)elapsed}s";
            }
        }

        if (_isRecording)
        {
            StopRecording();
        }
    }

    private void StartScrollViewRecording(CustomAudioRecordItemUI recordItem)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[CustomCharacterPanel] Không tìm thấy thiết bị Microphone nào!");
            if (recordingStatusText != null)
            {
                recordingStatusText.text = "No Microphone Found!";
                recordingStatusText.gameObject.SetActive(true);
                Invoke("HideRecordingStatus", 2f);
            }
            return;
        }

        _microphoneName = Microphone.devices[0];
        // Bắt đầu thu âm mono tần số 44.1kHz, kéo dài tối đa 10s
        _recordingClip = Microphone.Start(_microphoneName, false, MaxRecordingDuration, 44100);
        _isRecording = true;
        _recordingStartTime = Time.time;

        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Recording: 0s";
            recordingStatusText.gameObject.SetActive(true);
        }

        // Vô hiệu hóa tương tác các nút khác trong khi ghi âm
        SetPanelInteractive(false);

        // Chạy hiệu ứng đếm ngược 360 độ radial fill và viền đỏ
        StartCoroutine(ScrollViewRecordingRoutine(recordItem));
    }

    private void SetPanelInteractive(bool interactive)
    {
        if (backButton != null) backButton.interactable = interactive;
        if (characterTabButton != null) characterTabButton.interactable = interactive;
        if (instrumentTabButton != null) instrumentTabButton.interactable = interactive;
        if (animationTabButton != null) animationTabButton.interactable = interactive;
        if (actionButton != null) actionButton.interactable = interactive;
        if (saveButton != null) saveButton.interactable = interactive;
    }

    private IEnumerator ScrollViewRecordingRoutine(CustomAudioRecordItemUI recordItem)
    {
        float duration = (float)MaxRecordingDuration;
        float elapsed = 0f;

        recordItem.SetRecordingState(true);

        while (elapsed < duration)
        {
            yield return null;
            elapsed = Time.time - _recordingStartTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            recordItem.SetRadialFill(progress);

            if (recordingStatusText != null)
            {
                recordingStatusText.text = $"Recording: {(int)elapsed}s";
            }
        }

        recordItem.SetRadialFill(1f);
        recordItem.SetRecordingState(false);

        // Dừng ghi âm và lưu dữ liệu
        StopRecording();
    }

    private async void StopRecording()
    {
        if (!_isRecording) return;

        int lastSamplePos = Microphone.GetPosition(_microphoneName);
        Microphone.End(_microphoneName);
        _isRecording = false;

        // Bật lại các tương tác của panel
        SetPanelInteractive(true);

        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Processing...";
        }

        if (lastSamplePos <= 0)
        {
            Debug.LogWarning("[CustomCharacterPanel] Ghi âm trống!");
            if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);
            if (_recordingClip != null) Destroy(_recordingClip);
            PopulateInstruments(); // Refresh lại UI về trạng thái chưa ghi âm
            return;
        }

        // Tạo clip mới khớp với độ dài thực tế đã thu âm
        int channels = _recordingClip.channels;
        int frequency = _recordingClip.frequency;
        float[] samples = new float[lastSamplePos * channels];
        _recordingClip.GetData(samples, 0);

        AudioClip trimmedClip = AudioClip.Create("TempTrimmed", lastSamplePos, channels, frequency, false);
        trimmedClip.SetData(samples, 0);

        // Chuyển đổi sang file WAV nhị phân hoàn toàn trong bộ nhớ RAM
        byte[] wavBytes = WavUtility.FromAudioClip(trimmedClip);

        // Hủy clip ghi âm ngay lập tức để tránh rò rỉ RAM trong Unity
        Destroy(_recordingClip);
        Destroy(trimmedClip);

        if (wavBytes == null)
        {
            Debug.LogError("[CustomCharacterPanel] Lỗi mã hóa WAV bytes!");
            if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);
            PopulateInstruments();
            return;
        }

        // --- MÃ HÓA AES ---
        byte[] encryptedBytes;
        try
        {
            encryptedBytes = AudioEncryption.Encrypt(wavBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError("[CustomCharacterPanel] Lỗi mã hóa âm thanh: " + ex.Message);
            encryptedBytes = wavBytes; // Fallback
        }

        // Mã hóa Base64 để gửi lên Firestore
        string base64 = Convert.ToBase64String(encryptedBytes);
        wavBytes = null;
        encryptedBytes = null;

        string recId = "rec_" + DateTime.UtcNow.Ticks;
        string recName = "Rec " + DateTime.Now.ToString("HH:mm:ss");

        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Uploading to Firebase...";
        }

        // Upload dữ liệu Base64 lên Firestore
        bool success = await MainMenuDataManager.Instance.SaveRecordingAsync(recId, recName, base64);

        base64 = null; // Giải phóng chuỗi Base64
        GC.Collect();  // Gọi Garbage Collector dọn dẹp bộ nhớ RAM lập tức

        if (success)
        {
            if (recordingStatusText != null) recordingStatusText.text = "Saved to Firebase!";
            
            // Lưu lại ID bản ghi âm tùy chỉnh trong phiên này
            _customRecordingId = recId;
            _selectedAudioId = recId;

            // Đồng bộ sang AudioPanelController nếu có
            if (audioPanelController != null)
            {
                audioPanelController.SetCustomRecordingId(recId);
                audioPanelController.SelectAudioById(recId);
            }

            PopulateInstruments(); // Refresh danh sách (sẽ hiển thị thẻ đã thu và ẩn nút Record)
            
            // Ẩn nút Microphone nổi sau khi đã ghi âm xong
            if (micButton != null) micButton.gameObject.SetActive(false);
        }
        else
        {
            if (recordingStatusText != null) recordingStatusText.text = "Upload Failed!";
            PopulateInstruments();
        }

        Invoke("HideRecordingStatus", 2.0f);
    }

    private async void DeleteCustomRecording(string recordingId)
    {
        if (string.IsNullOrEmpty(recordingId)) return;

        if (recordingStatusText != null)
        {
            recordingStatusText.text = "Deleting recording...";
            recordingStatusText.gameObject.SetActive(true);
        }

        bool success = await MainMenuDataManager.Instance.DeleteRecordingAsync(recordingId);

        if (success)
        {
            if (recordingStatusText != null) recordingStatusText.text = "Deleted!";
            
            _customRecordingId = null;

            // Nếu âm thanh đang chọn là bản ghi âm bị xóa, đổi về nhạc cụ mặc định đầu tiên
            if (_selectedAudioId == recordingId)
            {
                if (instrumentPresets.Count > 0)
                {
                    _selectedAudioId = instrumentPresets[0].instrumentId;
                }
                else
                {
                    _selectedAudioId = "";
                }
            }

            // Đồng bộ sang AudioPanelController nếu có
            if (audioPanelController != null)
            {
                audioPanelController.SetCustomRecordingId(null);
                audioPanelController.SelectAudioById(_selectedAudioId);
            }

            PopulateInstruments(); // Tải lại danh sách
            
            // Hiện lại nút Microphone nổi
            if (micButton != null && _currentTab == CustomTab.Instrument)
            {
                micButton.gameObject.SetActive(true);
            }
        }
        else
        {
            if (recordingStatusText != null) recordingStatusText.text = "Delete Failed!";
            PopulateInstruments();
        }

        Invoke("HideRecordingStatus", 1.5f);
    }

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
            // Âm thanh tự thu: Tải động từ Firebase Firestore
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
    // Chức năng Lưu Nhân vật (Firestore)
    // ─────────────────────────────────────────────────────────────────────────

    private async void SaveCharacter()
    {
        string charName = !string.IsNullOrEmpty(_selectedPrefabName) ? _selectedPrefabName : "Custom Character";

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

        // Đẩy thẳng cấu hình nhân vật lên Firebase Firestore thông qua API có sẵn
        bool success = await MainMenuDataManager.Instance.CreateCharacterAsync(
            charName, 
            _selectedPrefabName, 
            _selectedAudioId, 
            _selectedAnimationId, 
            animIds
        );

        if (recordingStatusText != null)
        {
            recordingStatusText.text = success ? "Saved Successfully!" : "Save Failed!";
        }

        if (success && _shouldTransitionAfterSave)
        {
            StartCoroutine(TransitionAfterDelayRoutine(charName));
        }
        else
        {
            Invoke("HideRecordingStatus", 2f);
        }
    }

    private IEnumerator TransitionAfterDelayRoutine(string charName)
    {
        yield return new WaitForSeconds(1.0f);
        if (recordingStatusText != null) recordingStatusText.gameObject.SetActive(false);

        // Lưu dữ liệu castData để mang qua Scene AR
        CastData cast = new CastData(charName, _selectedPrefabName, _selectedAudioId, _selectedAnimationId);
        
        // Vào chế độ Custom Character: clear Band Mode selection
        BandSelectionManager.ClearSelection();
        
        StartCoroutine(CheckARCoreAndTransition(cast));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chuyển Scene AR/Non-AR theo cấu hình ARCore
    // ─────────────────────────────────────────────────────────────────────────

    private void OnActionButtonClicked()
    {
        if (_currentTab == CustomTab.Character)
        {
            SwitchTab(CustomTab.Instrument, true);
        }
        else if (_currentTab == CustomTab.Instrument)
        {
            SwitchTab(CustomTab.Animation, true);
        }
        else if (_currentTab == CustomTab.Animation)
        {
            // Thiết lập cờ để chuyển scene sau khi lưu thành công
            _shouldTransitionAfterSave = true;
            SaveCharacter();
        }
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

        bool isARCoreSupported = (ARSession.state != ARSessionState.Unsupported);

#if UNITY_EDITOR
        // Trong Unity Editor, coi như hỗ trợ để sử dụng AR Simulator của Unity
        isARCoreSupported = true;
#endif

        string targetSceneName = isARCoreSupported ? "AR Scene" : "Non-AR Scene";
        Debug.Log($"[CustomCharacterPanel] Thiết bị hỗ trợ ARCore: {isARCoreSupported}. Loading scene: {targetSceneName}");

        // Gán biến castData vào MainMenuDataManager
        MainMenuDataManager.Instance.castData = cast;

        // Chuyển scene mượt mà bằng SceneTransitionManager
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(targetSceneName);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích dọn dẹp
    // ─────────────────────────────────────────────────────────────────────────

    private void ClosePanel()
    {
        if (_previewAudioSource != null) _previewAudioSource.Stop();
        if (_previewInstance != null) Destroy(_previewInstance);
        
        // Chuyển Scene về Main Menu Scene
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene("Main Menu Scene");
        }
        else
        {
            SceneManager.LoadScene("Main Menu Scene");
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
}
