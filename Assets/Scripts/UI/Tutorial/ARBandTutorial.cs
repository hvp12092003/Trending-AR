using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class ARBandTutorialStep
{
    [Tooltip("Tên định danh của bước hướng dẫn")]
    public string stepName;

    [TextArea(2, 5)]
    [Tooltip("Nội dung hướng dẫn hiển thị")]
    public string message;

    [Tooltip("Nút mục tiêu bắt buộc người dùng bấm vào để chuyển qua bước tiếp theo. Nếu để trống (null), người dùng click vào vùng màn hình để qua bước.")]
    public Button targetButton;

    [Tooltip("Bật/tắt hiển thị bàn tay chỉ dẫn.")]
    public bool showHand = true;

    [Tooltip("Khoảng lệch hiển thị của bàn tay chỉ dẫn so với nút mục tiêu.")]
    public Vector2 handOffset = new Vector2(50f, -50f);

    [Tooltip("Góc xoay của bàn tay chỉ dẫn.")]
    public float handAngle = 0f;

    [Tooltip("Bật/tắt hiển thị nhân vật/khung hướng dẫn (Guider).")]
    public bool showGuider = true;

    [Tooltip("Bật/tắt hiệu ứng nhấp nháy/co giãn nút mục tiêu để gây chú ý.")]
    public bool pulseTarget = true;

    [Range(1f, 1.2f)]
    [Tooltip("Tỷ lệ co giãn của nút mục tiêu khi nhấp nháy.")]
    public float targetPulseScale = 1.05f;

    [Tooltip("Bật/tắt hiển thị nút Skip (Bỏ qua) ở bước này.")]
    public bool showSkipButton = true;

    [Tooltip("Danh sách các nút bấm UI cần khóa tương tác ở bước này.")]
    public List<Button> buttonsToDisable = new List<Button>();

    [Tooltip("ScrollRect cần khóa khả năng cuộn ở bước này.")]
    public ScrollRect targetScrollRect;
}

/// <summary>
/// Quản lý giao diện hướng dẫn người dùng cho Scene AR Band.
/// Tự động kiểm tra trạng thái hoàn thành để quyết định hiển thị hoặc bỏ qua.
/// </summary>
public class ARBandTutorial : MonoBehaviour
{
    [Header("Guide UI")]
    [SerializeField] private RectTransform textBox;
    [SerializeField] private TMP_Text tmp;
    [SerializeField] private RectTransform iconHand;
    [SerializeField] private RectTransform guider;


    [Header("Settings")]
    [SerializeField] private string guideId = "ARBandTutorial";
    [SerializeField] private FirstTimeGuideCompletionScope completionScope = FirstTimeGuideCompletionScope.Global;
    [SerializeField] private string customCompletionKey = "";
    [SerializeField] private float autoStartDelay = 0.25f;

    [Header("Steps")]
    [SerializeField] private ARBandTutorialStep step1;
    [SerializeField] private ARBandTutorialStep step2;
    [SerializeField] private ARBandTutorialStep step3;
    [SerializeField] private ARBandTutorialStep step4;
    [SerializeField] private ARBandTutorialStep step5;
    [SerializeField] private ARBandTutorialStep step6;

    private const float GuiderSlideDuration = 0.35f;
    private const float GuiderHiddenPadding = 24f;
    private const float TextBoxShowDuration = 0.2f;
    private const float StepTransitionDuration = 0.22f;
    private const float TextBoxStepScale = 0.96f;
    private const float HandPulseDuration = 0.45f;
    private const float TypewriterCharacterDelay = 0.025f;

    private RectTransform _guideRoot;
    private Canvas _guideCanvas;
    private CanvasGroup _textBoxCanvasGroup;
    private CanvasGroup _iconHandCanvasGroup;
    private Coroutine _startRoutine;
    private Coroutine _typeRoutine;
    private Sequence _textTransitionSequence;
    private Sequence _handPulseSequence;
    private int _currentStepIndex = -1;
    private bool _isRunning;
    private Vector2 _guiderShownPosition;
    private Vector2 _textBoxShownPosition;
    private Vector3 _iconHandOriginalEulerAngles;
    private CanvasGroup _guideCanvasGroup;
    private readonly List<Button> _disabledButtonsInCurrentStep = new List<Button>();
    private ScrollRect _disabledScrollRectInCurrentStep;

