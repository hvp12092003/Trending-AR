using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Quản lý việc vuốt (swipe/drag) để di chuyển hàng bệ chứa Cast (Pedestals) theo phương ngang,
/// giúp người dùng xem được các Cast bị ẩn ở bên phải hoặc bên trái.
/// </summary>
public class PedestalsScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [Tooltip("Hệ số tốc độ cuộn (1.0 là khớp 1:1 với chuyển động tay trên màn hình)")]
    [SerializeField] private float scrollSpeedFactor = 1.8f;

    [Tooltip("Thời gian đàn hồi / giảm tốc mượt mà khi cuộn")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Fixed Limits Settings")]
    [Tooltip("Bật tùy chọn này để sử dụng khoảng giới hạn cố định thay vì tự động tính toán")]
    [SerializeField] private bool useFixedLimits = true;

    [Tooltip("Vị trí giới hạn cuộn sang trái tối đa (Giá trị cục bộ X tối thiểu)")]
    [SerializeField] private float fixedMinX = -5.0f;

    [Tooltip("Vị trí giới hạn cuộn sang phải tối đa (Giá trị cục bộ X tối đa)")]
    [SerializeField] private float fixedMaxX = 1.0f;

    private float _initialLocalX;
    private float _minLocalX = -5f;
    private float _maxLocalX = 0f;

    private float _targetLocalX;
    private float _currentLocalVelocity;
    private bool _isDragging = false;
    private Vector2 _lastPointerPos;

    private Camera _mainCamera;
    private Joystick _joystick;
    private bool _originalUseFixedLimits;
    private float _originalFixedMinX;
    private float _originalFixedMaxX;

    public float FixedMinX
    {
        get => fixedMinX;
        set => fixedMinX = value;
    }

    public float FixedMaxX
    {
        get => fixedMaxX;
        set => fixedMaxX = value;
    }

    private void Awake()
    {
        _initialLocalX = transform.localPosition.x;
        _targetLocalX = _initialLocalX;
        _originalUseFixedLimits = useFixedLimits;
        _originalFixedMinX = fixedMinX;
        _originalFixedMaxX = fixedMaxX;
    }

    public void SetUseFixedLimits(bool use)
    {
        useFixedLimits = use;
    }

    public void ResetUseFixedLimits()
    {
        useFixedLimits = _originalUseFixedLimits;
        fixedMinX = _originalFixedMinX;
        fixedMaxX = _originalFixedMaxX;
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        CalculateScrollLimits();
    }

    /// <summary>
    /// Tính toán hoặc áp dụng giới hạn cuộn (min X và max X cục bộ).
    /// </summary>
    public void CalculateScrollLimits()
    {
        // Đưa về vị trí ban đầu trước khi tính toán để các vị trí cục bộ không bị lệch
        Vector3 localPos = transform.localPosition;
        transform.localPosition = new Vector3(_initialLocalX, localPos.y, localPos.z);
        _targetLocalX = _initialLocalX;

        // Nếu sử dụng giới hạn cố định
        if (useFixedLimits)
        {
            _minLocalX = fixedMinX;
            _maxLocalX = fixedMaxX;
        }
        else
        {
            // Ngược lại, tự động tính toán dựa trên các bệ đứng active thực tế
            var children = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    children.Add(child);
                }
            }

            if (children.Count > 0)
            {
                children.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));

                float minChildX = children[0].localPosition.x;
                float maxChildX = children[children.Count - 1].localPosition.x;
                float width = maxChildX - minChildX;

                if (width > 0.01f)
                {
                    _minLocalX = _initialLocalX - width;
                    _maxLocalX = _initialLocalX + 0.5f;
                }
                else
                {
                    _minLocalX = _initialLocalX;
                    _maxLocalX = _initialLocalX;
                }
            }
            else
            {
                _minLocalX = _initialLocalX;
                _maxLocalX = _initialLocalX;
            }
        }

        // Bổ sung giới hạn bảo vệ tuyệt đối để ngăn kéo vô hạn
        _minLocalX = Mathf.Max(_minLocalX, -7.0f);
        _maxLocalX = Mathf.Min(_maxLocalX, 2.0f);

        Debug.Log($"[PedestalsScroller] Giới hạn cuộn cuối cùng: Min X = {_minLocalX}, Max X = {_maxLocalX}, useFixedLimits = {useFixedLimits}");
    }

    private void Update()
    {
        HandleInput();

        // Di chuyển mượt mà tới vị trí mục tiêu bằng SmoothDamp
        Vector3 localPos = transform.localPosition;
        float newX = Mathf.SmoothDamp(localPos.x, _targetLocalX, ref _currentLocalVelocity, smoothTime);
        transform.localPosition = new Vector3(newX, localPos.y, localPos.z);
    }

    private void HandleInput()
    {
        Vector2 pointerPos = GetPointerPosition();

        if (IsPointerPressedThisFrame())
        {
            // Không thực hiện cuộn nếu chạm vào UI tương tác
            if (IsPointerOverUI(pointerPos))
            {
                Debug.Log("[PedestalsScroller] Nhấp bị chặn: trúng UI tương tác.");
                return;
            }

            // Không thực hiện cuộn nếu chạm vào nhân vật
            if (IsPointerOverCharacter(pointerPos))
            {
                Debug.Log("[PedestalsScroller] Nhấp bị chặn: trúng nhân vật.");
                return;
            }

            _isDragging = true;
            _lastPointerPos = pointerPos;
            Debug.Log($"[PedestalsScroller] Bắt đầu kéo cuộn từ vị trí: {_lastPointerPos}");
        }
        else if (IsPointerPressed() && _isDragging)
        {
            Vector2 delta = pointerPos - _lastPointerPos;

            if (_mainCamera == null) _mainCamera = Camera.main;

            float moveAmount = 0f;
            if (_mainCamera != null)
            {
                // Thuật toán chiếu màn hình: Chiếu vector vuốt delta 2D lên trục X 3D của hàng bệ đứng biểu diễn trên màn hình
                Vector3 origin = transform.position;
                Vector3 rightPoint = transform.position + transform.TransformDirection(Vector3.right);

                Vector2 screenOrigin = _mainCamera.WorldToScreenPoint(origin);
                Vector2 screenRight = _mainCamera.WorldToScreenPoint(rightPoint);

                Vector2 screenDirection = screenRight - screenOrigin;
                float screenDist = screenDirection.magnitude;

                if (screenDist > 1f)
                {
                    screenDirection.Normalize();
                    float projectedDelta = Vector2.Dot(delta, screenDirection);
                    moveAmount = (projectedDelta / screenDist) * scrollSpeedFactor;
                }
                else
                {
                    moveAmount = delta.x * 0.01f * scrollSpeedFactor;
                }
            }
            else
            {
                moveAmount = delta.x * 0.01f * scrollSpeedFactor;
            }

            // Cập nhật vị trí mục tiêu và giới hạn
            float prevTarget = _targetLocalX;
            _targetLocalX = Mathf.Clamp(_targetLocalX + moveAmount, _minLocalX, _maxLocalX);
            
            if (Mathf.Abs(_targetLocalX - prevTarget) > 0.0001f)
            {
                Debug.Log($"[PedestalsScroller] Đang cuộn. Lượng di chuyển: {moveAmount}, Vị trí X mục tiêu: {_targetLocalX}");
            }
            
            _lastPointerPos = pointerPos;
        }
        else if (IsPointerReleasedThisFrame())
        {
            if (_isDragging)
            {
                Debug.Log("[PedestalsScroller] Thả tay/chuột. Dừng kéo.");
                _isDragging = false;
            }
        }
    }

    private bool IsPointerOverCharacter(Vector2 screenPosition)
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return false;

        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.GetComponentInParent<Move>() != null)
            {
                return true;
            }
        }
        return false;
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
        return Input.mousePosition;
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
        return Input.GetMouseButton(0);
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
        return Input.GetMouseButtonDown(0);
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
        return Input.GetMouseButtonUp(0);
    }

    private bool IsTouchOnJoystick(Vector2 screenPosition)
    {
        if (_joystick == null)
        {
            _joystick = FindFirstObjectByType<Joystick>();
        }

        if (_joystick != null && _joystick.gameObject.activeInHierarchy)
        {
            RectTransform rectTransform = _joystick.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Canvas canvas = _joystick.GetComponentInParent<Canvas>();
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
        if (IsTouchOnJoystick(screenPosition))
        {
            return true;
        }

        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        System.Collections.Generic.List<RaycastResult> results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            if (r.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null || 
                r.gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
