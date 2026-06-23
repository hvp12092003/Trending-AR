using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

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

    [Header("Prefab References")]
    [Tooltip("Danh sách các character prefab có thể dùng để spawn. Tên của prefab nên trùng với cast.prefabName.")]
    [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [Tooltip("Tự động spawn ngay khi bắt đầu Scene.")]
    [SerializeField] private bool spawnOnStart = true;

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

    [Header("Pedestal Offset Settings")]
    [Tooltip("Vị trí cục bộ của nhân vật so với bệ đứng.")]
    [SerializeField] private Vector3 pedestalLocalPosition = new Vector3(0f, 0f, 0.0117f);
    
    [Tooltip("Góc xoay cục bộ của nhân vật so với bệ đứng.")]
    [SerializeField] private Vector3 pedestalLocalRotation = new Vector3(-90f, 90f, 90f);
    
    [Tooltip("Tỉ lệ scale cục bộ của nhân vật.")]
    [SerializeField] private Vector3 pedestalLocalScale = new Vector3(0.02f, 0.02f, 0.02f);

    public Vector3 PedestalLocalPosition => pedestalLocalPosition;
    public Vector3 PedestalLocalRotation => pedestalLocalRotation;
    public Vector3 PedestalLocalScale => pedestalLocalScale;

    // Trạng thái điều khiển camera follow
    private bool m_FollowCamera = true;
    private Transform m_FollowContainer;
    private List<CastData> _cachedCastsToSpawn = new List<CastData>();

    // Danh sách các thành viên đã được spawn ra thực tế
    private List<GameObject> _spawnedMembers = new List<GameObject>();

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
    }

    private async void Start()
    {
        await LoadCastsToSpawnAsync();

        // Nếu bật tự động spawn khi bắt đầu và có dữ liệu
        if (spawnOnStart && _cachedCastsToSpawn.Count > 0)
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            // Nếu tìm thấy Camera chính, tính toán vị trí trước mặt Camera
            if (Camera.main != null)
            {
                Transform camTrans = Camera.main.transform;
                Vector3 camForwardHorizontal = camTrans.forward;
                camForwardHorizontal.y = 0f;
                camForwardHorizontal.Normalize();
                
                spawnPos = camTrans.position + camForwardHorizontal * autoSpawnOffset.z + camTrans.right * autoSpawnOffset.x;
                spawnPos.y = camTrans.position.y + autoSpawnOffset.y; // Đồng bộ cao độ y

                // Quay mặt về phía Camera (container hướng ngược lại với camForwardHorizontal)
                spawnRot = Quaternion.LookRotation(-camForwardHorizontal, Vector3.up);
            }
            else
            {
                spawnPos = transform.position + autoSpawnOffset;
                spawnRot = transform.rotation;
            }

            SpawnBand(spawnPos, spawnRot);
        }
    }

    private void Update()
    {
        // Chỉ cập nhật vị trí container follow camera nếu không sử dụng bệ đứng
        bool usePedestals = pedestals != null && pedestals.Count > 0;
        if (!usePedestals && m_FollowCamera && Camera.main != null)
        {
            Transform camTrans = Camera.main.transform;

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
            // Trong chế độ Custom, lấy từ danh sách nhân vật đã tạo trong MainMenuDataManager (tối đa 7 nhân vật)
            if (MainMenuDataManager.Instance != null)
            {
                var createdCharacters = await MainMenuDataManager.Instance.GetCreatedCharactersAsync();
                if (createdCharacters != null && createdCharacters.Count > 0)
                {
                    int count = Mathf.Min(createdCharacters.Count, 7);
                    for (int i = 0; i < count; i++)
                    {
                        var charData = createdCharacters[i];
                        _cachedCastsToSpawn.Add(new CastData(
                            charData.name,
                            charData.prefabName,
                            charData.instrumentId,
                            charData.danceAnimId,
                            charData.danceAnimIds
                        ));
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
                    pedestal.SetActive(false); // Ẩn bệ đi khi nhân vật đã được kéo ra đặt vào AR
                    Debug.Log($"[BandARSpawner] Detached member: {member.name} từ bệ {pedestal.name} và ẩn bệ.");
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
    /// Spawn toàn bộ thành viên ban nhạc xếp ngang tại vị trí và góc xoay chỉ định.
    /// </summary>
    /// <param name="centerPosition">Vị trí trung tâm của hàng ban nhạc.</param>
    /// <param name="rotation">Góc xoay của ban nhạc (hướng mặt).</param>
    public void SpawnBand(Vector3 centerPosition, Quaternion rotation)
    {
        List<CastData> casts = new List<CastData>();
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

        // 1. Dọn dẹp các thành viên cũ nếu có
        ClearSpawnedMembers();

        int count = casts.Count;
        if (count == 0) return;

        Debug.Log($"[BandARSpawner] Đang spawn với {count} thành viên...");

        // Trường hợp 1: Sử dụng các bệ đứng (pedestals) đã được gán sẵn
        if (pedestals != null && pedestals.Count > 0)
        {
            for (int i = 0; i < pedestals.Count; i++)
            {
                if (pedestals[i] == null) continue;

                if (i < count)
                {
                    pedestals[i].SetActive(true);

                    CastData cast = casts[i];
                    GameObject prefab = GetCharacterPrefab(cast.prefabName);
                    if (prefab == null)
                    {
                        Debug.LogError($"[BandARSpawner] Không tìm thấy prefab nào cho tên: '{cast.prefabName}'.");
                        continue;
                    }

                    GameObject memberObj = Instantiate(prefab, pedestals[i].transform);
                    memberObj.transform.localPosition = pedestalLocalPosition;
                    memberObj.transform.localRotation = Quaternion.Euler(pedestalLocalRotation);
                    memberObj.name = $"BandMember_{i + 1}_{cast.name}";

                    SetupMemberComponents(memberObj, cast, i == 0);
                    _spawnedMembers.Add(memberObj);
                }
                else
                {
                    // Ẩn bệ thừa không có nhân vật
                    pedestals[i].SetActive(false);
                }
            }
        }
        else
        {
            // Trường hợp 2: Fallback tự động xếp hàng ngang (như cũ) nếu không gán bệ
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
                CastData cast = casts[i];
                GameObject prefab = GetCharacterPrefab(cast.prefabName);
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

                memberObj.name = $"BandMember_{i + 1}_{cast.name}";
                SetupMemberComponents(memberObj, cast, i == 0);
                _spawnedMembers.Add(memberObj);
            }
        }
    }

    /// <summary>
    /// Cấu hình các component cho thành viên ban nhạc sau khi spawn.
    /// </summary>
    private void SetupMemberComponents(GameObject memberObj, CastData cast, bool selectFirst)
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
            moveScript.WalkStateName = "mixamo_com";
            moveScript.Pose1StateName = "mixamo_com";
        }

        // Hiệu ứng xuất hiện phóng to từ 0 bằng DOTween
        Vector3 originalScale = pedestalLocalScale;
        memberObj.transform.localScale = Vector3.zero;
        memberObj.transform.DOKill();
        memberObj.transform.DOScale(originalScale, 0.45f).SetEase(Ease.OutBack);

        // ── Tự động cấu hình Dance Animation ──
        if (playDanceOnSpawn && !string.IsNullOrEmpty(cast.danceAnimId))
        {
            if (moveScript != null)
            {
                moveScript.PlayAnimation(cast.danceAnimId, 0.15f);
            }
        }

        // ── Tự động cấu hình Audio Nhạc cụ (Nếu được thiết lập) ──
        if (playAudioOnSpawn && !string.IsNullOrEmpty(cast.audioId))
        {
            AudioSource source = memberObj.GetComponent<AudioSource>();
            if (source == null)
            {
                source = memberObj.AddComponent<AudioSource>();
            }
            
            source.loop = true;

            if (cast.audioId.StartsWith("rec_"))
            {
                PlayFirebaseRecording(source, cast.audioId);
            }
            else
            {
                AudioClip clip = Resources.Load<AudioClip>("Audios/" + cast.audioId);
                if (clip == null) clip = Resources.Load<AudioClip>(cast.audioId);
                
                if (clip != null)
                {
                    source.clip = clip;
                    source.Play();
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

        // 1. Tìm trong danh sách Inspector
        if (characterPrefabs != null)
        {
            foreach (var prefab in characterPrefabs)
            {
                if (prefab != null && prefab.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

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
    /// Xóa toàn bộ các thành viên ban nhạc đã spawn.
    /// </summary>
    public void ClearSpawnedMembers()
    {
        foreach (var member in _spawnedMembers)
        {
            if (member != null)
            {
                member.transform.DOKill();
                Destroy(member);
            }
        }
        _spawnedMembers.Clear();

        if (m_FollowContainer != null)
        {
            Destroy(m_FollowContainer.gameObject);
            m_FollowContainer = null;
        }
    }

    private async void PlayFirebaseRecording(AudioSource source, string audioId)
    {
        if (source == null || string.IsNullOrEmpty(audioId)) return;

        Debug.Log($"[BandARSpawner] Đang tải bản ghi âm cho AudioSource: {audioId}");

        if (MainMenuDataManager.Instance != null)
        {
            var recordings = await MainMenuDataManager.Instance.GetRecordingsAsync();
            var targetRec = recordings.Find(r => r.recordingId == audioId);

            if (targetRec != null && !string.IsNullOrEmpty(targetRec.audioBase64) && source != null)
            {
                byte[] wavBytes = System.Convert.FromBase64String(targetRec.audioBase64);
                AudioClip clip = WavUtility.ToAudioClip(wavBytes, targetRec.name);
                
                if (clip != null && source != null)
                {
                    source.clip = clip;
                    source.Play();
                    Debug.Log($"[BandARSpawner] Đã phát thành công bản ghi âm {targetRec.name}");
                }
            }
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
        if (activeScene == "AR Scene" || activeScene == "Non-AR Scene")
        {
            if (FindFirstObjectByType<BandARSpawner>() == null)
            {
                GameObject spawnerObj = new GameObject("BandARSpawner_AutoCreated");
                spawnerObj.AddComponent<BandARSpawner>();
                Debug.Log("[BandARSpawner] Đã tự động tạo thành công tại runtime!");
            }
        }
    }
}
