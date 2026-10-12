#ifndef DIVINITY_SURFACE_FLOOR_LIT_DATA_INCLUDED
#define DIVINITY_SURFACE_FLOOR_LIT_DATA_INCLUDED

#define GetSurfaceAndBuiltinData GetSurfaceAndBuiltinData_HDRPLit
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
#undef GetSurfaceAndBuiltinData

#include "Assets/Surfaces/Shaders/SurfaceFloor.hlsl"

#if (SHADERPASS == SHADERPASS_GBUFFER) || (SHADERPASS == SHADERPASS_FORWARD) || (SHADERPASS == SHADERPASS_LIGHT_TRANSPORT)
    #define SURFACE_FLOOR_EVALUATE
#endif

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
{
#ifdef LOD_FADE_CROSSFADE
    LODDitheringTransition(ComputeFadeMaskSeed(V, posInput.positionSS), unity_LODFade.x);
#endif

    float3 doubleSidedConstants = GetDoubleSidedConstants();
    ApplyDoubleSidedFlipOrMirror(input, doubleSidedConstants);

    LayerTexCoord layerTexCoord;
    ZERO_INITIALIZE(LayerTexCoord, layerTexCoord);
    GetLayerTexCoord(input, layerTexCoord);

    float3 normalTS;
    float3 bentNormalTS;
    float alpha = GetSurfaceData(input, layerTexCoord, surfaceData, normalTS, bentNormalTS);

    surfaceData.geomNormalWS = input.tangentToWorld[2];
    surfaceData.specularOcclusion = 1.0;

    float3 floorEmission = 0;
#ifdef SURFACE_FLOOR_EVALUATE
    float3 positionAWS = GetAbsolutePositionWS(input.positionRWS);
    float3 floorColor;
    float floorSmoothness;
    EvaluateSurfaceFloor(positionAWS, floorColor, floorSmoothness, floorEmission);

    surfaceData.baseColor = floorColor;
    surfaceData.perceptualSmoothness = floorSmoothness;
    surfaceData.metallic = 0.0;
#endif

#ifdef DECAL_NORMAL_BLENDING
    if (_EnableDecals)
    {
        #ifndef SURFACE_GRADIENT
        normalTS = SurfaceGradientFromTangentSpaceNormalAndFromTBN(normalTS, input.tangentToWorld[0], input.tangentToWorld[1]);
        #endif
        DecalSurfaceData decalSurfaceData = GetDecalSurfaceData(posInput, input, alpha);
        ApplyDecalToSurfaceData(decalSurfaceData, input.tangentToWorld[2], surfaceData, normalTS);
    }
    GetNormalWS_SG(input, normalTS, surfaceData.normalWS, doubleSidedConstants);
#else
    GetNormalWS(input, normalTS, surfaceData.normalWS, doubleSidedConstants);
    #if HAVE_DECALS
    if (_EnableDecals)
    {
        DecalSurfaceData decalSurfaceData = GetDecalSurfaceData(posInput, input, alpha);
        ApplyDecalToSurfaceData(decalSurfaceData, input.tangentToWorld[2], surfaceData);
    }
    #endif
#endif

    float3 bentNormalWS = surfaceData.normalWS;

#if defined(DEBUG_DISPLAY)
    ApplyDebugToSurfaceData(input.tangentToWorld, surfaceData);
#endif

    surfaceData.tangentWS = Orthonormalize(surfaceData.tangentWS, surfaceData.normalWS);

    GetBuiltinData(input, V, posInput, surfaceData, alpha, bentNormalWS, 0.0, layerTexCoord.base, builtinData);

    builtinData.emissiveColor += floorEmission * GetInverseCurrentExposureMultiplier();
}

#endif
