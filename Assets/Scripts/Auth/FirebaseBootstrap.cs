using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Chạy màn hình loading và chuyển sang Scene tiếp theo.
/// Dự án hoàn toàn offline – không có Firebase dependency.
/// </summary>
public class FirebaseBootstrap : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Tên của Scene cần chuyển sang sau khi loading xong.")]
    [SerializeField] private string nextSceneName = "Main Menu Scene";

    [Header("UI Progress Bar")]
    [Tooltip("Image hiển thị thanh loading (Image Type phải cấu hình là Filled).")]
    [SerializeField] private Image loadingFillImage;

    [Tooltip("Text hiển thị phần trăm loading (tùy chọn).")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Loading Speed")]
    [Tooltip("Tốc độ chạy mượt của thanh fill progress.")]
    [SerializeField] private float fillSpeed = 2.0f;

    private IEnumerator Start()
    {
        float currentProgress = 0f;

        // Reset UI ban đầu
        UpdateLoadingUI(0f);

        Debug.Log("[FirebaseBootstrap] Khởi động chế độ offline. Đang load scene...");

        // Chạy loading giả lập mượt mà từ 0% đến 40%
        while (currentProgress < 0.4f)
        {
            currentProgress = Mathf.MoveTowards(currentProgress, 0.4f, Time.deltaTime * fillSpeed);
            UpdateLoadingUI(currentProgress);
            yield return null;
        }

        // Tải scene tiếp theo không đồng bộ
        AsyncOperation asyncLoad = null;
        try
        {
            asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseBootstrap] Lỗi khi load scene '{nextSceneName}': {e.Message}");
        }

        if (asyncLoad != null)
        {
            asyncLoad.allowSceneActivation = false;

            while (!asyncLoad.isDone)
            {
                // Quy đổi progress load scene (0→0.9) thành khoảng (40%→100%)
                float loadProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                float targetProgress = 0.4f + loadProgress * 0.6f;

                currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * fillSpeed);
                UpdateLoadingUI(currentProgress);

                if (currentProgress >= 0.99f && asyncLoad.progress >= 0.9f)
                {
                    UpdateLoadingUI(1f);
                    yield return new WaitForSeconds(0.3f);
                    asyncLoad.allowSceneActivation = true;
                }

                yield return null;
            }
        }
        else
        {
            // Fallback: load đồng bộ nếu có lỗi async
            Debug.LogWarning("[FirebaseBootstrap] Chuyển sang load đồng bộ dự phòng.");
            while (currentProgress < 1f)
            {
                currentProgress = Mathf.MoveTowards(currentProgress, 1f, Time.deltaTime * fillSpeed);
                UpdateLoadingUI(currentProgress);
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            SceneManager.LoadScene(nextSceneName);
        }
    }

    /// <summary>
    /// Cập nhật tiến độ lên UI Image và Text phần trăm.
    /// </summary>
    private void UpdateLoadingUI(float progress)
    {
        if (loadingFillImage != null)
            loadingFillImage.fillAmount = progress;

        if (progressText != null)
            progressText.text = $"{(int)(progress * 100)}%";
    }
}