    // Quản lý Canvas override để đẩy nút bấm lên trên Blocker
    private Button _focusedTargetButton;
    private Canvas _addedCanvas;
    private GraphicRaycaster _addedRaycaster;
    private bool _hadCanvasOriginally;
    private bool _hadRaycasterOriginally;
    private bool _originalOverrideSorting;
    private int _originalSortingOrder;
    private int _tutorialCanvasSortingOrder = 10;
    private int _lastTransitionFrame = -1;

    // Nút Bỏ qua (Skip)
    private Button _skipButton;

    // Quản lý nhấp nháy/co giãn nút mục tiêu
    private Sequence _targetPulseSequence;
    private Transform _pulsingTarget;
    private Vector3 _originalTargetScale;

    // Quản lý Canvas override để đẩy iconHand và textBox lên trên nút bấm mục tiêu
    private Canvas _handCanvas;
    private bool _hadHandCanvasOriginally;
    private bool _originalHandOverrideSorting;
    private int _originalHandSortingOrder;

    private Canvas _textBoxCanvas;
    private bool _hadTextBoxCanvasOriginally;
    private bool _originalTextBoxOverrideSorting;
    private int _originalTextBoxSortingOrder;

    public bool IsRunning => _isRunning;
    public int CurrentStepIndex => _currentStepIndex;

    private void Awake()
    {
        ResolveReferences();
        CachePositions();
        HideGuideObjects();
    }

    private void Start()
    {
        // Kiểm tra xem người dùng đã hoàn thành hướng dẫn chưa
        if (FirstTimeGuideProgress.IsCompleted(guideId, completionScope, customCompletionKey))
        {
            Debug.Log($"[ARBandTutorial] Hướng dẫn '{guideId}' đã hoàn thành trước đó. Ẩn Component.");
            gameObject.SetActive(false);
            return;
        }

        StartTutorial();
    }

    private void OnEnable()
    {
        BandARSpawner.OnCastPlacedToWorld += OnCastPlacedSuccess;
        StartCoroutine(SubscribeToCharacterManager());
    }

