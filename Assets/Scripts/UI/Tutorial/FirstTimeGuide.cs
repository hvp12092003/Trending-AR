using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class FirstTimeGuideStep
{
    public string iD;
    [TextArea(2, 5)]
    public string message;

    [Tooltip("UI chinh can duoc huong dan. Icon tay se chi vao UI nay.")]
    public RectTransform ui;

    [Tooltip("Bat/tat icon tay trong step nay.")]
    public bool showHand = true;

    public Vector2 handOffset = new Vector2(52f, -52f);

    [Tooltip("Goc xoay Z cua icon tay trong step nay.")]
    public float handAngle;

    [Range(1f, 1.25f)]
    public float targetPulseScale = 1.07f;
}

public class FirstTimeGuide : MonoBehaviour
{
    [Header("Guide UI")]
    [SerializeField] private RectTransform textBox;
    [SerializeField] private RectTransform bLocker;
    [SerializeField] private TMP_Text tmp;
    [SerializeField] private RectTransform iconHand;
    [SerializeField] private RectTransform guider;

    [Header("Steps")]
    [SerializeField] private List<FirstTimeGuideStep> steps = new List<FirstTimeGuideStep>();

    [Header("Focus")]
    [Tooltip("Danh sach UI chung se bi lam mo. UI dang duoc chi vao trong step hien tai se duoc giu ro.")]
    [SerializeField] private List<RectTransform> uiToDim = new List<RectTransform>();

    [SerializeField, Range(0f, 1f)] private float dimmedAlpha = 0.35f;

    [SerializeField, HideInInspector] private string guideId = "Default";
    [SerializeField, HideInInspector] private FirstTimeGuideCompletionScope completionScope = FirstTimeGuideCompletionScope.PerScene;
    [SerializeField, HideInInspector] private string customCompletionKey;

    private const float AutoStartDelay = 0.25f;
    private const float GuiderSlideDuration = 0.35f;
    private const float GuiderHiddenPadding = 24f;
    private const float TextBoxShowDuration = 0.2f;
    private const float StepTransitionDuration = 0.22f;
    private const float TextBoxStepScale = 0.96f;
    private const float HandPulseDuration = 0.45f;
    private const float TypewriterCharacterDelay = 0.025f;
    private const float FocusFadeDuration = 0.18f;
    private const float TargetPulseDuration = 0.45f;

    private RectTransform _guideRoot;
    private Canvas _guideCanvas;
    private Button _skipButton;
    private CanvasGroup _textBoxCanvasGroup;
    private CanvasGroup _iconHandCanvasGroup;
    private Coroutine _startRoutine;
    private Coroutine _typeRoutine;
    private Sequence _textTransitionSequence;
    private Sequence _handMoveSequence;
    private Sequence _handPulseSequence;
    private Sequence _targetPulseSequence;
    private RectTransform _focusedTarget;
    private Vector3 _focusedTargetOriginalScale;
    private Vector3 _iconHandOriginalEulerAngles;
    private Vector2 _guiderShownPosition;
    private Vector2 _textBoxShownPosition;
    private readonly Dictionary<CanvasGroup, float> _dimGroupOriginalAlphas = new Dictionary<CanvasGroup, float>();
    private readonly Dictionary<CanvasGroup, Tween> _dimTweens = new Dictionary<CanvasGroup, Tween>();
    private readonly List<CanvasGroup> _activeDimGroups = new List<CanvasGroup>();
    private int _currentStepIndex = -1;
    private bool _isRunning;
    private bool _isHandAnimating;

    public bool IsRunning => _isRunning;
    public int CurrentStepIndex => _currentStepIndex;

    private void Awake()
    {
        ResolveReferences();
        CachePositions();
        HideGuideObjects();
    }

    private void OnEnable()
    {
        StartGuide();
    }

    private void OnDisable()
    {
        StopGuideRuntime(false);
    }

    private void OnDestroy()
    {
        StopGuideRuntime(false);
    }

    private void LateUpdate()
    {
        if (!_isRunning)
        {
            return;
        }

        if (!_isHandAnimating)
        {
            UpdateHandPosition();
        }
    }

    public void StartGuide()
    {
        if (_isRunning || FirstTimeGuideProgress.IsCompleted(guideId, completionScope, customCompletionKey))
        {
            return;
        }

        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
        }

