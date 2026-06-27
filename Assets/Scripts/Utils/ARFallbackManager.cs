using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

/// <summary>
/// Quản lý tự động chuyển đổi giữa chế độ AR thực tế (ARCore) và chế độ giả lập (WebCam + mặt phẳng ảo)
/// cho các dòng máy cấu hình thấp hoặc khi chạy trong Unity Editor để dễ dàng test.
/// </summary>
public class ARFallbackManager : MonoBehaviour
{
    public enum SceneMode
    {
        ARScene,
        NonARScene
    }

    [Header("Mode Selection")]
    [SerializeField]
    private SceneMode m_SceneMode = SceneMode.ARScene;

    public SceneMode sceneMode
    {
        get => m_SceneMode;
        set => m_SceneMode = value;
    }

    [Header("Components")]
    [SerializeField] 
    [Tooltip("ARSession trong Scene. Nếu để trống, script sẽ tự động tìm.")]
    private ARSession m_ARSession;

    [SerializeField] 
    [Tooltip("ARPlaneManager trong Scene. Nếu để trống, script sẽ tự động tìm.")]
    private ARPlaneManager m_ARPlaneManager;

    [SerializeField] 
    [Tooltip("TapToPlacePrefab trong Scene. Nếu để trống, script sẽ tự động tìm.")]
    private TapToPlacePrefab m_TapToPlacePrefab;

    [SerializeField]
    [Tooltip("RawImage nền có sẵn trong Canvas BG. Script không tự tạo RawImage ở runtime.")]
    private RawImage m_FallbackRawImage;

    [SerializeField]
    [Tooltip("RenderTexture nền do bạn tự tạo và kéo vào Inspector. Nếu để trống, script sẽ giữ texture đang gán sẵn trên RawImage.")]
    private RenderTexture m_BackgroundRenderTexture;

    [SerializeField]
    [Tooltip("Nếu bật, khi thoát AR/Non-AR script sẽ gỡ RenderTexture khỏi RawImage để tránh giữ reference runtime.")]
    private bool m_ClearBackgroundTextureOnCleanup = true;

    private RawImage m_ActiveBackgroundRawImage;
    private RenderTexture m_ActiveBackgroundRenderTexture;
    private RenderTexture m_RuntimeBackgroundRenderTexture;
    private bool m_WebCamStartRequested;

    private WebCamTexture m_WebCamTexture;
    private float m_WebCamStartTime;
    private bool m_WebCamFrameLogged;
    private bool m_WebCamNoFrameWarned;
    private bool m_BackgroundRenderTextureResizeLogged;
    private GameObject m_FallbackCanvasObj;
    private RawImage m_WebCamRawImage;
    private RectTransform m_WebCamRectTransform;
    private bool m_WebCamIsFront;
    private float m_LastScreenWidth;
    private float m_LastScreenHeight;
    private int m_LastVideoRotationAngle = -1;
    private bool m_AutoAlignRawImage = false;

    [SerializeField]
    [Tooltip("Nếu được bật, script sẽ luôn chạy ở chế độ giả lập mà không cần kiểm tra ARCore.")]
    private bool m_ForceFallback = false;

    public bool forceFallback
    {
        get => m_ForceFallback;
        set => m_ForceFallback = value;
    }

    private bool m_InitialForceFallback;
    private string m_CurrentInitializedScene = "";
    private static ARFallbackManager s_Instance;
    private static bool s_ARModeRequestedForCurrentScene;
    private static bool s_StartAROnNextSceneLoad;
    private bool m_SceneLoadedRegistered;
    private bool m_IsCleaningUp;

    [Header("Persistence Settings")]
    [SerializeField]
    [Tooltip("Nếu true, đối tượng này sẽ không bị hủy khi chuyển scene.")]
    private bool m_DontDestroyOnLoad = false;

