using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ARFallbackManager))]
public class ARFallbackManagerEditor : Editor
{
    private SerializedProperty m_ForceFallback;
    private SerializedProperty m_ARSession;
    private SerializedProperty m_ARPlaneManager;
    private SerializedProperty m_TapToPlacePrefab;
    private SerializedProperty m_FallbackRawImage;
    private SerializedProperty m_AutoAlignRawImage;

    private void OnEnable()
    {
        m_ForceFallback = serializedObject.FindProperty("m_ForceFallback");
        m_ARSession = serializedObject.FindProperty("m_ARSession");
        m_ARPlaneManager = serializedObject.FindProperty("m_ARPlaneManager");
        m_TapToPlacePrefab = serializedObject.FindProperty("m_TapToPlacePrefab");
        m_FallbackRawImage = serializedObject.FindProperty("m_FallbackRawImage");
        m_AutoAlignRawImage = serializedObject.FindProperty("m_AutoAlignRawImage");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Hiển thị Dropdown chọn chế độ ở trên cùng
        string[] modeOptions = new string[] 
        { 
            "AR Mode (Có ARCore / Thiết bị hỗ trợ)", 
            "Non-AR Mode (Không có ARCore / Chạy giả lập)" 
        };
        
        int currentSelection = m_ForceFallback.boolValue ? 1 : 0;
        
        EditorGUILayout.Space(5);
        int newSelection = EditorGUILayout.Popup("Chế độ AR Setup", currentSelection, modeOptions);
        
        if (newSelection != currentSelection)
        {
            m_ForceFallback.boolValue = (newSelection == 1);
        }
        
        EditorGUILayout.Space(10);

        // 2. Hiển thị các trường tương ứng dựa trên chế độ đã chọn
        if (m_ForceFallback.boolValue)
        {
            // Chế độ Không có ARCore (Non-AR / Fallback)
            EditorGUILayout.LabelField("Cấu hình chế độ Non-AR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Chế độ này sẽ chạy bằng WebCam của máy tính/điện thoại thường và tạo mặt phẳng ảo để tương tác.", MessageType.Info);
            
            EditorGUILayout.PropertyField(m_FallbackRawImage);
            EditorGUILayout.PropertyField(m_AutoAlignRawImage);
            EditorGUILayout.PropertyField(m_TapToPlacePrefab);
        }
        else
        {
            // Chế độ Có ARCore (AR Mode)
            EditorGUILayout.LabelField("Cấu hình chế độ AR Core", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Chế độ này sẽ quét không gian bằng ARCore thực tế trên thiết bị di động được hỗ trợ.", MessageType.Info);
            
            EditorGUILayout.PropertyField(m_ARSession);
            EditorGUILayout.PropertyField(m_ARPlaneManager);
            EditorGUILayout.PropertyField(m_TapToPlacePrefab);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
