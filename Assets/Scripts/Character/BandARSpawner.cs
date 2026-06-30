using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

/// <summary>
/// Quản lý việc tạo (spawn) và sắp xếp đội hình cho các thành viên của ban nhạc được chọn hoặc các nhân vật đã tạo trong Scene AR.
/// </summary>
public class BandARSpawner : MonoBehaviour
{
    public enum SpawnerMode
    {
        Custom,
        Band
    }

    [Header("Spawner Mode")]
    [SerializeField] private SpawnerMode m_SpawnerMode = SpawnerMode.Custom;


    [Header("Spawn Settings")]

    [Tooltip("Khoảng cách hàng ngang giữa các thành viên.")]
    [SerializeField] private float memberSpacing = 0.8f;

    [Tooltip("Vị trí lệch (offset) so với camera khi tự động spawn lúc bắt đầu.")]
    [SerializeField] private Vector3 autoSpawnOffset = new Vector3(0, 0, 3f);

    [Header("Camera Follow Settings")]
    [Tooltip("Tự động di chuyển bám theo camera khi bắt đầu.")]
    [SerializeField] private bool followCameraOnStart = true;

    [Header("Animation & Audio")]
    [Tooltip("Tự động kích hoạt audio nhạc cụ của các thành viên sau khi spawn.")]
    [SerializeField] private bool playAudioOnSpawn = true;

    [Tooltip("Tự động chơi hoạt ảnh nhảy được chọn của từng thành viên.")]
    [SerializeField] private bool playDanceOnSpawn = true;

    [Header("Pedestal Settings")]
    [Tooltip("Danh sách 7 bệ đứng cho nhân vật trong Scene.")]
    [SerializeField] private List<GameObject> pedestals = new List<GameObject>();

    public List<GameObject> Pedestals => pedestals;

    [Header("Scroll Interaction Settings")]
    [Tooltip("Component PedestalsScroller được gán bằng tay trong Scene. Nếu để trống, hệ thống sẽ tự động tìm kiếm hoặc tự động tạo tại runtime.")]
    [SerializeField] private PedestalsScroller pedestalsScroller;

    [Tooltip("Legacy pedestal toggle button. Manual pedestal hiding is disabled; pedestals hide automatically after 4 Casts are placed.")]
    [SerializeField, HideInInspector] private Button togglePedestalsButton;

    [Header("Pedestal Offset Settings")]
    [Tooltip("Vị trí cục bộ của nhân vật so với bệ đứng.")]
    [SerializeField] private Vector3 pedestalLocalPosition = new Vector3(0f, 0f, 0.002f);
    
    [Tooltip("Góc xoay cục bộ của nhân vật so với bệ đứng.")]
    [SerializeField] private Vector3 pedestalLocalRotation = new Vector3(0f, 180f, 0f);
    
    [Tooltip("Tỉ lệ scale cục bộ của nhân vật.")]
    [SerializeField] private Vector3 pedestalLocalScale = new Vector3(0.02f, 0.02f, 0.02f);

