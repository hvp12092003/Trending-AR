using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

public class SceneDuplicator : EditorWindow
{
    [MenuItem("Tools/Generate Non-AR Scene & Loader")]
    public static void GenerateScenes()
    {
        string sceneDirectory = "Assets/Scenes";
        string mainScenePath = "Assets/Scenes/AR Scene.unity";
        string nonArScenePath = "Assets/Scenes/Non-AR Scene.unity";
        string menuScenePath = "Assets/Scenes/Menu Scene.unity";

        // Kiểm tra xem Scene gốc có tồn tại không
        if (!File.Exists(mainScenePath))
        {
            EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy Scene gốc tại: {mainScenePath}. Vui lòng kiểm tra lại đường dẫn.", "OK");
            return;
        }

        // Tạo thư mục nếu chưa có
        if (!Directory.Exists(sceneDirectory))
        {
            Directory.CreateDirectory(sceneDirectory);
        }

        // Bước 1: Nhân bản Main Scene thành Main Scene Non-AR
        AssetDatabase.DeleteAsset(nonArScenePath);
        if (AssetDatabase.CopyAsset(mainScenePath, nonArScenePath))
        {
            Debug.Log($"[SceneDuplicator] Đã nhân bản Main Scene thành: {nonArScenePath}");
        }
        else
        {
            EditorUtility.DisplayDialog("Lỗi", "Không thể nhân bản file Scene. Vui lòng kiểm tra lại quyền ghi file.", "OK");
            return;
        }

        AssetDatabase.Refresh();

        // Bước 2: Load Scene Non-AR và tiến hành sửa đổi
        UnityEngine.SceneManagement.Scene nonArScene = EditorSceneManager.OpenScene(nonArScenePath, OpenSceneMode.Single);

        // 2.1 Tìm và Xóa các Component AR và GameObjects liên quan đến Origin/XR
        var arSessions = Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var arSession in arSessions)
        {
            Undo.DestroyObjectImmediate(arSession.gameObject);
            Debug.Log("[SceneDuplicator] Đã loại bỏ AR Session.");
        }

        // Đệ quy xóa các đối tượng XR Origin, AR Session Origin dựa vào tên của chúng (an toàn khi compile)
        var rootGameObjects = nonArScene.GetRootGameObjects();
        foreach (var rootGo in rootGameObjects)
        {
            DeleteAROriginGameObjects(rootGo);
        }

        // 2.2 Tạo camera thường và đặt tag là MainCamera
        GameObject newCamObj = new GameObject("Main Camera");
        Camera newCam = newCamObj.AddComponent<Camera>();
        newCamObj.AddComponent<AudioListener>();
        newCamObj.tag = "MainCamera";
        newCamObj.transform.position = new Vector3(0f, 0f, 0f);
        newCamObj.transform.rotation = Quaternion.identity;
        newCam.clearFlags = CameraClearFlags.Skybox;
        Debug.Log("[SceneDuplicator] Đã tạo Camera thường mới.");

        // 2.3 Tạo mặt phẳng ảo (Virtual Ground Plane) tại y = -1.2f
        GameObject planeObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeObj.name = "VirtualGroundPlane";
        planeObj.transform.position = new Vector3(0f, -1.2f, 0f);
        planeObj.transform.localScale = new Vector3(100f, 1f, 100f);
        
        // Ẩn MeshRenderer của mặt phẳng để làm nó trở nên vô hình (chỉ giữ Collider để Raycast hoạt động)
        MeshRenderer meshRenderer = planeObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
        Debug.Log("[SceneDuplicator] Đã tạo Virtual Ground Plane tại y = -1.2.");

        // 2.4 Cấu hình các script về trạng thái Force Fallback
        var tapToPlace = Object.FindFirstObjectByType<TapToPlacePrefab>();
        if (tapToPlace != null)
        {
            tapToPlace.useFallbackMode = true;
            EditorUtility.SetDirty(tapToPlace);
            Debug.Log("[SceneDuplicator] Đã thiết lập useFallbackMode = true trên TapToPlacePrefab.");
        }

        var fallbackManager = Object.FindFirstObjectByType<ARFallbackManager>();
        if (fallbackManager != null)
        {
            fallbackManager.forceFallback = true;
            EditorUtility.SetDirty(fallbackManager);
            Debug.Log("[SceneDuplicator] Đã thiết lập forceFallback = true trên ARFallbackManager.");
        }

