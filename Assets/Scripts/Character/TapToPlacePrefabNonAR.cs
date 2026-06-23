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

    public float spawnDistance
    {
        get => m_SpawnDistance;
        set => m_SpawnDistance = value;
    }

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

                                // Tách nhân vật ra khỏi bệ của spawner (và ẩn bệ tương ứng)
                                spawner.DetachMember(m_DraggedObject);

                                // Lưu lại scale gốc của nhân vật sau khi tách khỏi bệ đứng
                                m_OriginalDraggedScale = m_DraggedObject.transform.localScale;
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

                // Hủy hẳn parent để cố định nhân vật trong thế giới giả lập
                m_DraggedObject.transform.SetParent(null);
                Debug.Log($"[TapToPlacePrefabNonAR] Đã thả cố định nhân vật: {m_DraggedObject.name}");
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
        Quaternion offsetRot = spawner != null ? Quaternion.Euler(spawner.PedestalLocalRotation) : Quaternion.Euler(-90f, 90f, 90f);
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
        m_DraggedObject.transform.rotation = targetRot * offsetRot;
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
