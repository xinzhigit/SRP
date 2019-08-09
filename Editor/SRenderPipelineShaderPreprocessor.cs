using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Build;
using UnityEditor.Callbacks;

public class SRenderPipelineShaderPreprocessor : IPreprocessShaders
{
    private static SRenderPipelineShaderPreprocessor _inst;

    private SRenderPipelineAsset _pipelineAsset;
    private int _shaderVariantCount;
    private int _strippedCount;

    private bool _stripCascadedShadows;
    private static ShaderKeyword cascadeShadowHardKeyword = new ShaderKeyword("_CASCADED_SHADOWS_HARD");
    private static ShaderKeyword cascadeShadowSoftKeyword = new ShaderKeyword("_CASCADED_SHADOWS_SOFT");

    public SRenderPipelineShaderPreprocessor() {
        _inst = this;
        _pipelineAsset = GraphicsSettings.renderPipelineAsset as SRenderPipelineAsset;
        if(_pipelineAsset == null) {
            return;
        }

        _stripCascadedShadows = !_pipelineAsset.HasShadowCascades;
    }

    public int callbackOrder {
        get { return 0; }
    }

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) {
        if(_pipelineAsset == null) {
            return;
        }

        _shaderVariantCount += data.Count;
        for(int n = 0; n < data.Count; ++n) {
            if(Strip(data[n])) {
                data.RemoveAt(n--);
                _strippedCount += 1;
            }
        }

        Debug.Log(shader.name);
    }

    [PostProcessBuild(0)]
    private static void LogVariantCount(BuildTarget target, string path) {
        _inst.LogVariantCount();
        _inst = null;
    }

    private void LogVariantCount() {
        if (_pipelineAsset == null) {
            return;
        }

        int finalCount = _shaderVariantCount - _strippedCount;
        int percentage = Mathf.RoundToInt(100f * finalCount / _shaderVariantCount);
        Debug.Log("included " + "shader variants out of " + _shaderVariantCount + " (" + percentage + "%).");
    }

    private bool Strip(ShaderCompilerData data) {
        return _stripCascadedShadows && (data.shaderKeywordSet.IsEnabled(cascadeShadowHardKeyword) || data.shaderKeywordSet.IsEnabled(cascadeShadowSoftKeyword));
    }
}