    private void Awake()
    {
        m_InitialForceFallback = m_ForceFallback;

        // Xử lý Singleton để tránh trùng lặp giữa instance tự động tạo và instance trong Scene
        if (s_Instance != null && s_Instance != this)
        {
            Debug.Log($"ARFallbackManager: Phát hiện instance mới trong scene '{gameObject.scene.name}'. Hủy instance cũ.");
            s_Instance.Cleanup(true);
            Destroy(s_Instance.gameObject);
        }

        s_Instance = this;
        if (m_DontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        // Tự động tìm kiếm các thành phần nếu chưa được gán trong Inspector
        if (m_ARSession == null)
        {
            m_ARSession = FindFirstObjectByType<ARSession>();
        }

        if (m_ARPlaneManager == null)
        {
            m_ARPlaneManager = FindFirstObjectByType<ARPlaneManager>();
        }

        if (m_TapToPlacePrefab == null)
        {
            m_TapToPlacePrefab = FindFirstObjectByType<TapToPlacePrefab>();
        }

        // Đăng ký sự kiện load scene để cập nhật cấu hình động
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (ShouldDeferCameraUntilARMode(activeSceneName) && !IsCameraRequestedForSceneLoad())
        {
            PrepareSceneWithoutCamera(activeSceneName);
        }

        RegisterSceneLoaded();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        RegisterSceneLoaded();

        if (s_Instance == this && !string.IsNullOrEmpty(m_CurrentInitializedScene))
        {
            RestartForCurrentScene();
        }
    }

    private void Start()
    {
        // Thực hiện khởi tạo cho scene hiện tại khi vừa bắt đầu
        InitializeForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"ARFallbackManager: Đã load scene mới = {scene.name}. Đang cấu hình lại...");

        Cleanup(false);
        m_CurrentInitializedScene = "";

        // Chuyển reference bị hủy về null hoàn toàn để tránh lỗi C# reference rác
        if (m_FallbackRawImage == null)
        {
            m_FallbackRawImage = null;
        }

        // Tìm lại các component tương ứng với scene vừa tải
        m_ARSession = FindFirstObjectByType<ARSession>();
        m_ARPlaneManager = FindFirstObjectByType<ARPlaneManager>();
        m_TapToPlacePrefab = FindFirstObjectByType<TapToPlacePrefab>();

        InitializeForScene(scene.name);
    }

    private bool IsARScene(string name)
    {
        return name == "AR Scene" || 
               name == "costom AR" || 
               name == "Custom AR" || 
               name == "Band AR" ||
               name == "Band AR Scene" ||
               name == "Custome AR Scene" ||
               name == "Band Mode AR Scene";
    }

    private bool IsNonARScene(string name)
    {
        return name == "Non-AR Scene" || 
               name == "costom Non AR" || 
               name == "Custom Non AR" || 
               name == "Band Non AR" ||
               name == "Band NonAR Scene" ||
               name == "Band Non-AR Scene" ||
               name == "Custome NonAR Scene" ||
               name == "Band Mode NonAR Scene";
    }

    private bool ShouldDeferCameraUntilARMode(string name)
    {
        return name == "Band AR" ||
               name == "Band AR Scene" ||
               name == "Band Non AR" ||
               name == "Band NonAR Scene" ||
               name == "Band Non-AR Scene" ||
               name == "Band Mode AR Scene" ||
               name == "Band Mode NonAR Scene" ||
               name == "Custome AR Scene" ||
               name == "Custome NonAR Scene" ||
               name == "Custom AR" ||
               name == "Custom Non AR" ||
               name == "costom AR" ||
               name == "costom Non AR";
    }

    private bool IsCameraRequestedForSceneLoad()
    {
        return s_ARModeRequestedForCurrentScene || s_StartAROnNextSceneLoad;
    }

    private void PrepareSceneWithoutCamera(string sceneName)
    {
        Debug.Log($"ARFallbackManager: Scene '{sceneName}' dang o menu chon. Tam dung AR/WebCam cho den khi nguoi dung vao AR mode.");
        Cleanup(false);
    }

    private void InitializeForScene(string sceneName)
    {
        if (m_CurrentInitializedScene == sceneName)
        {
            return;
        }

        m_CurrentInitializedScene = sceneName;

        // Dừng các coroutine cũ đang chạy
        StopAllCoroutines();

        // 1. Chỉ chạy ở AR Scene và Non-AR Scene. Các scene khác (khởi động, đăng nhập, menu chính, studio...) thì dọn dẹp camera và bỏ qua hoàn toàn.
        bool isArCapableScene = IsARScene(sceneName) || IsNonARScene(sceneName);
        if (!isArCapableScene)
        {
            s_ARModeRequestedForCurrentScene = false;
            s_StartAROnNextSceneLoad = false;
            Debug.Log($"ARFallbackManager: Đang ở scene '{sceneName}', tiến hành dọn dẹp và bỏ qua kích hoạt Camera.");
            Cleanup(false);
            return;
        }

        if (s_StartAROnNextSceneLoad)
        {
            s_ARModeRequestedForCurrentScene = true;
            s_StartAROnNextSceneLoad = false;
        }

        if (ShouldDeferCameraUntilARMode(sceneName) && !s_ARModeRequestedForCurrentScene)
        {
            PrepareSceneWithoutCamera(sceneName);
            return;
        }

        // Khôi phục giá trị cấu hình ban đầu
        m_ForceFallback = m_InitialForceFallback;

        // 2. Bắt buộc kích hoạt chế độ giả lập nếu ở Non-AR Scene hoặc được thiết lập cứng trong Inspector
        bool isNonAR = (m_SceneMode == SceneMode.NonARScene) || IsNonARScene(sceneName);
        if (isNonAR)
        {
            m_ForceFallback = true;
        }

        if (m_ForceFallback)
        {
            Debug.Log($"ARFallbackManager: Kích hoạt Chế độ Giả lập AR (Force Fallback) cho scene '{sceneName}'.");
            ActivateFallbackMode();
        }
        else
        {
            Debug.Log($"ARFallbackManager: Đang chạy kiểm tra khả năng tương thích AR cho scene '{sceneName}'.");
            StartCoroutine(CheckARAvailability());
        }
    }

