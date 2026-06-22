using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Khởi tạo các dịch vụ Firebase và kiểm tra phụ thuộc (Dependencies) trước khi chuyển qua Account Scene.
/// Đồng thời hiển thị tiến trình loading mượt mà lên UI Image (Filled).
/// </summary>
public class FirebaseBootstrap : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Tên của Scene tài khoản cần chuyển sang sau khi khởi tạo.")]
    [SerializeField] private string nextSceneName = "Account Scene";

    [Header("UI Progress Bar")]
    [Tooltip("Image hiển thị thanh loading (Image Type phải cấu hình là Filled).")]
    [SerializeField] private Image loadingFillImage;

    [Tooltip("Text hiển thị phần trăm loading (tùy chọn).")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Loading Speeds")]
    [Tooltip("Tốc độ chạy mượt của thanh fill progress.")]
    [SerializeField] private float fillSpeed = 2.0f;

    [Tooltip("Tốc độ chạy giả lập trong khi chờ Firebase khởi tạo.")]
    [SerializeField] private float fakeLoadSpeed = 0.2f;

    private IEnumerator Start()
    {
        float currentProgress = 0f;
        float targetProgress = 0f;

        // Reset UI ban đầu
        UpdateLoadingUI(0f);

        Debug.Log("[FirebaseBootstrap] Đang kiểm tra phụ thuộc Firebase...");
        
        // Khởi tạo Firebase App & kiểm tra phụ thuộc (Google Play Services trên Android)
        var checkTask = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();

        // 1. Giai đoạn 1: Chờ Firebase khởi tạo (tiến trình từ 0% -> 40%)
        while (!checkTask.IsCompleted)
        {
            // Tăng dần targetProgress lên 40%
            targetProgress = Mathf.MoveTowards(targetProgress, 0.4f, Time.deltaTime * fakeLoadSpeed);
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * fillSpeed);
            UpdateLoadingUI(currentProgress);
            yield return null;
        }

        // Đảm bảo target đạt chính xác 40% khi task khởi tạo xong
        targetProgress = 0.4f;

        var dependencyStatus = checkTask.Result;
        if (dependencyStatus == Firebase.DependencyStatus.Available)
        {
            var app = Firebase.FirebaseApp.DefaultInstance;
            Debug.Log($"[FirebaseBootstrap] Firebase khởi tạo thành công: {app.Name}");
        }
        else
        {
            Debug.LogError($"[FirebaseBootstrap] Không thể giải quyết phụ thuộc Firebase: {dependencyStatus}");
            // Vẫn tiếp tục để tránh kẹt màn hình loading của người dùng
        }

        // 2. Giai đoạn 2: Load scene tiếp theo không đồng bộ (tiến trình từ 40% -> 100%)
        Debug.Log($"[FirebaseBootstrap] Đang tải không đồng bộ scene: {nextSceneName}");
        
        // Sử dụng LoadSceneAsync để có tiến độ tải thực tế
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
            // Không tự động chuyển cảnh khi load xong, để đợi thanh loading chạy đầy 100%
            asyncLoad.allowSceneActivation = false;

            while (!asyncLoad.isDone)
            {
                // asyncLoad.progress chạy từ 0.0 đến 0.9 (0.9 nghĩa là đã load xong và chờ kích hoạt)
                float loadProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                
                // Quy đổi tiến trình load scene (0 -> 1) thành khoảng (40% -> 100%)
                targetProgress = 0.4f + (loadProgress * 0.6f);

                currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * fillSpeed);
                UpdateLoadingUI(currentProgress);

                // Khi thanh loading đã chạy đầy 100% và scene ở background đã load xong
                if (currentProgress >= 0.99f && asyncLoad.progress >= 0.9f)
                {
                    currentProgress = 1f;
                    UpdateLoadingUI(currentProgress);
                    
                    // Chờ thêm 0.3 giây tạo cảm giác mượt mà trước khi chuyển cảnh
                    yield return new WaitForSeconds(0.3f);
                    asyncLoad.allowSceneActivation = true;
                }

                yield return null;
            }
        }
        else
        {
            // Dự phòng nếu không tìm thấy scene hoặc lỗi tải async
            Debug.LogWarning($"[FirebaseBootstrap] Chuyển sang chế độ load đồng bộ dự phòng.");
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
    /// Cập nhật tiến độ lên UI Image và Text phần trăm
    /// </summary>
    private void UpdateLoadingUI(float progress)
    {
        if (loadingFillImage != null)
        {
            loadingFillImage.fillAmount = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{(int)(progress * 100)}%";
        }
    }
}
