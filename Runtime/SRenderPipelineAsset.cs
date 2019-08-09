using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/S Render Pipeline Asset")]
public class SRenderPipelineAsset : RenderPipelineAsset {
    [SerializeField]
    public bool DynamicBatching;

    [SerializeField]
    public bool Instancing;

    public enum EShadowMapSize {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }
    [SerializeField]
    public EShadowMapSize ShadowMapSize = EShadowMapSize._1024;

    [SerializeField]
    public float ShadowDistance = 20;

    public enum EShadowCascades {
        Zero = 0,
        Two = 2,
        Four = 4
    }
    [SerializeField]
    public EShadowCascades ShadowCascades = EShadowCascades.Zero;
    [SerializeField, HideInInspector]
    public float TwoCascadesSplit = 0.25f;
    [SerializeField, HideInInspector]
    public Vector3 FourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);

    [SerializeField, Range(0.01f, 2f)]
    public float ShadowFadeRange = 1f;

    protected override RenderPipeline CreatePipeline() {
        return new SRenderPipeline(this);
    }

    public bool HasShadowCascades {
        get { return ShadowCascades != EShadowCascades.Zero; }
    }
}
