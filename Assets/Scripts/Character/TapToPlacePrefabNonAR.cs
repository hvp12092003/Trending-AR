using System.Collections;
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



    private bool m_IsDraggingPlacedObject = false;
    private float m_DragDistance = 0f;
    private Vector3 m_DragOffset = Vector3.zero;

    private GameObject m_DraggedObject;
    private bool m_IsDragging = false;
    private Vector3 m_OriginalDraggedScale = Vector3.zero;

    // Phân biệt Tap và Drag cho nhân vật đã đặt
    private Vector2 m_StartPressPos;
    private bool m_MaybeDraggingPlacedObject = false;
    private float m_DragThreshold = 15f; // Ngưỡng pixel để bắt đầu kéo nhân vật đã đặt
    private GameObject m_PendingDragObject;
    private Move m_PendingDragMove;
    private CastAudioData m_PendingDragAudio;
    private CastPlacementState m_PendingDragPlacementState;

    // Cache variables to avoid GC allocations
    private PointerEventData m_PointerEventData;
    private List<RaycastResult> m_RaycastResultsCache = new List<RaycastResult>();
    private Camera m_MainCamera;
    private Joystick m_Joystick;
    private BandARSpawner m_BandSpawner;
    private BandPanelController m_BandPanel;
    private CustomCharacterPanelController m_CustomPanel;
    private Move m_DraggedMove;
    private CastAudioData m_DraggedAudio;
    private CastPlacementState m_DraggedPlacementState;

    private void Awake()
    {
        m_MainCamera = Camera.main;
        m_Joystick = FindFirstObjectByType<Joystick>();
        m_BandSpawner = FindFirstObjectByType<BandARSpawner>();
    }

    private void Start()
    {
        if (DeleteAreaUI.Instance == null)
        {
            DeleteAreaUI.Instance = FindFirstObjectByType<DeleteAreaUI>(FindObjectsInactive.Include);
        }
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
                        BandARSpawner spawner = GetBandSpawner();
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
                                m_DraggedMove = characterMove;
                                m_DraggedAudio = m_DraggedObject.GetComponent<CastAudioData>();
                                m_DraggedPlacementState = m_DraggedObject.GetComponent<CastPlacementState>();
                                m_IsDragging = true;
                                m_IsDraggingPlacedObject = false;

                                if (DeleteAreaUI.Instance != null)
                                {
                                    DeleteAreaUI.Instance.Show();
                                }

                                // Bỏ chọn nhân vật để ẩn tất cả UI (Joystick & Scale) khi kéo từ bệ
                                if (CharacterManager.Instance != null)
                                {
                                    CharacterManager.Instance.DeselectCharacter();
                                }

                                // Tách nhân vật ra khỏi bệ của spawner. Bệ chỉ tự ẩn khi đã đặt đủ 4 Cast.
                                spawner.DetachMember(m_DraggedObject);

                                // Đặt scale theo GetPlacedScale() ngay khi kéo khỏi bệ đứng và lưu lại
                                m_OriginalDraggedScale = GetPlacedScale();
                                m_DraggedObject.transform.localScale = m_OriginalDraggedScale;
                            }
                            else
                            {
                                // Nhân vật đã được đặt trước đó (hoặc không ở trên bệ)
                                if (IsARUIDisplayedNormally())
                                {
                                    // Lưu lại vị trí chạm để kiểm tra threshold phân biệt kéo/chạm
                                    m_StartPressPos = pointerPos;
                                    m_MaybeDraggingPlacedObject = true;
                                    m_PendingDragObject = characterMove.gameObject;
                                    m_PendingDragMove = characterMove;
                                    m_PendingDragAudio = m_PendingDragObject.GetComponent<CastAudioData>();
                                    m_PendingDragPlacementState = m_PendingDragObject.GetComponent<CastPlacementState>();
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
                    else
                    {
                        // Chạm trúng đối tượng khác không phải Cast -> Bỏ chọn nhân vật
                        if (CharacterManager.Instance != null)
                        {
                            CharacterManager.Instance.DeselectCharacter();
                        }
                    }
                }
                else
                {
                    // Chạm ra ngoài khoảng không -> Bỏ chọn nhân vật
                    if (CharacterManager.Instance != null)
                    {
                        CharacterManager.Instance.DeselectCharacter();
                    }
                }
            }
        }

        // 1.5 Kiểm tra xem có bắt đầu kéo nhân vật đã đặt hay không (nếu di chuyển qua threshold)
        if (m_MaybeDraggingPlacedObject && !m_IsDragging && m_PendingDragObject != null)
        {
            if (Vector2.Distance(pointerPos, m_StartPressPos) > m_DragThreshold)
            {
                m_DraggedObject = m_PendingDragObject;
                m_DraggedMove = m_PendingDragMove;
                m_DraggedAudio = m_PendingDragAudio;
                m_DraggedPlacementState = m_PendingDragPlacementState;
                m_IsDragging = true;
                m_IsDraggingPlacedObject = true;

                if (DeleteAreaUI.Instance != null)
                {
                    DeleteAreaUI.Instance.Show();
                }

                m_OriginalDraggedScale = m_DraggedObject.transform.localScale;
                m_DragDistance = Vector3.Distance(m_MainCamera.transform.position, m_DraggedObject.transform.position);
                Vector3 initialSpawnPos = m_MainCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, m_DragDistance));
                m_DragOffset = m_DraggedObject.transform.position - initialSpawnPos;

                // Bỏ chọn nhân vật để ẩn tất cả UI (Joystick & Scale) khi bắt đầu kéo
                if (CharacterManager.Instance != null)
                {
                    CharacterManager.Instance.DeselectCharacter();
                }

                m_MaybeDraggingPlacedObject = false;
                m_PendingDragObject = null;
                m_PendingDragMove = null;
                m_PendingDragAudio = null;
                m_PendingDragPlacementState = null;
            }
        }

        // 2. Trong khi giữ và kéo (Touch/Mouse Dragging)
        if (m_IsDragging && m_DraggedObject != null)
        {
            UpdateDragPosition(pointerPos);
            if (DeleteAreaUI.Instance != null)
            {
                DeleteAreaUI.Instance.UpdateHoverState(pointerPos);
            }

            // Báo cho CharacterManager reset auto-hide timer vì đang di chuyển nhân vật
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.ResetAutoHideTimer();
            }
        }

        // 3. Khi thả tay ra (Touch/Mouse Up)
        if (IsPointerReleasedThisFrame())
        {
            if (m_IsDragging && m_DraggedObject != null)
            {
                GameObject droppedObject = m_DraggedObject;
                Move droppedMove = m_DraggedMove;
                CastAudioData droppedAudio = m_DraggedAudio;
                CastPlacementState droppedPlacementState = m_DraggedPlacementState;
                bool isPlacedObject = m_IsDraggingPlacedObject;

                UpdateDragPosition(pointerPos);

                if (DeleteAreaUI.Instance != null && DeleteAreaUI.Instance.IsPointerInsideDeleteArea(pointerPos))
                {
                    // Trả nhân vật về bệ đỡ
                    BandARSpawner spawner = GetBandSpawner();
                    if (spawner != null)
                    {
                        spawner.ReturnMemberToPedestal(droppedObject);
                    }
                }
                else
                {
                    Vector3 currentPos = m_DraggedObject.transform.position;

                    // Dùng chính vị trí tay thả làm điểm đáp xuống
                    Vector3 finalPos = currentPos;

                    // Điểm bắt đầu rơi: nâng lên trên một chút để tạo hiệu ứng rơi từ trên xuống
                    Vector3 startPos = finalPos;
                    startPos.y += GetMinDropHeight();

                    // Hủy hẳn parent để cố định nhân vật trong thế giới giả lập
                    m_DraggedObject.transform.SetParent(null);
                    m_DraggedObject.transform.position = startPos;

                    // Để tránh giật scale, nếu là nhân vật đã đặt trước đó thì giữ nguyên scale hiện tại,
                    // ngược lại mới gán m_PlacedScale cho nhân vật mới đặt
                    if (isPlacedObject)
                    {
                        m_DraggedObject.transform.localScale = m_OriginalDraggedScale;
                    }
                    else
                    {
                        m_DraggedObject.transform.localScale = GetPlacedScale();
                    }

                    if (!isPlacedObject && droppedPlacementState != null)
                    {
                        droppedPlacementState.MarkPlaced();
                    }

                    // Thực hiện hiệu ứng rơi tự do có nảy nhẹ bằng DOTween
                    m_DraggedObject.transform.DOKill();
                    m_DraggedObject.transform.DOMove(finalPos, GetDropDuration()).SetEase(Ease.OutBounce);

                    if (isPlacedObject)
                    {
#if UNITY_EDITOR
                        Debug.Log($"[TapToPlacePrefabNonAR] Đã thả và kích hoạt hiệu ứng rơi cho nhân vật cũ: {droppedObject.name}");
#endif
                        StartCoroutine(CompleteDropPlacedObjectNextFrame(droppedObject, droppedMove));
                    }
                    else
                    {
#if UNITY_EDITOR
                        Debug.Log($"[TapToPlacePrefabNonAR] Đã thả và kích hoạt hiệu ứng rơi cho nhân vật mới: {droppedObject.name}");
#endif
                        StartCoroutine(CompleteDropNextFrame(droppedObject, droppedMove, droppedAudio));
                    }
                }
            }
            else if (m_MaybeDraggingPlacedObject && m_PendingDragObject != null)
            {
                // Chạm nhẹ và thả mà không vượt qua threshold -> Chọn nhân vật
                if (CharacterManager.Instance != null)
                {
                    CharacterManager.Instance.SelectCharacter(m_PendingDragObject);
                }
            }

            m_DraggedObject = null;
            m_DraggedMove = null;
            m_DraggedAudio = null;
            m_DraggedPlacementState = null;
            m_IsDragging = false;
            m_IsDraggingPlacedObject = false;
            m_OriginalDraggedScale = Vector3.zero;

            m_MaybeDraggingPlacedObject = false;
            m_PendingDragObject = null;
            m_PendingDragMove = null;
            m_PendingDragAudio = null;
            m_PendingDragPlacementState = null;

            if (DeleteAreaUI.Instance != null)
            {
                DeleteAreaUI.Instance.Hide();
            }
        }
    }

    private void UpdateDragPosition(Vector2 pointerPos)
    {
        if (m_DraggedObject == null || m_MainCamera == null) return;

        BandARSpawner spawner = GetBandSpawner();
        Vector3 targetScale = m_OriginalDraggedScale != Vector3.zero ? m_OriginalDraggedScale : (spawner != null ? spawner.PedestalLocalScale : new Vector3(0.02f, 0.02f, 0.02f));

        if (m_IsDraggingPlacedObject)
        {
            Vector3 targetPos = m_MainCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, m_DragDistance)) + m_DragOffset;
            Quaternion targetRot = m_DraggedObject.transform.rotation;

            if (m_RotateTowardsCamera)
            {
                Vector3 dirToCam = m_MainCamera.transform.position - targetPos;
                dirToCam.y = 0;
                if (dirToCam != Vector3.zero)
                {
                    targetRot = Quaternion.LookRotation(dirToCam);
                }
            }

            m_DraggedObject.transform.position = targetPos;
            m_DraggedObject.transform.rotation = targetRot;
            m_DraggedObject.transform.localScale = targetScale;
            return;
        }

        float distance = m_SpawnDistance > 0 ? m_SpawnDistance : 5f;
        Vector3 spawnPosition = m_MainCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, distance));
        
        Quaternion targetRotNormal = Quaternion.identity;

        if (m_RotateTowardsCamera)
        {
            Vector3 dirToCam = m_MainCamera.transform.position - spawnPosition;
            dirToCam.y = 0;
            if (dirToCam != Vector3.zero)
            {
                targetRotNormal = Quaternion.LookRotation(dirToCam);
            }
        }

        m_DraggedObject.transform.position = spawnPosition;
        m_DraggedObject.transform.rotation = targetRotNormal;
        m_DraggedObject.transform.localScale = targetScale;
    }

    private BandARSpawner GetBandSpawner()
    {
        if (m_BandSpawner == null)
        {
            m_BandSpawner = FindFirstObjectByType<BandARSpawner>();
        }
        return m_BandSpawner;
    }

    private Vector3 GetPlacedScale()
    {
        var spawner = GetBandSpawner();
        return spawner != null ? spawner.PlacedScale : m_PlacedScale;
    }

    private float GetMinDropHeight()
    {
        var spawner = GetBandSpawner();
        return spawner != null ? spawner.MinDropHeight : m_MinDropHeight;
    }

    private float GetDropDuration()
    {
        var spawner = GetBandSpawner();
        return spawner != null ? spawner.DropDuration : m_DropDuration;
    }

    private IEnumerator CompleteDropNextFrame(GameObject droppedObject, Move moveScript, CastAudioData castAudio)
    {
        yield return null;

        if (droppedObject == null)
        {
            yield break;
        }

        // Khi thả nhân vật, bỏ chọn nhân vật để ẩn các UI điều khiển và vòng chân chỉ báo
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.DeselectCharacter();
        }

        if (moveScript != null)
        {
            moveScript.PlayDance(0.15f);
        }

        if (castAudio != null)
        {
            castAudio.PlayAudio();
        }

        yield return null;

        if (droppedObject != null)
        {
            BandARSpawner spawner = GetBandSpawner();
            if (spawner != null)
            {
                spawner.TryAwardCustomCastUsePoints(droppedObject);
                spawner.CheckAndLimitCasts();
            }
            BandARSpawner.NotifyCastPlaced(droppedObject);
        }
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

    private IEnumerator CompleteDropPlacedObjectNextFrame(GameObject droppedObject, Move moveScript)
    {
        yield return null;

        if (droppedObject == null)
        {
            yield break;
        }

        // Khi thả nhân vật, bỏ chọn nhân vật để ẩn các UI điều khiển và vòng chân chỉ báo
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.DeselectCharacter();
        }

        if (moveScript != null)
        {
            moveScript.PlayDance(0.15f);
        }
    }

    private bool IsARUIDisplayedNormally()
    {
        var bandPanel = GetBandPanel();
        if (bandPanel != null)
        {
            return bandPanel.IsArUiActive && !bandPanel.IsUiHidden;
        }

        var customPanel = GetCustomPanel();
        if (customPanel != null)
        {
            return customPanel.IsArUiActive && !customPanel.IsUiHidden;
        }

        return true; // Fallback
    }

    private BandPanelController GetBandPanel()
    {
        if (m_BandPanel == null)
        {
            m_BandPanel = FindFirstObjectByType<BandPanelController>();
        }
        return m_BandPanel;
    }

    private CustomCharacterPanelController GetCustomPanel()
    {
        if (m_CustomPanel == null)
        {
            m_CustomPanel = FindFirstObjectByType<CustomCharacterPanelController>();
        }
        return m_CustomPanel;
    }
}
