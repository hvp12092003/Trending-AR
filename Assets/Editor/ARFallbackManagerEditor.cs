using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ARFallbackManager))]
public class ARFallbackManagerEditor : Editor
{
    private SerializedProperty m_SceneModeProp;
    private SerializedProperty m_ARSessionProp;
    private SerializedProperty m_ARPlaneManagerProp;
    private SerializedProperty m_TapToPlacePrefabProp;
    private SerializedProperty m_FallbackRawImageProp;
    private SerializedProperty m_AutoAlignRawImageProp;
    private SerializedProperty m_ForceFallbackProp;

    private void OnEnable()
    {
        m_SceneModeProp = serializedObject.FindProperty("m_SceneMode");
        m_ARSessionProp = serializedObject.FindProperty("m_ARSession");
        m_ARPlaneManagerProp = serializedObject.FindProperty("m_ARPlaneManager");
        m_TapToPlacePrefabProp = serializedObject.FindProperty("m_TapToPlacePrefab");
        m_FallbackRawImageProp = serializedObject.FindProperty("m_FallbackRawImage");
        m_AutoAlignRawImageProp = serializedObject.FindProperty("m_AutoAlignRawImage");
        m_ForceFallbackProp = serializedObject.FindProperty("m_ForceFallback");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Tiêu đề lớn cho giao diện đẹp đẽ
        EditorGUILayout.LabelField("AR Fallback Manager Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Hiển thị Option ở trên cùng
        EditorGUILayout.PropertyField(m_SceneModeProp, new GUIContent("Scene Mode", "Chọn chế độ hoạt động tương ứng với Scene"));

        EditorGUILayout.Space(10);

        ARFallbackManager.SceneMode currentMode = (ARFallbackManager.SceneMode)m_SceneModeProp.enumValueIndex;

        if (currentMode == ARFallbackManager.SceneMode.ARScene)
        {
            EditorGUILayout.HelpBox("Đang ở chế độ AR Scene. Script sẽ kiểm tra độ tương thích ARCore của thiết bị trước, nếu không hỗ trợ sẽ tự động chuyển sang camera WebCam làm nền giả lập.", MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("AR Components (Required for AR)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ARSessionProp);
            EditorGUILayout.PropertyField(m_ARPlaneManagerProp);
            EditorGUILayout.PropertyField(m_TapToPlacePrefabProp);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Fallback Screen Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_FallbackRawImageProp);
            EditorGUILayout.PropertyField(m_AutoAlignRawImageProp);
            EditorGUILayout.PropertyField(m_ForceFallbackProp);
        }
        else if (currentMode == ARFallbackManager.SceneMode.NonARScene)
        {
            EditorGUILayout.HelpBox("Đang ở chế độ Non-AR Scene. Script sẽ luôn chạy ở chế độ giả lập (WebCam + mặt phẳng trước camera), tắt các thành phần ARCore để tránh xung đột.", MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Non-AR Background Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_FallbackRawImageProp);
            EditorGUILayout.PropertyField(m_AutoAlignRawImageProp);
            
            // Ở Non-AR Scene thì Force Fallback được thiết lập ngầm là true
            GUI.enabled = false;
            EditorGUILayout.Toggle("Force Fallback (Always True)", true);
            GUI.enabled = true;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
