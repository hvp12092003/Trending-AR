using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;

/// <summary>
/// Component riêng dùng để tạo (instantiate) hoặc di chuyển một Prefab tại vị trí người dùng click hoặc chạm (tap) 
/// trong môi trường giả lập (Non-AR) trực tiếp theo mặt phẳng trước Camera.
/// </summary>
public class TapToPlacePrefabNonAR : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField]
    [Tooltip("Prefab sẽ được tạo ra khi người dùng click/chạm.")]
    private GameObject m_PrefabToSpawn;

    /// <summary>
    /// Prefab sẽ được tạo ra khi người dùng click/chạm.
    /// </summary>
    public GameObject prefabToSpawn
    {
        get => m_PrefabToSpawn;
        set => m_PrefabToSpawn = value;
    }

    public enum SpawnMode
    {
        SpawnMultiple, // Mỗi lần chạm sẽ tạo thêm một prefab mới
        MoveSingle     // Chỉ tạo một prefab duy nhất, các lần chạm sau sẽ di chuyển prefab đó
    }

    [Header("Spawn Settings")]
    [SerializeField]
    [Tooltip("Chế độ spawn: Tạo nhiều bản sao hay chỉ di chuyển một bản sao duy nhất.")]
    private SpawnMode m_SpawnMode = SpawnMode.SpawnMultiple;

    public SpawnMode spawnMode
    {
        get => m_SpawnMode;
        set => m_SpawnMode = value;
    }

    [SerializeField]
    [Tooltip("Nếu được bật, Prefab khi tạo ra sẽ tự động quay mặt về phía Camera.")]
    private bool m_RotateTowardsCamera = true;

    public bool rotateTowardsCamera
    {
        get => m_RotateTowardsCamera;
        set => m_RotateTowardsCamera = value;
    }

    [Header("Raycast Distance")]
    [SerializeField]
    [Tooltip("Khoảng cách spawn vật thể trước Camera.")]
    private float m_SpawnDistance = 5f;

    [Header("Drop Animation Settings")]
    [SerializeField]
    [Tooltip("Độ cao Y mặc định mà nhân vật rơi xuống nếu Raycast không tìm thấy mặt đất.")]
    private float m_FallbackGroundY = 0f;

    [SerializeField]
    [Tooltip("Độ cao rơi tối thiểu (m). Nâng nhân vật lên cao so với mặt sàn nếu thả quá sát sàn.")]
    private float m_MinDropHeight = 0.8f;

    [SerializeField]
    [Tooltip("Khoảng cách dò tìm mặt đất tối đa (m).")]
    private float m_MaxRaycastDistance = 20f;

    [SerializeField]
    [Tooltip("Thời gian thực hiện chuyển động rơi tiếp đất (s).")]
    private float m_DropDuration = 0.45f;

    public float spawnDistance
    {
        get => m_SpawnDistance;
        set => m_SpawnDistance = value;
    }

    [SerializeField]
    [Tooltip("Tỉ lệ scale của nhân vật khi được kéo ra đặt vào Non-AR.")]
    private Vector3 m_PlacedScale = new Vector3(0.5f, 0.5f, 0.5f);

    private GameObject m_DraggedObject;
    private bool m_IsDragging = false;
    private Vector3 m_OriginalDraggedScale = Vector3.zero;

    // Cache variables to avoid GC allocations
    private PointerEventData m_PointerEventData;
    private List<RaycastResult> m_RaycastResultsCache = new List<RaycastResult>();
    private Camera m_MainCamera;
    private Joystick m_Joystick;

    private void Awake()
    {
        m_MainCamera = Camera.main;
        m_Joystick = FindFirstObjectByType<Joystick>();
    }

    private void Update()
    {
        Vector2 pointerPos = GetPointerPosition();

        // 1. Bắt đầu nhấn giữ (Touch/Mouse Down) - Raycast chọn nhân vật trong container để kéo
        if (IsPointerPressedThisFrame())
        {
            if (IsPointerOverUI(pointerPos)) return;

            if (m_MainCamera == null) m_MainCamera = Camera.main;

            if (m_MainCamera != null)
            {
                Ray ray = m_MainCamera.ScreenPointToRay(pointerPos);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Move characterMove = hit.collider.GetComponentInParent<Move>();
                    if (characterMove != null)
                    {
                        // Kiểm tra xem nhân vật có thuộc bệ đứng của spawner hay không trước khi cho phép kéo
                        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
                        if (spawner != null)
                        {
                            bool canDrag = false;

                            if (spawner.Pedestals != null && spawner.Pedestals.Count > 0)
                            {
                                if (characterMove.transform.parent != null &&
                                    spawner.Pedestals.Contains(characterMove.transform.parent.gameObject))
                                {
                                    canDrag = true;
                                }
                            }
                            else
                            {
                                canDrag = true; // Fallback nếu không sử dụng bệ đứng
                            }

                            if (canDrag)
                            {
                                m_DraggedObject = characterMove.gameObject;
                                m_IsDragging = true;

                                // Chọn nhân vật làm active
                                if (CharacterManager.Instance != null)
                                {
                                    CharacterManager.Instance.SelectCharacter(m_DraggedObject);
                                }

                                // Tách nhân vật ra khỏi bệ của spawner. Bệ chỉ tự ẩn khi đã đặt đủ 4 Cast.
                                spawner.DetachMember(m_DraggedObject);

                                // Đặt scale theo m_PlacedScale ngay khi kéo khỏi bệ đứng và lưu lại
                                m_OriginalDraggedScale = m_PlacedScale;
                                m_DraggedObject.transform.localScale = m_OriginalDraggedScale;
                            }
                            else
                            {
                                // Chọn nhân vật làm active khi nhấp chuột/chạm tay vào nhân vật đã đặt trong Non-AR
                                if (CharacterManager.Instance != null)
                                {
                                    CharacterManager.Instance.SelectCharacter(characterMove.gameObject);
                                }
                            }
                        }
                    }
                }
            }
        }

        // 2. Trong khi giữ và kéo (Touch/Mouse Dragging)
        if (m_IsDragging && m_DraggedObject != null)
        {
            UpdateDragPosition(pointerPos);
        }

        // 3. Khi thả tay ra (Touch/Mouse Up)
        if (m_IsDragging && IsPointerReleasedThisFrame())
        {
            if (m_DraggedObject != null)
            {
                UpdateDragPosition(pointerPos);

                Vector3 currentPos = m_DraggedObject.transform.position;

                // Dùng chính vị trí tay thả làm điểm đáp xuống
                // (không snap về mặt đất để mỗi Cast có thể đặt ở độ cao / góc màn hình khác nhau)
                Vector3 finalPos = currentPos;

                // Điểm bắt đầu rơi: nâng lên trên một chút để tạo hiệu ứng rơi từ trên xuống
                Vector3 startPos = finalPos;
                startPos.y += m_MinDropHeight;

                // Hủy hẳn parent để cố định nhân vật trong thế giới giả lập
                m_DraggedObject.transform.SetParent(null);
                m_DraggedObject.transform.position = startPos;
                m_DraggedObject.transform.localScale = m_PlacedScale;

                // Thực hiện hiệu ứng rơi tự do có nảy nhẹ bằng DOTween
                m_DraggedObject.transform.DOKill();
                m_DraggedObject.transform.DOMove(finalPos, m_DropDuration).SetEase(Ease.OutBounce);

                Debug.Log($"[TapToPlacePrefabNonAR] Đã thả và kích hoạt hiệu ứng rơi cho nhân vật: {m_DraggedObject.name}");

                // Cập nhật lại lựa chọn trong CharacterManager để đồng bộ UI (Slider scale)
                if (CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter == m_DraggedObject)
                {
                    CharacterManager.Instance.SelectCharacter(m_DraggedObject);
                }

                // Chạy animation nhảy được cấu hình
                Move moveScript = m_DraggedObject.GetComponent<Move>();
                if (moveScript != null)
                {
                    moveScript.PlayDance(0.15f);
                }

                // Phát nhạc nhạc cụ khi Cast được thả ra thế giới Non-AR
                CastAudioData castAudio = m_DraggedObject.GetComponent<CastAudioData>();
                if (castAudio != null)
                {
                    castAudio.PlayAudio();
                    Debug.Log($"[TapToPlacePrefabNonAR] Đã kích hoạt phát nhạc cho: {m_DraggedObject.name} (audioId: {castAudio.audioId})");
                }
            }

            m_DraggedObject = null;
            m_IsDragging = false;
            m_OriginalDraggedScale = Vector3.zero;
        }
    }

    private void UpdateDragPosition(Vector2 pointerPos)
    {
        if (m_DraggedObject == null || m_MainCamera == null) return;

        float distance = m_SpawnDistance > 0 ? m_SpawnDistance : 5f;
        Vector3 spawnPosition = m_MainCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, distance));
        
        BandARSpawner spawner = FindFirstObjectByType<BandARSpawner>();
        Vector3 targetScale = m_OriginalDraggedScale != Vector3.zero ? m_OriginalDraggedScale : (spawner != null ? spawner.PedestalLocalScale : new Vector3(0.02f, 0.02f, 0.02f));
        
        Quaternion targetRot = Quaternion.identity;

        if (m_RotateTowardsCamera)
        {
            Vector3 dirToCam = m_MainCamera.transform.position - spawnPosition;
            dirToCam.y = 0;
            if (dirToCam != Vector3.zero)
            {
                targetRot = Quaternion.LookRotation(dirToCam);
            }
        }

        m_DraggedObject.transform.position = spawnPosition;
        m_DraggedObject.transform.rotation = targetRot;
        m_DraggedObject.transform.localScale = targetScale;
    }

    private Vector2 GetPointerPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            return Touchscreen.current.touches[0].position.ReadValue();
        }
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        return Vector2.zero;
    }

    private bool IsPointerPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            return Touchscreen.current.touches[0].press.isPressed;
        }
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
        return false;
    }

    private bool IsPointerPressedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            return Touchscreen.current.touches[0].press.wasPressedThisFrame;
        }
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
        return false;
    }

    private bool IsPointerReleasedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            return Touchscreen.current.touches[0].press.wasReleasedThisFrame;
        }
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasReleasedThisFrame;
        }
        return false;
    }

    private bool IsTouchOnJoystick(Vector2 screenPosition)
    {
        if (m_Joystick == null)
        {
            m_Joystick = FindFirstObjectByType<Joystick>();
        }

        if (m_Joystick != null && m_Joystick.gameObject.activeInHierarchy)
        {
            RectTransform rectTransform = m_Joystick.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Canvas canvas = m_Joystick.GetComponentInParent<Canvas>();
                Camera checkCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, checkCam))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (IsTouchOnJoystick(screenPosition))
        {
            return true;
        }

        if (Touchscreen.current != null)
        {
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (t.press.isPressed)
                {
                    int fingerId = t.touchId.ReadValue();
                    if (EventSystem.current.IsPointerOverGameObject(fingerId))
                    {
                        return true;
                    }
                }
            }
        }

        if (EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        if (m_PointerEventData == null)
        {
            m_PointerEventData = new PointerEventData(EventSystem.current);
        }

        m_PointerEventData.position = screenPosition;
        m_RaycastResultsCache.Clear();

        EventSystem.current.RaycastAll(m_PointerEventData, m_RaycastResultsCache);
        return m_RaycastResultsCache.Count > 0;
    }
}
