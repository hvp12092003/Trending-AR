using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Nút bắt đầu quay màn hình (sẽ tự động gắn sự kiện click)")]
    [SerializeField] private Button recordButton;

    [Tooltip("Nút dừng quay màn hình (sẽ tự động gắn sự kiện click)")]
    [SerializeField] private Button stopButton;

    [Tooltip("Nút chia sẻ video (không bắt buộc, sẽ tự động gắn sự kiện click)")]
    [SerializeField] private Button shareButton;

    [Tooltip("Vòng tròn đếm ngược thời gian quay (Image Type = Filled, Fill Method = Radial 360)")]
    [SerializeField] private Image progressCircleImage;

    [Header("Hide While Recording")]
    [Tooltip("Các UI GameObject cần ẩn đi khi đang quay màn hình (VD: bottom bar, record button...)")]
    [SerializeField] private List<GameObject> objectsToHideWhileRecording = new List<GameObject>();

    // ──────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // Tự động gắn sự kiện cho các nút nếu được kéo thả trong Inspector
        if (recordButton != null)
            recordButton.onClick.AddListener(OnRecordButtonClicked);

        if (stopButton != null)
            stopButton.onClick.AddListener(OnStopButtonClicked);

        if (shareButton != null)
            shareButton.onClick.AddListener(OnClickShareRecord);

        if (ScreenRecordManager.Instance == null)
        {
            Debug.LogWarning("[UIController] Không tìm thấy ScreenRecordManager trong Scene!");
            return;
        }

        ScreenRecordManager.Instance.OnRecordStarted.AddListener(OnStartRecord);
        ScreenRecordManager.Instance.OnRecordStopped.AddListener(OnStopRecord);

        // Trạng thái ban đầu
        SetStopButtonVisible(false);

        // Ẩn vòng tròn tiến trình
        if (progressCircleImage != null)
        {
            progressCircleImage.fillAmount = 0f;
            progressCircleImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (ScreenRecordManager.Instance == null || !ScreenRecordManager.Instance.IsRecording) return;

        if (progressCircleImage != null)
            progressCircleImage.fillAmount = 1f - ScreenRecordManager.Instance.RecordingProgress;
    }

    // ──────────────────────────────────────────────────────────────
    // Buttons (Tự động gắn trong Start)
    // ──────────────────────────────────────────────────────────────

    /// <summary>Gán vào OnClick của nút Record.</summary>
    private void OnRecordButtonClicked()
    {
        if (ScreenRecordManager.Instance == null) return;
        ScreenRecordManager.Instance.StartRecording();
    }

    /// <summary>
    /// Gán vào OnClick của nút Stop.
    /// Sẽ dừng quay → video tự lưu vào Album → popup chia sẻ hệ điều hành hiện lên.
    /// </summary>
    private void OnStopButtonClicked()
    {
        if (ScreenRecordManager.Instance == null) return;
        ScreenRecordManager.Instance.StopRecording();
    }

    /// <summary>Gán vào OnClick của nút Share (tùy chọn, dùng để chia sẻ thủ công).</summary>
    private void OnClickShareRecord()
    {
        if (ScreenRecordManager.Instance == null) return;
        ScreenRecordManager.Instance.ShareLastRecording();
    }

    // ──────────────────────────────────────────────────────────────
    // Recording Events
    // ──────────────────────────────────────────────────────────────

    private void OnStartRecord()
    {
        // Ẩn tất cả các UI trong danh sách
        SetHiddenObjectsActive(false);

        // Hiện nút Stop
        SetStopButtonVisible(true);

        // Hiện vòng tròn đếm ngược
        if (progressCircleImage != null)
        {
            progressCircleImage.gameObject.SetActive(true);
            progressCircleImage.fillAmount = 1f;
        }
    }

    private void OnStopRecord()
    {
        // Hiện lại tất cả các UI trong danh sách
        SetHiddenObjectsActive(true);

        // Ẩn nút Stop
        SetStopButtonVisible(false);

        // Ẩn vòng tròn đếm ngược
        if (progressCircleImage != null)
        {
            progressCircleImage.fillAmount = 0f;
            progressCircleImage.gameObject.SetActive(false);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private void SetStopButtonVisible(bool visible)
    {
        if (stopButton != null)
            stopButton.gameObject.SetActive(visible);
    }

    /// <summary>Ẩn / Hiện tất cả các đối tượng trong danh sách objectsToHideWhileRecording.</summary>
    private void SetHiddenObjectsActive(bool active)
    {
        foreach (var obj in objectsToHideWhileRecording)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}