        _startRoutine = StartCoroutine(StartGuideRoutine());
    }

    public void StartGuideForced()
    {
        ResetCompletion();

        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
        }

        _startRoutine = StartCoroutine(StartGuideRoutine());
    }

    public void NextStep()
    {
        if (!_isRunning)
        {
            return;
        }

        ShowStep(_currentStepIndex + 1);
    }

    public void ResetCompletion()
    {
        FirstTimeGuideProgress.Reset(guideId, completionScope, customCompletionKey);
    }

    private IEnumerator StartGuideRoutine()
    {
        ResolveReferences();
        CachePositions();

        if (steps == null || steps.Count == 0)
        {
            FinishGuide();
            yield break;
        }

        _isRunning = true;
        _currentStepIndex = -1;

        yield return new WaitForSecondsRealtime(AutoStartDelay);

        ShowGuideObjects();
        PlayGuiderIntro();
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        if (steps == null || index >= steps.Count)
        {
            FinishGuide();
            return;
        }

        if (index < 0)
        {
            index = 0;
        }

        bool animateTransition = _currentStepIndex >= 0;
        _currentStepIndex = index;
        FirstTimeGuideStep step = steps[_currentStepIndex];

        ApplyStepFocus(step);
        MoveHandToStepTarget(step, animateTransition);
        if (ShouldShowHand(step))
        {
            RestartHandPulse();
        }

        PlayStepText(step, animateTransition);
    }

    private void FinishGuide()
    {
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

        if (_handMoveSequence != null)
        {
            _handMoveSequence.Kill();
            _handMoveSequence = null;
        }

        StopHandPulse();

        _isHandAnimating = false;
        RestoreStepFocus();

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(NextStep);
        }

        _isRunning = false;
        _currentStepIndex = -1;

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
        SetActive(guider, true);
        SetActive(bLocker, true);

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(NextStep);
            _skipButton.onClick.AddListener(NextStep);
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
        if (tmp != null)
        {
            tmp.text = string.Empty;
        }

        if (_textBoxCanvasGroup != null)
        {
            _textBoxCanvasGroup.alpha = 1f;
        }

        if (_iconHandCanvasGroup != null)
        {
            _iconHandCanvasGroup.alpha = 1f;
        }

        SetActive(textBox, false);
        SetActive(iconHand, false);
        SetActive(guider, false);
        SetActive(bLocker, false);
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

        if (iconHand != null)
        {
            SetActive(iconHand, false);
        }

        if (guider != null)
        {
            guider.DOKill();
            guider.DOAnchorPos(GetGuiderHiddenPosition(), GuiderSlideDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => SetActive(guider, false));
        }

        SetActive(bLocker, false);
    }

    private void PlayGuiderIntro()
    {
        if (guider == null)
        {
            return;
        }

        guider.DOKill();
        guider.anchoredPosition = GetGuiderHiddenPosition();
        guider.DOAnchorPos(_guiderShownPosition, GuiderSlideDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    private Vector2 GetGuiderHiddenPosition()
    {
        if (guider == null)
        {
            return _guiderShownPosition;
        }

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
        if (tmp == null)
        {
            yield break;
        }

        tmp.text = string.Empty;

        for (int i = 0; i < message.Length; i++)
        {
            tmp.text = message.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(TypewriterCharacterDelay);
        }
    }

    private void PlayStepText(FirstTimeGuideStep step, bool animateTransition)
    {
        string message = step != null ? step.message : string.Empty;

        if (_textTransitionSequence != null)
        {
            _textTransitionSequence.Kill();
            _textTransitionSequence = null;
        }

        if (!animateTransition || textBox == null || _textBoxCanvasGroup == null)
        {
            if (textBox != null)
            {
                textBox.localScale = Vector3.one;
            }

            if (_textBoxCanvasGroup != null)
            {
                _textBoxCanvasGroup.alpha = 1f;
            }

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

    private void ApplyStepFocus(FirstTimeGuideStep step)
    {
        RestoreFocusedTarget();

        if (step == null)
        {
            ApplyDimGroups(null);
            return;
        }

        ApplyDimGroups(step.ui);
        StartTargetPulse(step.ui, step.targetPulseScale);
    }

    private void ApplyDimGroups(RectTransform focusedUi)
    {
        if (uiToDim == null)
        {
            return;
        }

        List<CanvasGroup> dimGroups = new List<CanvasGroup>();

        for (int i = 0; i < uiToDim.Count; i++)
        {
            RectTransform ui = uiToDim[i];
            if (ui == null || ShouldKeepUiClear(ui, focusedUi))
            {
                continue;
            }

            CanvasGroup group = ResolveDimGroup(ui);
            if (group == null)
            {
                continue;
            }

            if (!dimGroups.Contains(group))
            {
                dimGroups.Add(group);
            }

            if (!_dimGroupOriginalAlphas.ContainsKey(group))
            {
                _dimGroupOriginalAlphas[group] = group.alpha;
            }

            KillDimTween(group);
            if (!_activeDimGroups.Contains(group))
            {
                _activeDimGroups.Add(group);
            }

            _dimTweens[group] = group.DOFade(Mathf.Clamp01(dimmedAlpha), FocusFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        for (int i = _activeDimGroups.Count - 1; i >= 0; i--)
        {
            CanvasGroup group = _activeDimGroups[i];
            if (group == null || dimGroups.Contains(group))
            {
                continue;
            }

            RestoreDimGroup(group, true);
            _activeDimGroups.RemoveAt(i);
        }
    }

    private void StartTargetPulse(RectTransform target, float pulseScale)
    {
        if (target == null)
        {
            return;
        }

        _focusedTarget = target;
        _focusedTargetOriginalScale = target.localScale;

        Vector3 targetScale = _focusedTargetOriginalScale * Mathf.Max(1f, pulseScale);
        _targetPulseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(target.DOScale(targetScale, TargetPulseDuration).SetEase(Ease.InOutSine))
            .Append(target.DOScale(_focusedTargetOriginalScale, TargetPulseDuration).SetEase(Ease.InOutSine))
            .SetLoops(-1);
    }

    private void RestoreStepFocus()
    {
        RestoreFocusedTarget();
        RestoreDimGroups();
    }

    private void RestoreFocusedTarget()
    {
        if (_targetPulseSequence != null)
        {
            _targetPulseSequence.Kill();
            _targetPulseSequence = null;
        }

        if (_focusedTarget != null)
        {
            _focusedTarget.localScale = _focusedTargetOriginalScale;
            _focusedTarget = null;
        }
    }

    private void RestoreDimGroups()
    {
        for (int i = 0; i < _activeDimGroups.Count; i++)
        {
            CanvasGroup group = _activeDimGroups[i];
            if (group == null)
            {
                continue;
            }

            RestoreDimGroup(group, false);
        }

        _activeDimGroups.Clear();
    }

    private void RestoreDimGroup(CanvasGroup group, bool animate)
    {
        if (group == null)
        {
            return;
        }

        KillDimTween(group);

        if (!_dimGroupOriginalAlphas.TryGetValue(group, out float originalAlpha))
        {
            return;
        }

        if (!animate)
        {
            group.alpha = originalAlpha;
            return;
        }

        _dimTweens[group] = group.DOFade(originalAlpha, FocusFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => _dimTweens.Remove(group));
    }

    private void KillDimTween(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        if (_dimTweens.TryGetValue(group, out Tween tween) && tween != null)
        {
            tween.Kill();
        }

        _dimTweens.Remove(group);
    }

    private void UpdateHandPosition()
    {
        if (iconHand == null || steps == null || _currentStepIndex < 0 || _currentStepIndex >= steps.Count)
        {
            return;
        }

        FirstTimeGuideStep step = steps[_currentStepIndex];
        if (!ShouldShowHand(step))
        {
            iconHand.gameObject.SetActive(false);
            return;
        }

        RectTransform target = step.ui;

        iconHand.gameObject.SetActive(true);
        iconHand.anchoredPosition = GetTargetPositionInGuideRoot(target) + step.handOffset;
        iconHand.localEulerAngles = new Vector3(
            _iconHandOriginalEulerAngles.x,
            _iconHandOriginalEulerAngles.y,
            _iconHandOriginalEulerAngles.z + step.handAngle);
    }

    private void MoveHandToStepTarget(FirstTimeGuideStep step, bool animateTransition)
    {
        if (_handMoveSequence != null)
        {
            _handMoveSequence.Kill();
            _handMoveSequence = null;
        }

        _isHandAnimating = false;

        if (iconHand == null)
        {
            return;
        }

        if (!ShouldShowHand(step))
        {
            HideHand(animateTransition);
            return;
        }

        RectTransform target = step.ui;
        bool wasActive = iconHand.gameObject.activeSelf;
        iconHand.gameObject.SetActive(true);

        Vector2 targetPosition = GetTargetPositionInGuideRoot(target) + step.handOffset;
        Vector3 targetEulerAngles = new Vector3(
            _iconHandOriginalEulerAngles.x,
            _iconHandOriginalEulerAngles.y,
            _iconHandOriginalEulerAngles.z + step.handAngle);

        if (!animateTransition)
        {
            if (_iconHandCanvasGroup != null)
            {
                _iconHandCanvasGroup.alpha = 1f;
            }

            iconHand.anchoredPosition = targetPosition;
            iconHand.localEulerAngles = targetEulerAngles;
            return;
        }

        if (!wasActive)
        {
            iconHand.anchoredPosition = targetPosition;
            iconHand.localEulerAngles = targetEulerAngles;
        }

        if (_iconHandCanvasGroup != null)
        {
            _iconHandCanvasGroup.alpha = wasActive ? _iconHandCanvasGroup.alpha : 0f;
        }

        _isHandAnimating = true;
        _handMoveSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Join(wasActive
                ? iconHand.DOAnchorPos(targetPosition, StepTransitionDuration).SetEase(Ease.OutCubic)
                : iconHand.DOAnchorPos(targetPosition, 0f))
            .Join(iconHand.DOLocalRotate(targetEulerAngles, StepTransitionDuration).SetEase(Ease.OutCubic))
            .Join(_iconHandCanvasGroup != null
                ? _iconHandCanvasGroup.DOFade(1f, StepTransitionDuration).SetEase(Ease.OutQuad)
                : DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true))
            .OnComplete(() =>
            {
                _isHandAnimating = false;
                _handMoveSequence = null;
                UpdateHandPosition();
            });
    }

    private void HideHand(bool animateTransition)
    {
        StopHandPulse();

        if (iconHand == null)
        {
            return;
        }

        if (!animateTransition || !iconHand.gameObject.activeSelf || _iconHandCanvasGroup == null)
        {
            if (_iconHandCanvasGroup != null)
            {
                _iconHandCanvasGroup.alpha = 1f;
            }

            iconHand.gameObject.SetActive(false);
            return;
        }

        _isHandAnimating = true;
        _handMoveSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(_iconHandCanvasGroup.DOFade(0f, StepTransitionDuration).SetEase(Ease.OutQuad))
            .Join(iconHand.DOScale(0.92f, StepTransitionDuration).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                _isHandAnimating = false;
                _handMoveSequence = null;
                iconHand.gameObject.SetActive(false);
                iconHand.localScale = Vector3.one;
                _iconHandCanvasGroup.alpha = 1f;
            });
    }

    private static bool ShouldShowHand(FirstTimeGuideStep step)
    {
        return step != null && step.showHand && step.ui != null;
    }

    private static bool ShouldKeepUiClear(RectTransform ui, RectTransform focusedUi)
    {
        if (ui == null || focusedUi == null)
        {
            return false;
        }

        if (ui == focusedUi)
        {
            return true;
        }

        return focusedUi.IsChildOf(ui) || ui.IsChildOf(focusedUi);
    }

    private static CanvasGroup ResolveDimGroup(RectTransform ui)
    {
        return ResolveCanvasGroup(ui);
    }

    private static CanvasGroup ResolveCanvasGroup(RectTransform ui)
    {
        if (ui == null)
        {
            return null;
        }

        CanvasGroup group = ui.GetComponent<CanvasGroup>();
        return group != null ? group : ui.gameObject.AddComponent<CanvasGroup>();
    }

    private Vector2 GetTargetPositionInGuideRoot(RectTransform target)
    {
        RectTransform root = GetGuideRoot();
        if (target == null || root == null)
        {
            return Vector2.zero;
        }

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

    private void RestartHandPulse()
    {
        StopHandPulse();

        if (iconHand == null || !iconHand.gameObject.activeInHierarchy)
        {
            return;
        }

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

        if (iconHand != null)
        {
            iconHand.localScale = Vector3.one;
        }
    }

    private void ResolveReferences()
    {
        if (tmp == null && textBox != null)
        {
            tmp = textBox.GetComponentInChildren<TMP_Text>(true);
        }

        if (_skipButton == null && textBox != null)
        {
            _skipButton = FindSkipButton(textBox);
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

        _guideRoot = GetGuideRoot();
    }

    private RectTransform GetGuideRoot()
    {
        if (_guideRoot != null)
        {
            return _guideRoot;
        }

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
        if (guider != null)
        {
            _guiderShownPosition = guider.anchoredPosition;
        }

        if (textBox != null)
        {
            _textBoxShownPosition = textBox.anchoredPosition;
        }

        if (iconHand != null)
        {
            _iconHandOriginalEulerAngles = iconHand.localEulerAngles;
        }
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
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

        return buttons[0];
    }

    private static void SetActive(RectTransform rect, bool active)
    {
        if (rect != null)
        {
            rect.gameObject.SetActive(active);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preview Guide Forced")]
    private void PreviewGuideForced()
    {
        StartGuideForced();
    }

    [ContextMenu("Reset Guide Completion")]
    private void ResetGuideCompletionFromContext()
    {
        ResetCompletion();
        Debug.Log("[FirstTimeGuide] Reset completion.");
    }
#endif
}
