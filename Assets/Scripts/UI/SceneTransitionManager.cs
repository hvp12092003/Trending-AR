using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using TMPro;

public enum TransitionType
{
    Fade,
    Slide
}

/// <summary>
/// Quản lý chuyển cảnh mượt mà và đẹp mắt sử dụng Singleton (DontDestroyOnLoad).
/// Kết hợp DOTween và tải Scene bất đồng bộ (LoadSceneAsync).
/// Tự động tắt/bật Canvas để tối ưu hiệu năng tuyệt đối khi không hoạt động.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("UI Components")]
    [Tooltip("Canvas Group của UI chuyển cảnh dùng để tạo hiệu ứng Fade.")]
    [SerializeField] private CanvasGroup transitionCanvasGroup;

    [Tooltip("Panel chính chứa giao diện chuyển cảnh (dùng để Slide hoặc tắt/bật).")]
    [SerializeField] private GameObject loadingPanel;

    [Tooltip("RectTransform của Panel chính, dùng cho hiệu ứng Slide.")]
    [SerializeField] private RectTransform transitionRectTransform;

    [Tooltip("Image hiển thị tiến trình tải (Filled Image). Dùng nếu bạn thiết kế thanh load bằng 2 Image đè lên nhau.")]
    [SerializeField] private Image progressFillImage;

    [Tooltip("Text hiển thị phần trăm tải.")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Tooltip("Text hiển thị các câu giới thiệu/tips ngẫu nhiên.")]
    [SerializeField] private TextMeshProUGUI tipText;

    [Header("Transition Settings")]
    [Tooltip("Thời gian chạy hiệu ứng mờ dần (Fade).")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Tooltip("Thời gian chạy hiệu ứng trượt (Slide).")]
    [SerializeField] private float slideDuration = 0.5f;

    [Tooltip("Danh sách các câu Tips ngẫu nhiên hiển thị lúc tải.")]
    [SerializeField] private string[] tips = {
        "Scan a flat surface for the best AR experience!",
        "Customize your favorite character in the Studio panel.",
        "Design your own music band in AR Band mode.",
        "Don't forget to play Minigames to earn exciting rewards!",
        "A stable internet connection ensures a smooth AR experience."
    };

    private bool isTransitioning = false;
    private Vector2 screenResolution;
    private SynchronizationContext _mainContext;

    private void Awake()
    {
        // Thiết lập Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            _mainContext = SynchronizationContext.Current;
            
            // Lấy độ phân giải màn hình để tính toán khoảng cách trượt
            screenResolution = new Vector2(Screen.width, Screen.height);

            // Tự động ẩn Panel khi khởi tạo để tránh che khuất màn hình chính
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
            else if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.gameObject.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }



    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Bắt sự kiện khi tải xong Scene.
    /// Dùng để ẩn mượt mà Panel Loading ban đầu khi chuyển từ Bootstrap sang Account Scene.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Nếu không ở trong luồng chuyển cảnh của Manager (tức là do FirebaseBootstrap tự load ban đầu)
        if (!isTransitioning)
        {
            if (loadingPanel != null && loadingPanel.activeSelf)
            {
                if (transitionCanvasGroup != null)
                {
                    // Chặn click tạm thời trong khi fade out
                    transitionCanvasGroup.blocksRaycasts = true;
                    transitionCanvasGroup.DOKill();
                    transitionCanvasGroup.DOFade(0f, fadeDuration)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            loadingPanel.SetActive(false);
                            transitionCanvasGroup.blocksRaycasts = false;
                        });
                }
                else
                {
                    loadingPanel.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Kích hoạt quá trình chuyển cảnh từ bất kỳ Script nào.
    /// </summary>
    /// <param name="sceneName">Tên của Scene muốn tải.</param>
    /// <param name="type">Loại hiệu ứng chuyển cảnh (Fade hoặc Slide).</param>
    public void TransitionToScene(string sceneName, TransitionType type = TransitionType.Fade)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[SceneTransitionManager] Đang có một quá trình chuyển cảnh diễn ra.");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneTransitionManager] Tên Scene đích không được để trống!");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName, type));
    }

    private IEnumerator TransitionRoutine(string sceneName, TransitionType type)
    {
        isTransitioning = true;

        // 1. Cập nhật nội dung hiển thị ngẫu nhiên (Tips)
        if (tipText != null && tips != null && tips.Length > 0)
        {
            string randomTip = tips[UnityEngine.Random.Range(0, tips.Length)];
            tipText.text = randomTip;
        }

        // Đặt lại các giá trị tiến trình
        if (progressFillImage != null) progressFillImage.fillAmount = 0f;
        if (progressText != null) progressText.text = "0%";

        // 2. Kích hoạt Panel UI chuyển cảnh
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
        else if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.gameObject.SetActive(true);
        }

        // Đảm bảo chặn mọi click chuột từ người chơi vào các UI phía sau
        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.blocksRaycasts = true;
        }

        // 3. Thực hiện hiệu ứng xuất hiện (Transition In)
        if (type == TransitionType.Fade)
        {
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.DOKill();
                transitionCanvasGroup.alpha = 0f;
                yield return transitionCanvasGroup.DOFade(1f, fadeDuration)
                    .SetEase(Ease.OutQuad)
                    .WaitForCompletion();
            }
        }
        else if (type == TransitionType.Slide)
        {
            if (transitionRectTransform != null)
            {
                // Đảm bảo alpha của CanvasGroup là 1 để hiển thị panel trượt
                if (transitionCanvasGroup != null) transitionCanvasGroup.alpha = 1f;

                // Đặt panel ở ngoài rìa bên phải màn hình trước khi trượt vào
                transitionRectTransform.DOKill();
                transitionRectTransform.anchoredPosition = new Vector2(screenResolution.x, 0);

                // Trượt panel vào tâm màn hình (Vector2.zero)
                yield return transitionRectTransform.DOAnchorPos(Vector2.zero, slideDuration)
                    .SetEase(Ease.OutCubic)
                    .WaitForCompletion();
            }
        }

        // Trễ nhẹ một chút để đảm bảo hoạt ảnh mượt mà
        yield return new WaitForSeconds(0.1f);

        ARFallbackManager.ReleaseDeviceCamera();

        // 4. Tải Scene không đồng bộ (Async Loading)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneTransitionManager] Không tìm thấy Scene '{sceneName}' trong Build Settings!");
            
            // Tắt UI phục hồi trạng thái bình thường
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (transitionCanvasGroup != null) transitionCanvasGroup.blocksRaycasts = false;
            isTransitioning = false;
            yield break;
        }

        // Không cho phép tự kích hoạt scene mới ngay khi tải xong
        // để có thời gian nội suy thanh progress chạy mượt tới 100%
        op.allowSceneActivation = false;

        float targetProgress = 0f;
        float currentProgress = 0f;

        while (currentProgress < 1f)
        {
            // Unity chỉ load tới 0.9 là hoàn tất tài nguyên, 0.1 còn lại là kích hoạt scene
            targetProgress = Mathf.Clamp01(op.progress / 0.9f);

            // Nội suy giá trị tiến trình mượt mà (Tween) để tránh giật lag thanh Slider
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * 1.5f);

            if (progressFillImage != null) progressFillImage.fillAmount = currentProgress;
            if (progressText != null) progressText.text = $"{(int)(currentProgress * 100)}%";

            // Khi thanh load đạt 100% thực tế và tài nguyên đã load xong
            if (currentProgress >= 0.99f && op.progress >= 0.9f)
            {
                currentProgress = 1f;
                if (progressFillImage != null) progressFillImage.fillAmount = 1f;
                if (progressText != null) progressText.text = "100%";
                
                yield return new WaitForSeconds(0.2f); // Giữ màn hình 100% trong 0.2s để người dùng dễ nhìn
                op.allowSceneActivation = true; // Kích hoạt Scene mới
            }

            yield return null;
        }

        // Chờ đến khi Scene mới được kích hoạt và sẵn sàng hoàn toàn
        while (!op.isDone)
        {
            yield return null;
        }

        // Chờ thêm 0.2 giây tại Scene mới để tránh giật lag khung hình đầu tiên (Frame drop khi tải)
        yield return new WaitForSeconds(0.2f);

        // 5. Thực hiện hiệu ứng biến mất (Transition Out)
        if (type == TransitionType.Fade)
        {
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.DOKill();
                yield return transitionCanvasGroup.DOFade(0f, fadeDuration)
                    .SetEase(Ease.InQuad)
                    .WaitForCompletion();
            }
        }
        else if (type == TransitionType.Slide)
        {
            if (transitionRectTransform != null)
            {
                // Trượt panel tiếp sang bên trái màn hình để biến mất
                transitionRectTransform.DOKill();
                yield return transitionRectTransform.DOAnchorPos(new Vector2(-screenResolution.x, 0), slideDuration)
                    .SetEase(Ease.InCubic)
                    .WaitForCompletion();
            }
        }

        // 6. Hoàn tất quá trình chuyển cảnh
        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.blocksRaycasts = false;
        }

        // Tắt hoàn toàn Panel UI đi để tối ưu hóa hiệu năng render (đạt 0 draw calls)
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        else if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.gameObject.SetActive(false);
        }

        isTransitioning = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Tips to English")]
    private void ResetTipsToEnglish()
    {
        tips = new string[] {
            "Scan a flat surface for the best AR experience!",
            "Customize your favorite character in the Studio panel.",
            "Design your own music band in AR Band mode.",
            "Don't forget to play Minigames to earn exciting rewards!",
            "A stable internet connection ensures a smooth AR experience."
        };
        Debug.Log("[SceneTransitionManager] Loading tips have been reset to English in the Inspector!");
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────
    // Thread-safe Dispatcher & Internet connection warning logic
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<T> RunOnMainThreadAsync<T>(Func<T> func)
    {
        if (_mainContext == null || _mainContext == SynchronizationContext.Current)
        {
            return func();
        }

        var tcs = new TaskCompletionSource<T>();
        _mainContext.Post(_ =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return await tcs.Task;
    }

    public async Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> func)
    {
        if (_mainContext == null || _mainContext == SynchronizationContext.Current)
        {
            return await func();
        }

        var tcs = new TaskCompletionSource<T>();
        _mainContext.Post(async _ =>
        {
            try
            {
                T result = await func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return await tcs.Task;
    }

    public async Task RunOnMainThreadAsync(Action action)
    {
        if (_mainContext == null || _mainContext == SynchronizationContext.Current)
        {
            action();
            return;
        }

        var tcs = new TaskCompletionSource<bool>();
        _mainContext.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        await tcs.Task;
    }

    /// <summary>
    /// Kiểm tra kết nối Internet thực tế bằng cách gửi request nhẹ tới google (luôn trả về true vì dự án ngoại tuyến).
    /// </summary>
    public async Task<bool> CheckInternetConnectionAsync()
    {
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Đảm bảo kết nối mạng trước khi gọi API (luôn hoàn tất ngay lập tức vì dự án ngoại tuyến).
    /// </summary>
    public async Task EnsureInternetConnectionAsync()
    {
        await Task.CompletedTask;
    }
}