    [Header("Cast AR Settings")]
    [Tooltip("Tỉ lệ scale của nhân vật khi được kéo ra đặt vào AR.")]
    [SerializeField] private Vector3 placedScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("Độ cao rơi tối thiểu (m) khi thả nhân vật ra AR.")]
    [SerializeField] private float minDropHeight = 0.8f;

    [Tooltip("Thời gian rơi tiếp đất (s) khi thả nhân vật ra AR.")]
    [SerializeField] private float dropDuration = 0.45f;

    public Vector3 PedestalLocalPosition => pedestalLocalPosition;
    public Vector3 PedestalLocalRotation => pedestalLocalRotation;
    public Vector3 PedestalLocalScale => pedestalLocalScale;
    public Vector3 PlacedScale => placedScale;
    public float MinDropHeight => minDropHeight;
    public float DropDuration => dropDuration;

    private List<GameObject> instrumentPrefabs
    {
        get
        {
            if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.InstrumentPrefabs != null && MainMenuDataManager.Instance.InstrumentPrefabs.Count > 0)
            {
                return MainMenuDataManager.Instance.InstrumentPrefabs;
            }
            return null;
        }
    }

    [Header("Instrument Offset Settings")]
    [Tooltip("Vị trí cục bộ của nhạc cụ so với nhân vật.")]
    [SerializeField] private Vector3 instrumentLocalPosition = new Vector3(-0.775f, 1.5f, 0f);

    [Tooltip("Góc xoay cục bộ của nhạc cụ.")]
    [SerializeField] private Vector3 instrumentLocalRotation = Vector3.zero;

    [Tooltip("Tỉ lệ scale cục bộ của nhạc cụ.")]
    [SerializeField] private Vector3 instrumentLocalScale = Vector3.one;

    // Trạng thái điều khiển camera follow
    private bool m_FollowCamera = true;
    private Transform m_FollowContainer;
    private List<CastData> _cachedCastsToSpawn = new List<CastData>();
    private readonly List<GameObject> _unplacedMemberBuffer = new List<GameObject>();
    private Dictionary<GameObject, Vector3> _originalPedestalScales = new Dictionary<GameObject, Vector3>();
    private bool _pedestalsVisible = true;
    private bool m_ManualPedestalToggleEnabled = false;

    // Danh sách các thành viên đã được spawn ra thực tế
    private List<GameObject> _spawnedMembers = new List<GameObject>();
    private bool _didVibrateForFourPlacedCasts = false;
    private Coroutine _placementLimitCoroutine;
    private bool _placementLimitCleanupQueued = false;
    private bool _awardedSelectedBandPoints = false;
    private Camera m_MainCamera;
    private const int CUSTOM_CAST_USE_POINT_REWARD = 10;
    private TextMeshProUGUI _customCastPointNotificationText;
    private CanvasGroup _customCastPointNotificationGroup;
    private RectTransform _customCastPointNotificationRect;
    private Sequence _customCastPointNotificationSequence;

    [Header("Placement Performance")]
    [SerializeField, Tooltip("Delay nho de tranh cleanup/haptic chay dung frame dau cua thao tac drop.")]
    private float placementLimitCleanupDelay = 0.08f;

    public SpawnerMode spawnerMode
    {
        get => m_SpawnerMode;
        set => m_SpawnerMode = value;
    }

    public bool isFollowingCamera => m_FollowCamera;

    private void Awake()
    {
        // Đảm bảo bật follow camera mặc định
        m_FollowCamera = followCameraOnStart;
        m_MainCamera = Camera.main;
        AndroidUtils.WarmUpVibration();
    }

    private async void Start()
    {
        // Tự động cấu hình chế độ hoạt động SpawnerMode dựa trên tên Scene hiện hành
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeSceneName == "Band AR" || activeSceneName == "Band AR Scene" ||
            activeSceneName == "Band Non AR" || activeSceneName == "Band NonAR Scene" || activeSceneName == "Band Non-AR Scene" ||
            activeSceneName == "Band Mode Scene" ||
            activeSceneName == "Band Mode AR Scene" || activeSceneName == "Band Mode NonAR Scene")
        {
            m_SpawnerMode = SpawnerMode.Band;
            Debug.Log($"[BandARSpawner] Scene '{activeSceneName}' -> Đặt SpawnerMode = Band.");
        }
        else if (activeSceneName == "costom AR" || activeSceneName == "Custom AR" || 
                 activeSceneName == "costom Non AR" || activeSceneName == "Custom Non AR" ||
                 activeSceneName == "AR Scene" || activeSceneName == "Non-AR Scene" ||
                 activeSceneName == "Custome AR Scene" || activeSceneName == "Custome NonAR Scene")
        {
            m_SpawnerMode = SpawnerMode.Custom;
            Debug.Log($"[BandARSpawner] Scene '{activeSceneName}' -> Đặt SpawnerMode = Custom.");
        }

        // Kiểm tra xem có CustomCharacterPanelController đang hoạt động hay không
        CustomCharacterPanelController creatorPanel = FindFirstObjectByType<CustomCharacterPanelController>();
        if (creatorPanel != null && creatorPanel.gameObject.activeInHierarchy)
        {
            // Bỏ qua tự động spawn khi bắt đầu vì người dùng đang ở giao diện thiết kế
            Debug.Log("[BandARSpawner] Phát hiện CustomCharacterPanelController đang hoạt động. Bỏ qua tự động spawn trên Start.");
            return;
        }

        await LoadCastsToSpawnAsync();

        // Tự động spawn ngay khi bắt đầu nếu ở chế độ Band
        if (m_SpawnerMode == SpawnerMode.Band)
        {
            EditorBandData selectedEditorBand = MainMenuDataManager.Instance != null ? MainMenuDataManager.Instance.selectedBandData : null;
            bool hasEditorBand = selectedEditorBand != null && selectedEditorBand.members != null && selectedEditorBand.members.Count > 0;
            bool hasRuntimeBand = BandSelectionManager.SelectedBand != null && BandSelectionManager.SelectedBand.casts != null && BandSelectionManager.SelectedBand.casts.Count > 0;
            bool shouldAutoEnterAR = ARFallbackManager.IsARModeRequestedForCurrentScene() && (hasEditorBand || hasRuntimeBand);

            if (shouldAutoEnterAR)
            {
                SpawnBandAtDefaultPosition();
            }
            else
            {
                // Chưa chọn Band -> Hiện popup chọn ngay trong Scene chơi nhạc
                SetPedestalsAndUnplacedCastsActive(false);
                ShowBandSelectionUI();
            }
        }
    }

    private Camera GetMainCamera()
    {
        if (m_MainCamera == null)
        {
            m_MainCamera = Camera.main;
        }
        return m_MainCamera;
    }

    /// <summary>
    /// Tính toán vị trí trước camera và spawn ban nhạc.
    /// </summary>
    public void SpawnBandAtDefaultPosition()
    {
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        Camera mainCamera = GetMainCamera();
        if (mainCamera != null)
        {
            Transform camTrans = mainCamera.transform;
            Vector3 camForwardHorizontal = camTrans.forward;
            camForwardHorizontal.y = 0f;
            camForwardHorizontal.Normalize();
            
            spawnPos = camTrans.position + camForwardHorizontal * autoSpawnOffset.z + camTrans.right * autoSpawnOffset.x;
            spawnPos.y = camTrans.position.y + autoSpawnOffset.y;
            spawnRot = Quaternion.LookRotation(-camForwardHorizontal, Vector3.up);
        }
        else
        {
            spawnPos = transform.position + autoSpawnOffset;
            spawnRot = transform.rotation;
        }

        SpawnBand(spawnPos, spawnRot);
    }

    /// <summary>
    /// Hiển thị giao diện chọn ban nhạc ngay trong Scene AR/Non-AR.
    /// </summary>
    public void ShowBandSelectionUI()
    {
        // 1. Ưu tiên tìm kiếm các UI Panel tĩnh đã được thiết kế sẵn trong Scene
        GameObject staticUI = FindStaticBandSelectionUI();

        if (staticUI != null)
        {
            staticUI.SetActive(true);
            Debug.Log($"[BandARSpawner] Đã hiển thị UIBand tĩnh được thiết kế sẵn: {staticUI.name}");
            return;
        }

        // 2. Fallback: tự sinh UI nếu không tìm thấy thiết kế sẵn trong Scene
        BandSelectionPanelUI panel = FindFirstObjectByType<BandSelectionPanelUI>();
        if (panel == null)
        {
            GameObject obj = new GameObject("BandSelectionPanel_Manager");
            panel = obj.AddComponent<BandSelectionPanelUI>();
        }

        panel.Show((arScene, nonArScene) =>
        {
            // Spawn trực tiếp trong scene hiện tại mà không chuyển cảnh
            SpawnBandAtDefaultPosition();
        });
    }

    private GameObject FindStaticBandSelectionUI()
    {
        string[] knownNames = { "UIBand", "UI_Band", "Band Panel", "BandPanel" };

        foreach (string name in knownNames)
        {
            GameObject activeObj = GameObject.Find(name);
            if (activeObj != null)
            {
                return activeObj;
            }
        }

        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            GameObject found = FindChildByName(root.transform, knownNames);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private GameObject FindChildByName(Transform parent, string[] names)
    {
        if (parent == null) return null;

        for (int i = 0; i < names.Length; i++)
        {
            if (parent.name == names[i])
            {
                return parent.gameObject;
            }
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject found = FindChildByName(parent.GetChild(i), names);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void Update()
    {
        // Chỉ cập nhật vị trí container follow camera nếu không sử dụng bệ đứng
        bool usePedestals = pedestals != null && pedestals.Count > 0;
        Camera mainCamera = GetMainCamera();
        if (!usePedestals && m_FollowCamera && mainCamera != null)
        {
            Transform camTrans = mainCamera.transform;

            // Hướng nhìn ngang của camera (chiếu lên trục Y = 0)
            Vector3 camForwardHorizontal = camTrans.forward;
            camForwardHorizontal.y = 0f;
            if (camForwardHorizontal.sqrMagnitude < 0.001f)
            {
                camForwardHorizontal = camTrans.up;
                camForwardHorizontal.y = 0f;
            }
            camForwardHorizontal.Normalize();

            // Vị trí container trước mặt camera
            Vector3 targetPosition = camTrans.position + camForwardHorizontal * autoSpawnOffset.z;
            
            // Giữ độ cao Y cố định tương đối so với camera
            targetPosition.y = camTrans.position.y + autoSpawnOffset.y;

            // Xoay container mặt hướng về phía Camera (tức là hướng ngược lại với camForwardHorizontal)
            Quaternion targetRotation = Quaternion.LookRotation(-camForwardHorizontal, Vector3.up);

            if (m_FollowContainer == null)
            {
                m_FollowContainer = new GameObject("AR_FollowContainer").transform;
            }

            m_FollowContainer.position = targetPosition;
            m_FollowContainer.rotation = targetRotation;
        }
    }

    /// <summary>
    /// Tải danh sách các cast cần spawn dựa theo chế độ hoạt động SpawnerMode.
    /// </summary>
    private async System.Threading.Tasks.Task LoadCastsToSpawnAsync()
    {
        _cachedCastsToSpawn.Clear();

        if (m_SpawnerMode == SpawnerMode.Custom)
        {
            // Trong che do Custom, lay tu danh sach nhan vat da tao theo gioi han slot hien tai.
            if (MainMenuDataManager.Instance != null)
            {
                var createdCharacters = await MainMenuDataManager.Instance.GetCreatedCharactersAsync();
                if (createdCharacters != null && createdCharacters.Count > 0)
                {
                    int count = Mathf.Min(createdCharacters.Count, CastSlotUnlockManager.MaxSlotCount);
                    for (int i = 0; i < count; i++)
                    {
                        var charData = createdCharacters[i];
                        _cachedCastsToSpawn.Add(new CastData(
                            charData.name,
                            charData.prefabName,
                            charData.instrumentId,
                            charData.danceAnimId,
                            charData.danceAnimIds
                        )
                        {
                            characterId = charData.characterId
                        });
                    }
                    Debug.Log($"[BandARSpawner] Chế độ Custom: Đã tải {count} nhân vật từ MainMenuDataManager.");
                    return;
                }
            }

            // Fallback sang castData đơn lẻ nếu không có createdCharacters
            if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.castData != null)
            {
                _cachedCastsToSpawn.Add(MainMenuDataManager.Instance.castData);
                Debug.Log("[BandARSpawner] Chế độ Custom Fallback: Đã tải 1 castData.");
            }
        }
        else
        {
            // Chế độ Band: Chỉ lấy tối đa 4 nhân vật được chọn sẵn (BandSelectionManager)
            if (BandSelectionManager.SelectedBand != null)
            {
                _cachedCastsToSpawn.AddRange(BandSelectionManager.SelectedBand.casts);
                Debug.Log($"[BandARSpawner] Chế độ Band: Đã tải {BandSelectionManager.SelectedBand.casts.Count} nhân vật.");
            }
            else if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.castData != null)
            {
                _cachedCastsToSpawn.Add(MainMenuDataManager.Instance.castData);
                Debug.Log("[BandARSpawner] Chế độ Band Fallback: Đã tải 1 castData.");
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem có nhân vật nào sẵn sàng để spawn hay không.
    /// </summary>
    public bool HasCastsToSpawn()
    {
        if (m_SpawnerMode == SpawnerMode.Custom)
        {
            if (PlayerPrefs.HasKey("SavedCastsDataJSON") && !string.IsNullOrEmpty(PlayerPrefs.GetString("SavedCastsDataJSON")))
            {
                return true;
            }
            return MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.castData != null;
        }
        else
        {
            return BandSelectionManager.SelectedBand != null || 
                   (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.castData != null);
        }
    }

    /// <summary>
    /// Tách một thành viên ra khỏi container follow camera hoặc bệ đứng để đặt cố định vào thế giới.
    /// </summary>
    public void DetachMember(GameObject member)
    {
        if (member == null) return;
        
        // Trường hợp 1: Nếu nhân vật là con của một trong các bệ đứng
        if (pedestals != null && pedestals.Count > 0)
        {
            foreach (var pedestal in pedestals)
            {
                if (pedestal != null && member.transform.parent == pedestal.transform)
                {
                    member.transform.SetParent(null);
                    Debug.Log($"[BandARSpawner] Detached member: {member.name} từ bệ {pedestal.name}. Bệ sẽ chỉ tự ẩn khi đã đặt đủ 4 Cast.");
                    return;
                }
            }
        }

        // Trường hợp 2: Nếu sử dụng container ảo cũ (fallback)
        if (member.transform.parent == m_FollowContainer)
        {
            member.transform.SetParent(null);
            Debug.Log($"[BandARSpawner] Detached member: {member.name} từ container follow camera.");
        }
    }

    /// <summary>
    /// Kiểm tra số lượng Cast đã được đặt. Nếu đạt từ 4 Cast trở lên, tắt tất cả các bệ đứng và Cast còn lại.
    /// </summary>
    public void CheckAndLimitCasts()
    {
        int placedCount = CountPlacedMembers();

#if UNITY_EDITOR
        Debug.Log($"[BandARSpawner] Số lượng Cast đã đặt: {placedCount}");
#endif

        if (placedCount >= 4 && !_placementLimitCleanupQueued)
        {
            _placementLimitCleanupQueued = true;
            AwardSelectedBandPoints();
            if (_placementLimitCoroutine != null)
            {
                StopCoroutine(_placementLimitCoroutine);
            }
            _placementLimitCoroutine = StartCoroutine(HandlePlacementLimitReached());
        }
        else if (placedCount < 4)
        {
            _placementLimitCleanupQueued = false;
            RearrangeUnplacedCasts();
        }

        UpdateARUI();
    }

    /// <summary>
    /// Trả một thành viên từ thế giới AR quay trở lại bệ đứng/container.
    /// </summary>
    public void ReturnMemberToPedestal(GameObject member)
    {
        if (member == null) return;

        // 1. Dừng phát âm thanh nhạc cụ
        CastAudioData castAudio = member.GetComponent<CastAudioData>();
        if (castAudio != null)
        {
            castAudio.StopAudio();
        }

        // 2. Đặt lại trạng thái chưa đặt vào thế giới AR
        CastPlacementState placementState = member.GetComponent<CastPlacementState>();
        if (placementState != null)
        {
            placementState.MarkUnplaced();
        }

        // 3. Dừng di chuyển và đưa nhân vật về trạng thái Idle
        Move moveScript = member.GetComponent<Move>();
        if (moveScript != null)
        {
            moveScript.Stop();
        }

        // 4. Cho phép kích hoạt rung haptic trở lại khi người dùng đạt đủ 4 Cast
        _didVibrateForFourPlacedCasts = false;
        _placementLimitCleanupQueued = false;
        if (_placementLimitCoroutine != null)
        {
            StopCoroutine(_placementLimitCoroutine);
            _placementLimitCoroutine = null;
        }

        // 5. Bật hiển thị các bệ đứng trở lại nếu đang bị ẩn
        if (pedestals != null && pedestals.Count > 0)
        {
            GameObject pedestalParent = null;
            if (pedestals[0] != null && pedestals[0].transform.parent != null)
            {
                pedestalParent = pedestals[0].transform.parent.gameObject;
            }
            if (pedestalParent != null)
            {
                pedestalParent.SetActive(true);
            }
        }

        if (m_FollowContainer != null)
        {
            m_FollowContainer.gameObject.SetActive(true);
        }

        // 6. Xếp dồn nhân vật về bệ đứng trống đầu tiên
        RearrangeUnplacedCasts();

        Debug.Log($"[BandARSpawner] Trả nhân vật {member.name} quay về bệ đứng thành công.");
        UpdateARUI();
    }

    /// <summary>
    /// Dồn (snap) các Cast chưa đặt lên các bệ đầu tiên và ẩn các bệ trống ở sau, cập nhật giới hạn cuộn.
    /// </summary>
    public void RearrangeUnplacedCasts()
    {
        if (pedestals == null || pedestals.Count == 0) return;

        // Bật parent container và follow container nếu đang bị ẩn
        if (pedestals[0] != null && pedestals[0].transform.parent != null)
        {
            pedestals[0].transform.parent.gameObject.SetActive(true);
        }
        if (m_FollowContainer != null)
        {
            m_FollowContainer.gameObject.SetActive(true);
        }

        // 1. Thu thập tất cả các Cast chưa được đặt ra thế giới
        _unplacedMemberBuffer.Clear();
        foreach (var member in _spawnedMembers)
        {
            if (member == null) continue;
            CastPlacementState placementState = member.GetComponent<CastPlacementState>();
            bool isPlaced = placementState != null ? placementState.IsPlaced : member.transform.parent == null;
            if (!isPlaced)
            {
                _unplacedMemberBuffer.Add(member);
            }
        }

        // 2. Dồn các Cast chưa đặt lên các bệ đứng đầu tiên
        int targetIndex = 0;
        for (int i = 0; i < _unplacedMemberBuffer.Count; i++)
        {
            GameObject member = _unplacedMemberBuffer[i];
            if (member == null) continue;

            while (targetIndex < pedestals.Count && pedestals[targetIndex] == null)
            {
                targetIndex++;
            }

            if (targetIndex >= pedestals.Count)
            {
                member.SetActive(false);
                continue;
            }

            GameObject pedestal = pedestals[targetIndex];

            // Gán lại cha là bệ đứng mục tiêu
            member.transform.SetParent(pedestal.transform, true);

            // Chạy Tween di chuyển và xoay mượt mà về bệ đứng mới
            member.transform.DOKill();
            member.transform.DOLocalMove(pedestalLocalPosition, 0.35f).SetEase(Ease.OutQuad);
            member.transform.DOLocalRotate(pedestalLocalRotation, 0.35f).SetEase(Ease.OutQuad);
            member.transform.DOScale(pedestalLocalScale, 0.35f).SetEase(Ease.OutQuad);

            // Bật hiển thị bệ đứng bằng hiệu ứng phóng to mượt mà
            Vector3 originalScale = GetOriginalPedestalScale(pedestal);
            if (!pedestal.activeSelf)
            {
                pedestal.transform.DOKill();
                pedestal.SetActive(true);
                pedestal.transform.localScale = Vector3.zero;
                pedestal.transform.DOScale(originalScale, 0.35f).SetEase(Ease.OutBack);
            }
            else
            {
                pedestal.transform.DOKill();
                pedestal.transform.DOScale(originalScale, 0.35f).SetEase(Ease.OutQuad);
            }

            targetIndex++;
        }

        // 3. Ẩn tất cả các bệ đứng trống còn lại ở phía sau bằng hiệu ứng thu nhỏ
        for (int j = targetIndex; j < pedestals.Count; j++)
        {
            if (pedestals[j] != null && pedestals[j].activeSelf)
            {
                GameObject targetPedestal = pedestals[j];
                targetPedestal.transform.DOKill();
                targetPedestal.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    targetPedestal.SetActive(false);
                });
            }
        }

        // 4. Cập nhật lại giới hạn cuộn dựa trên số lượng bệ hoạt động
        PedestalsScroller scroller = FindFirstObjectByType<PedestalsScroller>();
        if (scroller == null)
        {
            scroller = pedestalsScroller;
        }
        if (scroller == null)
        {
            GameObject scrollerObj = GameObject.Find("Pedestals_ScrollContainer");
            if (scrollerObj != null)
            {
                scroller = scrollerObj.GetComponent<PedestalsScroller>();
            }
        }

        if (scroller != null)
        {
            // Cập nhật lại giới hạn cuộn cố định dựa trên số lượng bệ hoạt động
            int activeCount = _unplacedMemberBuffer.Count;
            float newMinX = scroller.FixedMaxX; // Mặc định khi chỉ có 1 hoặc 0 bệ thì không cho cuộn trái (min = max)
            if (activeCount > 1)
            {
                float spacing = 1.0f;
                if (pedestals.Count > 1 && pedestals[0] != null && pedestals[1] != null)
                {
                    spacing = Mathf.Abs(pedestals[1].transform.localPosition.x - pedestals[0].transform.localPosition.x);
                }
                newMinX = scroller.FixedMaxX - (activeCount - 1) * spacing;
            }
            scroller.FixedMinX = newMinX;
            scroller.CalculateScrollLimits();
        }
    }

    private int CountPlacedMembers()
    {
        int placedCount = 0;
        foreach (var member in _spawnedMembers)
        {
            if (member == null) continue;

            CastPlacementState placementState = member.GetComponent<CastPlacementState>();
            bool isPlaced = placementState != null ? placementState.IsPlaced : member.transform.parent == null;
            if (isPlaced)
            {
                placedCount++;
            }
        }

        return placedCount;
    }

    private static string GetEditorMemberDisplayName(GameObject prefab, int index)
    {
        if (prefab != null)
        {
            CastPrefab config = prefab.GetComponent<CastPrefab>();
            if (config != null && !string.IsNullOrWhiteSpace(config.Name))
            {
                return config.Name;
            }

            if (!string.IsNullOrWhiteSpace(prefab.name))
            {
                return prefab.name;
            }
        }

        return $"Cast {index + 1}";
    }

    private void AwardSelectedBandPoints()
    {
        if (m_SpawnerMode != SpawnerMode.Band || _awardedSelectedBandPoints || MainMenuDataManager.Instance == null)
        {
            return;
        }

        EditorBandData selectedBand = MainMenuDataManager.Instance.selectedBandData;
        if (selectedBand == null || selectedBand.point <= 0)
        {
            return;
        }

        int totalPoints = MainMenuDataManager.Instance.AddOfflineUserPoints(selectedBand.point);
        _awardedSelectedBandPoints = true;

#if UNITY_EDITOR
        Debug.Log($"[BandARSpawner] Awarded {selectedBand.point} points for completing selected band. Total points: {totalPoints}");
#endif
    }

    public void TryAwardCustomCastUsePoints(GameObject castObject)
    {
        if (m_SpawnerMode != SpawnerMode.Custom || castObject == null || MainMenuDataManager.Instance == null)
        {
            return;
        }

        CastRuntimeData runtimeData = castObject.GetComponent<CastRuntimeData>();
        CastData cast = runtimeData != null ? runtimeData.Data : null;
        if (cast == null)
        {
            return;
        }

        if (MainMenuDataManager.Instance.TryAwardCustomCastUsePoints(cast, CUSTOM_CAST_USE_POINT_REWARD, out int totalPoints))
        {
            ShowCustomCastPointNotification(CUSTOM_CAST_USE_POINT_REWARD, totalPoints);
#if UNITY_EDITOR
            Debug.Log($"[BandARSpawner] Awarded {CUSTOM_CAST_USE_POINT_REWARD} points for using custom cast '{cast.name}'. Total points: {totalPoints}");
#endif
        }
    }

    private void ShowCustomCastPointNotification(int awardedPoints, int totalPoints)
    {
        TextMeshProUGUI notificationText = GetOrCreateCustomCastPointNotification();
        if (notificationText == null || _customCastPointNotificationGroup == null || _customCastPointNotificationRect == null)
        {
            return;
        }

        notificationText.text = $"+{awardedPoints} Điểm";
        notificationText.gameObject.SetActive(true);
        _customCastPointNotificationGroup.gameObject.SetActive(true);
        _customCastPointNotificationSequence?.Kill();

        _customCastPointNotificationRect.anchoredPosition = new Vector2(0f, -132f);
        _customCastPointNotificationGroup.alpha = 0f;

        _customCastPointNotificationSequence = DOTween.Sequence();
        _customCastPointNotificationSequence
            .Append(_customCastPointNotificationGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad))
            .Join(_customCastPointNotificationRect.DOAnchorPosY(-112f, 0.2f).SetEase(Ease.OutBack))
            .AppendInterval(1.45f)
            .Append(_customCastPointNotificationGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad))
            .Join(_customCastPointNotificationRect.DOAnchorPosY(-92f, 0.3f).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                if (_customCastPointNotificationGroup != null)
                {
                    _customCastPointNotificationGroup.gameObject.SetActive(false);
                }
            });
    }

    private TextMeshProUGUI GetOrCreateCustomCastPointNotification()
    {
        if (_customCastPointNotificationText != null)
        {
            return _customCastPointNotificationText;
        }

        GameObject canvasObj = new GameObject("Custom Cast Points Notification Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject textObj = new GameObject("Custom Cast Points Notification TMP", typeof(RectTransform));
        textObj.transform.SetParent(canvasObj.transform, false);

        _customCastPointNotificationGroup = textObj.AddComponent<CanvasGroup>();
        _customCastPointNotificationGroup.alpha = 0f;
        _customCastPointNotificationGroup.interactable = false;
        _customCastPointNotificationGroup.blocksRaycasts = false;

        _customCastPointNotificationRect = textObj.GetComponent<RectTransform>();
        _customCastPointNotificationRect.anchorMin = new Vector2(0.5f, 1f);
        _customCastPointNotificationRect.anchorMax = new Vector2(0.5f, 1f);
        _customCastPointNotificationRect.pivot = new Vector2(0.5f, 0.5f);
        _customCastPointNotificationRect.sizeDelta = new Vector2(620f, 96f);
        _customCastPointNotificationRect.anchoredPosition = new Vector2(0f, -132f);

        _customCastPointNotificationText = textObj.AddComponent<TextMeshProUGUI>();
        _customCastPointNotificationText.alignment = TextAlignmentOptions.Center;
        _customCastPointNotificationText.enableAutoSizing = true;
        _customCastPointNotificationText.fontSizeMin = 28f;
        _customCastPointNotificationText.fontSizeMax = 52f;
        _customCastPointNotificationText.fontStyle = FontStyles.Bold;
        _customCastPointNotificationText.color = new Color(1f, 0.91f, 0.28f, 1f);
        _customCastPointNotificationText.outlineWidth = 0.2f;
        _customCastPointNotificationText.outlineColor = new Color(0f, 0f, 0f, 0.7f);
        _customCastPointNotificationText.raycastTarget = false;

        _customCastPointNotificationGroup.gameObject.SetActive(false);
        return _customCastPointNotificationText;
    }

    private IEnumerator HandlePlacementLimitReached()
    {
        yield return null;

        if (placementLimitCleanupDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(placementLimitCleanupDelay);
        }

        if (!_didVibrateForFourPlacedCasts)
        {
            AndroidUtils.Vibrate(3000);
            _didVibrateForFourPlacedCasts = true;
#if UNITY_EDITOR
            Debug.Log("[BandARSpawner] Four casts placed. Triggered 3s vibration feedback.");
#endif
        }

        HideUnplacedPedestalsAndCasts();
        _placementLimitCoroutine = null;
    }

    private void HideUnplacedPedestalsAndCasts()
    {
        // 1. Tat cac be dung con lai va cac nhan vat chua duoc drop ra world.
        if (pedestals != null && pedestals.Count > 0)
        {
            int activeCount = 0;
            int completedCount = 0;

            foreach (var pedestal in pedestals)
            {
                if (pedestal != null && pedestal.activeSelf)
                {
                    // Ẩn nhân vật chưa đặt đang đứng trên bệ với hiệu ứng thu nhỏ
                    for (int i = 0; i < pedestal.transform.childCount; i++)
                    {
                        Transform child = pedestal.transform.GetChild(i);
                        if (child != null && child.gameObject.activeSelf)
                        {
                            child.transform.DOKill();
                            child.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InQuad).OnComplete(() =>
                            {
                                child.gameObject.SetActive(false);
                            });
                        }
                    }

                    activeCount++;
                    pedestal.transform.DOKill();
                    pedestal.transform.DOScale(Vector3.zero, 0.35f).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        pedestal.SetActive(false);
                        completedCount++;
                        if (completedCount >= activeCount)
                        {
                            if (pedestals[0] != null && pedestals[0].transform.parent != null)
                            {
                                pedestals[0].transform.parent.gameObject.SetActive(false);
                            }
                        }
                    });
                }
            }

            if (activeCount == 0)
            {
                if (pedestals[0] != null && pedestals[0].transform.parent != null)
                {
                    pedestals[0].transform.parent.gameObject.SetActive(false);
                }
            }
        }

        // 2. Truong hop su dung container ao follow camera (fallback).
        if (m_FollowContainer != null)
        {
            m_FollowContainer.DOKill();
            m_FollowContainer.DOScale(Vector3.zero, 0.35f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                for (int i = 0; i < m_FollowContainer.childCount; i++)
                {
                    Transform child = m_FollowContainer.GetChild(i);
                    if (child != null)
                    {
                        child.gameObject.SetActive(false);
                    }
                }
                m_FollowContainer.gameObject.SetActive(false);
            });
        }

