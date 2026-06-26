using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the hand-authored record button inside the audio ScrollView content.
/// Expected children: Delete Button, Icon mic, Filled 1, Filled 2, Text.
/// </summary>
public class CustomAudioRecordItemUI : MonoBehaviour
{
    [Header("Main Button")]
    [SerializeField] private Button itemButton;
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Sprite unselectedSprite;

    [Header("Record Button Children")]
    [SerializeField] private Button deleteButton;
    [SerializeField] private Image iconMic;
    [SerializeField] private Image filled1;
    [SerializeField] private Image filled2;
    [SerializeField] private TextMeshProUGUI text;

    [Header("Labels")]
    [SerializeField] private string idleLabel = "Record";
    [SerializeField] private string recordingLabel = "Recording";
    [SerializeField] private string recordedLabel = "Record 1";

    public string ItemId { get; private set; }

    public string GetTitle() => text != null ? text.text : "";

    private bool _referencesResolved;
    private bool _hasRecording;

    private void EnsureReferences()
    {
        if (_referencesResolved) return;

        if (itemButton == null)
        {
            itemButton = GetComponent<Button>();
        }

        if (deleteButton == null)
        {
            deleteButton = FindChildComponent<Button>("Delete Button", "DeleteButton");
        }

        if (iconMic == null)
        {
            iconMic = FindChildComponent<Image>("Icon mic", "Icon Mic", "IconMic", "Mic", "Icon");
        }

        if (filled1 == null)
        {
            filled1 = FindChildComponent<Image>("Filled 1", "Filled1", "Fill 1", "Fill1");
        }

        if (filled2 == null)
        {
            filled2 = FindChildComponent<Image>("Filled 2", "Filled2", "Fill 2", "Fill2");
        }

        if (text == null)
        {
            text = FindChildComponent<TextMeshProUGUI>("Text", "Label", "Name");
        }

        ConfigureFilled2();
        _referencesResolved = true;
    }

    public void SetupRecordButton(bool hasRecording, Action onClickCallback, Action onDeleteCallback)
    {
        EnsureReferences();

        _hasRecording = hasRecording;
        ItemId = hasRecording ? "Record_Audio" : "Record_Trigger";

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => onClickCallback?.Invoke());
            itemButton.interactable = true;
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDeleteCallback?.Invoke());
        }

        if (hasRecording)
        {
            ShowRecordedVisual();
        }
        else
        {
            ShowIdleVisual();
        }

        SetSelected(false);
    }

    public void SetupRecordTrigger(Sprite recordIcon, Action onClickCallback)
    {
        SetupRecordButton(false, onClickCallback, null);
    }

    public void SetupRecordedItem(string name, Sprite icon, Action onClickCallback, Action onDeleteCallback)
    {
        recordedLabel = string.IsNullOrEmpty(name) ? recordedLabel : name;
        SetupRecordButton(true, onClickCallback, onDeleteCallback);
    }

    public void SetRecordingState(bool isRecording)
    {
        EnsureReferences();

        if (isRecording)
        {
            ShowRecordingVisual();
        }
        else if (_hasRecording)
        {
            ShowRecordedVisual();
        }
        else
        {
            ShowIdleVisual();
        }

        if (itemButton != null)
        {
            itemButton.interactable = !isRecording;
        }
    }

    public void SetRadialFill(float amount)
    {
        EnsureReferences();

        if (filled2 == null) return;

        ConfigureFilled2();
        filled2.gameObject.SetActive(true);
        filled2.fillAmount = Mathf.Clamp01(amount);
    }

    public void SetRecordedState(bool hasRecording)
    {
        EnsureReferences();

        _hasRecording = hasRecording;
        if (hasRecording)
        {
            ShowRecordedVisual();
        }
        else
        {
            ShowIdleVisual();
        }
    }

    public void SetSelected(bool isSelected)
    {
        EnsureReferences();

        if (itemButton == null) return;

        Image buttonImage = itemButton.image != null ? itemButton.image : itemButton.GetComponent<Image>();
        if (buttonImage == null) return;

        Sprite targetSprite = isSelected ? selectedSprite : unselectedSprite;
        if (targetSprite != null)
        {
            buttonImage.sprite = targetSprite;
        }
    }

    private void ShowIdleVisual()
    {
        SetText(idleLabel);
        SetActive(iconMic, true);
        SetActive(filled1, false);
        SetActive(filled2, false);
        SetActive(deleteButton, false);

        if (filled2 != null)
        {
            ConfigureFilled2();
            filled2.fillAmount = 0f;
        }
    }

    private void ShowRecordingVisual()
    {
        SetText(recordingLabel);
        SetActive(iconMic, false);
        SetActive(filled1, true);
        SetActive(filled2, true);
        SetActive(deleteButton, false);

        if (filled2 != null)
        {
            ConfigureFilled2();
            filled2.fillAmount = 0f;
        }
    }

    private void ShowRecordedVisual()
    {
        SetText(recordedLabel);
        SetActive(iconMic, false);
        SetActive(filled1, false);
        SetActive(filled2, true);
        SetActive(deleteButton, true);

        if (filled2 != null)
        {
            ConfigureFilled2();
            filled2.fillAmount = 1f;
        }
    }

    private void ConfigureFilled2()
    {
        if (filled2 == null) return;

        filled2.type = Image.Type.Filled;
        filled2.fillMethod = Image.FillMethod.Horizontal;
        filled2.fillOrigin = (int)Image.OriginHorizontal.Left;
        filled2.fillClockwise = true;
    }

    private void SetText(string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static void SetActive(Graphic graphic, bool active)
    {
        if (graphic != null)
        {
            graphic.gameObject.SetActive(active);
        }
    }

    private static void SetActive(Selectable selectable, bool active)
    {
        if (selectable != null)
        {
            selectable.gameObject.SetActive(active);
        }
    }

    private T FindChildComponent<T>(params string[] names) where T : Component
    {
        foreach (string childName in names)
        {
            Transform child = transform.Find(childName);
            if (child == null) continue;

            T component = child.GetComponent<T>();
            if (component != null) return component;
        }

        return null;
    }
}