    private IEnumerator CheckARAvailability()
    {
        Debug.Log("ARFallbackManager: Đang kiểm tra tính tương thích của thiết bị với ARCore...");
        
        // Chờ kiểm tra trạng thái khả dụng của ARSession
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        Debug.Log($"ARFallbackManager: Trạng thái AR Session = {ARSession.state}");

        // Nếu thiết bị không hỗ trợ ARCore
        bool isUnsupported = (ARSession.state == ARSessionState.Unsupported);
        
#if UNITY_EDITOR
        // Trong Unity Editor, mặc định chạy chế độ AR bình thường để sử dụng AR Simulator của Unity
        isUnsupported = false; 
#endif

        if (isUnsupported)
        {
            Debug.LogWarning("ARFallbackManager: Thiết bị không hỗ trợ ARCore. Kích hoạt Chế độ Giả lập AR (WebCam + Mặt phẳng ảo).");
            ActivateFallbackMode();
        }
        else
        {
            Debug.Log("ARFallbackManager: Kích hoạt Chế độ AR thực tế (hoặc AR Simulator trong Editor).");
            ActivateARMode();
        }
    }

    /// <summary>
    /// Kích hoạt chế độ AR thực tế sử dụng ARCore quét mặt phẳng.
    /// </summary>
    private void ActivateARMode()
    {
        StopFallbackVisuals();
        StartCoroutine(StartARCoreFlow());
    }

    private IEnumerator StartARCoreFlow()
    {
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            if (XRGeneralSettings.Instance.Manager.activeLoader == null)
            {
                Debug.Log("ARFallbackManager: Khởi tạo XR Loader thủ công...");
                yield return XRGeneralSettings.Instance.Manager.InitializeLoader();
            }

            if (XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                Debug.Log("ARFallbackManager: Bắt đầu các subsystems...");
                XRGeneralSettings.Instance.Manager.StartSubsystems();
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning("ARFallbackManager: Không thể khởi tạo XR Loader trong Editor. Chuyển sang chế độ giả lập.");
#else
                Debug.LogError("ARFallbackManager: Không thể khởi tạo XR Loader trên thiết bị. Chuyển sang chế độ giả lập.");
#endif
                ActivateFallbackMode();
                yield break;
            }
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("ARFallbackManager: XRGeneralSettings hoặc XR Manager chưa sẵn sàng trong Editor. Chuyển sang chế độ giả lập.");
#else
            Debug.LogError("ARFallbackManager: XRGeneralSettings hoặc XR Manager chưa sẵn sàng. Chuyển sang chế độ giả lập.");
#endif
            ActivateFallbackMode();
            yield break;
        }

        EnableARComponentsInScene();

        if (m_TapToPlacePrefab != null)
        {
            m_TapToPlacePrefab.useFallbackMode = false;
        }

        if (m_FallbackRawImage != null)
        {
            m_FallbackRawImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Kích hoạt chế độ Giả lập AR sử dụng camera thường và mặt phẳng ảo.
    /// </summary>
    private void ActivateFallbackMode()
    {
        if (m_FallbackRawImage != null)
        {
            m_FallbackRawImage.gameObject.SetActive(true);
        }

        DisableARComponentsInScene();
        StopXRSubsystems();

        // 1. Tắt các thành phần quét không gian của ARCore để tránh xung đột
        if (m_ARSession != null)
        {
            m_ARSession.enabled = false;
        }

        if (m_ARPlaneManager != null)
        {
            m_ARPlaneManager.enabled = false;
        }

        // Tắt component ARCameraBackground trên Main Camera để tránh xung đột làm đen màn hình
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = FindFirstObjectByType<Camera>();
        }
        if (mainCam != null)
        {
            ARCameraBackground cameraBackground = mainCam.GetComponent<ARCameraBackground>();
            if (cameraBackground != null)
            {
                cameraBackground.enabled = false;
            }
        }

        // 2. Chuyển đổi logic tương tác sang chế độ mặt phẳng ảo
        if (m_TapToPlacePrefab != null)
        {
            m_TapToPlacePrefab.useFallbackMode = true;
        }

        // 3. Yêu cầu cấp quyền camera và khởi chạy luồng hình ảnh Camera di động làm phông nền (Chỉ chạy ở Non-AR Scene)
        string activeScene = SceneManager.GetActiveScene().name;
        if (IsNonARScene(activeScene))
        {
            ConfigureManualBackground();
        }
        else
        {
            // Ở AR Scene khi chạy giả lập: Chỉ hiển thị hình nền giả lập màu tối, không bật camera phông nền
            ConfigureManualBackground();
        }
    }

