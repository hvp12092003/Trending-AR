using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private UnityEngine.UI.Button recordButton;
    [SerializeField] private UnityEngine.UI.Image progressCircleImage; // Yêu cầu Image này có Type = Filled và Fill Method = Radial 360

    private void Start()
    {
        if (ScreenRecordManager.Instance != null)
        {
            // Đăng ký sự kiện thông qua ScreenRecordManager
            ScreenRecordManager.Instance.OnRecordStarted.AddListener(OnStartRecord);
            ScreenRecordManager.Instance.OnRecordStopped.AddListener(OnStopRecord);
            
            // Khởi tạo trạng thái ban đầu của vòng tròn tiến trình
            if (progressCircleImage != null)
            {
                progressCircleImage.fillAmount = 0f;
                progressCircleImage.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy ScreenRecordManager.Instance trong Scene!");
        }
    }

    private void Update()
    {
        if (ScreenRecordManager.Instance != null && ScreenRecordManager.Instance.IsRecording)
        {
            if (progressCircleImage != null)
            {
                // Thay đổi để chạy từ 1 về 0 (đếm ngược thời gian)
                progressCircleImage.fillAmount = 1f - ScreenRecordManager.Instance.RecordingProgress;
            }
        }
    }

    /// <summary>
    /// Hàm click nút Record (kéo thả qua Inspector hoặc gọi từ code)
    /// </summary>
    public void OnRecordButtonClicked()
    {
        if (ScreenRecordManager.Instance == null) return;

        if (ScreenRecordManager.Instance.IsRecording)
        {
            ScreenRecordManager.Instance.StopRecording();
        }
        else
        {
            ScreenRecordManager.Instance.StartRecording();
        }
    }

    /// <summary>
    /// Hàm gọi khi nhấn nút Chia sẻ (Share) trên UI
    /// </summary>
    public void OnClickShareRecord()
    {
        if (ScreenRecordManager.Instance != null)
        {
            ScreenRecordManager.Instance.ShareLastRecording();
        }
    }

    private void OnStartRecord()
    {
        if (progressCircleImage != null)
        {
            progressCircleImage.gameObject.SetActive(true);
            progressCircleImage.fillAmount = 1f; // Khởi đầu đầy (1) khi bấm bắt đầu quay
        }
    }

    private void OnStopRecord()
    {
        if (progressCircleImage != null)
        {
            progressCircleImage.fillAmount = 0f;
            progressCircleImage.gameObject.SetActive(false);
        }
    }
}
