// BurtRP Lit 材质属性布局文件，集中声明 UnityPerMaterial 以稳定 SRP Batcher 兼容性。
#ifndef BURT_LIT_PROPERTIES_INCLUDED // 开始 include guard，防止同一个 pass 中重复声明 UnityPerMaterial。
#define BURT_LIT_PROPERTIES_INCLUDED // 标记 BurtLitProperties.hlsl 已经被包含过，后续重复 include 会被跳过。

// UnityPerMaterial 是 Unity SRP Batcher 识别材质常量的固定 CBUFFER 名称。
CBUFFER_START(UnityPerMaterial)

    // 保存材质基础颜色，Forward 用它参与 albedo，DepthOnly 和 ShadowCaster 用它参与 alpha clip。
    float4 _BaseColor;

    // 保存 Base Map 的 Tiling 和 Offset，xy 是缩放，zw 是偏移。
    float4 _BaseMap_ST;

    // 保存 Mask Map 的 Tiling 和 Offset，xy 是缩放，zw 是偏移。
    float4 _MaskMap_ST;

    // 保存 alpha clip 开关，0 表示关闭，1 表示开启。
    float _AlphaClip;

    // 保存 alpha clip 阈值，BaseMap alpha 低于这个值时会被丢弃。
    float _Cutoff;

    // 保存法线贴图强度，Forward 用它缩放 normal map 扰动。
    float _NormalScale;

    // Back-face tangent-space normal constants for XRender-style None / Flip / Mirror modes.
    float4 _DoubleSidedNormalModeConstants;

    // 保存 XRender 风格 reflectance，Forward 会在 BRDF 内部把它映射成非金属 F0。
    float _Reflectance;

    // 保存金属度标量，最终 metallic 会等于这个标量乘以 Mask Map 的 R 通道。
    float _Metallic;

    // 保存光滑度标量，最终 smoothness 会等于这个标量乘以 Mask Map 的 A 通道。
    float _Smoothness;

    // 保存环境遮蔽强度，0 表示忽略 Mask Map 的 AO，1 表示完全使用 Mask Map 的 AO。
    float _OcclusionStrength;

    // 保存自发光颜色，Forward 用它和 Emission Map 相乘。
    float4 _EmissionColor;

    // 保存 Emission Map 的 Tiling 和 Offset，允许自发光贴图使用独立 UV 变换。
    float4 _EmissionMap_ST;

// 结束 UnityPerMaterial；所有 BurtLit pass 都包含这个文件，所以字段顺序天然一致。
CBUFFER_END

#endif // BURT_LIT_PROPERTIES_INCLUDED // 结束 BurtLitProperties.hlsl 的 include guard。