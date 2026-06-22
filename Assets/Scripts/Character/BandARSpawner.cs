using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Quản lý việc tạo (spawn) và sắp xếp đội hình cho 4 thành viên của ban nhạc được chọn trong Scene AR.
/// </summary>
public class BandARSpawner : MonoBehaviour
{
    [Header("Prefab References")]
    [Tooltip("Danh sách các character prefab có thể dùng để spawn. Tên của prefab nên trùng với cast.prefabName.")]
    [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [Tooltip("Tự động spawn ngay khi bắt đầu Scene nếu có ban nhạc được chọn (hữu ích cho giả lập/test editor).")]
    [SerializeField] private bool spawnOnStart = false;

    [Tooltip("Khoảng cách hàng ngang giữa các thành viên trong ban nhạc.")]
    [SerializeField] private float memberSpacing = 0.8f;

    [Tooltip("Vị trí lệch (offset) so với camera khi tự động spawn lúc bắt đầu.")]
    [SerializeField] private Vector3 autoSpawnOffset = new Vector3(0, 0, 3f);

    [Header("Animation & Audio")]
    [Tooltip("Tự động kích hoạt audio nhạc cụ của các thành viên sau khi spawn.")]
    [SerializeField] private bool playAudioOnSpawn = true;

    [Tooltip("Tự động chơi hoạt ảnh nhảy được chọn của từng thành viên.")]
    [SerializeField] private bool playDanceOnSpawn = true;

    // Danh sách các thành viên ban nhạc đã được spawn ra thực tế
    private List<GameObject> _spawnedMembers = new List<GameObject>();

    private void Start()
    {
        // Nếu bật tự động spawn khi bắt đầu và có dữ liệu ban nhạc hoặc nhân vật custom
        bool hasSelectedBand = BandSelectionManager.SelectedBand != null;
        bool hasCustomCast = MainMenuDataManager.Instance != null && MainMenuDataManager.Instance.castData != null;
        if (spawnOnStart && (hasSelectedBand || hasCustomCast))
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            // Nếu tìm thấy Camera chính, tính toán vị trí trước mặt Camera
            if (Camera.main != null)
            {
                Transform camTrans = Camera.main.transform;
                spawnPos = camTrans.position + camTrans.forward * autoSpawnOffset.z + camTrans.right * autoSpawnOffset.x;
                spawnPos.y = camTrans.position.y + autoSpawnOffset.y; // Đồng bộ cao độ y

                // Quay mặt về phía Camera
                Vector3 dirToCam = camTrans.position - spawnPos;
                dirToCam.y = 0;
                if (dirToCam != Vector3.zero)
                {
                    spawnRot = Quaternion.LookRotation(dirToCam);
                }
            }
            else
            {
                spawnPos = transform.position + autoSpawnOffset;
                spawnRot = transform.rotation;
            }

            SpawnBand(spawnPos, spawnRot);
        }
    }

