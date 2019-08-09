using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LitShaderGUI : ShaderGUI
{
    private MaterialEditor _editor;
    private Object[] _materials;
    private MaterialProperty[] _properties;

    private const string ShadowPassName = "ShadowCaster";

    private bool _showPresets;

    private enum EClipMode {
        Off,
        On,
        Shadows
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) {
        base.OnGUI(materialEditor, properties);

        _editor = materialEditor;
        _materials = materialEditor.targets;
        _properties = properties;

        CastShadowsToggle();
        GlobalIlluminationToggle();

        EditorGUILayout.Space();
        _showPresets = EditorGUILayout.Foldout(_showPresets, "Presets", true);
        if(_showPresets) {
            OpaquePreset();
            ClipPreset();
            ClipDoubleSidePreset();
            FadePreset();
            FadeWithShadowsPreset();
            TransparentPreset();
            TransparentWithShadowsPreset();
        }
    }

    #region interface
    private void CastShadowsToggle() {
        string toggleName = "Cast Shadows";

        bool? enabled = IsPassEnabled(ShadowPassName);
        if(!enabled.HasValue) {
            EditorGUI.showMixedValue = true;
            enabled = false;
        }

        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.Toggle(toggleName, enabled.Value);
        if(EditorGUI.EndChangeCheck()) {
            _editor.RegisterPropertyChangeUndo(toggleName);
            SetPassEnabled(ShadowPassName, enabled.Value);
        }

        EditorGUI.showMixedValue = false;
    }

    private void GlobalIlluminationToggle() {
        EditorGUI.BeginChangeCheck();
        _editor.LightmapEmissionProperty();
        if(EditorGUI.EndChangeCheck()) {
            foreach(Material m in _editor.targets) {
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    private void OpaquePreset() {
        if(!GUILayout.Button("Opaque")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Opaque Preset");

        Cliping = EClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled(ShadowPassName, true);
        RenderQueue = RenderQueue.Geometry;
    }

    private void ClipPreset() {
        if (!GUILayout.Button("Clip")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Clip Preset");

        Cliping = EClipMode.On;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled(ShadowPassName, true);
        RenderQueue = RenderQueue.AlphaTest;
    }

    private void ClipDoubleSidePreset() {
        if (!GUILayout.Button("Clip Double-Sided")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Clip Double-Sided Preset");

        Cliping = EClipMode.On;
        Cull = CullMode.Off;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled(ShadowPassName, true);
        RenderQueue = RenderQueue.AlphaTest;
    }

    private void FadePreset() {
        if (!GUILayout.Button("Fade")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Fade Preset");

        Cliping = EClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.SrcAlpha;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = false;
        PremultiplyAlpha = false;
        SetPassEnabled(ShadowPassName, false);
        RenderQueue = RenderQueue.Transparent;
    }

    private void FadeWithShadowsPreset() {
        if (!GUILayout.Button("Fade With Shadows")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Fade With Shadows Preset");

        Cliping = EClipMode.Shadows;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.SrcAlpha;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled(ShadowPassName, true);
        RenderQueue = RenderQueue.Transparent;
    }

    private void TransparentPreset() {
        if (!GUILayout.Button("Transparent")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Transparent Preset");

        Cliping = EClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = false;
        PremultiplyAlpha = true;
        SetPassEnabled(ShadowPassName, false);
        RenderQueue = RenderQueue.Transparent;
    }

    private void TransparentWithShadowsPreset() {
        if (!GUILayout.Button("Transparent With Shadows")) {
            return;
        }
        _editor.RegisterPropertyChangeUndo("Transparent With Shadows Preset");

        Cliping = EClipMode.Shadows;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = true;
        PremultiplyAlpha = true;
        SetPassEnabled(ShadowPassName, true);
        RenderQueue = RenderQueue.Transparent;
    }
    #endregion

    #region properties
    private EClipMode Cliping {
        set {
            FindProperty("_Clipping", _properties).floatValue = (float)value;
            SetKeywordEnabled("_CLIPPING_ON", value == EClipMode.On);
            SetKeywordEnabled("_CLIPPING_OFF", value == EClipMode.Off);
            SetKeywordEnabled("_CLIPPING_SHADOWS", value == EClipMode.Shadows);
        }
    }

    private bool ReceiveShadows {
        set {
            FindProperty("_Clipping", _properties).floatValue = value ? 1 : 0;
            SetKeywordEnabled("_RECEIVE_SHADOWS", value);
        }
    }

    private CullMode Cull {
        set {
            FindProperty("_Cull", _properties).floatValue = (float)value;
        }
    }

    private BlendMode SrcBlend {
        set {
            FindProperty("_SrcBlend", _properties).floatValue = (float)value;
        }
    }

    private BlendMode DstBlend {
        set {
            FindProperty("_DstBlend", _properties).floatValue = (float)value;
        }
    }

    private bool ZWrite {
        set {
            FindProperty("_ZWrite", _properties).floatValue = value ? 1 : 0;
        }
    }

    private bool PremultiplyAlpha {
        set {
            FindProperty("_PremultiplyAlpha", _properties).floatValue = value ? 1 : 0;
            SetKeywordEnabled("_PREMULTIPLY_ALPHA", value);
        }
    }


    private RenderQueue RenderQueue {
        set {
            foreach (Material m in _materials) {
                m.renderQueue = (int)value;
            }
        }
    }

    private void SetPassEnabled(string pass, bool enabled) {
        foreach (Material m in _materials) {
            m.SetShaderPassEnabled(pass, enabled);
        }
    }

    private bool? IsPassEnabled(string pass) {
        bool enabled = ((Material)_materials[0]).GetShaderPassEnabled(pass);
        for (int n = 1; n < _materials.Length; ++n) {
            if (enabled != ((Material)_materials[n]).GetShaderPassEnabled(pass)) {
                return null;
            }
        }

        return enabled;
    }

    private void SetKeywordEnabled(string keyword, bool enabled) {
        if (enabled) {
            foreach (Material m in _materials) {
                m.EnableKeyword(keyword);
            }
        }
        else {
            foreach (Material m in _materials) {
                m.DisableKeyword(keyword);
            }
        }
    }
    #endregion
}
