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

    private GameObject m_SpawnedObject;

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
        // Kiểm tra xem có click chuột (Editor) hoặc chạm màn hình (Mobile) hay không
        if (GetInputPose(out Vector2 touchPosition))
        {
            // Kiểm tra xem người dùng có đang chạm vào UI (Nút bấm, Menu...) hay không để tránh spawn đè lên UI
            if (IsPointerOverUI(touchPosition))
            {
                return;
            }

            // 1. Kiểm tra xem người dùng có chạm/click trúng nhân vật (có Collider & script Move) hay không
            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
            }

            if (m_MainCamera != null)
            {
                Ray ray = m_MainCamera.ScreenPointToRay(touchPosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // Lấy script Move trên nhân vật (hoặc cha của nó nếu va chạm trúng collider con)
                    Move characterMove = hit.collider.GetComponentInParent<Move>();
                    if (characterMove != null)
                    {
                        if (CharacterManager.Instance != null)
                        {
                            CharacterManager.Instance.SelectCharacter(characterMove.gameObject);
                            return; // Dừng lại ở đây, không thực hiện spawn/di chuyển
                        }
                    }
                }
            }

            // 2. Thực hiện tạo nhân vật trực tiếp theo mặt phẳng song song với màn hình camera
            if (m_MainCamera != null)
            {
                Vector3 spawnPosition = m_MainCamera.ScreenToWorldPoint(new Vector3(touchPosition.x, touchPosition.y, m_SpawnDistance));
                Pose hitPose = new Pose(spawnPosition, Quaternion.identity);
                HandleSpawning(hitPose);
            }
        }
    }

    /// <summary>
    /// Xử lý việc tạo mới hoặc di chuyển Prefab tùy theo chế độ SpawnMode.
    /// </summary>
    private void HandleSpawning(Pose hitPose)
    {
        // ── Kiểm tra xem có ban nhạc nào đang được chọn và có Spawner trong scene hay không ──
        BandARSpawner bandSpawner = FindFirstObjectByType<BandARSpawner>();
        if (BandSelectionManager.SelectedBand != null && bandSpawner != null)
        {
            Quaternion spawnRot = hitPose.rotation;
            if (m_RotateTowardsCamera)
            {
                if (m_MainCamera == null) m_MainCamera = Camera.main;
                if (m_MainCamera != null)
                {
                    Vector3 dirToCam = m_MainCamera.transform.position - hitPose.position;
                    dirToCam.y = 0;
                    if (dirToCam != Vector3.zero)
                    {
                        spawnRot = Quaternion.LookRotation(dirToCam);
                    }
                }
            }
            bandSpawner.SpawnBand(hitPose.position, spawnRot);
            return;
        }

        if (m_PrefabToSpawn == null)
        {
            Debug.LogWarning("TapToPlacePrefabNonAR: Chưa gán PrefabToSpawn trong Inspector!", this);
            return;
        }

        Quaternion spawnRotation = hitPose.rotation;

        // Nếu bật chế độ quay về phía Camera
        if (m_RotateTowardsCamera)
        {
            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
            }

            Camera mainCamera = m_MainCamera;
            if (mainCamera != null)
            {
                Vector3 cameraPosition = mainCamera.transform.position;
                // Chỉ lấy góc xoay theo trục Y (ngang) để vật thể không bị nghiêng lên/xuống
                Vector3 directionToCamera = cameraPosition - hitPose.position;
                directionToCamera.y = 0; // Giữ hướng ngang
                
                if (directionToCamera != Vector3.zero)
                {
                    spawnRotation = Quaternion.LookRotation(directionToCamera);
                }
            }
        }

        if (m_SpawnMode == SpawnMode.SpawnMultiple)
        {
            // Chế độ tạo nhiều: Mỗi lần chạm tạo ra một bản sao mới và phóng to dần từ 0
            GameObject newObj = Instantiate(m_PrefabToSpawn, hitPose.position, spawnRotation);

            Vector3 originalScale = newObj.transform.localScale;
            newObj.transform.localScale = Vector3.zero;
            newObj.transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutBack);

            // Tự động chọn nhân vật vừa tạo để điều khiển
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.SelectCharacter(newObj);
            }
        }
        else if (m_SpawnMode == SpawnMode.MoveSingle)
        {
            // Chế độ tạo một: Nếu chưa có thì tạo mới, nếu có rồi thì di chuyển đến vị trí mới
            if (m_SpawnedObject == null)
            {
                m_SpawnedObject = Instantiate(m_PrefabToSpawn, hitPose.position, spawnRotation);

                Vector3 originalScale = m_SpawnedObject.transform.localScale;
                m_SpawnedObject.transform.localScale = Vector3.zero;
                m_SpawnedObject.transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutBack);

                // Tự động chọn nhân vật vừa tạo để điều khiển
                if (CharacterManager.Instance != null)
                {
                    CharacterManager.Instance.SelectCharacter(m_SpawnedObject);
                }
            }
            else
            {
                // Smoothly di chuyển và quay bằng DOTween
                m_SpawnedObject.transform.DOKill();
                m_SpawnedObject.transform.DOMove(hitPose.position, 0.35f).SetEase(Ease.OutQuad);
                m_SpawnedObject.transform.DORotateQuaternion(spawnRotation, 0.35f).SetEase(Ease.OutQuad);

                // Cập nhật chọn nhân vật nếu cần
                if (CharacterManager.Instance != null && CharacterManager.Instance.SelectedCharacter != m_SpawnedObject)
                {
                    CharacterManager.Instance.SelectCharacter(m_SpawnedObject);
                }
            }
        }
    }

    /// <summary>
    /// Nhận thông tin điểm chạm từ chuột hoặc cảm ứng màn hình.
    /// </summary>
    private bool GetInputPose(out Vector2 touchPosition)
    {
        touchPosition = Vector2.zero;

        // 1. Kiểm tra Touch trên Mobile (hỗ trợ đa chạm khi đang giữ Joystick)
        if (Touchscreen.current != null)
        {
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.press.wasPressedThisFrame)
                {
                    touchPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }

        // 2. Kiểm tra Click chuột trên Editor để dễ dàng test
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            touchPosition = Mouse.current.position.ReadValue();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem điểm chạm có nằm trong khu vực Joystick hay không.
    /// </summary>
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

    /// <summary>
    /// Kiểm tra xem điểm chạm có đè lên phần tử UI nào hay không.
    /// </summary>
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        // 1. Kiểm tra nếu chạm trúng vùng Rect của Joystick
        if (IsTouchOnJoystick(screenPosition))
        {
            return true;
        }

        // 2. Kiểm tra bằng API chuẩn của EventSystem
        // Cho Mobile
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

        // Cho Editor
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        // 3. Fallback bằng RaycastAll
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
