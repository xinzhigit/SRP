using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

[CustomEditor(typeof(SRenderPipelineAsset))]
public class SRenderPipelineAssetEditor : Editor
{
    SerializedProperty ShadowCascades;
    SerializedProperty TwoCascadesSplit;
    SerializedProperty FourCascadesSplit;

    private void OnEnable() {
        ShadowCascades = serializedObject.FindProperty("ShadowCascades");
        TwoCascadesSplit = serializedObject.FindProperty("TwoCascadesSplit");
        FourCascadesSplit = serializedObject.FindProperty("FourCascadesSplit");
    }

    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        // switch(ShadowCascades.enumValueIndex) {
        //     case 0:
        //         return;
        //     case 1:
        //         EditorUtils.draw<float>(ref TwoCascadesSplit);
        //         break;
        //     case 2:
        //         CoreEditorUtils.DrawCascadeSplitGUI<Vector3>(ref FourCascadesSplit);
        //         break;
        // }

        serializedObject.ApplyModifiedProperties();
    }
}
