using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PopupStudio : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Fixed Cast slot buttons in the Studio popup. Missing slots up to 9 are cloned at runtime.")]
    [SerializeField] private List<PopupCastButton> castButtons = new List<PopupCastButton>();

    [Tooltip("Cancel button closes the popup without continuing.")]
    [SerializeField] private Button cancelButton;

    [Tooltip("Start button continues only when there is a free unlocked Cast slot.")]
    [SerializeField] private Button startButton;

    public event Action OnCancel;
    public event Action OnStart;

    private int _currentCharacterCount = 0;
    private bool _isOpening = false;
    private bool _isUnlockingSlot = false;

    private void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(ClosePopup);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

    }

    private void OnDestroy()
    {
        UnregisterCastButtonEvents();
    }

    public void OpenPopup()
    {
        if (_isOpening) return;
        _isOpening = true;

        gameObject.SetActive(true);
        EnsureCastButtonSlots();
        CancelAllDeleteModes();

        transform.localScale = Vector3.one * 0.8f;
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
        transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            _isOpening = false;
        });

        RefreshGrid();
    }

    public void ClosePopup()
    {
        CancelAllDeleteModes();

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
        transform.DOScale(0.8f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            gameObject.SetActive(false);
            OnCancel?.Invoke();
        });
    }

    public async void RefreshGrid()
    {
        if (MainMenuDataManager.Instance == null || castButtons == null) return;

        EnsureCastButtonSlots();

        List<CharacterData> characters = await MainMenuDataManager.Instance.GetCreatedCharactersAsync();
        _currentCharacterCount = characters != null ? characters.Count : 0;

        int unlockedSlotCount = MainMenuDataManager.Instance.GetUnlockedCastSlotCount();
        int nextUnlockSlotNumber = MainMenuDataManager.Instance.GetNextUnlockableCastSlotNumber();
        bool canUnlockMore = MainMenuDataManager.Instance.CanUnlockMoreCastSlots();

        for (int i = 0; i < castButtons.Count; i++)
        {
            PopupCastButton castButton = castButtons[i];
            if (castButton == null) continue;

            bool isVisibleSlot = i < CastSlotUnlockManager.MaxSlotCount;
            castButton.gameObject.SetActive(isVisibleSlot);
            UnregisterCastButtonEvents(castButton);

            if (!isVisibleSlot)
            {
                continue;
            }

            int slotNumber = i + 1;
            if (i < _currentCharacterCount && characters != null)
            {
                CharacterData charData = characters[i];
                Sprite avatar = MainMenuDataManager.Instance.GetCharacterAvatarSprite(charData.prefabName);
                Sprite instrument = MainMenuDataManager.Instance.GetInstrumentAvatarSprite(charData.instrumentId);

                castButton.Setup(charData, avatar, instrument);
                castButton.OnDeleted += OnCharacterDeleted;
                castButton.OnDeleteModeEntered += OnButtonDeleteModeEntered;
                continue;
            }

            if (i < unlockedSlotCount)
            {
                castButton.Setup(null, null, null);
                continue;
            }

            bool canRequestUnlock = canUnlockMore && !_isUnlockingSlot && slotNumber == nextUnlockSlotNumber;
            castButton.SetupLocked(slotNumber, canRequestUnlock);
            castButton.OnUnlockRequested += OnUnlockRequested;
        }

        UpdateStartButtonInteractive();
    }

    private void EnsureCastButtonSlots()
    {
        if (castButtons == null)
        {
            castButtons = new List<PopupCastButton>();
        }

        PopupCastButton template = null;
        for (int i = 0; i < castButtons.Count; i++)
        {
            if (castButtons[i] != null)
            {
                template = castButtons[i];
                break;
            }
        }

        if (template == null)
        {
            return;
        }

        Transform parent = template.transform.parent;
        while (castButtons.Count < CastSlotUnlockManager.MaxSlotCount)
        {
            PopupCastButton clone = Instantiate(template, parent);
            clone.name = "PopupCastButton_Slot_" + (castButtons.Count + 1);
            castButtons.Add(clone);
        }

        for (int i = 0; i < castButtons.Count; i++)
        {
            if (castButtons[i] != null)
            {
                castButtons[i].gameObject.SetActive(i < CastSlotUnlockManager.MaxSlotCount);
            }
        }
    }

    private void OnCharacterDeleted()
    {
        RefreshGrid();
    }

    private void OnButtonDeleteModeEntered(PopupCastButton sender)
    {
        foreach (PopupCastButton btn in castButtons)
        {
            if (btn != null && btn != sender)
            {
                btn.CancelDeleteMode();
            }
        }

    }

    private void OnUnlockRequested(PopupCastButton sender)
    {
        if (_isUnlockingSlot || MainMenuDataManager.Instance == null)
        {
            return;
        }

        if (!MainMenuDataManager.Instance.CanUnlockMoreCastSlots())
        {
            Debug.LogWarning("[PopupStudio] Maximum Cast slot limit reached.");
            return;
        }

        _isUnlockingSlot = true;
        RefreshGrid();

        RewardedAdService.GetOrCreateInstance().ShowRewardedAd((rewarded, message) =>
        {
            _isUnlockingSlot = false;

            if (rewarded && MainMenuDataManager.Instance != null)
            {
                bool unlocked = MainMenuDataManager.Instance.TryUnlockNextCastSlot();
                Debug.Log(unlocked
                    ? "[PopupStudio] Cast slot unlocked via rewarded ad."
                    : "[PopupStudio] Reward received, but no Cast slot could be unlocked.");
            }
            else
            {
                Debug.LogWarning("[PopupStudio] Rewarded ad did not unlock a slot: " + message);
            }

            RefreshGrid();
        });
    }

    public void CancelAllDeleteModes()
    {
        foreach (PopupCastButton btn in castButtons)
        {
            if (btn != null)
            {
                btn.CancelDeleteMode();
            }
        }

    }

    private void UpdateStartButtonInteractive()
    {
        if (startButton == null) return;

        bool hasFreeSpace = MainMenuDataManager.Instance != null
            ? MainMenuDataManager.Instance.HasFreeCastSlot()
            : _currentCharacterCount < CastSlotUnlockManager.BaseUnlockedSlotCount;

        startButton.interactable = hasFreeSpace;

        CanvasGroup cg = startButton.GetComponent<CanvasGroup>();
        if (cg == null) cg = startButton.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = hasFreeSpace ? 1.0f : 0.4f;
    }

    private void OnStartButtonClicked()
    {
        if (MainMenuDataManager.Instance != null && !MainMenuDataManager.Instance.HasFreeCastSlot())
        {
            if (MainMenuDataManager.Instance.CanUnlockMoreCastSlots())
            {
                Debug.LogWarning("[PopupStudio] Watch an ad to unlock Cast slot " + MainMenuDataManager.Instance.GetNextUnlockableCastSlotNumber() + ".");
            }
            else
            {
                Debug.LogWarning("[PopupStudio] Maximum Cast slot limit reached.");
            }
            return;
        }

        CancelAllDeleteModes();

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();
        transform.DOKill();
        cg.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
        transform.DOScale(0.8f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            gameObject.SetActive(false);
            OnStart?.Invoke();
        });
    }

    private void UnregisterCastButtonEvents()
    {
        if (castButtons == null)
        {
            return;
        }

        foreach (PopupCastButton btn in castButtons)
        {
            UnregisterCastButtonEvents(btn);
        }
    }

    private void UnregisterCastButtonEvents(PopupCastButton btn)
    {
        if (btn == null)
        {
            return;
        }

        btn.OnDeleted -= OnCharacterDeleted;
        btn.OnDeleteModeEntered -= OnButtonDeleteModeEntered;
        btn.OnUnlockRequested -= OnUnlockRequested;
    }
}
