
# BurtRenderPipeine



### Default Lit (`modelID = 0`, `stencil = 32`) / `BurtRP/Lit`

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / R8G8B8A8_SRGB|UNorm</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">NormalWS Octa / 32 bit</td>
<td align="center">Compat Packed Model + Metallic / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center"><code>GBuffer1.b</code> 位于 Default Lit bucket 时表示 Metallic</td>
</tr>
<tr>
<td align="center">GBuffer2 / ARGBHalf</td>
<td colspan="3" align="center">Emission.rgb / 48 bit</td>
<td align="center">Reflectance / 16f</td>
<td align="center">固定 half，保留 alpha reflectance</td>
</tr>
<tr>
<td align="center">GBuffer3 / R8G8B8A8_UNorm</td>
<td colspan="2" align="center">Reserved Normal Mirror / 16 bit</td>
<td colspan="2" align="center">Reserved / 16 bit</td>
<td align="center">写入安全默认值，避免未绑定采样</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">TangentWS Octa / 32 bit</td>
<td align="center">Signed Anisotropy, Encoded 0..1 / 16f</td>
<td align="center">Reserved / 16f</td>
<td align="center">直接光使用 anisotropic GGX，anisotropy 为 0 时退化为普通 GGX</td>
</tr>
</tbody>
</table>

### Hair (`modelID = 1`, `stencil = 96`) / `BurtRP/Hair`

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / R8G8B8A8_SRGB|UNorm</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">StrandDirectionWS Octa / 32 bit</td>
<td align="center">Compat Packed Model + Hair Payload / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center">Hair Payload 打包 Scatter 和 Longitudinal Shift Scale</td>
</tr>
<tr>
<td align="center">GBuffer2 / ARGBHalf</td>
<td colspan="3" align="center">Emission.rgb / 48 bit</td>
<td align="center">Reflectance / 16f</td>
<td align="center">Reflectance 包含 Hair Specular Scale</td>
</tr>
<tr>
<td align="center">GBuffer3 / R8G8B8A8_UNorm</td>
<td colspan="4" align="center">Low precision custom / 32 bit</td>
<td align="center">预留给后续覆盖率、第二方向或深度不透明度</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">Fallback Tangent Octa / 32 bit</td>
<td colspan="2" align="center">Reserved / 32 bit</td>
<td align="center">Hair 不使用 Lit anisotropy 标量，方向性由 strand/lobe 表达</td>
</tr>
</tbody>
</table>

### Clear Coat (`modelID = 2`, `stencil = 128`) / `BurtRP/Clear Coat`

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / R8G8B8A8_SRGB|UNorm</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">Base NormalWS Octa / 32 bit</td>
<td align="center">Compat Packed Model + Metallic / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center">底层材质保留 Default Lit 语义</td>
</tr>
<tr>
<td align="center">GBuffer2 / ARGBHalf</td>
<td colspan="3" align="center">Emission.rgb / 48 bit</td>
<td align="center">Reflectance / 16f</td>
<td align="center">固定 half，保留 alpha reflectance</td>
</tr>
<tr>
<td align="center">GBuffer3 / R8G8B8A8_UNorm</td>
<td colspan="2" align="center">ClearCoatNormalWS Octa / 16 bit</td>
<td align="center">ClearCoatMask / 8 bit</td>
<td align="center">ClearCoatRoughness / 8 bit</td>
<td align="center">顶层 coat 的直接高光、IBL、SSR 读取该法线和粗糙度</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">Base TangentWS Octa / 32 bit</td>
<td align="center">Base Signed Anisotropy, Encoded 0..1 / 16f</td>
<td align="center">Reserved / 16f</td>
<td align="center">顶层 coat 保持 isotropic，底层继续使用 tangent / anisotropy</td>
</tr>
</tbody>
</table>

### Subsurface (`modelID = 3`, `stencil = 64`) / `BurtRP/Subsurface`

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / R8G8B8A8_SRGB|UNorm</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">NormalWS Octa / 32 bit</td>
<td align="center">Compat Packed Model + SubsurfaceStrength / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center">Subsurface 按非金属处理，Material Scalar 表示 Strength</td>
</tr>
<tr>
<td align="center">GBuffer2 / ARGBHalf</td>
<td colspan="3" align="center">Emission.rgb / 48 bit</td>
<td align="center">Reflectance / 16f</td>
<td align="center">Profile Index 不占用 GBuffer2</td>
</tr>
<tr>
<td align="center">GBuffer3 / R8G8B8A8_UNorm</td>
<td colspan="3" align="center">SubsurfaceTint.rgb / 24 bit</td>
<td align="center">Packed Power + Ambient / 8 bit</td>
<td align="center">直射 LUT、transmission 和 5S blur 共享这些控制量</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">TangentWS Octa / 32 bit</td>
<td align="center">SubsurfaceDistortion / 16f</td>
<td align="center">Packed Thickness + ProfileIndex / 16f</td>
<td align="center">Profile Index 映射到管线资产的 0-7 Profile Palette</td>
</tr>
</tbody>
</table>

## Feature

- Material
    - Common
        - [x] DefaultLit (Aniso)
        - [x] Clear Coat
        - [ ] Crystal
    - Character
        - Subsurface
            - [x] 5S
            - [x] 4S
            - [x] 4S
            - [x] Dual Specular
            - [ ] Transmission
        - Hair
            - [ ] Dither
            - [ ] Dual Pass
        - [ ] Eye
        - [ ] Skinne Decal
        - [ ] Combine Texture Decal
    - Environment
        - [ ] Ocean
        - Weather
            - [ ] Wetness
            - [ ] Rain
            - [ ] Snow
            - [ ] Thunder
        - [ ] Foliage
        - [ ] Grass
        - [ ] Flower
        - [ ] Rock
        - [ ] DBuffer Decal
        - Cloud
            - [ ] Cloud Skybox
            - [ ] Light Function
        - [ ] Interior Mapping
    - Fur
        - [ ] Fur Shading
        - [x] Fur Blur
    - Feature
        - [ ] Glint
- Post Processing
    - Anti-Aliasing
        - [ ] TAA
        - [ ] SMAA
    - Exposure
        - [x] Mannual Exposure
        - [ ] Physical Light Units
        - [ ] Auto Exposure
        - [ ] Local Exposure
    - Environment
        - GI
            - [x] SSR
            - [ ] Screen Space Global Illumination
            - Ambient Occlusion
                - [x] SSAO
                - [x] GTAO
                - [ ] HBAO
            - [ ] SSDO
        - Fog
            - [x] Height Fog
            - [ ] Volumetric Fog
        - [ ] Atmosphere
        - [ ] Screen Space Shadow
    - [x] Color Grading
    - [x] Tone Mapping
    - [x] Bloom
    - [ ] Vignette
    - [ ] Light Shaft
    - [ ] Lens Flare
    - [ ] FSR Sharpness
- Shadow
    - [x] Main Light Shadow
    - [x] Additional Light Shadow
    - [ ] CSM
    - [ ] Per Object Shadow
- Lighting
    - [x] Main Light
    - [x] Additional Lighting
    - [ ] Tile Lighting
    - [ ] Cluster Lighting
    - [x] Sky Light
    - [x] Reflection Probe
- Other Feature
    - [x] HIZ Depth
    - [x] IBL
    - [x] Shading Debug