    private void ConfigureManualBackground()
    {
        if (m_FallbackRawImage == null)
        {
            Debug.LogWarning("ARFallbackManager: Chưa gán Fallback Raw Image. Hãy kéo RawImage trong Canvas BG vào Inspector.");
            return;
        }

        m_ActiveBackgroundRawImage = m_FallbackRawImage;
        m_ActiveBackgroundRawImage.gameObject.SetActive(true);
        m_ActiveBackgroundRawImage.raycastTarget = false;
        m_ActiveBackgroundRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        m_ActiveBackgroundRenderTexture = ResolveBackgroundRenderTexture();
        if (m_ActiveBackgroundRenderTexture != null)
        {
            m_ActiveBackgroundRawImage.texture = m_ActiveBackgroundRenderTexture;
            m_ActiveBackgroundRawImage.color = Color.white;
            if (!m_WebCamStartRequested && m_WebCamTexture == null)
            {
                m_WebCamStartRequested = true;
                StartCoroutine(RequestCameraPermissionAndStartWebCam());
            }
        }
        else
        {
            Debug.LogWarning("ARFallbackManager: Chưa gán Background Render Texture. Bạn có thể kéo RenderTexture vào Manager hoặc gán trực tiếp vào RawImage.");
        }
    }

    private RenderTexture ResolveBackgroundRenderTexture()
    {
        if (m_BackgroundRenderTexture != null)
        {
            return m_BackgroundRenderTexture;
        }

        if (m_FallbackRawImage != null && m_FallbackRawImage.texture is RenderTexture renderTexture)
        {
            return renderTexture;
        }

        return null;
    }

    /// <summary>
    /// Yêu cầu quyền CAMERA trên thiết bị Android trước khi khởi chạy WebCamTexture.
    /// </summary>
    private IEnumerator RequestCameraPermissionAndStartWebCam()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        string cameraPermission = UnityEngine.Android.Permission.Camera;
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cameraPermission))
        {
            Debug.Log("ARFallbackManager: Yêu cầu cấp quyền CAMERA...");
            UnityEngine.Android.Permission.RequestUserPermission(cameraPermission);
            
            // Chờ tối đa 5 giây hoặc cho tới khi được cấp quyền
            float elapsed = 0f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cameraPermission) && elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