        // Lưu scene đã chỉnh sửa
        EditorSceneManager.SaveScene(nonArScene);
        Debug.Log("[SceneDuplicator] Đã lưu Scene Non-AR chỉnh sửa.");

        // Bước 3: Tạo Menu Scene chứa Scroll View chọn Mini Game
        UnityEngine.SceneManagement.Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // Tìm camera mặc định trong scene mới tạo và xóa AudioListener cũ nếu cần
        Camera menuCam = Camera.main;
        if (menuCam != null)
        {
            menuCam.clearFlags = CameraClearFlags.SolidColor;
            menuCam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
        }

        // Tạo Main Menu Controller
        GameObject menuControllerObj = new GameObject("MainMenuController");
        MainMenuController menuController = menuControllerObj.AddComponent<MainMenuController>();

        // Tạo Canvas cho UI
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Load font TMPro mặc định từ dự án
        TMPro.TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        // Tiêu đề game chính
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(canvasObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, 220f);
        titleRect.sizeDelta = new Vector2(600f, 120f);
        var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "TRENDING AR\n& MINI GAMES";
        titleText.font = tmpFont;
        titleText.fontSize = 42;
        titleText.fontStyle = TMPro.FontStyles.Bold;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = new Color(0.9f, 0.8f, 0.2f, 1f); // Màu vàng gold đẹp mắt

        // TẠO SCROLL VIEW CHỨA CÁC MINI GAMES
        GameObject scrollViewObj = new GameObject("MiniGamesScrollView");
        scrollViewObj.transform.SetParent(canvasObj.transform, false);
        var scrollRectTransform = scrollViewObj.AddComponent<RectTransform>();
        scrollRectTransform.anchoredPosition = new Vector2(0f, -40f);
        scrollRectTransform.sizeDelta = new Vector2(500f, 360f);

        var scrollRect = scrollViewObj.AddComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        
        // Thêm hình nền mờ nhẹ cho Scroll View
        var scrollBgImage = scrollViewObj.AddComponent<UnityEngine.UI.Image>();
        scrollBgImage.color = new Color(1f, 1f, 1f, 0.05f);

        // Viewport của ScrollView
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportObj.AddComponent<UnityEngine.UI.RectMask2D>(); // Mask để cuộn

