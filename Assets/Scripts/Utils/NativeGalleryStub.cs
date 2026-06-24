// NativeGallery Stub – thay thế tạm thời khi chưa cài plugin chính thức.
// Để sử dụng NativeGallery thật: cài package từ
//   https://github.com/yasirkula/UnityNativeGallery/releases
// sau đó XÓA file stub này đi.

using System;
using UnityEngine;

/// <summary>
/// Stub class cho NativeGallery. Cung cấp đúng API signature để project compile được.
/// Chức năng chọn ảnh từ thư viện sẽ không khả dụng cho đến khi cài plugin thật.
/// </summary>
public static class NativeGallery
{
    /// <summary>
    /// Callback khi người dùng chọn hoặc huỷ chọn media.
    /// </summary>
    public delegate void MediaPickCallback(string path);

    /// <summary>
    /// Mở thư viện ảnh để người dùng chọn ảnh (stub – không làm gì).
    /// </summary>
    public static Permission GetImageFromGallery(
        MediaPickCallback callback,
        string title           = "",
        string mime            = "image/*",
        int    maxSize         = -1)
    {
        Debug.LogWarning("[NativeGallery Stub] Plugin NativeGallery chưa được cài đặt.\n" +
                         "Tải plugin tại: https://github.com/yasirkula/UnityNativeGallery/releases");
        callback?.Invoke(null);
        return Permission.Denied;
    }

    /// <summary>
    /// Load ảnh từ đường dẫn (stub – luôn trả về null).
    /// </summary>
    public static Texture2D LoadImageAtPath(
        string imagePath,
        int    maxSize                = -1,
        bool   markTextureNonReadable = true)
    {
        Debug.LogWarning("[NativeGallery Stub] LoadImageAtPath không khả dụng (plugin chưa được cài).");
        return null;
    }

    public enum Permission { Denied = 0, Granted = 1, ShouldAsk = 2 }
}