#if UNITY_EDITOR
        Debug.Log("[BandARSpawner] Da dat du 4 Cast. Tat tat ca cac be dung va Cast con lai.");
#endif
    }

    /// <summary>
    /// Spawn toàn bộ thành viên ban nhạc xếp ngang tại vị trí và góc xoay chỉ định.
    /// </summary>
    /// <param name="centerPosition">Vị trí trung tâm của hàng ban nhạc.</param>
    /// <param name="rotation">Góc xoay của ban nhạc (hướng mặt).</param>
    public async void SpawnBand(Vector3 centerPosition, Quaternion rotation)
    {
        // Kiểm tra xem có cấu hình EditorBand được chọn hay không
        EditorBandData editorBand = MainMenuDataManager.Instance != null ? MainMenuDataManager.Instance.selectedBandData : null;
        bool useEditorConfig = editorBand != null && editorBand.members != null && editorBand.members.Count > 0;

        List<CastData> casts = new List<CastData>();
        if (!useEditorConfig)
        {
            // Luôn tải lại danh sách Casts mới nhất trước khi thực hiện spawn
            await LoadCastsToSpawnAsync();

            if (_cachedCastsToSpawn != null && _cachedCastsToSpawn.Count > 0)
            {
                casts.AddRange(_cachedCastsToSpawn);
            }
            else
            {
                BandData band = BandSelectionManager.SelectedBand;
                CastData customCast = (MainMenuDataManager.Instance != null) ? MainMenuDataManager.Instance.castData : null;

                if (band == null && customCast != null)
                {
                    band = new BandData(new List<CastData> { customCast });
                }

                if (band != null)
                {
                    casts.AddRange(band.casts);
                }
            }

            if (casts.Count == 0)
            {
                Debug.LogWarning("[BandARSpawner] Không có dữ liệu Cast nào để spawn!");
                return;
            }
        }

        // 1. Dọn dẹp các thành viên cũ nếu có
        ClearSpawnedMembers();

        int count = useEditorConfig ? editorBand.members.Count : casts.Count;
        if (count == 0) return;

        Debug.Log($"[BandARSpawner] Đang spawn với {count} thành viên (Cấu hình Editor: {useEditorConfig})...");

        // Khởi tạo BandAudioManager để quản lý âm thanh tập trung cho Scene AR/Non-AR
        BandAudioManager.GetOrCreateInstance(useEditorConfig ? editorBand.fullSongAudio : null);

        if (pedestals != null && pedestals.Count > 0)
        {
            // Lưu scale ban đầu của toàn bộ bệ đứng
            foreach (var pedestal in pedestals)
            {
                if (pedestal != null && !_originalPedestalScales.ContainsKey(pedestal))
                {
                    _originalPedestalScales[pedestal] = pedestal.transform.localScale;
                }
            }

            PedestalsScroller scroller = pedestalsScroller;

            if (scroller == null)
            {
                Transform commonParent = null;

                // Kiểm tra xem tất cả các bệ đứng có chung một cha hợp lệ không
                bool shareSameParent = true;
                Transform firstParent = pedestals[0] != null ? pedestals[0].transform.parent : null;

                for (int i = 1; i < pedestals.Count; i++)
                {
                    if (pedestals[i] != null && pedestals[i].transform.parent != firstParent)
                    {
                        shareSameParent = false;
                        break;
                    }
                }

                if (shareSameParent && firstParent != null && 
                    !firstParent.name.Contains("AR Session") && 
                    !firstParent.name.Contains("XR Origin") && 
                    !firstParent.name.Contains("Camera"))
                {
                    commonParent = firstParent;
                }
                else
                {
                    GameObject container = GameObject.Find("Pedestals_ScrollContainer");
                    if (container == null)
                    {
                        container = new GameObject("Pedestals_ScrollContainer");
                        if (pedestals[0] != null)
                        {
                            container.transform.position = pedestals[0].transform.position;
                            container.transform.rotation = pedestals[0].transform.rotation;
                            if (pedestals[0].transform.parent != null)
                            {
                                container.transform.SetParent(pedestals[0].transform.parent);
                            }
                        }
                    }
                    commonParent = container.transform;

                    foreach (var pedestal in pedestals)
                    {
                        if (pedestal != null && pedestal.transform.parent != commonParent)
                        {
                            pedestal.transform.SetParent(commonParent, true);
                        }
                    }
                }

                if (commonParent != null)
                {
                    commonParent.gameObject.SetActive(true);
                    scroller = commonParent.GetComponent<PedestalsScroller>();
                    if (scroller == null)
                    {
                        scroller = commonParent.gameObject.AddComponent<PedestalsScroller>();
                    }
                }
            }
            else
            {
                scroller.gameObject.SetActive(true);
            }

            for (int i = 0; i < pedestals.Count; i++)
            {
                if (pedestals[i] == null) continue;

                if (i < count)
                {
                    pedestals[i].SetActive(true);

                    GameObject prefab = null;
                    string memberName = "";
                    CastData cast = null;
                    EditorBandMember editorMember = null;

                    if (useEditorConfig)
                    {
                        editorMember = editorBand.members[i];
                        if (MainMenuDataManager.Instance != null && 
                            editorMember.castPrefabIndex >= 0 && 
                            editorMember.castPrefabIndex < MainMenuDataManager.Instance.CharacterEntries.Count)
                        {
                            var entry = MainMenuDataManager.Instance.CharacterEntries[editorMember.castPrefabIndex];
                            if (entry != null)
                            {
                                prefab = await MainMenuDataManager.Instance.LoadCharacterPrefabAsync(entry.Id);
                            }
                        }
                        memberName = GetEditorMemberDisplayName(prefab, i);
                    }
                    else
                    {
                        cast = casts[i];
                        if (MainMenuDataManager.Instance != null)
                        {
                            prefab = await MainMenuDataManager.Instance.LoadCharacterPrefabAsync(cast.prefabName);
                        }
                        else
                        {
                            prefab = GetCharacterPrefab(cast.prefabName);
                        }
                        memberName = cast.name;
                    }

                    if (prefab == null)
                    {
                        Debug.LogError($"[BandARSpawner] Không tìm thấy prefab nào cho thành viên thứ {i + 1}.");
                        continue;
                    }

                    GameObject memberObj = Instantiate(prefab, pedestals[i].transform);
                    memberObj.transform.localPosition = pedestalLocalPosition;
                    memberObj.transform.localRotation = Quaternion.Euler(pedestalLocalRotation);
                    memberObj.name = $"BandMember_{i + 1}_{memberName}";

                    await SetupMemberComponents(memberObj, cast, false, editorMember);
                    _spawnedMembers.Add(memberObj);
                }
                else
                {
                    pedestals[i].SetActive(false);
                }
            }

            if (scroller != null)
            {
                scroller.CalculateScrollLimits();
            }

        }
        else
        {
            Vector3 rightDir = rotation * Vector3.right;

            if (m_FollowCamera)
            {
                if (m_FollowContainer == null)
                {
                    m_FollowContainer = new GameObject("AR_FollowContainer").transform;
                }
                m_FollowContainer.position = centerPosition;
                m_FollowContainer.rotation = rotation;
            }

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = null;
                string memberName = "";
                CastData cast = null;
                EditorBandMember editorMember = null;

                if (useEditorConfig)
                {
                    editorMember = editorBand.members[i];
                    if (MainMenuDataManager.Instance != null && 
                        editorMember.castPrefabIndex >= 0 && 
                        editorMember.castPrefabIndex < MainMenuDataManager.Instance.CharacterEntries.Count)
                    {
                        var entry = MainMenuDataManager.Instance.CharacterEntries[editorMember.castPrefabIndex];
                        if (entry != null)
                        {
                            prefab = await MainMenuDataManager.Instance.LoadCharacterPrefabAsync(entry.Id);
                        }
                    }
                    memberName = GetEditorMemberDisplayName(prefab, i);
                }
                else
                {
                    cast = casts[i];
                    if (MainMenuDataManager.Instance != null)
                    {
                        prefab = await MainMenuDataManager.Instance.LoadCharacterPrefabAsync(cast.prefabName);
                    }
                    else
                    {
                        prefab = GetCharacterPrefab(cast.prefabName);
                    }
                    memberName = cast.name;
                }

                if (prefab == null) continue;

                GameObject memberObj;
                if (m_FollowCamera)
                {
                    float offsetFactor = i - (count - 1) / 2.0f;
                    Vector3 localPos = new Vector3(offsetFactor * memberSpacing, 0f, 0f);

                    memberObj = Instantiate(prefab, m_FollowContainer);
                    memberObj.transform.localPosition = localPos;
                    memberObj.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    float offsetFactor = i - (count - 1) / 2.0f;
                    Vector3 memberPos = centerPosition + rightDir * (offsetFactor * memberSpacing);

                    memberObj = Instantiate(prefab, memberPos, rotation);
                }

                memberObj.name = $"BandMember_{i + 1}_{memberName}";
                await SetupMemberComponents(memberObj, cast, false, editorMember);
                _spawnedMembers.Add(memberObj);
            }

            // Đồng bộ hóa hoạt ảnh nhảy cho tất cả các thành viên sau khi spawn xong (không dùng bệ)
            foreach (var member in _spawnedMembers)
            {
                if (member != null)
                {
                    Move move = member.GetComponent<Move>();
                    if (move != null)
                    {
                        move.ResetLocalDanceState();
                    }
                }
            }
        }

        // Tự động tìm kiếm và kích hoạt giao diện AR UI của Band Mode sau khi spawn xong
        BandPanelController bandPanel = null;
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            bandPanel = root.GetComponentInChildren<BandPanelController>(true);
            if (bandPanel != null)
            {
                break;
            }
        }

        if (bandPanel != null)
        {
            Debug.Log($"[BandARSpawner] Phát hiện BandPanelController: {bandPanel.name}. Bắt đầu chuyển cảnh sang AR.");
            bandPanel.TransitionToAR();
        }
        else
        {
            Debug.LogWarning("[BandARSpawner] Không tìm thấy BandPanelController trong Scene để kích hoạt AR UI!");
        }
    }

    /// <summary>
    /// Cấu hình các component cho thành viên ban nhạc sau khi spawn.
    /// </summary>
    private async System.Threading.Tasks.Task SetupMemberComponents(GameObject memberObj, CastData cast, bool selectFirst, EditorBandMember editorMember = null)
    {
        // Đảm bảo nhân vật có Collider để có thể chạm/click chọn
        Collider col = memberObj.GetComponent<Collider>();
        if (col == null)
        {
            var capsule = memberObj.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
        }

        // Đảm bảo nhân vật có component Move để di chuyển và điều khiển hoạt ảnh
        Move moveScript = memberObj.GetComponent<Move>();
        if (moveScript == null)
        {
            moveScript = memberObj.AddComponent<Move>();
            moveScript.IdleStateName = "Idle";
            moveScript.WalkStateName = "Run";
            moveScript.Pose1StateName = "Dance1";
        }

        CastPlacementState placementState = memberObj.GetComponent<CastPlacementState>();
        if (placementState == null)
        {
            placementState = memberObj.AddComponent<CastPlacementState>();
        }
        placementState.MarkUnplaced();

        if (cast != null)
        {
            CastRuntimeData runtimeData = memberObj.GetComponent<CastRuntimeData>();
            if (runtimeData == null)
            {
                runtimeData = memberObj.AddComponent<CastRuntimeData>();
            }
            runtimeData.Initialize(cast);
        }

        // Tắt applyRootMotion trên Animator để cho phép tự do thay đổi scale và di chuyển
        Animator anim = memberObj.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        // Hiệu ứng xuất hiện phóng to từ 0 bằng DOTween
        Vector3 originalScale = pedestalLocalScale;
        memberObj.transform.localScale = Vector3.zero;
        memberObj.transform.DOKill();
        memberObj.transform.DOScale(originalScale, 0.45f).SetEase(Ease.OutBack);

        // Nếu nhân vật là con của một bệ đứng, tween cả vị trí từ Vector3.zero đến pedestalLocalPosition
        bool isOnPedestal = false;
        if (pedestals != null && pedestals.Count > 0 && memberObj.transform.parent != null)
        {
            if (pedestals.Contains(memberObj.transform.parent.gameObject))
            {
                isOnPedestal = true;
            }
        }

        if (isOnPedestal)
        {
            memberObj.transform.localPosition = Vector3.zero;
            memberObj.transform.DOLocalMove(pedestalLocalPosition, 0.45f).SetEase(Ease.OutBack);
        }

        // ── Tự động cấu hình Dance Animation ──
        string danceAnim = "";
        if (editorMember != null)
        {
            if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.selectedBandData != null)
            {
                danceAnim = MainMenuDataManager.Instance.selectedBandData.danceAnimId;
            }
            if (string.IsNullOrEmpty(danceAnim))
            {
                danceAnim = editorMember.danceAnimId;
            }
        }
        else if (cast != null)
        {
            danceAnim = cast.danceAnimId;
        }

        if (!string.IsNullOrEmpty(danceAnim))
        {
            if (moveScript != null)
            {
                moveScript.Pose1StateName = danceAnim;
                if (isOnPedestal)
                {
                    moveScript.PlayIdle();
                }
                else
                {
                    moveScript.PlayDance(0.15f);
                }
            }
        }

        // ── Tự động tạo mô hình 3D nhạc cụ và cấu hình Audio ──
        if (editorMember != null)
        {
            CastAudioData castAudioData = memberObj.GetComponent<CastAudioData>();
            if (castAudioData == null)
            {
                castAudioData = memberObj.AddComponent<CastAudioData>();
            }
            castAudioData.audioId = editorMember.audioClip != null ? editorMember.audioClip.name : "editor_audio";
            castAudioData.reduceVolumeWhenTogether = editorMember.reduceVolumeWhenTogether;
            castAudioData.reduceAmount = editorMember.reduceAmount;

            AudioSource designatedSource = null;

            GameObject instPrefab = null;
            if (MainMenuDataManager.Instance != null && 
                editorMember.instrumentPrefabIndex >= 0 && 
                editorMember.instrumentPrefabIndex < MainMenuDataManager.Instance.InstrumentEntries.Count)
            {
                var entry = MainMenuDataManager.Instance.InstrumentEntries[editorMember.instrumentPrefabIndex];
                if (entry != null)
                {
                    instPrefab = await MainMenuDataManager.Instance.LoadInstrumentPrefabAsync(entry.Id);
                }
            }

            if (instPrefab != null)
            {
                GameObject instrumentObj = Instantiate(instPrefab, memberObj.transform);
                instrumentObj.name = $"Instrument_{castAudioData.audioId}";
                instrumentObj.transform.localPosition = instrumentLocalPosition;
                instrumentObj.transform.localRotation = Quaternion.Euler(instrumentLocalRotation);
                instrumentObj.transform.localScale = instrumentLocalScale;
                SetLayerRecursively(instrumentObj, memberObj.layer);

                designatedSource = instrumentObj.GetComponentInChildren<AudioSource>(true);
                if (designatedSource == null)
                {
                    designatedSource = instrumentObj.AddComponent<AudioSource>();
                }
            }
            else
            {
                designatedSource = memberObj.GetComponent<AudioSource>();
                if (designatedSource == null)
                {
                    designatedSource = memberObj.AddComponent<AudioSource>();
                }
            }

            if (designatedSource != null)
            {
                designatedSource.clip = editorMember.audioClip;
                designatedSource.loop = true;
                designatedSource.playOnAwake = false;
                designatedSource.enabled = true;
                
                castAudioData.preparedSource = designatedSource;
                Debug.Log($"[BandARSpawner] Đã nạp AudioSource Editor cho {memberObj.name} với clip: {(editorMember.audioClip != null ? editorMember.audioClip.name : "null")}");
            }
        }
        else if (cast != null && !string.IsNullOrEmpty(cast.audioId))
        {
            CastAudioData castAudioData = memberObj.GetComponent<CastAudioData>();
            if (castAudioData == null)
            {
                castAudioData = memberObj.AddComponent<CastAudioData>();
            }
            castAudioData.audioId = cast.audioId;

            if (cast.audioId.StartsWith("rec_"))
            {
                AudioSource recSource = memberObj.GetComponent<AudioSource>();
                if (recSource == null) recSource = memberObj.AddComponent<AudioSource>();
                recSource.loop = true;
                recSource.playOnAwake = false;
                castAudioData.preparedSource = recSource;
                PrepareLocalRecording(recSource, cast.audioId);
            }
            else
            {
                GameObject instrumentPrefab = null;
                if (MainMenuDataManager.Instance != null)
                {
                    instrumentPrefab = await MainMenuDataManager.Instance.LoadInstrumentPrefabAsync(cast.audioId);
                }
                else
                {
                    instrumentPrefab = GetInstrumentPrefab(cast.audioId);
                }

                if (instrumentPrefab != null)
                {
                    GameObject instrumentObj = Instantiate(instrumentPrefab, memberObj.transform);
                    instrumentObj.name = $"Instrument_{cast.audioId}";
                    instrumentObj.transform.localPosition = instrumentLocalPosition;
                    instrumentObj.transform.localRotation = Quaternion.Euler(instrumentLocalRotation);
                    instrumentObj.transform.localScale = instrumentLocalScale;
                    SetLayerRecursively(instrumentObj, memberObj.layer);

                    AudioConfig audioConfig = instrumentObj.GetComponentInChildren<AudioConfig>(true);
                    AudioSource designatedSource = null;

                    if (audioConfig != null && audioConfig.audioSource != null && audioConfig.audioSource.clip != null)
                    {
                        designatedSource = audioConfig.audioSource;
                        designatedSource.loop = true;
                        designatedSource.playOnAwake = false;
                        designatedSource.enabled = true;
                        Debug.Log($"[BandARSpawner] Dùng AudioConfig.audioSource cho nhạc cụ: {cast.audioId} (clip: {designatedSource.clip.name})");

                        AudioSource[] otherSources = instrumentObj.GetComponentsInChildren<AudioSource>(true);
                        foreach (var src in otherSources)
                        {
                            if (src != designatedSource)
                                src.enabled = false;
                        }
                    }
                    else
                    {
                        AudioSource[] allSources = instrumentObj.GetComponentsInChildren<AudioSource>(true);
                        foreach (var s in allSources)
                        {
                            if (s.clip != null)
                            {
                                designatedSource = s;
                                designatedSource.loop = true;
                                designatedSource.playOnAwake = false;
                                designatedSource.enabled = true;
                                Debug.Log($"[BandARSpawner] Fallback: dùng AudioSource đầu tiên có clip trên nhạc cụ: {cast.audioId}");
                                break;
                            }
                        }

                        if (designatedSource == null)
                        {
                            AudioClip clip = Resources.Load<AudioClip>("Audios/" + cast.audioId);
                            if (clip == null) clip = Resources.Load<AudioClip>(cast.audioId);

                            if (clip != null)
                            {
                                AudioSource resSource = memberObj.GetComponent<AudioSource>();
                                if (resSource == null) resSource = memberObj.AddComponent<AudioSource>();
                                resSource.clip = clip;
                                resSource.loop = true;
                                resSource.playOnAwake = false;
                                designatedSource = resSource;
                                Debug.Log($"[BandARSpawner] Fallback Resources: đã load clip '{clip.name}' cho audioId: {cast.audioId}");
                            }
                        }

                        AudioSource[] allSourcesNow = instrumentObj.GetComponentsInChildren<AudioSource>(true);
                        foreach (var src in allSourcesNow)
                        {
                            if (src != designatedSource)
                            {
                                src.enabled = false;
                            }
                        }
                    }

                    castAudioData.preparedSource = designatedSource;
                    Debug.Log($"[BandARSpawner] Đã tạo nhạc cụ 3D '{instrumentObj.name}' – preparedSource: {(designatedSource != null ? designatedSource.clip?.name ?? "null clip" : "null")}");
                }
                else
                {
                    AudioClip clip = Resources.Load<AudioClip>("Audios/" + cast.audioId);
                    if (clip == null) clip = Resources.Load<AudioClip>(cast.audioId);
                    if (clip != null)
                    {
                        AudioSource resSource = memberObj.GetComponent<AudioSource>();
                        if (resSource == null) resSource = memberObj.AddComponent<AudioSource>();
                        resSource.clip = clip;
                        resSource.loop = true;
                        resSource.playOnAwake = false;
                        castAudioData.preparedSource = resSource;
                    }
                    else
                    {
                        Debug.LogWarning($"[BandARSpawner] Không tìm thấy prefab nhạc cụ lẫn AudioClip cho ID: {cast.audioId}");
                    }
                }
            }
        }


        // Chọn thành viên đầu tiên làm đối tượng điều khiển mặc định thông qua CharacterManager
        if (selectFirst && CharacterManager.Instance != null)
        {
            CharacterManager.Instance.SelectCharacter(memberObj);
        }
    }

    /// <summary>
    /// Tìm kiếm Prefab phù hợp dựa trên tên prefabName.
    /// </summary>
    private GameObject GetCharacterPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;


        // Fallback: Tìm trong Character Catalog của MainMenuDataManager
        if (MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.CharacterPrefabs != null)
        {
            foreach (var prefab in MainMenuDataManager.Instance.CharacterPrefabs)
            {
                if (prefab != null && prefab.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

        // 2. Fallback tìm trong Resources/Prefabs/ hoặc Resources/
        GameObject resPrefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
        if (resPrefab == null)
        {
            resPrefab = Resources.Load<GameObject>(prefabName);
        }

        return resPrefab;
    }

    /// <summary>
    /// Tìm kiếm Prefab nhạc cụ phù hợp dựa trên ID nhạc cụ (audioId).
    /// </summary>
    private GameObject GetInstrumentPrefab(string audioId)
    {
        if (string.IsNullOrEmpty(audioId)) return null;

        List<GameObject> resolvedInstrumentPrefabs = instrumentPrefabs;
        if (resolvedInstrumentPrefabs != null)
        {
            foreach (var prefab in resolvedInstrumentPrefabs)
            {
                if (prefab == null) continue;

                // 1. So sánh tên prefab gốc
                if (prefab.name.Equals(audioId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }

                // 2. So sánh tên được cấu hình trong AudioConfig
                AudioConfig config = prefab.GetComponent<AudioConfig>();
                if (config != null && !string.IsNullOrEmpty(config.Name) &&
                    config.Name.Equals(audioId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

        // Fallback: Tìm trong Resources/Prefabs/Intrument hoặc tương tự
        GameObject resPrefab = Resources.Load<GameObject>("Prefabs/Intrument/" + audioId);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>("Prefabs/" + audioId);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(audioId);

        return resPrefab;
    }

    /// <summary>
    /// Đệ quy thiết lập Layer cho GameObject nhạc cụ và các con của nó.
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    /// <summary>
    /// Xóa toàn bộ các thành viên ban nhạc đã spawn.
    /// </summary>
    public void ClearSpawnedMembers()
    {
        if (_placementLimitCoroutine != null)
        {
            StopCoroutine(_placementLimitCoroutine);
            _placementLimitCoroutine = null;
        }
        _placementLimitCleanupQueued = false;

        foreach (var member in _spawnedMembers)
        {
            if (member != null)
            {
                member.transform.DOKill();
                Destroy(member);
            }
        }
        _spawnedMembers.Clear();
        _didVibrateForFourPlacedCasts = false;
        _awardedSelectedBandPoints = false;

        if (m_FollowContainer != null)
        {
            Destroy(m_FollowContainer.gameObject);
            m_FollowContainer = null;
        }
    }

    /// <summary>
    /// Dọn sạch toàn bộ Cast đã spawn trong AR và reset các bệ đứng về trạng thái ban đầu
    /// (hiển thị lại đầy đủ, sẵn sàng cho lần spawn tiếp theo).
    /// Được gọi khi người dùng bấm Back từ chế độ AR để quay lại UI tạo nhân vật.
    /// </summary>
    public void DestroyAllAndResetPedestals()
    {
        // 1. Dừng âm thanh và xóa toàn bộ Cast đã spawn
        BandAudioManager bandAudio = BandAudioManager.GetOrCreateInstance(null);
        if (bandAudio != null)
        {
            bandAudio.StopAll();
        }

        ClearSpawnedMembers();

        // 2. Reset toàn bộ bệ đứng: xóa con (Cast cũ nếu còn), restore scale, bật lại
        if (pedestals != null)
        {
            foreach (var pedestal in pedestals)
            {
                if (pedestal == null) continue;

                // Dừng tween đang chạy
                pedestal.transform.DOKill();

                // Xóa tất cả nhân vật con còn sót lại trên bệ
                for (int i = pedestal.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = pedestal.transform.GetChild(i);
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }

                // Khôi phục scale gốc và bật lại bệ
                Vector3 originalScale = GetOriginalPedestalScale(pedestal);
                pedestal.transform.localScale = originalScale;
                pedestal.SetActive(true);
            }

            // Bật lại parent container của bệ đứng nếu bị tắt
            if (pedestals.Count > 0 && pedestals[0] != null && pedestals[0].transform.parent != null)
            {
                pedestals[0].transform.parent.gameObject.SetActive(true);
            }
        }

        // 3. Reset trạng thái scroller nếu có
        if (pedestalsScroller != null)
        {
            pedestalsScroller.gameObject.SetActive(true);
            pedestalsScroller.ResetUseFixedLimits();
            pedestalsScroller.CalculateScrollLimits();
        }
        else
        {
            // Tự tìm scroller theo tên phổ biến
            GameObject scrollerObj = GameObject.Find("Pedestals_ScrollContainer");
            if (scrollerObj != null)
            {
                scrollerObj.SetActive(true);
                PedestalsScroller sc = scrollerObj.GetComponent<PedestalsScroller>();
                if (sc != null)
                {
                    sc.ResetUseFixedLimits();
                    sc.CalculateScrollLimits();
                }
            }
        }

        // 4. Reset flag hiển thị bệ
        _pedestalsVisible = true;
        _originalPedestalScales.Clear();
        _cachedCastsToSpawn.Clear();

        Debug.Log("[BandARSpawner] DestroyAllAndResetPedestals: Đã dọn sạch Cast AR và reset toàn bộ bệ đứng.");
    }

    /// <summary>
    /// Chuyển đổi trạng thái hiển thị của các bệ đứng chưa được kéo nhân vật ra ngoài.
    /// Thu nhỏ biến mất hoặc phóng to hiển thị lại.
    /// </summary>
    public void TogglePedestalsVisibility()
    {
        if (!m_ManualPedestalToggleEnabled)
        {
            Debug.Log("[BandARSpawner] Manual pedestal toggle is disabled. Pedestals hide automatically after 4 Casts are placed.");
            return;
        }

        _pedestalsVisible = !_pedestalsVisible;
        Debug.Log($"[BandARSpawner] Chuyển đổi trạng thái hiển thị bệ đứng: _pedestalsVisible = {_pedestalsVisible}");

        foreach (var pedestal in pedestals)
        {
            if (pedestal == null) continue;

            // Kiểm tra xem bệ đứng này có chứa nhân vật nào chưa được kéo ra không
            bool hasCastInside = false;
            for (int i = 0; i < pedestal.transform.childCount; i++)
            {
                Transform child = pedestal.transform.GetChild(i);
                if (child.gameObject.activeSelf && child.GetComponent<Move>() != null)
                {
                    hasCastInside = true;
                    break;
                }
            }

            // Chỉ thực hiện hiệu ứng scale đối với bệ đứng chưa bị kéo nhân vật ra ngoài
            if (hasCastInside || pedestal.activeSelf)
            {
                pedestal.transform.DOKill();
                if (_pedestalsVisible)
                {
                    // Đảm bảo bệ đứng được active
                    pedestal.SetActive(true);
                    
                    Vector3 targetScale = _originalPedestalScales.ContainsKey(pedestal) ? 
                        _originalPedestalScales[pedestal] : Vector3.one;

                    pedestal.transform.DOScale(targetScale, 0.45f).SetEase(Ease.OutBack);
                }
                else
                {
                    // Thu nhỏ về 0
                    pedestal.transform.DOScale(Vector3.zero, 0.35f).SetEase(Ease.InBack);
                }
            }
        }

        // Cập nhật lại text của nút UI để phản ánh trạng thái mới
        Button targetButton = togglePedestalsButton;
        if (targetButton == null)
        {
            GameObject btnObj = GameObject.Find("Btn_TogglePedestals");
            if (btnObj != null)
            {
                targetButton = btnObj.GetComponent<Button>();
            }
        }

        if (targetButton != null)
        {
            var txt = targetButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = _pedestalsVisible ? "Ẩn Bệ Đứng" : "Hiện Bệ Đứng";
            }
        }
    }

    /// <summary>
    /// Tự động tạo hoặc liên kết nút bấm UI Ẩn/Hiện bệ đứng tại runtime.
    /// </summary>
    private void CreateToggleUI()
    {
        if (!m_ManualPedestalToggleEnabled)
        {
            return;
        }

        // 1. Nếu đã được gán trực tiếp qua Inspector
        if (togglePedestalsButton != null)
        {
            togglePedestalsButton.onClick.RemoveListener(TogglePedestalsVisibility);
            togglePedestalsButton.onClick.AddListener(TogglePedestalsVisibility);

            var txtInspector = togglePedestalsButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txtInspector != null) txtInspector.text = _pedestalsVisible ? "Ẩn Bệ Đứng" : "Hiện Bệ Đứng";

            Debug.Log("[BandARSpawner] Đã liên kết nút bấm được gán trên Inspector thành công.");
            return;
        }

        // 2. Nếu có nút tự tạo bằng tay trong Hierarchy đặt tên là Btn_TogglePedestals
        GameObject existingBtn = GameObject.Find("Btn_TogglePedestals");
        if (existingBtn != null)
        {
            Button btnComp = existingBtn.GetComponent<Button>();
            if (btnComp != null)
            {
                btnComp.onClick.RemoveListener(TogglePedestalsVisibility);
                btnComp.onClick.AddListener(TogglePedestalsVisibility);

                var txtHierarchy = existingBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (txtHierarchy != null) txtHierarchy.text = _pedestalsVisible ? "Ẩn Bệ Đứng" : "Hiện Bệ Đứng";

                Debug.Log("[BandARSpawner] Đã tìm thấy và liên kết nút bấm 'Btn_TogglePedestals' trong Hierarchy.");
                return;
            }
        }

        // 3. Nếu không tìm thấy, tự động tạo nút mới tại runtime
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[BandARSpawner] Không tìm thấy Canvas nào trong Scene để tạo nút Toggle.");
            return;
        }

        // Tạo Button GameObject
        GameObject btnObj = new GameObject("Btn_TogglePedestals");
        btnObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        Image img = btnObj.AddComponent<Image>();
        Button btn = btnObj.AddComponent<Button>();

        // Thiết lập kích thước và neo ở góc trên bên trái, tọa độ Y lệch xuống khoảng 180px để tránh nút Back
        rect.sizeDelta = new Vector2(160, 50);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f, -180f);

        // Thiết lập background đẹp mắt (màu xám đen bo tròn nhẹ)
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        img.type = Image.Type.Simple;

        // Tạo Text bên trong nút
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TMPro.TextMeshProUGUI txtCreated = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        txtCreated.text = _pedestalsVisible ? "Ẩn Bệ Đứng" : "Hiện Bệ Đứng";
        txtCreated.fontSize = 18;
        txtCreated.alignment = TMPro.TextAlignmentOptions.Center;
        txtCreated.color = Color.white;

        // Đăng ký sự kiện click
        btn.onClick.AddListener(TogglePedestalsVisibility);

        // Hiệu ứng đổi màu tương tác
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;

        Debug.Log("[BandARSpawner] Đã tự động tạo nút UI 'Btn_TogglePedestals' mới thành công.");
    }

    /// <summary>
    /// Tải bản ghi âm local (base64) vào AudioSource nhưng KHÔNG phát ngay.
    /// Việc phát sẽ do CastAudioData.PlayAudio() trigger sau khi Cast được thả ra AR.
    /// </summary>
    private async void PrepareLocalRecording(AudioSource source, string audioId)
    {
        if (source == null || string.IsNullOrEmpty(audioId)) return;

        Debug.Log($"[BandARSpawner] Đang nạp bản ghi âm (chưa phát) cho: {audioId}");

        if (MainMenuDataManager.Instance != null)
        {
            var recordings = await MainMenuDataManager.Instance.GetRecordingsAsync();
            var targetRec = recordings.Find(r => r.recordingId == audioId);

            if (targetRec != null && !string.IsNullOrEmpty(targetRec.audioBase64) && source != null)
            {
                byte[] wavBytes = System.Convert.FromBase64String(targetRec.audioBase64);

                try
                {
                    wavBytes = AudioEncryption.Decrypt(wavBytes);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[BandARSpawner] Giai ma am thanh khong thanh cong, thu dung truc tiep: " + ex.Message);
                }

                AudioClip clip = WavUtility.ToAudioClip(wavBytes, targetRec.name);

                if (clip != null && source != null)
                {
                    source.clip = clip;
                    // Không gọi source.Play() ở đây – sẽ phát khi Cast được thả ra AR
                    Debug.Log($"[BandARSpawner] Đã nạp xong clip bản ghi âm '{targetRec.name}' vào source (chờ thả Cast).");
                }
            }
        }
    }


    private Vector3 GetOriginalPedestalScale(GameObject pedestal)
    {
        if (pedestal == null) return Vector3.one;
        if (_originalPedestalScales.TryGetValue(pedestal, out Vector3 originalScale))
        {
            return originalScale;
        }
        // Nếu chưa lưu và scale hiện tại khác 0, lưu lại
        if (pedestal.transform.localScale != Vector3.zero)
        {
            _originalPedestalScales[pedestal] = pedestal.transform.localScale;
            return pedestal.transform.localScale;
        }
        return Vector3.one;
    }

    private void AnimatePedestalShow(GameObject pedestal)
    {
        if (pedestal == null) return;
        pedestal.transform.DOKill();
        pedestal.SetActive(true);
        Vector3 originalScale = GetOriginalPedestalScale(pedestal);
        pedestal.transform.localScale = Vector3.zero;
        pedestal.transform.DOScale(originalScale, 0.35f).SetEase(Ease.OutBack);
    }

    private void AnimatePedestalHide(GameObject pedestal, System.Action onComplete = null)
    {
        if (pedestal == null)
        {
            onComplete?.Invoke();
            return;
        }
        pedestal.transform.DOKill();
        pedestal.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            pedestal.SetActive(false);
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Bật/Tắt hiển thị của các bệ đứng và các nhân vật chưa được kéo đặt vào thế giới.
    /// Dùng khi ẩn/hiện UI để quay phim. Supports transition scaling.
    /// </summary>
    public void SetPedestalsAndUnplacedCastsActive(bool active)
    {
        SetPedestalsAndUnplacedCastsActive(active, false, 0.35f);
    }

    public void SetPedestalsAndUnplacedCastsActive(bool active, bool useTransition, float duration)
    {
        // 1. Điều khiển trạng thái hoạt động của các Cast chưa được đặt ra AR trực tiếp
        if (_spawnedMembers != null)
        {
            foreach (var member in _spawnedMembers)
            {
                if (member == null) continue;
                CastPlacementState placementState = member.GetComponent<CastPlacementState>();
                bool isPlaced = placementState != null ? placementState.IsPlaced : member.transform.parent == null;
                if (!isPlaced)
                {
                    member.SetActive(active);
                }
            }
        }

        // 2. Điều khiển các bệ đứng
        if (pedestals != null && pedestals.Count > 0)
        {
            // Lấy parent container của bệ đứng để bật/tắt cùng
            GameObject pedestalParent = null;
            if (pedestals[0] != null && pedestals[0].transform.parent != null)
            {
                pedestalParent = pedestals[0].transform.parent.gameObject;
            }

            if (!active)
            {
                // Tắt: ẩn từng bệ rồi tắt parent container
                foreach (var pedestal in pedestals)
                {
                    if (pedestal == null) continue;
                    pedestal.transform.DOKill();

                    if (useTransition)
                    {
                        GameObject targetPedestal = pedestal;
                        pedestal.transform.DOScale(Vector3.zero, duration)
                            .SetEase(Ease.InQuad)
                            .OnComplete(() =>
                            {
                                targetPedestal.SetActive(false);
                            });
                    }
                    else
                    {
                        pedestal.SetActive(false);
                    }
                }

                // Tắt parent container sau khi animation kết thúc (hoặc ngay lập tức)
                if (pedestalParent != null)
                {
                    if (useTransition)
                    {
                        // Delay tắt parent sau khi tween xong
                        GameObject targetParent = pedestalParent;
                        DOVirtual.DelayedCall(duration + 0.05f, () =>
                        {
                            if (targetParent != null) targetParent.SetActive(false);
                        });
                    }
                    else
                    {
                        pedestalParent.SetActive(false);
                    }
                }
            }
            else
            {
                // Bật: bật parent container trước, rồi bật từng bệ có Cast bên trong
                if (pedestalParent != null)
                {
                    pedestalParent.SetActive(true);
                }

                foreach (var pedestal in pedestals)
                {
                    if (pedestal == null) continue;
                    pedestal.transform.DOKill();

                    // Kiểm tra xem bệ đứng này có chứa nhân vật chưa kéo ra hay không (dùng activeSelf vì có thể bệ cha đang bị ẩn)
                    bool hasCastInside = false;
                    for (int i = 0; i < pedestal.transform.childCount; i++)
                    {
                        Transform child = pedestal.transform.GetChild(i);
                        if (child.gameObject.activeSelf && child.GetComponent<Move>() != null)
                        {
                            hasCastInside = true;
                            break;
                        }
                    }

                    bool shouldShow = _pedestalsVisible && hasCastInside;
                    if (shouldShow)
                    {
                        pedestal.SetActive(true);

                        if (useTransition)
                        {
                            pedestal.transform.localScale = Vector3.zero;
                            Vector3 targetScale = GetOriginalPedestalScale(pedestal);
                            pedestal.transform.DOScale(targetScale, duration + 0.1f)
                                .SetEase(Ease.OutBack);
                        }
                        else
                        {
                            pedestal.transform.localScale = GetOriginalPedestalScale(pedestal);
                        }
                    }
                    else
                    {
                        pedestal.SetActive(false);
                    }
                }
            }
        }

        // 2. Điều khiển container ảo follow camera (fallback)
        if (m_FollowContainer != null)
        {
            m_FollowContainer.DOKill();
            if (!active)
            {
                if (useTransition)
                {
                    m_FollowContainer.DOScale(Vector3.zero, duration)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            m_FollowContainer.gameObject.SetActive(false);
                        });
                }
                else
                {
                    m_FollowContainer.gameObject.SetActive(false);
                }
            }
            else
            {
                m_FollowContainer.gameObject.SetActive(true);
                if (useTransition)
                {
                    m_FollowContainer.localScale = Vector3.zero;
                    m_FollowContainer.DOScale(Vector3.one, duration + 0.1f)
                        .SetEase(Ease.OutBack);
                }
                else
                {
                    m_FollowContainer.localScale = Vector3.one;
                }
            }
        }
    }

    /// <summary>
    /// Tìm kiếm UI controller đang hoạt động và cập nhật trạng thái UI AR dựa vào việc có nhân vật nào được đặt hay chưa.
    /// </summary>
    public void UpdateARUI()
    {
        int placedCount = CountPlacedMembers();
        bool hasPlaced = placedCount > 0;

        // Tìm BandPanelController
        BandPanelController bandPanel = null;
#if UNITY_2023_1_OR_NEWER
        bandPanel = FindAnyObjectByType<BandPanelController>();
#else
        bandPanel = FindObjectOfType<BandPanelController>();
#endif
        if (bandPanel != null)
        {
            bandPanel.UpdateARUIBasedOnPlacement(hasPlaced);
        }

        // Tìm CustomCharacterPanelController
        CustomCharacterPanelController customPanel = null;
#if UNITY_2023_1_OR_NEWER
        customPanel = FindAnyObjectByType<CustomCharacterPanelController>();
#else
        customPanel = FindObjectOfType<CustomCharacterPanelController>();
#endif
        if (customPanel != null)
        {
            customPanel.UpdateARUIBasedOnPlacement(hasPlaced);
        }
    }

    private void OnDestroy()
    {
        ClearSpawnedMembers();
    }

    /// <summary>
    /// Tự động tạo đối tượng BandARSpawner trong scene khi bắt đầu game tại runtime,
    /// giúp lập trình viên không cần kéo thả component thủ công trong Unity Editor.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == "AR Scene" || activeScene == "Non-AR Scene" ||
            activeScene == "costom AR" || activeScene == "Custom AR" ||
            activeScene == "costom Non AR" || activeScene == "Custom Non AR" ||
            activeScene == "Band AR" || activeScene == "Band AR Scene" ||
            activeScene == "Band Non AR" || activeScene == "Band NonAR Scene" || activeScene == "Band Non-AR Scene" ||
            activeScene == "Custome AR Scene" || activeScene == "Custome NonAR Scene" ||
            activeScene == "Band Mode AR Scene" || activeScene == "Band Mode NonAR Scene")
        {
            if (FindFirstObjectByType<BandARSpawner>() == null)
            {
                GameObject spawnerObj = new GameObject("BandARSpawner_AutoCreated");
                spawnerObj.AddComponent<BandARSpawner>();
                Debug.Log($"[BandARSpawner] Đã tự động tạo thành công tại runtime trong scene '{activeScene}'!");
            }
        }
    }
}
