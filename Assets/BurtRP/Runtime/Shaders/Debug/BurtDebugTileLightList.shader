Shader "Hidden/BurtRP/DebugTileLightList"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Debug Tile Light List"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            StructuredBuffer<uint> _BurtTileLightCountBuffer;
            StructuredBuffer<uint> _BurtTileLightListBuffer;
            StructuredBuffer<uint2> _BurtTileLightOffsetBuffer;
            float4 _BurtTileLightGridParams; // x=tilesX, y=tilesY, z=tileSize, w=maxLightsPerTile
            float4 _BurtTileLightDebugStats; // x=minCount, y=maxCount, z=averageCount, w=additionalLightCount
            float _BurtTileLightDebugMode; // 1=tile count, 2=tile occupancy
            float _BurtTileLightCountBufferEnabled;
            sampler2D _BurtTileLightDebugColorTexture;
            float _BurtTileLightDebugColorTextureEnabled;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((float)((input.vertexID << 1) & 2u), (float)(input.vertexID & 2u));
                output.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                output.screenUV = uv;
                return output;
            }

            float3 BurtTileHeatColor(float heat)
            {
                heat = saturate(heat);
                float3 cold = float3(0.02f, 0.08f, 0.32f);
                float3 mid = float3(0.0f, 0.75f, 0.55f);
                float3 warm = float3(1.0f, 0.78f, 0.08f);
                float3 hot = float3(1.0f, 0.08f, 0.02f);
                float3 low = lerp(cold, mid, saturate(heat * 2.0f));
                float3 high = lerp(warm, hot, saturate((heat - 0.5f) * 2.0f));
                return lerp(low, high, step(0.5f, heat));
            }

            float3 BurtTileCountBandColor(float count)
            {
                if (count <= 0.5f)
                {
                    return float3(0.015f, 0.025f, 0.08f);
                }

                if (count <= 1.5f)
                {
                    return float3(0.0f, 0.42f, 1.0f);
                }

                if (count <= 2.5f)
                {
                    return float3(0.0f, 0.95f, 0.34f);
                }

                if (count <= 3.5f)
                {
                    return float3(1.0f, 0.92f, 0.05f);
                }

                if (count <= 4.5f)
                {
                    return float3(1.0f, 0.45f, 0.0f);
                }

                return float3(1.0f, 0.0f, 0.12f);
            }

            float BurtTileGridMask(float2 screenUV, float2 gridSize)
            {
                float2 tileUV = frac(screenUV * gridSize);
                float gridMask = 1.0f - step(0.045f, min(tileUV.x, tileUV.y));
                return saturate(gridMask * 0.35f);
            }

            float3 BurtTileOccupancyColor(uint tileIndex, uint rawCount, float2 tileUV)
            {
                uint maxLightsPerTile = (uint)max(round(_BurtTileLightGridParams.w), 1.0f);
                uint2 range = _BurtTileLightOffsetBuffer[tileIndex];
                uint storedCount = min(range.y, maxLightsPerTile);
                bool overflow = rawCount > maxLightsPerTile;

                float frameScale = max(min(_BurtTileLightDebugStats.y, (float)maxLightsPerTile), 1.0f);
                float capacityHeat = (float)storedCount / (float)maxLightsPerTile;
                float frameHeat = (float)storedCount / frameScale;
                float heat = max(capacityHeat, frameHeat * 0.85f);
                float3 color = storedCount > 0u ? BurtTileHeatColor(heat) : float3(0.025f, 0.04f, 0.12f);

                if (storedCount > 0u)
                {
                    uint offset = range.x;
                    uint firstLight = _BurtTileLightListBuffer[offset];
                    uint lastLight = _BurtTileLightListBuffer[offset + storedCount - 1u];
                    float tint = frac((float)(firstLight * 13u + lastLight * 7u + storedCount * 3u) * 0.1031f);
                    color = lerp(color, float3(tint, 0.35f + tint * 0.35f, 1.0f - tint * 0.25f), 0.1f);
                }

                if (overflow)
                {
                    float checker = fmod(floor(tileUV.x * 4.0f) + floor(tileUV.y * 4.0f), 2.0f);
                    color = lerp(float3(1.0f, 0.0f, 1.0f), float3(1.0f, 1.0f, 1.0f), checker * 0.35f);
                }

                return color;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                int tileCountX = max(1, (int)round(_BurtTileLightGridParams.x));
                int tileCountY = max(1, (int)round(_BurtTileLightGridParams.y));
                float2 uv = saturate(input.screenUV);
                int tileX = min((int)floor(uv.x * tileCountX), tileCountX - 1);
                int tileY = min((int)floor(uv.y * tileCountY), tileCountY - 1);
                int tileIndex = tileY * tileCountX + tileX;
                float2 tileUV = frac(uv * float2(tileCountX, tileCountY));

                if (_BurtTileLightCountBufferEnabled <= 0.5f)
                {
                    float disabledGrid = BurtTileGridMask(uv, float2(tileCountX, tileCountY));
                    float3 disabledColor = lerp(float3(0.16f, 0.0f, 0.18f), float3(1.0f, 0.15f, 1.0f), disabledGrid);
                    return float4(disabledColor, 1.0f);
                }

                float grid = BurtTileGridMask(uv, float2(tileCountX, tileCountY));
                if (_BurtTileLightDebugColorTextureEnabled > 0.5f)
                {
                    float2 tileCenterUV = (float2(tileX, tileY) + 0.5f) / float2(tileCountX, tileCountY);
                    float3 textureColor = tex2D(_BurtTileLightDebugColorTexture, tileCenterUV).rgb;
                    textureColor = lerp(textureColor, float3(1.0f, 1.0f, 1.0f), grid);
                    return float4(textureColor, 1.0f);
                }

                float debugMode = round(_BurtTileLightDebugMode);
                uint lightCount = _BurtTileLightCountBuffer[tileIndex];

                float count = (float)lightCount;
                float3 color = BurtTileCountBandColor(count);
                if (debugMode == 2.0f)
                {
                    color = BurtTileOccupancyColor((uint)tileIndex, lightCount, tileUV);
                }
                color = lerp(color, float3(1.0f, 1.0f, 1.0f), grid);
                return float4(color, 1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