#endif
        StartFallbackWebCam();
        m_WebCamStartRequested = false;
        yield break;
    }

    /// <summary>
    /// Thiết lập và khởi chạy camera thường (WebCamTexture) hiển thị làm hình nền giả lập AR.
    /// </summary>
    private void StartFallbackWebCam()
    {
        if (m_WebCamTexture != null)
        {
            if (!m_WebCamTexture.isPlaying)
            {
                m_WebCamTexture.Play();
            }
            return;
        }

        if (m_ActiveBackgroundRenderTexture == null)
        {
            m_ActiveBackgroundRenderTexture = ResolveBackgroundRenderTexture();
        }

        if (m_ActiveBackgroundRenderTexture == null)
        {
            Debug.LogWarning("ARFallbackManager: Khong co RenderTexture de nhan hinh webcam.");
            return;
        }

        // Tìm và bật camera sau của điện thoại
        string deviceName = "";
        bool isFront = false;
        if (WebCamTexture.devices.Length > 0)
        {
            deviceName = WebCamTexture.devices[0].name;
            isFront = WebCamTexture.devices[0].isFrontFacing;
            for (int i = 0; i < WebCamTexture.devices.Length; i++)
            {
                if (!WebCamTexture.devices[i].isFrontFacing)
                {
                    deviceName = WebCamTexture.devices[i].name;
                    isFront = false;
                    break;
                }
                else
                {
                    isFront = true;
                }
            }
        }

        if (m_FallbackRawImage != null)
        {
            // Sử dụng RawImage do người dùng truyền vào
            m_WebCamRawImage = m_FallbackRawImage;
            m_WebCamRectTransform = m_WebCamRawImage.GetComponent<RectTransform>();
            m_AutoAlignRawImage = true;
            
            if (m_AutoAlignRawImage)
            {
                // Cấu hình các anchor và pivot về chính giữa để xoay và scale không bị lệch góc
                m_WebCamRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                m_WebCamRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                m_WebCamRectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
        }
        else
        {
            // Tự động tạo Canvas và RawImage mới nếu người dùng để trống
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindFirstObjectByType<Camera>();
            }

            m_FallbackCanvasObj = new GameObject("ARFallbackCanvas");
            Canvas canvas = m_FallbackCanvasObj.AddComponent<Canvas>();
            
            if (mainCam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCam;
                canvas.planeDistance = Mathf.Clamp(mainCam.farClipPlane - 10f, 10f, 100f);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -100;
            }

            m_FallbackCanvasObj.AddComponent<CanvasScaler>();
            m_FallbackCanvasObj.AddComponent<GraphicRaycaster>();

            GameObject rawImageObj = new GameObject("WebCamBackground");
            rawImageObj.transform.SetParent(m_FallbackCanvasObj.transform, false);
            m_WebCamRawImage = rawImageObj.AddComponent<RawImage>();
            m_WebCamRectTransform = m_WebCamRawImage.GetComponent<RectTransform>();

            m_WebCamRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            m_WebCamRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            m_WebCamRectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        // Tắt raycastTarget trên RawImage phông nền để không chặn click chuột / chạm tay ngoài 3D
        if (m_WebCamRawImage != null)
        {
            m_WebCamRawImage.raycastTarget = false;
        }

        if (!string.IsNullOrEmpty(deviceName))
        {
            m_WebCamTexture = new WebCamTexture(deviceName, 1280, 720, 30);
            m_WebCamTexture.Play();
            m_WebCamIsFront = isFront;
            m_WebCamStartTime = Time.unscaledTime;
            m_WebCamFrameLogged = false;
            m_WebCamNoFrameWarned = false;
            Debug.Log($"ARFallbackManager: Dang mo WebCamTexture device='{deviceName}', front={isFront}.");
        }
        else
        {
            Debug.LogWarning("ARFallbackManager: Không tìm thấy camera vật lý trên thiết bị (Có thể do chạy trên Unity Editor máy tính không có WebCam). Một hình nền giả lập màu xám tối sẽ được bật để bạn vẫn có thể test các tính năng spawn nhân vật và di chuyển bình thường.");
            m_WebCamRawImage.color = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        }
    }

    /// <summary>
    /// Thiết lập hình nền giả lập màu xám tối và không kích hoạt camera thực tế (cho Non-AR Scene).
    /// </summary>
    private void StartFallbackWithoutCamera()
    {
        if (m_FallbackCanvasObj != null || m_WebCamRawImage != null) return;

        if (m_FallbackRawImage != null)
        {
            m_WebCamRawImage = m_FallbackRawImage;
            m_WebCamRawImage.color = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        }
        else
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindFirstObjectByType<Camera>();
            }

            m_FallbackCanvasObj = new GameObject("ARFallbackCanvas");
            Canvas canvas = m_FallbackCanvasObj.AddComponent<Canvas>();
            
            if (mainCam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCam;
                canvas.planeDistance = Mathf.Clamp(mainCam.farClipPlane - 10f, 10f, 100f);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -100;
            }

            m_FallbackCanvasObj.AddComponent<CanvasScaler>();
            m_FallbackCanvasObj.AddComponent<GraphicRaycaster>();

            GameObject rawImageObj = new GameObject("WebCamBackground");
            rawImageObj.transform.SetParent(m_FallbackCanvasObj.transform, false);
            m_WebCamRawImage = rawImageObj.AddComponent<RawImage>();
            m_WebCamRawImage.color = new Color(0.15f, 0.15f, 0.15f, 1.0f);
            
            m_WebCamRectTransform = m_WebCamRawImage.GetComponent<RectTransform>();
            m_WebCamRectTransform.anchorMin = Vector2.zero;
            m_WebCamRectTransform.anchorMax = Vector2.one;
            m_WebCamRectTransform.sizeDelta = Vector2.zero;
        }

        if (m_WebCamRawImage != null)
        {
            m_WebCamRawImage.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (m_WebCamTexture != null && m_WebCamTexture.isPlaying && m_ActiveBackgroundRenderTexture != null)
        {
            BlitWebCamToRenderTexture();
        }

        if (m_WebCamTexture != null && m_WebCamTexture.isPlaying && m_WebCamRectTransform != null && m_AutoAlignRawImage)
        {
            UpdateWebCamLayout();
        }
    }

    private void BlitWebCamToRenderTexture()
    {
        if (m_WebCamTexture.width < 16 || m_WebCamTexture.height < 16)
        {
            WarnIfWebCamHasNoFrameYet();
            return;
        }

        if (!m_WebCamFrameLogged && m_WebCamTexture.didUpdateThisFrame)
        {
            m_WebCamFrameLogged = true;
            Debug.Log($"ARFallbackManager: Da nhan frame webcam dau tien {m_WebCamTexture.width}x{m_WebCamTexture.height}, rotation={m_WebCamTexture.videoRotationAngle}, mirrored={m_WebCamTexture.videoVerticallyMirrored}.");
        }

        EnsureBackgroundRenderTextureMatchesWebCam();
        Graphics.Blit(m_WebCamTexture, m_ActiveBackgroundRenderTexture);
    }

    private void WarnIfWebCamHasNoFrameYet()
    {
        if (m_WebCamNoFrameWarned || m_WebCamStartTime <= 0f)
        {
            return;
        }

        if (Time.unscaledTime - m_WebCamStartTime < 3f)
        {
            return;
        }

        m_WebCamNoFrameWarned = true;
        Debug.LogWarning("ARFallbackManager: Camera da duoc mo nhung WebCamTexture chua tra frame hop le sau 3 giay. Kiem tra device camera, quyen camera, hoac thu doi camera mac dinh trong he dieu hanh.");
    }

    private void EnsureBackgroundRenderTextureMatchesWebCam()
    {
        if (m_ActiveBackgroundRenderTexture == null)
        {
            return;
        }

        int sourceWidth = Mathf.Max(16, m_WebCamTexture.width);
        int sourceHeight = Mathf.Max(16, m_WebCamTexture.height);
        if (m_ActiveBackgroundRenderTexture.width == sourceWidth &&
            m_ActiveBackgroundRenderTexture.height == sourceHeight)
        {
            return;
        }

        if (m_RuntimeBackgroundRenderTexture != null &&
            m_RuntimeBackgroundRenderTexture.width == sourceWidth &&
            m_RuntimeBackgroundRenderTexture.height == sourceHeight)
        {
            m_ActiveBackgroundRenderTexture = m_RuntimeBackgroundRenderTexture;
            if (m_ActiveBackgroundRawImage != null)
            {
                m_ActiveBackgroundRawImage.texture = m_ActiveBackgroundRenderTexture;
            }
            return;
        }

        RenderTexture sourceSettings = m_BackgroundRenderTexture != null ? m_BackgroundRenderTexture : m_ActiveBackgroundRenderTexture;
        RenderTextureFormat colorFormat = sourceSettings != null ? sourceSettings.format : RenderTextureFormat.ARGB32;
        FilterMode filterMode = sourceSettings != null ? sourceSettings.filterMode : FilterMode.Bilinear;
        TextureWrapMode wrapMode = sourceSettings != null ? sourceSettings.wrapMode : TextureWrapMode.Clamp;

        ReleaseRuntimeBackgroundRenderTexture();

        m_RuntimeBackgroundRenderTexture = new RenderTexture(sourceWidth, sourceHeight, 0, colorFormat)
        {
            name = "ARFallbackWebCamBackground_Runtime",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = filterMode,
            wrapMode = wrapMode
        };
        m_RuntimeBackgroundRenderTexture.Create();

        m_ActiveBackgroundRenderTexture = m_RuntimeBackgroundRenderTexture;
        if (m_ActiveBackgroundRawImage != null)
        {
            m_ActiveBackgroundRawImage.texture = m_ActiveBackgroundRenderTexture;
        }

        if (!m_BackgroundRenderTextureResizeLogged)
        {
            m_BackgroundRenderTextureResizeLogged = true;
            Debug.Log($"ARFallbackManager: Da tao Background RenderTexture runtime {sourceWidth}x{sourceHeight} de tranh keo dan webcam.");
        }
    }

    /// <summary>
    /// Điều chỉnh tỉ lệ và góc xoay của WebCamBackground để hiển thị chính xác theo hướng màn hình di động.
    /// </summary>
    private void UpdateWebCamLayout()
    {
        if (m_WebCamTexture.width < 100) return;

        // Lấy kích thước của Canvas (parent) để tính toán chính xác thay vì dùng Screen.width/height trực tiếp
        RectTransform parentRect = m_WebCamRectTransform.parent as RectTransform;
        if (parentRect == null) return;

        int rotationAngle = m_WebCamTexture.videoRotationAngle;
        float parentWidth = parentRect.rect.width;
        float parentHeight = parentRect.rect.height;

        // Chỉ cập nhật nếu có sự thay đổi về kích thước camera, kích thước Canvas hoặc góc xoay để tránh recalculate không cần thiết
        if (m_WebCamRectTransform.sizeDelta.x == m_WebCamTexture.width && 
            m_WebCamRectTransform.sizeDelta.y == m_WebCamTexture.height &&
            m_LastScreenWidth == parentWidth &&
            m_LastScreenHeight == parentHeight &&
            m_LastVideoRotationAngle == rotationAngle)
        {
            return;
        }

        m_LastScreenWidth = parentWidth;
        m_LastScreenHeight = parentHeight;
        m_LastVideoRotationAngle = rotationAngle;

        // Cập nhật kích thước RectTransform bằng kích thước gốc của camera
        m_WebCamRectTransform.sizeDelta = new Vector2(m_WebCamTexture.width, m_WebCamTexture.height);
        m_WebCamRectTransform.localEulerAngles = new Vector3(0, 0, -rotationAngle);

        // Tính toán kích thước canvas tương ứng với các trục local của RectTransform sau khi xoay
        float targetWidth, targetHeight;
        if (rotationAngle == 90 || rotationAngle == 270)
        {
            targetWidth = parentHeight;
            targetHeight = parentWidth;
        }
        else
        {
            targetWidth = parentWidth;
            targetHeight = parentHeight;
        }

        // Tính tỉ lệ phóng to (Aspect Fill) để lấp đầy toàn bộ canvas mà không bị méo hình
        float scaleX = targetWidth / m_WebCamTexture.width;
        float scaleY = targetHeight / m_WebCamTexture.height;
        float s = Mathf.Max(scaleX, scaleY);

        // Xử lý lật hình (mirroring): camera trước thường bị lật, và kiểm tra videoVerticallyMirrored của Unity
        float scaleXSign = m_WebCamIsFront ? -1.0f : 1.0f;
        float scaleYSign = m_WebCamTexture.videoVerticallyMirrored ? -1.0f : 1.0f;

        m_WebCamRectTransform.localScale = new Vector3(s * scaleXSign, s * scaleYSign, 1.0f);
    }

    public static void ReleaseDeviceCamera()
    {
        s_ARModeRequestedForCurrentScene = false;

        if (s_Instance != null)
        {
            s_Instance.Cleanup(false);
        }
    }

    public static void RequestAROnNextSceneLoad()
    {
        s_StartAROnNextSceneLoad = true;
    }

    public static void ResumeForCurrentScene()
    {
        s_ARModeRequestedForCurrentScene = true;

        if (s_Instance != null)
        {
            s_Instance.RestartForCurrentScene();
        }
    }

    public static bool IsARModeRequestedForCurrentScene()
    {
        return s_ARModeRequestedForCurrentScene || s_StartAROnNextSceneLoad;
    }

    private void RestartForCurrentScene()
    {
        if (!isActiveAndEnabled) return;

        m_CurrentInitializedScene = "";
        InitializeForScene(SceneManager.GetActiveScene().name);
    }

    private void RegisterSceneLoaded()
    {
        if (m_SceneLoadedRegistered) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        m_SceneLoadedRegistered = true;
    }

    private void UnregisterSceneLoaded()
    {
        if (!m_SceneLoadedRegistered) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        m_SceneLoadedRegistered = false;
    }

    public void Cleanup(bool unregisterEvent = false)
    {
        if (m_IsCleaningUp) return;
        m_IsCleaningUp = true;

        if (unregisterEvent)
        {
            UnregisterSceneLoaded();
        }

        StopAllCoroutines();
        DisableARComponentsInScene();
        StopXRSubsystems();
        StopWebCamTexture();
        ReleaseRuntimeBackgroundRenderTexture();
        ReleaseManualBackgroundTexture();

        if (m_FallbackRawImage != null)
        {
            m_FallbackRawImage.gameObject.SetActive(false);
        }

        if (m_FallbackCanvasObj != null)
        {
            DestroyUnityObject(m_FallbackCanvasObj);
            m_FallbackCanvasObj = null;
        }

        m_WebCamRawImage = null;
        m_WebCamRectTransform = null;
        m_LastVideoRotationAngle = -1;
        m_LastScreenWidth = 0f;
        m_LastScreenHeight = 0f;

        m_IsCleaningUp = false;
    }

    private void StopWebCamTexture()
    {
        if (m_WebCamRawImage != null && m_WebCamRawImage.texture == m_WebCamTexture)
        {
            m_WebCamRawImage.texture = null;
        }

        if (m_FallbackRawImage != null && m_FallbackRawImage.texture == m_WebCamTexture)
        {
            m_FallbackRawImage.texture = null;
        }

        if (m_WebCamTexture == null)
        {
            m_WebCamStartRequested = false;
            return;
        }

        if (m_WebCamTexture.isPlaying)
        {
            m_WebCamTexture.Stop();
        }

        DestroyUnityObject(m_WebCamTexture);
        m_WebCamTexture = null;
        m_WebCamStartRequested = false;
        m_WebCamStartTime = 0f;
        m_WebCamFrameLogged = false;
        m_WebCamNoFrameWarned = false;
        m_BackgroundRenderTextureResizeLogged = false;
    }

    private void ReleaseManualBackgroundTexture()
    {
        if (!m_ClearBackgroundTextureOnCleanup || m_BackgroundRenderTexture == null)
        {
            return;
        }

        if (m_ActiveBackgroundRawImage != null && m_ActiveBackgroundRawImage.texture == m_BackgroundRenderTexture)
        {
            m_ActiveBackgroundRawImage.texture = null;
        }

        if (m_FallbackRawImage != null && m_FallbackRawImage.texture == m_BackgroundRenderTexture)
        {
            m_FallbackRawImage.texture = null;
        }

        m_ActiveBackgroundRawImage = null;
    }

    private void ReleaseRuntimeBackgroundRenderTexture()
    {
        if (m_RuntimeBackgroundRenderTexture == null)
        {
            return;
        }

        if (m_ActiveBackgroundRawImage != null && m_ActiveBackgroundRawImage.texture == m_RuntimeBackgroundRenderTexture)
        {
            m_ActiveBackgroundRawImage.texture = null;
        }

        if (m_FallbackRawImage != null && m_FallbackRawImage.texture == m_RuntimeBackgroundRenderTexture)
        {
            m_FallbackRawImage.texture = null;
        }

        if (m_ActiveBackgroundRenderTexture == m_RuntimeBackgroundRenderTexture)
        {
            m_ActiveBackgroundRenderTexture = null;
        }

        m_RuntimeBackgroundRenderTexture.Release();
        DestroyUnityObject(m_RuntimeBackgroundRenderTexture);
        m_RuntimeBackgroundRenderTexture = null;
    }

    private void StopFallbackVisuals()
    {
        StopWebCamTexture();
        ReleaseRuntimeBackgroundRenderTexture();
        ReleaseManualBackgroundTexture();

        if (m_FallbackRawImage != null)
        {
            m_FallbackRawImage.gameObject.SetActive(false);
        }

        if (m_FallbackCanvasObj != null)
        {
            DestroyUnityObject(m_FallbackCanvasObj);
            m_FallbackCanvasObj = null;
        }

        m_WebCamRawImage = null;
        m_WebCamRectTransform = null;
    }

    private void EnableARComponentsInScene()
    {
        if (m_ARSession != null)
        {
            m_ARSession.enabled = true;
        }

        if (m_ARPlaneManager != null)
        {
            m_ARPlaneManager.enabled = true;
        }

        ARCameraManager[] cameraManagers = FindObjectsByType<ARCameraManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARCameraManager cameraManager in cameraManagers)
        {
            if (cameraManager != null)
            {
                cameraManager.enabled = true;
            }
        }

        ARCameraBackground[] cameraBackgrounds = FindObjectsByType<ARCameraBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARCameraBackground cameraBackground in cameraBackgrounds)
        {
            if (cameraBackground != null)
            {
                cameraBackground.enabled = true;
            }
        }
    }

    private void DisableARComponentsInScene()
    {
        if (m_ARSession != null)
        {
            m_ARSession.enabled = false;
        }

        if (m_ARPlaneManager != null)
        {
            m_ARPlaneManager.enabled = false;
        }

        ARSession[] sessions = FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARSession session in sessions)
        {
            if (session != null)
            {
                session.enabled = false;
            }
        }

        ARPlaneManager[] planeManagers = FindObjectsByType<ARPlaneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARPlaneManager planeManager in planeManagers)
        {
            if (planeManager != null)
            {
                planeManager.enabled = false;
            }
        }

        ARCameraManager[] cameraManagers = FindObjectsByType<ARCameraManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARCameraManager cameraManager in cameraManagers)
        {
            if (cameraManager != null)
            {
                cameraManager.enabled = false;
            }
        }

        ARCameraBackground[] cameraBackgrounds = FindObjectsByType<ARCameraBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ARCameraBackground cameraBackground in cameraBackgrounds)
        {
            if (cameraBackground != null)
            {
                cameraBackground.enabled = false;
            }
        }
    }

    private void StopXRSubsystems()
    {
        XRManagerSettings xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager == null || !xrManager.isInitializationComplete) return;

        Debug.Log("ARFallbackManager: Dừng subsystems và hủy khởi tạo XR Loader...");
        xrManager.StopSubsystems();
        xrManager.DeinitializeLoader();
    }

    private static void DestroyUnityObject(UnityEngine.Object obj)
    {
        if (obj == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
            return;
        }
#endif
        Destroy(obj);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;

        Cleanup(false);
        UnregisterSceneLoaded();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Cleanup(false);
            return;
        }

        RestartForCurrentScene();
    }

    private void OnApplicationQuit()
    {
        Cleanup(true);
    }

    private void OnDestroy()
    {
        Cleanup(true);
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }

    /// <summary>
    /// Tự động tạo đối tượng ARFallbackManager trong scene khi bắt đầu game tại runtime,
    /// giúp lập trình viên không cần kéo thả component thủ công trong Unity Editor.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<ARFallbackManager>() == null)
        {
            GameObject managerObj = new GameObject("ARFallbackManager_AutoCreated");
            managerObj.AddComponent<ARFallbackManager>();
            DontDestroyOnLoad(managerObj);
            Debug.Log("ARFallbackManager: Đã tự động khởi tạo thành công tại runtime!");
        }
    }
}
