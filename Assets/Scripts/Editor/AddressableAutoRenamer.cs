using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.IO;

/// <summary>
/// Công cụ tự động đổi tên Addressable Name cho nhân vật (cast/*) và nhạc cụ (instrument/*) theo đúng cấu hình Catalog.
/// </summary>
public static class AddressableAutoRenamer
{
    [MenuItem("Tools/Trending AR/Auto Rename Addressables")]
    public static void RenameAddressables()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAutoRenamer] Không tìm thấy Addressable Asset Settings!");
            return;
        }

        int count = 0;
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;

            // Sao chép danh sách để tránh lỗi sửa đổi danh sách khi đang duyệt
            var entries = new System.Collections.Generic.List<AddressableAssetEntry>(group.entries);
            foreach (AddressableAssetEntry entry in entries)
            {
                if (entry == null) continue;

                string path = entry.AssetPath;
                if (string.IsNullOrEmpty(path)) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                string newAddress = null;

                // Kiểm tra nếu là nhân vật (Cast)
                if (path.StartsWith("Assets/Prefabs/Cast/", System.StringComparison.OrdinalIgnoreCase) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    newAddress = "cast/" + fileName;
                }
                // Kiểm tra nếu là nhạc cụ (Intrument)
                else if (path.StartsWith("Assets/Prefabs/Intrument/", System.StringComparison.OrdinalIgnoreCase) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    newAddress = "instrument/" + fileName;
                }

                if (newAddress != null && entry.address != newAddress)
                {
                    string oldAddress = entry.address;
                    entry.address = newAddress;
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
                    Debug.Log($"[AddressableAutoRenamer] Đã đổi tên: '{oldAddress}' thành '{newAddress}'");
                    count++;
                }
            }
        }

        if (count > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressableAutoRenamer] Thành công! Đã tự động sửa định dạng tên cho {count} Addressable Entries.");
            EditorUtility.DisplayDialog("Auto Rename Addressables", $"Đã sửa định dạng Addressable Name thành công cho {count} entries!", "OK");
        }
        else
        {
            Debug.Log("[AddressableAutoRenamer] Không có Addressable Entries nào cần sửa định dạng.");
            EditorUtility.DisplayDialog("Auto Rename Addressables", "Tất cả các Addressable Entries đã đúng định dạng (cast/* hoặc instrument/*). Không cần đổi tên!", "OK");
        }
    }
}
