using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal struct SPipelineSettings {
    public bool DynamicBatching { get; private set; }
    public bool Instancing { get; private set; }
    public int ShadowMapSize { get; private set; }
    public float ShadowDistance { get; private set; }
    public int ShadowCascades { get; private set; }
    public Vector3 ShadowCascadeSplit { get; private set; }
    public float ShadowFadeRange { get; private set; }

    public static SPipelineSettings Create(SRenderPipelineAsset asset) {
        SPipelineSettings cache = new SPipelineSettings();

        cache.DynamicBatching = asset.DynamicBatching;
        cache.Instancing = asset.Instancing;
        cache.ShadowMapSize = (int)asset.ShadowMapSize;
        cache.ShadowDistance = asset.ShadowDistance;
        cache.ShadowCascades = (int)asset.ShadowCascades;
        if (asset.ShadowCascades == SRenderPipelineAsset.EShadowCascades.Four) {
            cache.ShadowCascadeSplit = asset.FourCascadesSplit;
        }
        else {
            cache.ShadowCascadeSplit = new Vector3(asset.TwoCascadesSplit, 0);
        }
        cache.ShadowFadeRange = asset.ShadowFadeRange;

        return cache;
    }
}
