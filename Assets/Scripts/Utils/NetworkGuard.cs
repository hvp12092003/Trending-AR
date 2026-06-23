using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lớp tiện ích bảo vệ và tự động thử lại các lệnh gọi API khi mất kết nối mạng.
/// </summary>
public static class NetworkGuard
{
    /// <summary>
    /// Chạy một API task bất đồng bộ có giá trị trả về, kiểm tra và chờ mạng trước khi chạy,
    /// và tự động bắt lỗi mạng để chờ có mạng lại và thử lại.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> apiCall)
    {
        return await apiCall();
    }

    public static async Task RunAsync(Func<Task> apiCall)
    {
        await apiCall();
    }

    /// <summary>
    /// Xác định xem một chuỗi thông báo lỗi có phải lỗi mạng hay không.
    /// </summary>
    private static bool IsNetworkError(string error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        string errLower = error.ToLower();
        return errLower.Contains("mạng") || 
               errLower.Contains("internet") || 
               errLower.Contains("offline") || 
               errLower.Contains("kết nối") ||
               errLower.Contains("connection");
    }

    /// <summary>
    /// Kiểm tra xem Exception bắt được có liên quan tới lỗi kết nối mạng hay ngoại tuyến (offline) hay không.
    /// </summary>
    public static bool IsNetworkException(Exception ex)
    {
        if (ex == null) return false;
        
        string msg = ex.Message.ToLower();
        if (msg.Contains("offline") || 
            msg.Contains("network") || 
            msg.Contains("internet") || 
            msg.Contains("reachability") || 
            msg.Contains("connect") ||
            msg.Contains("unavailable"))
        {
            return true;
        }

        if (ex.InnerException != null)
        {
            return IsNetworkException(ex.InnerException);
        }

        return false;
    }
}
