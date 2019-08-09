using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstanceMaterialProperties : MonoBehaviour
{
    [SerializeField]
    private Color _color = Color.white;

    [SerializeField]
    private Color _emissionColor = Color.black;
    [SerializeField]
    private float _pulseEmissionFreqency;

    [SerializeField, Range(0f, 1f)]
    private float _metallic = 0.0f;

    [SerializeField, Range(0f, 1f)]
    private float _smoothness = 0.5f;

    private static MaterialPropertyBlock _propertyBlock;
    private static int _colorID = Shader.PropertyToID("_Color");
    private static int _emissionColorID = Shader.PropertyToID("_Emission");
    private static int _metallicID = Shader.PropertyToID("_Metallic");
    private static int _smoothnessID = Shader.PropertyToID("_Smoothness");

    private void Awake() {
        OnValidate();

        if(_pulseEmissionFreqency <= 0) {
            enabled = false;
        }
    }

    private void OnValidate() {
        if(_propertyBlock == null) {
            _propertyBlock = new MaterialPropertyBlock();
        }
        _propertyBlock.SetColor(_colorID, _color);
        _propertyBlock.SetColor(_emissionColorID, _emissionColor);
        _propertyBlock.SetFloat(_metallicID, _metallic);
        _propertyBlock.SetFloat(_smoothnessID, _smoothness);
        GetComponent<MeshRenderer>().SetPropertyBlock(_propertyBlock);
    }

    private void Update() {
        Color originalEmissionColor = _emissionColor;
        _emissionColor *= 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * _pulseEmissionFreqency * Time.time);
        OnValidate();
        DynamicGI.SetEmissive(GetComponent<MeshRenderer>(), _emissionColor);
        _emissionColor = originalEmissionColor;
    }
}
