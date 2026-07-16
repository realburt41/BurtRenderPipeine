// BurtRP shading-debug mode ids and C# driven global controls.
#ifndef BURT_SHADING_DEBUG_MODES_INCLUDED
#define BURT_SHADING_DEBUG_MODES_INCLUDED

float _BurtShadingDebugMode; // 保存 C# 侧 BurtShadingDebugSettings 上传的当前调试模式编号。
float _BurtShadingDebugEnabled; // 保存 C# 侧上传的调试开关，0 表示关闭，1 表示开启。

#define BURT_SHADING_DEBUG_MODE_ALBEDO (100.0f) // 对应 C# BurtShadingDebugMode.Albedo，用来显示材质基础色。
#define BURT_SHADING_DEBUG_MODE_NORMAL_WS (101.0f) // 对应 C# BurtShadingDebugMode.NormalWS，用来显示世界空间法线。
#define BURT_SHADING_DEBUG_MODE_SMOOTHNESS (102.0f) // 对应 C# BurtShadingDebugMode.Smoothness，用来显示最终材质光滑度。
#define BURT_SHADING_DEBUG_MODE_METALLIC (103.0f) // 对应 C# BurtShadingDebugMode.Metallic，用来显示最终材质金属度。
#define BURT_SHADING_DEBUG_MODE_OCCLUSION (104.0f) // 对应 C# BurtShadingDebugMode.Occlusion，用来显示材质环境遮蔽。
#define BURT_SHADING_DEBUG_MODE_REFLECTANCE (105.0f) // 对应 C# BurtShadingDebugMode.Reflectance，用来显示 XRender 风格介质反射率。
#define BURT_SHADING_DEBUG_MODE_ROUGHNESS (106.0f) // 对应 C# BurtShadingDebugMode.Roughness，用来显示材质感知粗糙度。
#define BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS (107.0f) // 对应 C# BurtShadingDebugMode.SpecularAARoughness，用来显示直接高光实际粗糙度。
#define BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION (108.0f) // 对应 C# BurtShadingDebugMode.SpecularEnergyCompensation，用来显示直接高光能量补偿强度。
#define BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION (109.0f) // 对应 C# BurtShadingDebugMode.SpecularOcclusion，用来显示间接高光遮蔽。
#define BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION (110.0f) // 对应 C# BurtShadingDebugMode.EnergyPreservation，用来显示 XRender 底层 diffuse 保能比例。
#define BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION (111.0f) // 对应 C# BurtShadingDebugMode.IndirectSpecularEnergyCompensation，用来显示间接高光能量补偿强度。
#define BURT_SHADING_DEBUG_MODE_DIFFUSE_COLOR (112.0f) // 对应 C# BurtShadingDebugMode.DiffuseColor，用来显示 XRender GenericData.DiffuseColor。
#define BURT_SHADING_DEBUG_MODE_HEIGHT (113.0f) // Corresponds to C# BurtShadingDebugMode.Height, showing Mask Map B.
#define BURT_SHADING_DEBUG_MODE_PRESKIN_POSITION (114.0f) // Corresponds to C# BurtShadingDebugMode.PreSkinPosition, showing decoded pre-skin object position as linear [-16,16] RGB.
#define BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D (115.0f) // 对应 C# BurtShadingDebugMode.DirectBRDFD，用来显示 GGX D 项。
#define BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY (116.0f) // 对应 C# BurtShadingDebugMode.DirectBRDFVisibility，用来显示 Smith Joint Visibility。
#define BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL (117.0f) // 对应 C# BurtShadingDebugMode.DirectBRDFFresnel，用来显示 Schlick Fresnel。
#define BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE (118.0f) // 对应 C# BurtShadingDebugMode.DirectDiffuseLobe，用来显示 Lambert / Burley diffuse lobe。
#define BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF (119.0f) // 对应 C# BurtShadingDebugMode.DirectDiffuseBRDF，用来显示未乘灯光的直接 diffuse BRDF。
#define BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF (120.0f) // 对应 C# BurtShadingDebugMode.DirectSpecularBRDF，用来显示未乘灯光的直接 specular BRDF。
#define BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE (121.0f) // 对应 C# BurtShadingDebugMode.SpecularAANormalVariance，用来显示 Normal Filtering 的法线方差。
#define BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA (122.0f) // 对应 C# BurtShadingDebugMode.SpecularAARoughnessDelta，用来显示 Specular AA 增加的 roughness。
#define BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG (123.0f) // 对应 C# BurtShadingDebugMode.IndirectSpecularDFG，用来显示间接高光 DFG.xy。
#define BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF (124.0f) // 对应 C# BurtShadingDebugMode.IndirectSpecularEnvBRDF，用来显示 DFG 应用到 F0/F90 后的环境 BRDF。
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID (125.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION (126.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT (127.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT (128.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_DIRECT_TRANSMISSION (129.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR (130.0f) // 对应 C# BurtShadingDebugMode.GBufferBaseColor，用来显示 GBuffer 解码 BaseColor。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS (131.0f) // 对应 C# BurtShadingDebugMode.GBufferNormalWS，用来显示 GBuffer 解码向量槽。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC (132.0f) // 对应 C# BurtShadingDebugMode.GBufferMetallic，用来显示 GBuffer 解码材质通道。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS (133.0f) // 对应 C# BurtShadingDebugMode.GBufferSmoothness，用来显示 GBuffer 解码 Smoothness。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION (134.0f) // 对应 C# BurtShadingDebugMode.GBufferOcclusion，用来显示 GBuffer 解码 AO。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE (135.0f) // 对应 C# BurtShadingDebugMode.GBufferReflectance，用来显示 GBuffer 解码 Reflectance。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS (136.0f) // 对应 C# BurtShadingDebugMode.GBufferRoughness，用来显示 GBuffer 还原的 Base.Roughness。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_DIFFUSE_COLOR (137.0f) // 对应 C# BurtShadingDebugMode.GBufferDiffuseColor，用来显示 GBuffer 还原的 DiffuseColor。
#define BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK (141.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH (142.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS (143.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS (144.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY (145.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS (146.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS (147.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX (148.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_BRDF (149.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_SHADOW (150.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_PHASE (151.0f)
#define BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_THICKNESS (152.0f)
#define BURT_SHADING_DEBUG_MODE_GBUFFER_FOLIAGE_SCREEN_SPACE_SHADOW_INTENSITY (158.0f)
#define BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION (161.0f)
#define BURT_SHADING_DEBUG_MODE_FOLIAGE_DIRECT_TRANSMISSION (162.0f)
#define BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_BRDF (163.0f)
#define BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_SHADOW (164.0f)
#define BURT_SHADING_DEBUG_MODE_FOLIAGE_SPECULAR_BRDF (165.0f)
#define BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION (170.0f)
#define BURT_SHADING_DEBUG_MODE_GRASS_DIRECT_TRANSMISSION (171.0f)
#define BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_BRDF (172.0f)
#define BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_SHADOW (173.0f)
#define BURT_SHADING_DEBUG_MODE_GRASS_SPECULAR_BRDF (174.0f)
#define BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING (200.0f) // 对应 C# BurtShadingDebugMode.DetailLighting，用 0.18 中灰 BaseColor 显示光照细节。
#define BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING (201.0f) // 对应 C# BurtShadingDebugMode.IndirectLighting，用来显示 PBR 间接光。
#define BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE (202.0f) // 对应 C# BurtShadingDebugMode.DirectDiffuse，用来显示直接漫反射。
#define BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR (203.0f) // 对应 C# BurtShadingDebugMode.DirectSpecular，用来显示直接高光。
#define BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE (204.0f) // 对应 C# BurtShadingDebugMode.IndirectDiffuse，用来显示 SH / Light Probe 漫反射。
#define BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR (205.0f) // 对应 C# BurtShadingDebugMode.IndirectSpecular，用来显示 Reflection Probe 镜面反射。
#define BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION (206.0f) // 对应 C# BurtShadingDebugMode.ShadowAttenuation，用来显示主光阴影衰减。
#define BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION (207.0f) // 对应 C# BurtShadingDebugMode.AmbientOcclusion，用来显示参与间接光遮蔽的 AO。
#define BURT_SHADING_DEBUG_MODE_EMISSION (208.0f) // 对应 C# BurtShadingDebugMode.Emission，用来显示自发光贡献。
#define BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING (209.0f) // 对应 C# BurtShadingDebugMode.FinalLighting，用来显示写入 CameraColor 前的最终材质光照。
#define BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE (210.0f) // 对应 C# BurtShadingDebugMode.HairPrimaryLobe，用来显示 Hair primary lobe。
#define BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE (211.0f) // 对应 C# BurtShadingDebugMode.HairSecondaryLobe，用来显示 Hair secondary lobe。
#define BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE (212.0f) // 对应 C# BurtShadingDebugMode.HairTransmissionLobe，用来显示 Hair backlit/transmission lobe。
#define BURT_SHADING_DEBUG_MODE_HAIR_SCATTER (213.0f) // 对应 C# BurtShadingDebugMode.HairScatter，用来显示 Hair lighting scatter。
#define BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX (214.0f) // 对应 C# BurtShadingDebugMode.ShadowCascadeIndex，用颜色显示 CSM cascade。
#define BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND (215.0f) // 对应 C# BurtShadingDebugMode.ShadowCascadeBlend，用来显示 cascade 边界混合。
#define BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE (216.0f) // 对应 C# BurtShadingDebugMode.ShadowDistanceFade，用来显示远距离淡出。
#define BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS (217.0f) // 对应 C# BurtShadingDebugMode.ShadowPCSSRadius，用来显示 PCSS 半影半径。
#define BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA (218.0f) // 对应 C# BurtShadingDebugMode.ShadowReceiverDepthDelta，用来显示 receiver/shadow depth 差。
#define BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION (225.0f) // 对应 C# BurtShadingDebugMode.ShadowPCSSBlockerFraction，用来显示 blocker 搜索命中比例。
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING (219.0f) // 对应 C# BurtShadingDebugMode.AdditionalLighting，只显示追加光直接光。
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE (220.0f) // 对应 C# BurtShadingDebugMode.AdditionalDiffuse，只显示追加光漫反射。
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR (221.0f) // 对应 C# BurtShadingDebugMode.AdditionalSpecular，只显示追加光高光。
#define BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING (222.0f) // 对应 C# BurtShadingDebugMode.HairAdditionalLighting，只显示 Hair 追加光。
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION (226.0f) // 对应 C# BurtShadingDebugMode.AdditionalShadowAttenuation，用来显示追加光阴影衰减。
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED (227.0f) // 对应 C# BurtShadingDebugMode.AdditionalLightingUnshadowed，用来对照追加光未乘阴影时的贡献。
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE (228.0f)
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV (229.0f)
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH (230.0f)
#define BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA (231.0f)
#define BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RECEIVER_DEPTH (234.0f)
#define BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RAW_DEPTH (235.0f)
#define BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_COMPARE (236.0f)
#define BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_PROJECTION_VALIDITY (237.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_OBJECT_INDEX (476.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_SLICE (477.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_UV (478.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_DEPTH (479.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_COMPARE (480.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_DEPTH (481.0f)
#define BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_THICKNESS (493.0f)
#define BURT_SHADING_DEBUG_MODE_GI_PROBE_IRRADIANCE (503.0f)
#define BURT_SHADING_DEBUG_MODE_GI_PROBE_VALIDITY (504.0f)
#define BURT_SHADING_DEBUG_MODE_GI_PROBE_SKY_VISIBILITY (505.0f)
#define BURT_SHADING_DEBUG_MODE_SCREEN_SPACE_GLOBAL_ILLUMINATION_SCENE_VOXEL_OCCUPANCY (506.0f)
#define BURT_SHADING_DEBUG_MODE_GI_PROBE_RUNTIME_INFO (507.0f)
#define BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT (8.0f)

#endif // BURT_SHADING_DEBUG_MODES_INCLUDED
