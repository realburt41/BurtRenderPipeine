// BurtRP shared material and geometry input helpers for Lit-style shaders.
#ifndef BURT_INPUT_INCLUDED // Starts an include guard so this file can be included from several shader libraries safely.
#define BURT_INPUT_INCLUDED // Marks BurtInput.hlsl as included for the current shader compilation unit.

// Declares the Base Map texture object used by BurtLit.shader for albedo sampling.
sampler2D _BaseMap;

// Stores the surface properties that BurtLighting.hlsl consumes when evaluating the current Lit model.
struct BurtSurfaceData
{
    // Stores the final albedo color after material color, texture, and future color inputs are combined.
    float4 baseColor;

    // Stores the alpha channel separately so lighting code can preserve it without unpacking baseColor every time.
    float alpha;
};

// Stores per-fragment geometric inputs reserved for future lighting features such as specular, fog, and additional lights.
struct BurtInputData
{
    // Stores the world-space position of the shaded point for future distance or shadow calculations.
    float3 positionWS;

    // Stores the world-space normal used by Lambert or future BRDF lighting functions.
    float3 normalWS;

    // Stores the normalized world-space view direction for future specular or fresnel lighting.
    float3 viewDirectionWS;
};

// Applies Unity's texture scale and offset convention to mesh UV0 for the Base Map.
float2 BurtTransformBaseMapUV(float2 uv0, float4 baseMapST)
{
    // Multiplies uv0 by tiling stored in baseMapST.xy and adds offset stored in baseMapST.zw.
    return uv0 * baseMapST.xy + baseMapST.zw;
}

// Samples the material Base Map at the already transformed UV coordinate.
float4 BurtSampleBaseMap(float2 baseMapUV)
{
    // Uses Unity's sampler2D path so this helper works with the UnityCG.cginc include style already used by BurtLit.shader.
    return tex2D(_BaseMap, baseMapUV);
}

// Creates BurtSurfaceData from the already combined base color for the current fragment.
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor)
{
    // Allocates the output struct that will be filled field by field for clarity and future extension.
    BurtSurfaceData surfaceData;

    // Stores the RGB and alpha color that lighting should treat as the surface albedo input.
    surfaceData.baseColor = baseColor;

    // Copies alpha into the dedicated field so the forward pass can return it directly after lighting.
    surfaceData.alpha = baseColor.a;

    // Returns the populated surface description to the caller.
    return surfaceData;
}

#endif // BURT_INPUT_INCLUDED // Ends the include guard for BurtInput.hlsl.