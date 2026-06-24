using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lớp tiện ích bảo vệ các lệnh gọi task bất đồng bộ, có thể mở rộng để retry khi mất mạng.
/// </summary>
public static class NetworkGuard
{
    /// <summary>
    /// Chạy một async task có giá trị trả về.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> apiCall)
    {
        return await apiCall();
    }

    /// <summary>
    /// Chạy một async task không có giá trị trả về.
    /// </summary>
    public static async Task RunAsync(Func<Task> apiCall)
    {
        await apiCall();
    }

    /// <summary>
    /// Kiểm tra xem Exception có liên quan tới lỗi kết nối mạng hay không.
    /// </summary>
    public static bool IsNetworkException(Exception ex)
    {
        if (ex == null) return false;

        string msg = ex.Message.ToLower();
        if (msg.Contains("offline")      ||
            msg.Contains("network")      ||
            msg.Contains("internet")     ||
            msg.Contains("reachability") ||
            msg.Contains("connect")      ||
            msg.Contains("unavailable"))
        {
            return true;
        }

        return ex.InnerException != null && IsNetworkException(ex.InnerException);
    }
}