        // Content chứa các nút bấm của ScrollView
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 400f);

        var layoutGroup = contentObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layoutGroup.spacing = 15;
        layoutGroup.padding = new RectOffset(15, 15, 15, 15);
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;

        var sizeFitter = contentObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        sizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        // Liên kết Viewport và Content vào ScrollRect
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        // DANH SÁCH CÁC NÚT MINI GAME
        // 1. Nút "Trending AR Game"
        GameObject arBtnObj = CreateMenuButton("ARGameButton", contentRect, "Trending AR Game (AR Foundation)", new Color(0.15f, 0.6f, 0.3f, 1f), tmpFont);
        var arButton = arBtnObj.GetComponent<UnityEngine.UI.Button>();
        UnityAction arDelegate = System.Delegate.CreateDelegate(typeof(UnityAction), menuController, "PlayARGame") as UnityAction;
        UnityEventTools.AddPersistentListener(arButton.onClick, arDelegate);

        // 2. Nút "Mini Game 2: Flappy Bird"
        GameObject game2BtnObj = CreateMenuButton("FlappyBirdButton", contentRect, "Mini Game: Flappy Bird", new Color(0.2f, 0.45f, 0.75f, 1f), tmpFont);
        var game2Button = game2BtnObj.GetComponent<UnityEngine.UI.Button>();
        UnityAction<string> game2Delegate = System.Delegate.CreateDelegate(typeof(UnityAction<string>), menuController, "PlayOtherMiniGame") as UnityAction<string>;
        UnityEventTools.AddStringPersistentListener(game2Button.onClick, game2Delegate, "Flappy Bird");

        // 3. Nút "Mini Game 3: Tic Tac Toe"
        GameObject game3BtnObj = CreateMenuButton("TicTacToeButton", contentRect, "Mini Game: Tic Tac Toe", new Color(0.7f, 0.3f, 0.3f, 1f), tmpFont);
        var game3Button = game3BtnObj.GetComponent<UnityEngine.UI.Button>();
        UnityEventTools.AddStringPersistentListener(game3Button.onClick, game2Delegate, "Tic Tac Toe");

        // 4. Nút "Mini Game 4: Chess"
        GameObject game4BtnObj = CreateMenuButton("ChessButton", contentRect, "Mini Game: Chess", new Color(0.45f, 0.3f, 0.6f, 1f), tmpFont);
        var game4Button = game4BtnObj.GetComponent<UnityEngine.UI.Button>();
        UnityEventTools.AddStringPersistentListener(game4Button.onClick, game2Delegate, "Chess");

        // TẠO LOADING OVERLAY
        GameObject overlayObj = new GameObject("LoadingOverlay");
        overlayObj.transform.SetParent(canvasObj.transform, false);
        var overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        
        var overlayBg = overlayObj.AddComponent<UnityEngine.UI.Image>();
        overlayBg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f); // Đen mờ đẹp mắt

        // Status text trong overlay
        GameObject statusTextObj = new GameObject("StatusText");
        statusTextObj.transform.SetParent(overlayObj.transform, false);
        var statusTextRect = statusTextObj.AddComponent<RectTransform>();
        statusTextRect.sizeDelta = new Vector2(500f, 200f);
        var statusText = statusTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        statusText.text = "Đang kiểm tra...";
        statusText.font = tmpFont;
        statusText.fontSize = 26;
        statusText.alignment = TMPro.TextAlignmentOptions.Center;
        statusText.color = Color.white;

        // Liên kết Loading Overlay và Text vào MainMenuController
        menuController.loadingOverlay = overlayObj;
        menuController.loadingText = statusText;
        EditorUtility.SetDirty(menuController);

        // Lưu Menu Scene
        EditorSceneManager.SaveScene(menuScene, menuScenePath);
        Debug.Log("[SceneDuplicator] Đã tạo và lưu Menu Scene thành công.");

        // Bước 4: Đăng ký Scene vào Build Settings
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        
        // Menu Scene nằm ở vị trí index 0
        buildScenes.Add(new EditorBuildSettingsScene(menuScenePath, true));
        // Main Scene (AR) nằm ở index 1
        buildScenes.Add(new EditorBuildSettingsScene(mainScenePath, true));
        // Main Scene Non-AR nằm ở index 2
        buildScenes.Add(new EditorBuildSettingsScene(nonArScenePath, true));

        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log("[SceneDuplicator] Đã đăng ký 3 Scene vào Build Settings.");

        // Mở lại Menu Scene để sẵn sàng kiểm thử
        EditorSceneManager.OpenScene(menuScenePath, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Thành công", "Đã tạo Menu Scene với Scroll View, Scene giả lập Non-AR và đăng ký Build Settings thành công!\n\nHãy nhấn Play trong Editor hoặc Build lại game trên Android để kiểm thử.", "Tuyệt vời");
    }

    private static GameObject CreateMenuButton(string name, Transform parent, string text, Color color, TMPro.TMP_FontAsset font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        
        var rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(440f, 70f);

        var image = btnObj.AddComponent<UnityEngine.UI.Image>();
        image.color = color;

        var button = btnObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;

        // Tạo phần Text cho nút
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var uiText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        uiText.text = text;
        uiText.font = font;
        uiText.fontSize = 20;
        uiText.alignment = TMPro.TextAlignmentOptions.Center;
        uiText.color = Color.white;

        return btnObj;
    }

    private static void DeleteAROriginGameObjects(GameObject go)
    {
        if (go == null) return;

        string nameLower = go.name.ToLower();
        if (nameLower == "xr origin" || nameLower == "ar session origin" || nameLower == "arsessionorigin")
        {
            Debug.Log($"[SceneDuplicator] Đã tìm thấy và loại bỏ: {go.name}");
            Undo.DestroyObjectImmediate(go);
            return;
        }

        // Đệ quy ngược từ dưới lên để tránh lỗi index khi xóa đối tượng con
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            if (i < go.transform.childCount)
            {
                DeleteAROriginGameObjects(go.transform.GetChild(i).gameObject);
            }
        }
    }
}
