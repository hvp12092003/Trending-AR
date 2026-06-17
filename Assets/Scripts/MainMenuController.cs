using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Panel hiển thị đè màn hình khi đang tải / kiểm tra thiết bị")]
    public GameObject loadingOverlay;

    [Tooltip("Text hiển thị trạng thái kiểm tra")]
    public TMPro.TextMeshProUGUI loadingText;

    private void Start()
    {
        // Tự động kiểm tra độ tương thích AR và chuyển Scene ngay khi bắt đầu
        StartCoroutine(CheckARAndLoadScene());
    }

    /// <summary>
    /// Hàm gọi khi nhấn nút chọn game AR
    /// </summary>
    public void PlayARGame()
    {
        StartCoroutine(CheckARAndLoadScene());
    }
    private IEnumerator CheckARAndLoadScene()
    {
        // 1. Hiển thị Loading Overlay và cập nhật thông báo
        if (loadingOverlay != null)
        {
            loadingOverlay.SetActive(true);
        }

        if (loadingText != null)
            loadingText.text = "Đang kiểm tra độ tương thích ARCore...";

        yield return new WaitForSeconds(0.5f); // Tạo độ trễ nhẹ cho hiệu ứng mượt mà

#if UNITY_EDITOR
        // Trong Unity Editor, tải trực tiếp Scene AR để phát triển/test bằng AR Simulator
        if (loadingText != null)
            loadingText.text = "Đang khởi động camera AR (Unity Editor)...";
        
        yield return new WaitForSeconds(0.8f);
        SceneManager.LoadScene("AR Scene");
#else
        // 2. Chạy Coroutine kiểm tra trạng thái tương thích ARCore trên thiết bị thật
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        Debug.Log($"MainMenuController: Trạng thái AR Session = {ARSession.state}");

        // 3. Điều hướng dựa vào kết quả trên thiết bị thật
        if (ARSession.state == ARSessionState.Unsupported)
        {
            if (loadingText != null)
                loadingText.text = "Không hỗ trợ ARCore.\nĐang tải chế độ giả lập...";
            
            yield return new WaitForSeconds(0.8f);
            SceneManager.LoadScene("Non-AR Scene");
        }
        else
        {
            if (loadingText != null)
                loadingText.text = "Thiết bị tương thích ARCore.\nĐang khởi động camera AR...";
            
            yield return new WaitForSeconds(0.8f);
            SceneManager.LoadScene("AR Scene");
        }
#endif
    }
}
