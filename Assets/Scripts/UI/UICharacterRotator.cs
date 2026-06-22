using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Cho phép xoay nhân vật 3D preview bằng cách vuốt/kéo (drag) trên vùng RawImage của Canvas.
/// </summary>
public class UICharacterRotator : MonoBehaviour, IDragHandler
{
    [Tooltip("Transform của nhân vật hoặc Container chứa nhân vật cần xoay.")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("Tốc độ xoay khi kéo chuột/tay.")]
    [SerializeField] private float rotationSpeed = 0.4f;

    /// <summary>
    /// Thiết lập động đối tượng xoay (nếu cần gán lúc runtime).
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    /// <summary>
    /// Reset góc xoay về mặc định.
    /// </summary>
    public void ResetRotation()
    {
        if (targetTransform != null)
        {
            targetTransform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Xử lý sự kiện kéo (Drag) từ EventSystem của Unity.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (targetTransform != null)
        {
            // Xoay quanh trục Y (lên trên) dựa theo khoảng cách di chuyển ngang (delta.x)
            // Dấu trừ (-) giúp chiều xoay tự nhiên hơn theo hướng vuốt
            targetTransform.Rotate(Vector3.up, -eventData.delta.x * rotationSpeed, Space.Self);
        }
    }
}