    /// <summary>
    /// Spawn toàn bộ 4 thành viên ban nhạc xếp ngang tại vị trí và góc xoay chỉ định.
    /// </summary>
    /// <param name="centerPosition">Vị trí trung tâm của hàng ban nhạc.</param>
    /// <param name="rotation">Góc xoay của ban nhạc (hướng mặt).</param>
    public void SpawnBand(Vector3 centerPosition, Quaternion rotation)
    {
        BandData band = BandSelectionManager.SelectedBand;
        CastData customCast = (MainMenuDataManager.Instance != null) ? MainMenuDataManager.Instance.castData : null;

        if (band == null && customCast != null)
        {
            // Tạo một BandData tạm thời chứa duy nhất nhân vật custom
            band = new BandData(new List<CastData> { customCast });
        }

        if (band == null)
        {
            Debug.LogWarning("[BandARSpawner] Không có dữ liệu ban nhạc hoặc nhân vật custom nào để spawn!");
            return;
        }

        // 1. Dọn dẹp các thành viên cũ nếu có
        ClearSpawnedMembers();

        int count = band.casts.Count;
        if (count == 0) return;

        // Tính hướng dịch ngang (trục X cục bộ dựa trên góc xoay)
        Vector3 rightDir = rotation * Vector3.right;

        Debug.Log($"[BandARSpawner] Đang spawn ban nhạc với {count} thành viên...");

        for (int i = 0; i < count; i++)
        {
            CastData cast = band.casts[i];
            if (cast == null) continue;

            // Tìm prefab nhân vật phù hợp
            GameObject prefab = GetCharacterPrefab(cast.prefabName);
            if (prefab == null)
            {
                Debug.LogError($"[BandARSpawner] Không tìm thấy prefab nào cho tên: '{cast.prefabName}'. Hãy cấu hình trong danh sách Inspector hoặc Resources/Prefabs.");
                continue;
            }

            // Tính toán vị trí xếp hàng ngang cân đối
            // Ví dụ: 4 thành viên sẽ có offset là: i=0 -> -1.5, i=1 -> -0.5, i=2 -> 0.5, i=3 -> 1.5 (nhân với spacing / 2)
            float offsetFactor = i - (count - 1) / 2.0f;
            Vector3 memberPos = centerPosition + rightDir * (offsetFactor * memberSpacing);

            // Spawn nhân vật
            GameObject memberObj = Instantiate(prefab, memberPos, rotation);
            memberObj.name = $"BandMember_{i + 1}_{cast.name}";

            // Hiệu ứng xuất hiện phóng to từ 0 bằng DOTween
            Vector3 originalScale = memberObj.transform.localScale;
            memberObj.transform.localScale = Vector3.zero;
            memberObj.transform.DOKill();
            memberObj.transform.DOScale(originalScale, 0.45f).SetEase(Ease.OutBack);

            _spawnedMembers.Add(memberObj);

            // ── Tự động cấu hình Dance Animation ──
            if (playDanceOnSpawn && !string.IsNullOrEmpty(cast.danceAnimId))
            {
                Move moveScript = memberObj.GetComponent<Move>();
                if (moveScript != null)
                {
                    moveScript.PlayAnimation(cast.danceAnimId, 0.15f);
                    Debug.Log($"[BandARSpawner] Member {cast.name} đang chạy dance: {cast.danceAnimId}");
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
                    // Tải ghi âm từ Firebase và giải mã trong bộ nhớ
                    PlayFirebaseRecording(source, cast.audioId);
                }
                else
                {
                    // Tải từ Resources
                    AudioClip clip = Resources.Load<AudioClip>("Audios/" + cast.audioId);
                    if (clip == null) clip = Resources.Load<AudioClip>(cast.audioId);
                    
                    if (clip != null)
                    {
                        source.clip = clip;
                        source.Play();
                    }
                    else
                    {
                        Debug.LogWarning($"[BandARSpawner] Không tìm thấy AudioClip mặc định cho ID: {cast.audioId}");
                    }
                }
            }

            // Chọn thành viên đầu tiên làm đối tượng điều khiển mặc định thông qua CharacterManager
            if (CharacterManager.Instance != null && i == 0)
            {
                CharacterManager.Instance.SelectCharacter(memberObj);
            }
        }
    }

    /// <summary>
    /// Tìm kiếm Prefab phù hợp dựa trên tên prefabName.
    /// Ưu tiên tìm trong Inspector list, sau đó tìm trong thư mục Resources.
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
                // Hủy hoạt ảnh đang chạy để tránh rác nhớ
                member.transform.DOKill();
                Destroy(member);
            }
        }
        _spawnedMembers.Clear();
    }

    private async void PlayFirebaseRecording(AudioSource source, string audioId)
    {
        if (source == null || string.IsNullOrEmpty(audioId)) return;

        Debug.Log($"[BandARSpawner] Đang tải bản ghi âm từ Firebase cho AudioSource: {audioId}");

        if (MainMenuDataManager.Instance != null)
        {
            var recordings = await MainMenuDataManager.Instance.GetRecordingsAsync();
            var targetRec = recordings.Find(r => r.recordingId == audioId);

            if (targetRec != null && !string.IsNullOrEmpty(targetRec.audioBase64) && source != null)
            {
                byte[] wavBytes = System.Convert.FromBase64String(targetRec.audioBase64);
                // Giải mã mảng byte WAV thành AudioClip trong RAM
                AudioClip clip = WavUtility.ToAudioClip(wavBytes, targetRec.name);
                
                if (clip != null && source != null)
                {
                    source.clip = clip;
                    source.Play();
                    Debug.Log($"[BandARSpawner] Đã phát thành công bản ghi âm {targetRec.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[BandARSpawner] Không tìm thấy bản ghi âm {audioId} trên Firestore!");
            }
        }
    }

    private void OnDestroy()
    {
        ClearSpawnedMembers();
    }
}
