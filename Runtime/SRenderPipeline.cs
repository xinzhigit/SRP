using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public class SRenderPipeline : RenderPipeline
{
    #region properties
    private const string renderName = "Main Camera Render";

    private CullingResults _cull;
    private CommandBuffer _renderBuffer = new CommandBuffer() { name = renderName };

    private Material _errorMaterial;
    #endregion

    #region setting
    private SPipelineSettings _settings;
    #endregion

    #region light
    private const int MaxVisibleLights = 16;
    private static int _visibleLightColorsID = Shader.PropertyToID("_VisibleLightColors");
    private static int _visibleLightDirectionsOrPositionsID = Shader.PropertyToID("_visibleLightDirectionsOrPositions");
    private static int _visibleLightAttenuationsID = Shader.PropertyToID("_visibleLightAttenuations");
    private static int _visibleLightSpotDirectionsID = Shader.PropertyToID("_visibleLightSpotDirections");
    private static int _lightIndicesOffsetAndCount = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    private static int _visibleLightOcclusionMasksID = Shader.PropertyToID("_visibleLightOcclusionMasks");
    private Vector4[] _visibleLightColors = new Vector4[MaxVisibleLights];
    private Vector4[] _visibleLightDirectionsOrPositions = new Vector4[MaxVisibleLights];
    private Vector4[] _visibleLightAttenuations = new Vector4[MaxVisibleLights];
    private Vector4[] _visibleLightSpotDirections = new Vector4[MaxVisibleLights];
    private Vector4[] _visibleLightOcclusionMasks = new Vector4[MaxVisibleLights];
    private bool _mainLightExists = false;
    private Vector4[] _occlusionMasks = {
        new Vector4(-1f, 0f, 0f, 0f),
        new Vector4(1f, 0f, 0f, 0f),
        new Vector4(0f, 1f, 0f, 0f),
        new Vector4(0f, 0f, 1f, 0f),
        new Vector4(0f, 0f, 0f, 1f)
    };
    #endregion

    #region shadow
    public const string ShadowHardKeyword = "_SHADOWS_HARD";
    public const string ShadowSoftKeyword = "_SHADOWS_SOFT";
    public const string CascadedShadowHardKeyword = "_CASCADED_SHADOWS_HARD";
    public const string CascadedShadowSoftKeyword = "_CASCADED_SHADOWS_SOFT";
    public const string ShadowmaskKeyword = "_SHADOWMASK";
    public const string DistancShadowmaskKeyword = "_DISTANCE_SHADOWMASK";
    public const string SubtractiveLightingKeyword = "_SUBTRACTIVE_LIGHTING";

    CommandBuffer _shadowBuffer = new CommandBuffer() { name = "Render Shadows" };

    private static int _shadowMapID = Shader.PropertyToID("_ShadowMap");
    private static int _shadowBiasID = Shader.PropertyToID("_ShadowBias");
    private static int _shadowMapSizeID = Shader.PropertyToID("_ShadowMapSize");
    private static int _shadowDatasID = Shader.PropertyToID("_ShadowDatas");
    private static int _worldToShadowMatricesID = Shader.PropertyToID("_WorldToShadowMatrices");
    private static int _globalShadowDataID = Shader.PropertyToID("_GlobalShadowData");
    private static int _cascadedShadowMapID = Shader.PropertyToID("_CascadedShadowMap");
    private static int _cascadedWorldToShadowMatricesID = Shader.PropertyToID("_CascadedWorldToShadowMatrices");
    private static int _cascadedShadowMapSizeID = Shader.PropertyToID("_CascadedShadowMapSize");
    private static int _cascadedShadowStrengthID = Shader.PropertyToID("_CascadedShadowStrength");
    private static int _cascadedCullingSpheresID = Shader.PropertyToID("_CascadedCullingSpheres");
    private static int _subtractiveShadowColorID = Shader.PropertyToID("_SubtractiveShadowColor");

    private Vector4[] _shadowDatas = new Vector4[MaxVisibleLights];
    private Matrix4x4[] _worldToShadowMatrices = new Matrix4x4[MaxVisibleLights];
    private int _shadowTileCount;
    private RenderTexture _shadowMap;
    private RenderTexture _cascadedShadowMap;
    private Vector4[] _cascadedCullingSpheres = new Vector4[4];
    // That works correctly when at least one culling sphere encompasses the point. 
    // But when a point lies outside all spheres we end up with zero, incorrectly sampling from the first cascade. 
    // A trick Unity uses here is to provide an extra world-to-shadow matrix for a fifth nonexistent cascade. 
    // It's a zero matrix, which sets the shadow position to the near plane and thus never results in a shadow. 
    // We can do that by simply adding a fifth element to the worldToShadowCascadeMatrices array in MyPipeline.
    private Matrix4x4[] _cascadedWorldToShadowMatrices = new Matrix4x4[5];
    private Vector4 _globalShadowDatas = Vector4.zero;
    #endregion

#if UNITY_EDITOR
    private static Lightmapping.RequestLightsDelegate _lightmappingLightsDelegate = 
        (Light[] inputLights, NativeArray<LightDataGI> outputLights) => {
            LightDataGI lightData = new LightDataGI();
            for(int n = 0; n < inputLights.Length; ++n) {
                Light light = inputLights[n];
                switch (light.type) {
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var areaLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref areaLight);
                        lightData.Init(ref areaLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }
                
                lightData.falloff = FalloffType.InverseSquared;
                outputLights[n] = lightData;
            }
        };
#endif

    public SRenderPipeline(SRenderPipelineAsset asset) {
        _settings = SPipelineSettings.Create(asset);

        // use linear space
        GraphicsSettings.lightsUseLinearIntensity = true;

        // However, when a reverse Z buffer is used we have to push the shadow position Z coordinate to 1 instead. 
        // We can do that by setting the m33 field of the dummy matrix to 1 in the constructor.
        if (SystemInfo.usesReversedZBuffer) {
            _cascadedWorldToShadowMatrices[4].m33 = 1f;
        }

        // We'll put 1/0 in the Z component of the global shadow data and 1−s/r in its W component.
        _globalShadowDatas.y = _settings.ShadowDistance * _settings.ShadowDistance;
        _globalShadowDatas.z = 1f / _settings.ShadowFadeRange;

#if UNITY_EDITOR
        Lightmapping.SetDelegate(_lightmappingLightsDelegate);
#endif
    }

#if UNITY_EDITOR
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }
#endif

    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras) {
        BeginFrameRendering(renderContext, cameras);
        
        for(int n = 0; n < cameras.Length; ++n) {
            Camera camera = cameras[n];
            BeginCameraRendering(renderContext, camera);
            Render(renderContext, camera);
            EndCameraRendering(renderContext, camera);
        }

        EndFrameRendering(renderContext, cameras);
    }

    private void Render(ScriptableRenderContext renderContext, Camera camera) {

        // Not all camera settings are valid, resulting in degenerate results that cannot be used for culling. 
        // So if it fails, we have nothing to render and can exit from Render.
        ScriptableCullingParameters cullingParameters;
        if (!camera.TryGetCullingParameters(false, out cullingParameters)) {
            return;
        }
        cullingParameters.shadowDistance = Mathf.Min(_settings.ShadowDistance, camera.farClipPlane);

#if UNITY_EDITOR
        // draw UI before cull
        if (camera.cameraType == CameraType.SceneView) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif

        // cull
        _cull = renderContext.Cull(ref cullingParameters);

        // render shadows before the regular scene
        if(_cull.visibleLights.Length > 0) {
            ConfigLights();
            if(_mainLightExists) {
                RenderCascadedShadows(renderContext);
            }
            else {
                _renderBuffer.DisableShaderKeyword(CascadedShadowHardKeyword);
                _renderBuffer.DisableShaderKeyword(CascadedShadowSoftKeyword);
            }
            if (_shadowTileCount > 0) {
                RenderShadows(renderContext);
            }
            else {
                _renderBuffer.DisableShaderKeyword(ShadowHardKeyword);
                _renderBuffer.DisableShaderKeyword(ShadowSoftKeyword);
            }
        }
        else {
            _renderBuffer.SetGlobalVector(_lightIndicesOffsetAndCount, Vector4.zero);
            _renderBuffer.DisableShaderKeyword(ShadowHardKeyword);
            _renderBuffer.DisableShaderKeyword(ShadowSoftKeyword);
            _renderBuffer.DisableShaderKeyword(CascadedShadowHardKeyword);
            _renderBuffer.DisableShaderKeyword(CascadedShadowSoftKeyword);
        }

        // setup camera properties
        renderContext.SetupCameraProperties(camera);

        // clear target
        CameraClearFlags clearFlags = camera.clearFlags;
        _renderBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0,
                                (clearFlags & CameraClearFlags.Color) != 0,
                                camera.backgroundColor);

        // begin simpling after clear render target
        _renderBuffer.BeginSample(renderName);
        _renderBuffer.SetGlobalVectorArray(_visibleLightColorsID, _visibleLightColors);
        _renderBuffer.SetGlobalVectorArray(_visibleLightDirectionsOrPositionsID, _visibleLightDirectionsOrPositions);
        _renderBuffer.SetGlobalVectorArray(_visibleLightAttenuationsID, _visibleLightAttenuations);
        _renderBuffer.SetGlobalVectorArray(_visibleLightSpotDirectionsID, _visibleLightSpotDirections);
        _renderBuffer.SetGlobalVectorArray(_visibleLightOcclusionMasksID, _visibleLightOcclusionMasks);
        _globalShadowDatas.w = 1f - cullingParameters.shadowDistance * _globalShadowDatas.z;
        _renderBuffer.SetGlobalVector(_globalShadowDataID, _globalShadowDatas);
        renderContext.ExecuteCommandBuffer(_renderBuffer);
        _renderBuffer.Clear();

        // draw opaque objects
        var drawSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque });

        // Another possibility is that there are zero visible lights. 
        // This should just work, but unfortunately Unity crashes when trying to set the light indices in this case. 
        // We can avoid the crash by only using per-object light indices when we have at least one visible light.
        if (_cull.visibleLights.Length > 0) {
            drawSettings.perObjectData = PerObjectData.LightIndices;
        }
        drawSettings.perObjectData |= 
            PerObjectData.ReflectionProbes |
            PerObjectData.Lightmaps |
            PerObjectData.LightProbe |
            PerObjectData.LightProbeProxyVolume |
            PerObjectData.ShadowMask |
            PerObjectData.OcclusionProbe |
            PerObjectData.OcclusionProbeProxyVolume;

        var filterSettings = new FilteringSettings(RenderQueueRange.opaque, -1);
        renderContext.DrawRenderers(_cull, ref drawSettings, ref filterSettings);

        // draw skybox
        renderContext.DrawSkybox(camera);

        // draw transparent objects
        drawSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent });
        drawSettings.enableDynamicBatching = _settings.DynamicBatching;
        drawSettings.enableInstancing = _settings.Instancing;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        renderContext.DrawRenderers(_cull, ref drawSettings, ref filterSettings);

        // visualize those objects with error shader
        DrawDefaultPipeline(renderContext, camera);

        // end simpling
        _renderBuffer.EndSample(renderName);
        renderContext.ExecuteCommandBuffer(_renderBuffer);
        _renderBuffer.Clear();

        // start draw
        renderContext.Submit();

        // end clear shadows
        ReleaseShadows();
    }
    
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void DrawDefaultPipeline(ScriptableRenderContext renderContext, Camera camera) {
        if(_errorMaterial == null) {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            _errorMaterial = new Material(errorShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        // replace same pass with error shader
        var drawSettings = new DrawingSettings(new ShaderTagId("ForwardBase"), new SortingSettings(camera));
        drawSettings.SetShaderPassName(1, new ShaderTagId("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderTagId("Always"));
        drawSettings.SetShaderPassName(3, new ShaderTagId("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderTagId("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderTagId("VertexLM"));
        drawSettings.overrideMaterialPassIndex = 0;
        drawSettings.overrideMaterial = _errorMaterial;

        var filterSettings = new FilteringSettings(RenderQueueRange.opaque, -1);
        renderContext.DrawRenderers(_cull, ref drawSettings, ref filterSettings);
    }

    #region light
    private void ConfigLights() {
        _shadowTileCount = 0;
        _mainLightExists = false;
        bool shadowmaskExsit = false;
        bool subtractiveLighting = false;

        for(int n = 0; n < _cull.visibleLights.Length; ++n) {
            if (n >= MaxVisibleLights) {
                break;
            }

            VisibleLight visibleLight = _cull.visibleLights[n];

            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1;
            Vector4 shadow = Vector4.zero;
            _visibleLightColors[n] = visibleLight.finalColor;
            
            LightBakingOutput baking = visibleLight.light.bakingOutput;
            _visibleLightOcclusionMasks[n] = _occlusionMasks[baking.occlusionMaskChannel + 1];
            if(baking.lightmapBakeType == LightmapBakeType.Mixed) {
                shadowmaskExsit |= baking.mixedLightingMode == MixedLightingMode.Shadowmask;
                if(baking.mixedLightingMode == MixedLightingMode.Subtractive) {
                    subtractiveLighting = true;
                    _renderBuffer.SetGlobalColor(_subtractiveShadowColorID, RenderSettings.subtractiveShadowColor.linear);
                }
            }
            
            if(visibleLight.lightType == LightType.Directional) {
                Vector4 lightDir = visibleLight.localToWorldMatrix.GetColumn(2);
                lightDir.x = -lightDir.x;
                lightDir.y = -lightDir.y;
                lightDir.z = -lightDir.z;
                _visibleLightDirectionsOrPositions[n] = lightDir;

                shadow = SetShadowData(n, visibleLight.light);

                // Because there are some differences when rendering a directional shadow map, 
                // let's signal that we're dealing with a directional light here.
                // We can do that by using the Z component of the shadow data as a flag.
                shadow.z = 1f;

                // We won't try to fit the shadow cascade maps in the same texture along with all other shadow maps. 
                // That would make them too small if there are multiply lights with shadows. 
                // So decrement the shadow tile count when we encounter a main light.
                if (n == 0 && shadow.x > 0f && _settings.ShadowCascades > 0) {
                    _mainLightExists = true;
                    _shadowTileCount -= 1;
                }
            }
            else {
                _visibleLightDirectionsOrPositions[n] = visibleLight.localToWorldMatrix.GetColumn(3);
                // store 1/r2 to avoiding a divsion by zero
                attenuation.x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);

                if(visibleLight.lightType == LightType.Spot) {
                    Vector4 lightDir = visibleLight.localToWorldMatrix.GetColumn(2);
                    lightDir.x = -lightDir.x;
                    lightDir.y = -lightDir.y;
                    lightDir.z = -lightDir.z;
                    _visibleLightSpotDirections[n] = lightDir;

                    float outerRad = Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan((46f / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;

                    shadow = SetShadowData(n, visibleLight.light);
                }
                else {
                    _visibleLightSpotDirections[n] = Vector4.one;
                }
            }

            _visibleLightAttenuations[n] = attenuation;
            _shadowDatas[n] = shadow;
        }
        
        if (_mainLightExists || _cull.visibleLights.Length > MaxVisibleLights) {
            NativeArray<int> lightIndices = _cull.GetLightIndexMap(Allocator.Temp);
            if (_mainLightExists) {
                lightIndices[0] = -1;
            }
            for (int n = MaxVisibleLights; n < _cull.visibleLights.Length; ++n) {
                lightIndices[n] = -1;
            }
            _cull.SetLightIndexMap(lightIndices);
            lightIndices.Dispose();
        }

        bool useDistanceShadowmask = QualitySettings.shadowmaskMode == ShadowmaskMode.DistanceShadowmask;
        CoreUtils.SetKeyword(_renderBuffer, ShadowmaskKeyword, shadowmaskExsit && !useDistanceShadowmask);
        CoreUtils.SetKeyword(_renderBuffer, DistancShadowmaskKeyword, shadowmaskExsit && useDistanceShadowmask);
        CoreUtils.SetKeyword(_renderBuffer, SubtractiveLightingKeyword, subtractiveLighting);
    }
    #endregion

    #region shadow
    private Vector4 SetShadowData(int n, Light shadowLight) {
        Bounds shadowBounds;
        Vector4 shadow = Vector4.zero;
        if (shadowLight.shadows != LightShadows.None && _cull.GetShadowCasterBounds(n, out shadowBounds)) {
            shadow.x = shadowLight.shadowStrength;
            shadow.y = shadowLight.shadows == LightShadows.Soft ? 1.0f : 0.0f;

            _shadowTileCount += 1;
        }
        return shadow;
    }

    private RenderTexture SetShadowRenderTarget() {
        RenderTexture rt = RenderTexture.GetTemporary(_settings.ShadowMapSize, _settings.ShadowMapSize, 16, RenderTextureFormat.Shadowmap);

        // Make sure that the texture's filter mode is set the bilinear and its wrap mode is set to clamp.
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(_shadowBuffer, rt, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Depth);

        return rt;
    }

    private Vector2 ConfigShadowTile(int tileIndex, int split, float tileSize) {
        Vector2 tileOffset = Vector2.zero;
        tileOffset.x = tileIndex % split;
        tileOffset.y = tileIndex / split;
        Rect tileViewport = new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);
        _shadowBuffer.SetViewport(tileViewport);
        _shadowBuffer.EnableScissorRect(new Rect(tileViewport.x + 4f, tileViewport.y + 4, tileSize - 8f, tileSize - 8f));
        return tileOffset;
    }

    private void CalWorldToShadowMatrix(ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix, out Matrix4x4 worldToShadowMatrix) {
        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
        if (SystemInfo.usesReversedZBuffer) {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }
        worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
    }

    private void RenderShadows(ScriptableRenderContext context) {
        //Debug.Log("render shadows!");

        int shadowSplit = 1;
        if (_shadowTileCount <= 1) {
            shadowSplit = 1;
        }
        else if (_shadowTileCount <= 4) {
            shadowSplit = 2;
        }
        else if (_shadowTileCount <= 9) {
            shadowSplit = 3;
        }
        else {
            shadowSplit = 4;
        }

        bool hardShadow = false;
        bool softShadow = false;

        float tileSize = _settings.ShadowMapSize / shadowSplit;
        float tileScale = 1f / shadowSplit;

        _shadowMap = SetShadowRenderTarget();

        _shadowBuffer.BeginSample("Render Shadows");
        _globalShadowDatas.x = tileScale;
        context.ExecuteCommandBuffer(_shadowBuffer);
        _shadowBuffer.Clear();

        int tileIndex = 0;
        for (int n = _mainLightExists ? 1 : 0; n < _cull.visibleLights.Length; ++n) {
            if(n >= MaxVisibleLights) {
                break;
            }

            if (_shadowDatas[n].x <= 0f) {
                continue;
            }

            VisibleLight visibleLight = _cull.visibleLights[n];

            Vector2 tileOffset = ConfigShadowTile(tileIndex, shadowSplit, tileSize);
            _shadowDatas[n].z = tileOffset.x * tileScale;
            _shadowDatas[n].w = tileOffset.y * tileScale;
            tileIndex += 1;

            Matrix4x4 viewMatrix = Matrix4x4.identity;
            Matrix4x4 projectionMatrix = Matrix4x4.identity;
            ShadowSplitData splitData = new ShadowSplitData();
            bool validShadows = false;
            if(visibleLight.lightType == LightType.Directional) {
                validShadows = _cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(n, 0, 1, Vector3.right, (int)tileSize, visibleLight.light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);
            }
            else if(visibleLight.lightType == LightType.Spot) {
                validShadows = _cull.ComputeSpotShadowMatricesAndCullingPrimitives(n, out viewMatrix, out projectionMatrix, out splitData);
            }
            if(!validShadows) {
                _shadowDatas[n].x = 0f;
                continue;
            }
            _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            _shadowBuffer.SetGlobalFloat(_shadowBiasID, visibleLight.light.shadowBias);
            context.ExecuteCommandBuffer(_shadowBuffer);
            _shadowBuffer.Clear();

            var shadowSettings = new ShadowDrawingSettings(_cull, n);
            shadowSettings.splitData = splitData;
            context.DrawShadows(ref shadowSettings);

            // Clip space goes from −1 to 1, while texture coordinates and depth go from 0 to 1.We can bake that range conversion into our matrix, 
            // via an additional multiplication with a matrix that scales and offsets by half a unit in all dimensions.
            CalWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out _worldToShadowMatrices[n]);

            if (_shadowDatas[n].y <= 0f) {
                hardShadow = true;
            }
            else {
                softShadow = true;
            }
        }

        // We have to disable the scissor rectangle by invoking DisableScissorRect after we're done rendering shadows, otherwise regular rendering will be affected too.
        _shadowBuffer.DisableScissorRect();

        _shadowBuffer.SetGlobalMatrixArray(_worldToShadowMatricesID, _worldToShadowMatrices);
        _shadowBuffer.SetGlobalVectorArray(_shadowDatasID, _shadowDatas);
        _shadowBuffer.SetGlobalTexture(_shadowMapID, _shadowMap);
        float invShadowMapSize = 1f / _settings.ShadowMapSize;
        _shadowBuffer.SetGlobalVector(_shadowMapSizeID, new Vector4(invShadowMapSize, invShadowMapSize, _settings.ShadowMapSize, _settings.ShadowMapSize));

        CoreUtils.SetKeyword(_shadowBuffer, ShadowHardKeyword, hardShadow);
        CoreUtils.SetKeyword(_shadowBuffer, ShadowSoftKeyword, softShadow);

        _shadowBuffer.EndSample("Render Shadows");

        context.ExecuteCommandBuffer(_shadowBuffer);
        _shadowBuffer.Clear();
    }

    private void RenderCascadedShadows(ScriptableRenderContext context) {
        //Debug.Log("render cascaded shadows!");

        float tileSize = _settings.ShadowMapSize / 2;
        _cascadedShadowMap = SetShadowRenderTarget();

        _shadowBuffer.BeginSample("Render Shadows");
        _globalShadowDatas.x = 0;
        
        context.ExecuteCommandBuffer(_shadowBuffer);
        _shadowBuffer.Clear();

        Light shadowLight = _cull.visibleLights[0].light;
        _shadowBuffer.SetGlobalFloat(_shadowBiasID, shadowLight.shadowBias);

        var shadowSettings = new ShadowDrawingSettings(_cull, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        for(int n = 0; n < _settings.ShadowCascades; ++n) {
            Matrix4x4 viewMatrix;
            Matrix4x4 projectionMatrix;
            ShadowSplitData splitData;
            _cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(0, n, _settings.ShadowCascades, _settings.ShadowCascadeSplit, (int)tileSize, shadowLight.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);

            Vector2 tileOffset = ConfigShadowTile(n, 2, tileSize);
            _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            context.ExecuteCommandBuffer(_shadowBuffer);
            _shadowBuffer.Clear();

            shadowSettings.splitData = splitData;
            _cascadedCullingSpheres[n] = splitData.cullingSphere;
            // because we only need to check whether the fragment lies inside the sphere we can make do with a squared distance comparison, so square the stored radii.
            _cascadedCullingSpheres[n].w *= splitData.cullingSphere.w;
            context.DrawShadows(ref shadowSettings);

            CalWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out _cascadedWorldToShadowMatrices[n]);
            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            _cascadedWorldToShadowMatrices[n] = tileMatrix * _cascadedWorldToShadowMatrices[n];
        }

        _shadowBuffer.DisableScissorRect();
        _shadowBuffer.SetGlobalTexture(_cascadedShadowMapID, _cascadedShadowMap);
        _shadowBuffer.SetGlobalMatrixArray(_cascadedWorldToShadowMatricesID, _cascadedWorldToShadowMatrices);
        float invShadowMapSize = 1f / _settings.ShadowMapSize;
        _shadowBuffer.SetGlobalVector(_cascadedShadowMapSizeID, new Vector4(invShadowMapSize, invShadowMapSize, _settings.ShadowMapSize, _settings.ShadowMapSize));
        _shadowBuffer.SetGlobalFloat(_cascadedShadowStrengthID, shadowLight.shadowStrength);
        _shadowBuffer.SetGlobalVectorArray(_cascadedCullingSpheresID, _cascadedCullingSpheres);

        bool hard = shadowLight.shadows == LightShadows.Hard;
        CoreUtils.SetKeyword(_shadowBuffer, CascadedShadowHardKeyword, hard);
        CoreUtils.SetKeyword(_shadowBuffer, CascadedShadowSoftKeyword, !hard);

        _shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(_shadowBuffer);
        _shadowBuffer.Clear();
    }

    private void ReleaseShadows() {
        if (_shadowMap) {
            RenderTexture.ReleaseTemporary(_shadowMap);
            _shadowMap = null;
        }

        if(_cascadedShadowMap) {
            RenderTexture.ReleaseTemporary(_cascadedShadowMap);
            _cascadedShadowMap = null;
        }
    }
    #endregion
}