    private void OnDisable()
    {
        BandARSpawner.OnCastPlacedToWorld -= OnCastPlacedSuccess;
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterSelected -= OnCharacterSelected;
        }
        StopGuideRuntime(false);
    }

    private IEnumerator SubscribeToCharacterManager()
    {
        yield return null;
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterSelected -= OnCharacterSelected;
            CharacterManager.Instance.OnCharacterSelected += OnCharacterSelected;
        }
    }

    private void OnCastPlacedSuccess(GameObject castObject)
    {
        if (_isRunning && _currentStepIndex == 1)
        {
            Debug.Log("[ARBandTutorial] Cast thành công! Chuyển sang step3.");
            NextStep();
        }
    }

    private void OnCharacterSelected(GameObject selectedCharacter)
    {
        if (_isRunning && _currentStepIndex == 5 && selectedCharacter != null)
        {
            Debug.Log("[ARBandTutorial] Nhân vật được chọn thành công ở step6, chuyển sang step tiếp theo.");
            NextStep();
        }
    }

    private void OnDestroy()
    {
        StopGuideRuntime(false);
    }

    private void Update()
    {
        if (!_isRunning) return;

        // Nếu bước hiện tại không có targetButton, người dùng được click vào màn hình để qua bước.
        // Tuy nhiên, ở step 2 (index == 1) và step 6 (index == 5) chúng ta bắt buộc người dùng thực hiện nhiệm vụ (thả Cast / chọn nhân vật).
        if (_currentStepIndex != 1 && _currentStepIndex != 5 && IsPointerPressedThisFrame())
        {
            ARBandTutorialStep currentStep = GetStep(_currentStepIndex);
            if (currentStep != null && currentStep.targetButton == null)
            {
                NextStep();
            }
        }
    }

    private bool IsPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.touches.Count > 0)
        {
            return UnityEngine.InputSystem.Touchscreen.current.touches[0].press.wasPressedThisFrame;
        }
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            return UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private void LateUpdate()
    {
        if (!_isRunning) return;

        UpdateHandPosition();
    }

    /// <summary>
    /// Bắt đầu quá trình hướng dẫn
    /// </summary>
    public void StartTutorial()
    {
        if (_isRunning) return;

        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
        }
        _startRoutine = StartCoroutine(StartGuideRoutine());
    }

    /// <summary>
    /// Bắt buộc chạy hướng dẫn bằng cách reset tiến trình cũ
    /// </summary>
    public void StartTutorialForced()
    {
        ResetCompletion();
        StartTutorial();
    }

    /// <summary>
    /// Chuyển tiếp sang bước tiếp theo
    /// </summary>
    public void NextStep()
    {
        if (!_isRunning) return;

        if (Time.frameCount == _lastTransitionFrame) return;
        _lastTransitionFrame = Time.frameCount;

        ShowStep(_currentStepIndex + 1);
    }

    /// <summary>
    /// Reset tiến trình hướng dẫn
    /// </summary>
    public void ResetCompletion()
    {
        FirstTimeGuideProgress.Reset(guideId, completionScope, customCompletionKey);
    }

    private IEnumerator StartGuideRoutine()
    {
        ResolveReferences();
        CachePositions();

        if (step1 == null)
        {
            FinishGuide();
            yield break;
        }

        _isRunning = true;
        _currentStepIndex = -1;

        yield return new WaitForSecondsRealtime(autoStartDelay);

        ShowGuideObjects();
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        if (index >= GetTotalSteps())
        {
            FinishGuide();
            return;
        }

        if (index < 0) index = 0;

        bool animateTransition = _currentStepIndex >= 0;
        _currentStepIndex = index;
        ARBandTutorialStep step = GetStep(_currentStepIndex);

        if (step == null)
        {
            FinishGuide();
            return;
        }

        // Khôi phục trạng thái cho nút trước đó (nếu có)
        UnhighlightTarget();
        UnsubscribeFromTargetButton();
        StopTargetPulse();

        // Thiết lập cho nút mới nếu có chỉ định targetButton
        if (step.targetButton != null)
        {
            SubscribeToTargetButton(step.targetButton);
            HighlightTarget(step.targetButton);

            // Bỏ qua nhấp nháy nút mục tiêu ở Step 4 (index == 3)
            if (step.pulseTarget && index != 3)
            {
                StartTargetPulse(step.targetButton, step.targetPulseScale);
            }
        }

        if (_guideCanvasGroup != null)
        {
            // Tắt blocksRaycasts ở step2 (index == 1) và step6 (index == 5) để click xuyên qua toàn bộ Canvas của tutorial
            _guideCanvasGroup.blocksRaycasts = (index != 1 && index != 5);
        }

        // Khôi phục tương tác cho các nút và ScrollRect đã bị khóa ở bước trước
        RestoreAllButtons();
        RestoreScrollRect();

        // Khóa các nút chỉ định ở bước mới (ngoại trừ targetButton)
        if (step.buttonsToDisable != null && step.buttonsToDisable.Count > 0)
        {
            foreach (var btn in step.buttonsToDisable)
            {
                if (btn != null)
                {
                    btn.interactable = (step.targetButton != null && btn == step.targetButton);
                    _disabledButtonsInCurrentStep.Add(btn);
                }
            }
        }

        // Khóa ScrollRect ở bước mới
        if (step.targetScrollRect != null)
        {
            step.targetScrollRect.enabled = false;
            _disabledScrollRectInCurrentStep = step.targetScrollRect;
        }

        if (index == 1) // step2
        {
            Vector2 startPos = GetStep2HandStartPosition() + step.handOffset;
            StartHandSwipeAnimation(startPos);
        }
        else
        {
            UpdateHandPosition();
            if (ShouldShowHand(step))
            {
                // Bỏ qua nhấp nháy bàn tay chỉ dẫn ở Step 4 (index == 3)
                if (index == 3)
                {
                    StopHandPulse();
                }
                else
                {
                    RestartHandPulse();
                }
            }
            else
            {
                StopHandPulse();
            }
        }

        // Điều khiển hiển thị Guider dựa trên cấu hình của step
        if (guider != null)
        {
            if (step.showGuider)
            {
                if (!guider.gameObject.activeSelf)
                {
                    SetActive(guider, true);
                    PlayGuiderIntro();
                }
            }
            else
            {
                if (guider.gameObject.activeSelf)
                {
                    guider.DOKill();
                    guider.DOAnchorPos(GetGuiderHiddenPosition(), GuiderSlideDuration)
                        .SetEase(Ease.InQuad)
                        .SetUpdate(true)
                        .OnComplete(() => SetActive(guider, false));
                }
            }
        }

        // Điều khiển hiển thị nút Skip
        if (_skipButton != null)
        {
            // Ẩn nút Skip ở step 1 (index == 0) và step 2 (index == 1) theo yêu cầu
            _skipButton.gameObject.SetActive(index > 1 && step.showSkipButton);
        }

        PlayStepText(step, animateTransition);
    }

    private void FinishGuide()
    {
        Debug.Log($"[ARBandTutorial] Đã hoàn thành toàn bộ hướng dẫn '{guideId}'.");
        FirstTimeGuideProgress.MarkCompleted(guideId, completionScope, customCompletionKey);
        StopGuideRuntime(true);
    }

    private void StopGuideRuntime(bool animateOut)
    {
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }

        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }

        if (_textTransitionSequence != null)
        {
            _textTransitionSequence.Kill();
            _textTransitionSequence = null;
        }

        StopHandPulse();
        StopTargetPulse();
        UnhighlightTarget();
        UnsubscribeFromTargetButton();

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(FinishGuide);
        }

        _isRunning = false;
        _currentStepIndex = -1;

        RestoreAllButtons();
        RestoreScrollRect();

        if (animateOut)
        {
            HideGuideObjectsAnimated();
        }
        else
        {
            HideGuideObjects();
        }
    }

    private void ShowGuideObjects()
    {
        SetActive(textBox, true);
        SetActive(iconHand, true);

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(FinishGuide);
            _skipButton.onClick.AddListener(FinishGuide);
        }

        if (textBox != null)
        {
            textBox.DOKill();
            textBox.localScale = Vector3.one * 0.92f;
            textBox.anchoredPosition = _textBoxShownPosition;
            textBox.DOScale(1f, TextBoxShowDuration).SetEase(Ease.OutBack).SetUpdate(true);
        }

        if (_textBoxCanvasGroup != null)
        {
            _textBoxCanvasGroup.DOKill();
            _textBoxCanvasGroup.alpha = 1f;
        }

        if (_iconHandCanvasGroup != null)
        {
            _iconHandCanvasGroup.DOKill();
            _iconHandCanvasGroup.alpha = 1f;
        }
    }

    private void HideGuideObjects()
    {
        if (tmp != null) tmp.text = string.Empty;

        if (_textBoxCanvasGroup != null) _textBoxCanvasGroup.alpha = 1f;
        if (_iconHandCanvasGroup != null) _iconHandCanvasGroup.alpha = 1f;

        SetActive(textBox, false);
        SetActive(iconHand, false);
        SetActive(guider, false);
    }

    private void HideGuideObjectsAnimated()
    {
        if (textBox != null)
        {
            textBox.DOKill();
            textBox.DOScale(0.92f, TextBoxShowDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => SetActive(textBox, false));
        }

        if (iconHand != null) SetActive(iconHand, false);

        if (guider != null)
        {
            guider.DOKill();
            guider.DOAnchorPos(GetGuiderHiddenPosition(), GuiderSlideDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => SetActive(guider, false));
        }
    }

    private void PlayGuiderIntro()
    {
        if (guider == null) return;

        guider.DOKill();
        guider.anchoredPosition = GetGuiderHiddenPosition();
        guider.DOAnchorPos(_guiderShownPosition, GuiderSlideDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    private Vector2 GetGuiderHiddenPosition()
    {
        if (guider == null) return _guiderShownPosition;

        float visualWidth = Mathf.Abs(guider.rect.width * guider.localScale.x);
        float rightExtentFromPivot = visualWidth * (1f - guider.pivot.x);
        float slideDistance = Mathf.Max(
            GuiderHiddenPadding,
            _guiderShownPosition.x + rightExtentFromPivot + GuiderHiddenPadding);

        return new Vector2(_guiderShownPosition.x - slideDistance, _guiderShownPosition.y);
    }

    private void StartTypewriter(string message)
    {
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
        }
        _typeRoutine = StartCoroutine(TypewriterRoutine(message ?? string.Empty));
    }

    private IEnumerator TypewriterRoutine(string message)
    {
        if (tmp == null) yield break;

        tmp.text = string.Empty;
        for (int i = 0; i < message.Length; i++)
        {
            tmp.text = message.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(TypewriterCharacterDelay);
        }
    }

    private void PlayStepText(ARBandTutorialStep step, bool animateTransition)
    {
        string message = step != null ? step.message : string.Empty;

        if (_textTransitionSequence != null)
        {
            _textTransitionSequence.Kill();
            _textTransitionSequence = null;
        }

        if (!animateTransition || textBox == null || _textBoxCanvasGroup == null)
        {
            if (textBox != null) textBox.localScale = Vector3.one;
            if (_textBoxCanvasGroup != null) _textBoxCanvasGroup.alpha = 1f;
            StartTypewriter(message);
            return;
        }

        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }

        _textTransitionSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_textBoxCanvasGroup.DOFade(0f, StepTransitionDuration * 0.45f).SetEase(Ease.OutQuad))
            .Join(textBox.DOScale(TextBoxStepScale, StepTransitionDuration * 0.45f).SetEase(Ease.OutQuad))
            .AppendCallback(() => StartTypewriter(message))
            .Append(_textBoxCanvasGroup.DOFade(1f, StepTransitionDuration * 0.55f).SetEase(Ease.OutQuad))
            .Join(textBox.DOScale(1f, StepTransitionDuration * 0.55f).SetEase(Ease.OutBack))
            .OnComplete(() => _textTransitionSequence = null);
    }

    private void UpdateHandPosition()
    {
        if (_currentStepIndex == 1)
        {
            return;
        }

        if (_currentStepIndex == 5)
        {
            ARBandTutorialStep step5 = GetStep(5);
            if (iconHand != null && step5 != null)
            {
                Transform targetChar = GetStep6TargetCharacter();
                if (targetChar != null)
                {
                    iconHand.gameObject.SetActive(true);
                    iconHand.anchoredPosition = GetTransformPositionInGuideRoot(targetChar) + step5.handOffset;
                    iconHand.localEulerAngles = new Vector3(
                        _iconHandOriginalEulerAngles.x,
                        _iconHandOriginalEulerAngles.y,
                        _iconHandOriginalEulerAngles.z + step5.handAngle);
                }
                else
                {
                    iconHand.gameObject.SetActive(false);
                }
            }
            return;
        }

        ARBandTutorialStep step = GetStep(_currentStepIndex);
        if (iconHand == null || step == null)
        {
            return;
        }
        if (!ShouldShowHand(step))
        {
            iconHand.gameObject.SetActive(false);
            return;
        }

        Button target = step.targetButton;
        if (target == null || target.image == null)
        {
            iconHand.gameObject.SetActive(false);
            return;
        }

        iconHand.gameObject.SetActive(true);
        iconHand.anchoredPosition = GetTargetPositionInGuideRoot(target.image.rectTransform) + step.handOffset;
        iconHand.localEulerAngles = new Vector3(
            _iconHandOriginalEulerAngles.x,
            _iconHandOriginalEulerAngles.y,
            _iconHandOriginalEulerAngles.z + step.handAngle);
    }

    private void RestartHandPulse()
    {
        StopHandPulse();

        if (iconHand == null || !iconHand.gameObject.activeInHierarchy) return;

        iconHand.localScale = Vector3.one;
        _handPulseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(iconHand.DOScale(1.08f, HandPulseDuration).SetEase(Ease.InOutSine))
            .Append(iconHand.DOScale(1f, HandPulseDuration).SetEase(Ease.InOutSine))
            .SetLoops(-1);
    }

    private void StopHandPulse()
    {
        if (_handPulseSequence != null)
        {
            _handPulseSequence.Kill();
            _handPulseSequence = null;
        }

        if (iconHand != null) iconHand.localScale = Vector3.one;
    }

    private void StartTargetPulse(Button button, float pulseScale)
    {
        if (button == null) return;

        _pulsingTarget = button.transform;
        _originalTargetScale = _pulsingTarget.localScale;

        Vector3 targetScale = _originalTargetScale * Mathf.Max(1f, pulseScale);

        _targetPulseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_pulsingTarget.DOScale(targetScale, 0.5f).SetEase(Ease.InOutSine))
            .Append(_pulsingTarget.DOScale(_originalTargetScale, 0.5f).SetEase(Ease.InOutSine))
            .SetLoops(-1);
    }

    private void StopTargetPulse()
    {
        if (_targetPulseSequence != null)
        {
            _targetPulseSequence.Kill();
            _targetPulseSequence = null;
        }

        if (_pulsingTarget != null)
        {
            _pulsingTarget.localScale = _originalTargetScale;
            _pulsingTarget = null;
        }
    }

    private void HighlightTarget(Button button)
    {
        if (button == null) return;

        Canvas tutorialCanvas = GetComponentInParent<Canvas>();
        if (tutorialCanvas != null)
        {
            _tutorialCanvasSortingOrder = tutorialCanvas.sortingOrder;
        }

        Canvas buttonCanvas = button.GetComponent<Canvas>();
        if (buttonCanvas != null)
        {
            _hadCanvasOriginally = true;
            _originalOverrideSorting = buttonCanvas.overrideSorting;
            _originalSortingOrder = buttonCanvas.sortingOrder;
        }
        else
        {
            _hadCanvasOriginally = false;
            buttonCanvas = button.gameObject.AddComponent<Canvas>();
            _addedCanvas = buttonCanvas;
        }

        buttonCanvas.overrideSorting = true;
        buttonCanvas.sortingOrder = _tutorialCanvasSortingOrder + 1;

        GraphicRaycaster raycaster = button.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            _hadRaycasterOriginally = true;
        }
        else
        {
            _hadRaycasterOriginally = false;
            raycaster = button.gameObject.AddComponent<GraphicRaycaster>();
            _addedRaycaster = raycaster;
        }

        // Đẩy iconHand lên trên nút bấm mục tiêu (sortingOrder + 2)
        if (iconHand != null)
        {
            Canvas handCanvas = iconHand.GetComponent<Canvas>();
            if (handCanvas != null)
            {
                _hadHandCanvasOriginally = true;
                _originalHandOverrideSorting = handCanvas.overrideSorting;
                _originalHandSortingOrder = handCanvas.sortingOrder;
            }
            else
            {
                _hadHandCanvasOriginally = false;
                handCanvas = iconHand.gameObject.AddComponent<Canvas>();
                _handCanvas = handCanvas;
            }
            handCanvas.overrideSorting = true;
            handCanvas.sortingOrder = _tutorialCanvasSortingOrder + 2;
        }

        // Đẩy textBox lên trên nút bấm mục tiêu (sortingOrder + 2)
        if (textBox != null)
        {
            Canvas textCanvas = textBox.GetComponent<Canvas>();
            if (textCanvas != null)
            {
                _hadTextBoxCanvasOriginally = true;
                _originalTextBoxOverrideSorting = textCanvas.overrideSorting;
                _originalTextBoxSortingOrder = textCanvas.sortingOrder;
            }
            else
            {
                _hadTextBoxCanvasOriginally = false;
                textCanvas = textBox.gameObject.AddComponent<Canvas>();
                _textBoxCanvas = textCanvas;
            }
            textCanvas.overrideSorting = true;
            textCanvas.sortingOrder = _tutorialCanvasSortingOrder + 2;
        }
    }

    private void UnhighlightTarget()
    {
        // Khôi phục nút bấm mục tiêu
        if (!_hadRaycasterOriginally && _addedRaycaster != null)
        {
            Destroy(_addedRaycaster);
        }
        _addedRaycaster = null;

        if (_hadCanvasOriginally)
        {
            if (_addedCanvas != null)
            {
                _addedCanvas.overrideSorting = _originalOverrideSorting;
                _addedCanvas.sortingOrder = _originalSortingOrder;
            }
            else
            {
                Canvas buttonCanvas = _focusedTargetButton != null ? _focusedTargetButton.GetComponent<Canvas>() : null;
                if (buttonCanvas != null)
                {
                    buttonCanvas.overrideSorting = _originalOverrideSorting;
                    buttonCanvas.sortingOrder = _originalSortingOrder;
                }
            }
        }
        else if (_addedCanvas != null)
        {
            Destroy(_addedCanvas);
        }
        _addedCanvas = null;

        // Khôi phục trạng thái Canvas của iconHand
        if (iconHand != null)
        {
            if (_hadHandCanvasOriginally)
            {
                Canvas handCanvas = iconHand.GetComponent<Canvas>();
                if (handCanvas != null)
                {
                    handCanvas.overrideSorting = _originalHandOverrideSorting;
                    handCanvas.sortingOrder = _originalHandSortingOrder;
                }
            }
            else if (_handCanvas != null)
            {
                Destroy(_handCanvas);
            }
            _handCanvas = null;
        }

        // Khôi phục trạng thái Canvas của textBox
        if (textBox != null)
        {
            if (_hadTextBoxCanvasOriginally)
            {
                Canvas textCanvas = textBox.GetComponent<Canvas>();
                if (textCanvas != null)
                {
                    textCanvas.overrideSorting = _originalTextBoxOverrideSorting;
                    textCanvas.sortingOrder = _originalTextBoxSortingOrder;
                }
            }
            else if (_textBoxCanvas != null)
            {
                Destroy(_textBoxCanvas);
            }
            _textBoxCanvas = null;
        }
    }

    private void SubscribeToTargetButton(Button button)
    {
        if (button == null) return;
        _focusedTargetButton = button;
        button.onClick.AddListener(OnTargetButtonClicked);
    }

    private void UnsubscribeFromTargetButton()
    {
        if (_focusedTargetButton != null)
        {
            _focusedTargetButton.onClick.RemoveListener(OnTargetButtonClicked);
            _focusedTargetButton = null;
        }
    }

    private void OnTargetButtonClicked()
    {
        Debug.Log("[ARBandTutorial] Nút mục tiêu đã được bấm, chuyển qua bước tiếp theo.");
        NextStep();
    }

    private void ResolveReferences()
    {
        if (tmp == null && textBox != null)
        {
            tmp = textBox.GetComponentInChildren<TMP_Text>(true);
        }

        if (_textBoxCanvasGroup == null && textBox != null)
        {
            _textBoxCanvasGroup = ResolveCanvasGroup(textBox);
        }

        if (_iconHandCanvasGroup == null && iconHand != null)
        {
            _iconHandCanvasGroup = ResolveCanvasGroup(iconHand);
        }

        if (_guideCanvas == null)
        {
            _guideCanvas = GetComponentInParent<Canvas>();
            if (_guideCanvas == null && iconHand != null)
            {
                _guideCanvas = iconHand.GetComponentInParent<Canvas>();
            }
        }

        // Đảm bảo canvas của tutorial luôn hiển thị trên cùng (sorting order 999)
        if (_guideCanvas != null)
        {
            _guideCanvas.overrideSorting = true;
            _guideCanvas.sortingOrder = 999;
            _tutorialCanvasSortingOrder = 999;
        }

        // Đã loại bỏ bLocker, quản lý nút trực tiếp qua interactable list

        if (_skipButton == null && textBox != null)
        {
            _skipButton = FindSkipButton(textBox);
        }

        _guideRoot = GetGuideRoot();

        _guideCanvasGroup = GetComponent<CanvasGroup>();
        if (_guideCanvasGroup == null)
        {
            _guideCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private static Button FindSkipButton(RectTransform root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name.ToLowerInvariant().Contains("skip"))
            {
                return buttons[i];
            }
        }

        return null;
    }

    private CanvasGroup ResolveCanvasGroup(RectTransform ui)
    {
        if (ui == null) return null;
        CanvasGroup group = ui.GetComponent<CanvasGroup>();
        return group != null ? group : ui.gameObject.AddComponent<CanvasGroup>();
    }

    private RectTransform GetGuideRoot()
    {
        if (_guideRoot != null) return _guideRoot;

        if (iconHand != null && iconHand.parent is RectTransform iconParent)
        {
            _guideRoot = iconParent;
            return _guideRoot;
        }

        if (transform is RectTransform selfRect)
        {
            _guideRoot = selfRect;
            return _guideRoot;
        }

        return _guideCanvas != null ? _guideCanvas.GetComponent<RectTransform>() : null;
    }

    private void CachePositions()
    {
        if (guider != null) _guiderShownPosition = guider.anchoredPosition;
        if (textBox != null) _textBoxShownPosition = textBox.anchoredPosition;
        if (iconHand != null) _iconHandOriginalEulerAngles = iconHand.localEulerAngles;
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera;
    }

    private Vector2 GetTargetPositionInGuideRoot(RectTransform target)
    {
        RectTransform root = GetGuideRoot();
        if (target == null || root == null) return Vector2.zero;

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCamera = GetCanvasCamera(targetCanvas);
        Camera guideCamera = GetCanvasCamera(_guideCanvas);
        Vector3 targetWorldPosition = target.TransformPoint(target.rect.center);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(targetCamera, targetWorldPosition);

        Vector2 localPosition;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, guideCamera, out localPosition)
            ? localPosition
            : Vector2.zero;
    }

    private static bool ShouldShowHand(ARBandTutorialStep step)
    {
        return step != null && step.showHand && step.targetButton != null;
    }

    private static void SetActive(RectTransform rect, bool active)
    {
        if (rect != null) rect.gameObject.SetActive(active);
    }

    /// <summary>
    /// Thiết lập động nút mục tiêu ở runtime cho một bước cụ thể.
    /// Hữu ích khi các nút được khởi tạo tự động trong ScrollView.
    /// </summary>
    public void SetStepTargetButton(int stepIndex, Button button)
    {
        ARBandTutorialStep step = GetStep(stepIndex);
        if (step != null)
        {
            step.targetButton = button;
            if (_isRunning && _currentStepIndex == stepIndex)
            {
                UnhighlightTarget();
                UnsubscribeFromTargetButton();

                SubscribeToTargetButton(button);
                HighlightTarget(button);
                UpdateHandPosition();
            }
        }
    }

    private int GetTotalSteps()
    {
        // Hướng dẫn gồm 6 bước
        return 6;
    }

    private ARBandTutorialStep GetStep(int index)
    {
        switch (index)
        {
            case 0: return step1;
            case 1: return step2;
            case 2: return step3;
            case 3: return step4;
            case 4: return step5;
            case 5: return step6;
            default: return null;
        }
    }

    private Transform GetStep6TargetCharacter()
    {
        CastPlacementState[] states = FindObjectsByType<CastPlacementState>(FindObjectsSortMode.None);
        foreach (var state in states)
        {
            if (state != null && state.IsPlaced)
            {
                return state.transform;
            }
        }
        return null;
    }

    private Vector2 GetStep2HandStartPosition()
    {
        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
        if (spawner != null && spawner.Pedestals != null)
        {
            foreach (var p in spawner.Pedestals)
            {
                if (p != null && p.activeSelf)
                {
                    return GetTransformPositionInGuideRoot(p.transform);
                }
            }
        }
        return new Vector2(0f, -300f);
    }

    private Vector2 GetTransformPositionInGuideRoot(Transform target3D)
    {
        RectTransform root = GetGuideRoot();
        if (target3D == null || root == null) return Vector2.zero;

        Camera mainCam = Camera.main;
        Camera guideCamera = GetCanvasCamera(_guideCanvas);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(mainCam, target3D.position);

        Vector2 localPosition;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, guideCamera, out localPosition)
            ? localPosition
            : Vector2.zero;
    }

    private void StartHandSwipeAnimation(Vector2 startPos)
    {
        StopHandPulse();
        if (iconHand == null) return;

        iconHand.gameObject.SetActive(true);
        iconHand.anchoredPosition = startPos;
        iconHand.localEulerAngles = _iconHandOriginalEulerAngles;

        if (_iconHandCanvasGroup != null)
        {
            _iconHandCanvasGroup.alpha = 1f;
        }

        _handPulseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(iconHand.DOAnchorPos(startPos, 0.2f).SetEase(Ease.Linear))
            .Append(iconHand.DOAnchorPosY(startPos.y + 250f, 1.2f).SetEase(Ease.OutQuad))
            .Join(_iconHandCanvasGroup != null ? _iconHandCanvasGroup.DOFade(0f, 0.25f).SetDelay(0.95f) : DOTween.Sequence())
            .AppendCallback(() => {
                iconHand.anchoredPosition = startPos;
                if (_iconHandCanvasGroup != null) _iconHandCanvasGroup.alpha = 0f;
            })
            .Append(_iconHandCanvasGroup != null ? _iconHandCanvasGroup.DOFade(1f, 0.2f) : DOTween.Sequence())
            .SetLoops(-1);
    }

    private void RestoreAllButtons()
    {
        if (_disabledButtonsInCurrentStep != null)
        {
            foreach (var btn in _disabledButtonsInCurrentStep)
            {
                if (btn != null)
                {
                    btn.interactable = true;
                }
            }
            _disabledButtonsInCurrentStep.Clear();
        }
    }

    private void RestoreScrollRect()
    {
        if (_disabledScrollRectInCurrentStep != null)
        {
            _disabledScrollRectInCurrentStep.enabled = true;
            _disabledScrollRectInCurrentStep = null;
        }
    }
}
