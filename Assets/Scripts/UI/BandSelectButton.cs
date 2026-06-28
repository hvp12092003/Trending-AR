using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn vào các Button chọn Ban nhạc tĩnh được thiết kế sẵn trong Unity Editor.
/// Tự động cấu hình và lưu thông tin Ban nhạc được chọn sang MainMenuDataManager khi click,
/// kích hoạt spawn ban nhạc và tự động ẩn giao diện UIBand chứa ScrollView đi.
/// </summary>
[RequireComponent(typeof(Button))]
public class BandSelectButton : MonoBehaviour
{
    [Header("Band Configuration")]
    [Tooltip("Cấu hình thông tin ban nhạc chi tiết cho nút bấm này")]
    [SerializeField] private EditorBandData bandData;
    
    public EditorBandData BandData
    {
        get => bandData;
        set => bandData = value;
    }

    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnButtonClicked);
            btn.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (bandData == null || bandData.members == null || bandData.members.Count == 0)
        {
            Debug.LogWarning("[BandSelectButton] Nút bấm chưa được cấu hình dữ liệu ban nhạc hợp lệ!");
            return;
        }

        string displayBandName = GetDisplayBandName();
        Debug.Log($"[BandSelectButton] Đã chọn ban nhạc từ button thiết kế sẵn: {displayBandName}");

        // 1. Gán cấu hình ban nhạc cho MainMenuDataManager
        if (MainMenuDataManager.Instance != null)
        {
            MainMenuDataManager.Instance.selectedBandData = bandData;
        }

        // Đồng bộ ngược với BandData cũ để tương thích với các logic khác
        System.Collections.Generic.List<CastData> oldCastsList = new System.Collections.Generic.List<CastData>();
        System.Collections.Generic.List<Sprite> avatarsList = new System.Collections.Generic.List<Sprite>();
        
        foreach (var m in bandData.members)
        {
            if (m == null) continue;
            
            string prefabName = "";
            GameObject castPrefab = null;
            if (MainMenuDataManager.Instance != null && 
                m.castPrefabIndex >= 0 && 
                m.castPrefabIndex < MainMenuDataManager.Instance.CharacterPrefabs.Count)
            {
                castPrefab = MainMenuDataManager.Instance.CharacterPrefabs[m.castPrefabIndex];
                if (castPrefab != null) prefabName = castPrefab.name;
            }
            
            System.Collections.Generic.List<string> anims = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(m.danceAnimId)) anims.Add(m.danceAnimId);
            
            string displayCastName = GetCastDisplayName(castPrefab, oldCastsList.Count + 1);
            CastData cd = new CastData(displayCastName, prefabName, m.audioClip != null ? m.audioClip.name : "", m.danceAnimId, anims);
            oldCastsList.Add(cd);

            Sprite avatar = null;
            if (castPrefab != null)
            {
                CastPrefab cp = castPrefab.GetComponent<CastPrefab>();
                if (cp != null) avatar = cp.characterAvatar;
            }
            avatarsList.Add(avatar);
        }

        // Dùng tên hiển thị làm ID tạm thời do bandId đã bị xóa
        string bandId = string.IsNullOrEmpty(displayBandName) ? "editor_band" : displayBandName;
        BandSelectionManager.SelectedBand = new BandData(bandId, displayBandName, oldCastsList);
        BandSelectionManager.SelectedBandAvatars = avatarsList;
        BandSelectionManager.SelectedBandName = displayBandName;

        // 2. Kích hoạt spawner sinh ban nhạc ra Scene
        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
        if (spawner != null)
        {
            spawner.SpawnBandAtDefaultPosition();
        }
        else
        {
            Debug.LogError("[BandSelectButton] Không tìm thấy BandARSpawner trong Scene để spawn ban nhạc!");
        }

        // 3. Tự động tìm kiếm và ẩn giao diện chọn ban nhạc (UIBand / UI_Band)
        GameObject foundPanel = GameObject.Find("UIBand");
        if (foundPanel == null) foundPanel = GameObject.Find("UI_Band");
        
        if (foundPanel == null)
        {
            // Tìm kiếm trong tất cả root objects (kể cả object bị disable nếu spawner đã biết cha của nó)
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = root.transform.Find("UIBand");
                if (found == null) found = root.transform.Find("UI_Band");
                if (found != null)
                {
                    foundPanel = found.gameObject;
                    break;
                }
            }
        }

        if (foundPanel != null)
        {
            foundPanel.SetActive(false);
            Debug.Log($"[BandSelectButton] Đã ẩn giao diện chọn ban nhạc: {foundPanel.name}");
        }
        else
        {
            // Fallback: Duyệt tìm cha trực tiếp của button có tên chứa UIBand, Panel hoặc Canvas
            Transform parentPanel = transform.parent;
            bool parentHidden = false;
            while (parentPanel != null)
            {
                if (parentPanel.name.Contains("UIBand") || parentPanel.name.Contains("Panel") || parentPanel.name.Contains("Canvas"))
                {
                    parentPanel.gameObject.SetActive(false);
                    parentHidden = true;
                    Debug.Log($"[BandSelectButton] Fallback: Đã ẩn panel cha: {parentPanel.name}");
                    break;
                }
                parentPanel = parentPanel.parent;
            }

            if (!parentHidden)
            {
                // Fallback cuối cùng: ẩn chính ScrollView cha trực tiếp
                if (transform.parent != null)
                {
                    transform.parent.gameObject.SetActive(false);
                }
            }
        }
    }

    private string GetDisplayBandName()
    {
        if (bandData != null && !string.IsNullOrWhiteSpace(bandData.bandName))
        {
            return bandData.bandName;
        }

        return string.IsNullOrWhiteSpace(gameObject.name) ? "Editor Band" : gameObject.name;
    }

    private static string GetCastDisplayName(GameObject castPrefab, int index)
    {
        if (castPrefab != null)
        {
            CastPrefab config = castPrefab.GetComponent<CastPrefab>();
            if (config != null && !string.IsNullOrWhiteSpace(config.Name))
            {
                return config.Name;
            }

            if (!string.IsNullOrWhiteSpace(castPrefab.name))
            {
                return castPrefab.name;
            }
        }

        return $"Cast {index}";
    }
}
