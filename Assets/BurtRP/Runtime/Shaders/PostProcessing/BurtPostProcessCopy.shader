// 定义 Shader 在 Unity 内部查找时使用的隐藏路径。
Shader "Hidden/BurtRP/PostProcessCopy"
{
    // 定义 SubShader，当前后处理框架使用一个全屏后处理 SubShader 承载 No-op Copy 和 Tonemapping。
    SubShader
    {
        // 给 SubShader 打标签，标记这是 BurtRP 专用 shader。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义一个全屏后处理 Pass，用来执行 No-op Copy 或 Tonemapping。
        Pass
        {
            // 给 Pass 起名，方便 Frame Debugger 中识别。
            Name "Burt Post Process Copy"

            // 关闭剔除，因为全屏三角形不依赖正反面。
            Cull Off

            // 关闭深度写入，避免后处理污染 CameraDepth。
            ZWrite Off

            // 始终通过深度测试，让拷贝覆盖整个颜色目标。
            ZTest Always

            // 开始 HLSL 程序。
            HLSLPROGRAM

            // 使用 shader model 3.5，保证可以用 SV_VertexID 生成全屏三角形。
            #pragma target 3.5

            // 声明顶点 shader 函数名。
            #pragma vertex Vert

            // 声明片元 shader 函数名。
            #pragma fragment Frag

            // 引入 Unity 基础 shader 宏，保持和现有 FinalBlit shader 风格一致。
            #include "UnityCG.cginc"

            // 声明当前后处理使用的源纹理，C# Pass 会在每次绘制前设置它。
            sampler2D _BurtPostProcessSourceTexture;

            // 声明 Bloom 合成纹理，C# Pass 会在最终 Tonemapping 前绑定 mip0。
            sampler2D _BurtBloomTexture;

            // 声明是否在最终合成中启用 Bloom。
            float _BurtUseBloom;

            // 声明 Bloom 合成强度。
            float _BurtBloomIntensity;

            // 声明 Bloom 是否把强度写入最终 alpha。
            float _BurtUseBloomAlpha;

            // 声明是否执行 Color Adjustments，0 表示关闭，1 表示启用。
            float _BurtUseColorAdjustments;

            // 声明 Color Adjustments 饱和度，1 表示保持原饱和度。
            float _BurtColorAdjustmentsSaturation;

            // 声明 Color Adjustments 对比度，1 表示保持原对比度。
            float _BurtColorAdjustmentsContrast;

            // 声明 Color Adjustments Gamma，1 表示保持原明暗曲线。
            float _BurtColorAdjustmentsGamma;

            // 声明 Color Adjustments 颜色滤镜，白色表示不额外染色。
            float4 _BurtColorAdjustmentsColorFilter;

            // 声明 Tonemapping 模式，0 表示 None，1 表示 Neutral，2 表示 XRender / UE Filmic ACES。
            float _BurtTonemappingMode;

            // 声明 Tonemapping 前使用的线性曝光倍率，1 表示不改变亮度。
            float _BurtPostExposure;

            // 声明 UE/XRender Film Slope，默认 0.88，对齐 XRender TonemappingComponent。
            float _BurtFilmSlope;

            // 声明 UE/XRender Film Toe，默认 0.55，对齐 XRender TonemappingComponent。
            float _BurtFilmToe;

            // 声明 UE/XRender Film Shoulder，默认 0.26，对齐 XRender TonemappingComponent。
            float _BurtFilmShoulder;

            // 声明 UE/XRender Film Black Clip，默认 0.0，对齐 XRender TonemappingComponent。
            float _BurtFilmBlackClip;

            // 声明 UE/XRender Film White Clip，默认 0.04，对齐 XRender TonemappingComponent。
            float _BurtFilmWhiteClip;

            // 声明 XRender CombineLUT 使用的 Blue Correction 强度，默认 0.6。
            float _BurtFilmBlueCorrection;

            // 声明 XRender CombineLUT 使用的 Expand Gamut 强度，默认 1.0。
            float _BurtFilmExpandGamut;

            // 声明 XRender CombineLUT 使用的 Tone Curve Amount，默认 1.0。
            float _BurtFilmToneCurveAmount;

            // 定义圆周率常量，用于把 atan2 得到的弧度转换成角度。
            static const float BURT_PI = 3.14159265358979323846;

            // 定义 AP0 到 AP1 的转换矩阵，来源和 XRender Shaders/Library/ACES.hlsl 保持一致。
            static const float3x3 BURT_AP0_TO_AP1 = float3x3(
                1.4514393161, -0.2365107469, -0.2149285693,
                -0.0765537734, 1.1762296998, -0.0996759264,
                0.0083161484, -0.0060324498, 0.9977163014);

            // 定义 AP1 到 AP0 的转换矩阵，UE/XRender 的 RRT Glow 和 Red Modifier 在 AP0 中执行。
            static const float3x3 BURT_AP1_TO_AP0 = float3x3(
                0.6954522414, 0.1406786965, 0.1638690622,
                0.0447945634, 0.8596711185, 0.0955343182,
                -0.0055258826, 0.0040252103, 1.0015006723);

            // 定义线性 sRGB 到 ACEScg/AP1 的转换矩阵，直接使用 XRender ACES.hlsl 中已经合并白点适配后的版本。
            static const float3x3 BURT_SRGB_TO_AP1 = float3x3(
                0.6130974290, 0.3395231370, 0.0473794527,
                0.0701937228, 0.9163538810, 0.0134523986,
                0.0206155926, 0.1095697730, 0.8698146340);

            // 定义 ACEScg/AP1 到线性 sRGB 的转换矩阵，直接使用 XRender ACES.hlsl 中已经合并白点适配后的版本。
            static const float3x3 BURT_AP1_TO_SRGB = float3x3(
                1.7050509500, -0.6217920180, -0.0832588672,
                -0.1302564140, 1.1408046500, -0.0105483187,
                -0.0240033530, -0.1289689690, 1.1529723400);

            // 定义 AP1 的亮度权重，XRender FilmToneMap 的预去饱和和后去饱和使用这组权重。
            static const float3 BURT_AP1_RGB_TO_Y = float3(0.2722287168, 0.6740817658, 0.0536895174);

            // 定义 XRender CombineLUT 使用的 Blue Correction 矩阵，用于修正高亮蓝色偏紫的问题。
            static const float3x3 BURT_BLUE_CORRECT = float3x3(
                0.9404372683, -0.0183068787, 0.0778696104,
                0.0083786969, 0.8286599939, 0.1629613092,
                0.0005471261, -0.0008833746, 1.0003362486);

            // 定义 XRender CombineLUT 使用的 Blue Correction 逆矩阵，用于 Tonemapping 后恢复白点。
            static const float3x3 BURT_BLUE_CORRECT_INV = float3x3(
                1.0631800000, 0.0233956000, -0.0865726000,
                -0.0106337000, 1.2063200000, -0.1956900000,
                -0.0005908870, 0.0010524800, 0.9995380000);

            // 定义 XRender CombineLUT 使用的 Wide Gamut 到 XYZ 矩阵，用于扩展高饱和颜色。
            static const float3x3 BURT_WIDE_TO_XYZ = float3x3(
                0.5441691000, 0.2395926000, 0.1666943000,
                0.2394656000, 0.7021530000, 0.0583814000,
                -0.0023439000, 0.0361834000, 1.0552183000);

            // 定义 XYZ 到 AP1 的转换矩阵，和上方 Wide Gamut 扩展矩阵配合使用。
            static const float3x3 BURT_XYZ_TO_AP1 = float3x3(
                1.6410233797, -0.3248032942, -0.2364246952,
                -0.6636628587, 1.6153315917, 0.0167563477,
                0.0117218943, -0.0082844420, 0.9883948585);

            // 定义顶点输入结构，全屏三角形只需要系统顶点 ID。
            struct Attributes
            {
                // 读取当前程序化顶点编号，取值为 0、1、2。
                uint vertexID : SV_VertexID;
            };

            // 定义顶点输出结构，传递裁剪空间位置和屏幕 UV。
            struct Varyings
            {
                // 输出裁剪空间位置，供 GPU 光栅化使用。
                float4 positionCS : SV_POSITION;

                // 输出屏幕 UV，供片元 shader 采样源纹理。
                float2 uv : TEXCOORD0;
            };

            // 定义把 RGB 转成饱和度的函数，UE/XRender 的 RRT Glow 和 Red Modifier 会使用它。
            float BurtRgbToSaturation(float3 rgb)
            {
                // 取三个通道的最小值，用于估算颜色离灰轴的距离。
                float minRgb = min(min(rgb.r, rgb.g), rgb.b);

                // 取三个通道的最大值，用于估算颜色离灰轴的距离。
                float maxRgb = max(max(rgb.r, rgb.g), rgb.b);

                // 按 ACES/UE 的写法归一化饱和度，并用很小的下限避免除零。
                return (max(maxRgb, 1e-10) - max(minRgb, 1e-10)) / max(maxRgb, 1e-2);
            }

            // 定义 ACES 的 YC 亮度代理函数，UE/XRender 的 Glow 模块用它判断高亮范围。
            float BurtRgbToYc(float3 rgb)
            {
                // 读取红色通道，保持公式和 ACES 参考实现一致。
                float r = rgb.r;

                // 读取绿色通道，保持公式和 ACES 参考实现一致。
                float g = rgb.g;

                // 读取蓝色通道，保持公式和 ACES 参考实现一致。
                float b = rgb.b;

                // 计算色度项，max 用来避免负数精度误差进入 sqrt。
                float chroma = sqrt(max(b * (b - g) + g * (g - r) + r * (r - b), 0.0));

                // 返回 ACES YC 亮度代理值，1.75 是 XRender/UE 默认的 chroma 权重。
                return (b + g + r + 1.75 * chroma) / 3.0;
            }

            // 定义 Sigmoid 形状函数，UE/XRender 用它让 Glow 只在指定饱和度附近过渡。
            float BurtSigmoidShaper(float value)
            {
                // 把输入压到 -2..2 附近的柔和过渡范围。
                float t = max(1.0 - abs(0.5 * value), 0.0);

                // 输出 0..1 的 S 形曲线结果。
                return 0.5 * (1.0 + sign(value) * (1.0 - t * t));
            }

            // 定义 Glow 前向函数，UE/XRender 的 RRT Glow 会用它给特定亮度和饱和度添加轻微光晕感。
            float BurtGlowForward(float ycIn, float glowGainIn, float glowMid)
            {
                // 如果亮度代理值低于中点的三分之二，就使用完整 Glow 增益。
                if (ycIn <= 2.0 / 3.0 * glowMid)
                {
                    // 返回完整增益，让低亮区域的 Glow 行为和 XRender 一致。
                    return glowGainIn;
                }

                // 如果亮度代理值高于两倍中点，就不再增加 Glow。
                if (ycIn >= 2.0 * glowMid)
                {
                    // 返回 0，避免高亮区域过度发光。
                    return 0.0;
                }

                // 在中间区域按 ACES/UE 公式平滑衰减 Glow 增益。
                return glowGainIn * (glowMid / ycIn - 0.5);
            }

            // 定义 RGB 到 Hue 角度的函数，UE/XRender 的红色修正模块会使用它。
            float BurtRgbToHue(float3 rgb)
            {
                // 如果三个通道完全相等，Hue 没有意义，这里按 XRender 做法返回 0。
                if (rgb.r == rgb.g && rgb.g == rgb.b)
                {
                    // 返回 0，避免中性色产生 NaN。
                    return 0.0;
                }

                // 按 ACES 几何 Hue 公式计算角度，输出单位是度。
                float hue = (180.0 / BURT_PI) * atan2(sqrt(3.0) * (rgb.g - rgb.b), 2.0 * rgb.r - rgb.g - rgb.b);

                // 如果角度为负，就加 360 变成 0..360 范围。
                hue = hue < 0.0 ? hue + 360.0 : hue;

                // 把结果限制到合法角度范围，避免极端输入造成异常。
                return clamp(hue, 0.0, 360.0);
            }

            // 定义 Hue 重新居中的函数，UE/XRender 红色修正会围绕目标 Hue 计算权重。
            float BurtCenterHue(float hue, float centerHue)
            {
                // 先把 Hue 平移到以目标 Hue 为中心的坐标。
                float centeredHue = hue - centerHue;

                // 如果角度小于 -180，就加 360 回到最近的等价角。
                centeredHue = centeredHue < -180.0 ? centeredHue + 360.0 : centeredHue;

                // 如果角度大于 180，就减 360 回到最近的等价角。
                centeredHue = centeredHue > 180.0 ? centeredHue - 360.0 : centeredHue;

                // 返回重新居中后的 Hue。
                return centeredHue;
            }

            // 定义中性 Tonemapping 曲线，用简单压缩把 HDR 颜色映射到 0..1 附近。
            float3 BurtTonemapNeutral(float3 color)
            {
                // 保证进入曲线的颜色不会是负值，避免负 HDR 值造成奇怪的压缩结果。
                color = max(color, 0.0);

                // 使用 Reinhard 风格的简单压缩，亮度越高越接近 1，但不会突然截断。
                return color / (color + 1.0);
            }

            // 定义 UE/XRender 的 FilmToneMap 核心曲线，输入和输出都在 ACEScg/AP1 空间。
            float3 BurtFilmToneMapAP1(float3 colorAP1)
            {
                // 给 Film Slope 加安全下限，避免用户把 Volume 参数拖到 0 时出现除零。
                float filmSlope = max(_BurtFilmSlope, 1e-5);

                // 把 AP1 转到 AP0，因为 UE/XRender 的 Glow 和红色修正模块在 AP0 中执行。
                float3 colorAP0 = mul(BURT_AP1_TO_AP0, colorAP1);

                // 计算 AP0 饱和度，后面 Glow 和红色修正都需要它。
                float saturation = BurtRgbToSaturation(colorAP0);

                // 计算 ACES YC 亮度代理值，用于 Glow 强度衰减。
                float ycIn = BurtRgbToYc(colorAP0);

                // 根据饱和度计算 Glow 权重，0.4 和 0.2 对齐 XRender TonemapCommon.hlsl。
                float glowWeight = BurtSigmoidShaper((saturation - 0.4) / 0.2);

                // 计算最终 Glow 倍率，0.05 和 0.08 对齐 XRender/UE 的 RRT Glow 常量。
                float addedGlow = 1.0 + BurtGlowForward(ycIn, 0.05 * glowWeight, 0.08);

                // 把 Glow 倍率应用到 AP0 颜色上。
                colorAP0 *= addedGlow;

                // 计算当前颜色的 Hue，用于定位红色修正范围。
                float hue = BurtRgbToHue(colorAP0);

                // 把 Hue 以红色中心点重新居中，XRender 当前红色中心为 0 度。
                float centeredHue = BurtCenterHue(hue, 0.0);

                // 计算红色修正权重，平方的 smoothstep 形式对齐 XRender 当前实现。
                float hueWeight = smoothstep(0.0, 1.0, 1.0 - abs(2.0 * centeredHue / 135.0));

                // 再乘一次自身，得到 XRender 注释里提到的 UE Square 权重。
                hueWeight *= hueWeight;

                // 对红色通道做 ACES RRT Red Modifier，让高饱和红色更接近 UE/XRender 外观。
                colorAP0.r += hueWeight * saturation * (0.03 - colorAP0.r) * (1.0 - 0.82);

                // 把修正后的 AP0 转回 AP1，进入 Film 曲线计算。
                float3 workingColor = mul(BURT_AP0_TO_AP1, colorAP0);

                // 保证曲线输入非负，避免 log10 处理负数。
                workingColor = max(workingColor, 0.0);

                // 计算 AP1 亮度，用于预去饱和。
                float workingLuma = dot(workingColor, BURT_AP1_RGB_TO_Y);

                // 执行 XRender/UE 的预去饱和，0.96 是 TonemapCommon.hlsl 中的默认值。
                workingColor = lerp(workingLuma.xxx, workingColor, 0.96);

                // 计算 Toe 段缩放，并加安全下限，避免极端参数导致除零。
                float toeScale = max(1.0 + _BurtFilmBlackClip - _BurtFilmToe, 1e-5);

                // 计算 Shoulder 段缩放，并加安全下限，避免极端参数导致除零。
                float shoulderScale = max(1.0 + _BurtFilmWhiteClip - _BurtFilmShoulder, 1e-5);

                // 定义 UE/XRender 用来匹配中灰输入的亮度值。
                const float inMatch = 0.18;

                // 定义 UE/XRender 用来匹配中灰输出的亮度值。
                const float outMatch = 0.18;

                // 声明 ToeMatch，后续根据 Toe 参数选择不同求解方式。
                float toeMatch;

                // 如果 Toe 很大，中灰落在直线段，用直线公式求 ToeMatch。
                if (_BurtFilmToe > 0.8)
                {
                    // 按 XRender/UE 的直线段公式求 ToeMatch。
                    toeMatch = (1.0 - _BurtFilmToe - outMatch) / filmSlope + log10(inMatch);
                }
                else
                {
                    // 计算 Toe 段的辅助变量，用于让 0.18 输入匹配 0.18 输出。
                    float bt = (outMatch + _BurtFilmBlackClip) / toeScale - 1.0;

                    // 把 bt 限制在安全范围，避免极端参数让 log 出现无穷大。
                    bt = clamp(bt, -0.999999, 0.999999);

                    // 按 XRender/UE 的 Toe 段公式求 ToeMatch。
                    toeMatch = log10(inMatch) - 0.5 * log((1.0 + bt) / (1.0 - bt)) * (toeScale / filmSlope);
                }

                // 计算直线段匹配点，决定中间段在 log 空间中的位置。
                float straightMatch = (1.0 - _BurtFilmToe) / filmSlope - toeMatch;

                // 计算 Shoulder 匹配点，决定高光肩部开始位置。
                float shoulderMatch = _BurtFilmShoulder / filmSlope - straightMatch;

                // 对工作颜色取 log10，使用 1e-6 下限避免黑色像素产生 -inf。
                float3 logColor = log10(max(workingColor, 1e-6));

                // 计算中间直线段输出。
                float3 straightColor = filmSlope * (logColor + straightMatch);

                // 计算 Toe 曲线输出，也就是暗部压缩段。
                float3 toeColor = -_BurtFilmBlackClip + (2.0 * toeScale) / (1.0 + exp((-2.0 * filmSlope / toeScale) * (logColor - toeMatch)));

                // 计算 Shoulder 曲线输出，也就是高光压缩段。
                float3 shoulderColor = (1.0 + _BurtFilmWhiteClip) - (2.0 * shoulderScale) / (1.0 + exp((2.0 * filmSlope / shoulderScale) * (logColor - shoulderMatch)));

                // 生成 Toe 选择权重，logColor 小于 ToeMatch 时为 1。
                float3 toeSelector = 1.0 - step(toeMatch, logColor);

                // 在非 Toe 区域使用直线段，在 Toe 区域使用 Toe 曲线。
                toeColor = lerp(straightColor, toeColor, toeSelector);

                // 生成 Shoulder 选择权重，logColor 大于 ShoulderMatch 时为 1。
                float3 shoulderSelector = step(shoulderMatch, logColor);

                // 在非 Shoulder 区域使用直线段，在 Shoulder 区域使用 Shoulder 曲线。
                shoulderColor = lerp(straightColor, shoulderColor, shoulderSelector);

                // 计算 Toe 到 Shoulder 之间的平滑混合参数。
                float3 blendT = saturate((logColor - toeMatch) / (shoulderMatch - toeMatch));

                // 如果 ShoulderMatch 小于 ToeMatch，就反转混合方向，保持极端参数稳定。
                blendT = shoulderMatch < toeMatch ? 1.0 - blendT : blendT;

                // 使用 smoothstep 等价多项式，让曲线段之间过渡更平滑。
                blendT = (3.0 - 2.0 * blendT) * blendT * blendT;

                // 在 Toe 和 Shoulder 结果之间混合得到最终 Film 曲线输出。
                float3 toneColor = lerp(toeColor, shoulderColor, blendT);

                // 计算 Tonemapping 后的 AP1 亮度，用于后去饱和。
                float toneLuma = dot(toneColor, BURT_AP1_RGB_TO_Y);

                // 执行 XRender/UE 的后去饱和，0.93 是 TonemapCommon.hlsl 中的默认值。
                toneColor = lerp(toneLuma.xxx, toneColor, 0.93);

                // 返回非负 AP1 颜色，避免后续转回 sRGB 时出现负输出。
                return max(toneColor, 0.0);
            }

            // 定义 XRender CombineLUT 风格的 UE Filmic Tonemapping，输入和输出都使用线性 sRGB。
            float3 BurtTonemapXRenderUE(float3 color)
            {
                // 保证进入曲线的颜色不会是负值，避免矩阵和 log 过程产生异常。
                color = max(color, 0.0);

                // 把线性 sRGB 转到 AP1，和 XRender CombineLUT 的 FilmToneMap 输入空间一致。
                float3 colorAP1 = mul(BURT_SRGB_TO_AP1, color);

                // 计算 AP1 亮度，用于 XRender 的 Expand Gamut 权重。
                float lumaAP1 = max(dot(colorAP1, BURT_AP1_RGB_TO_Y), 1e-5);

                // 计算色度离灰轴的距离，用于决定高饱和颜色扩展强度。
                float3 chromaAP1 = colorAP1 / lumaAP1;

                // 计算色度距离平方，和 XRender CombineLUT 保持同一形态。
                float chromaDistSqr = dot(chromaAP1 - 1.0, chromaAP1 - 1.0);

                // 计算扩展强度，_BurtFilmExpandGamut 默认 1，对齐 XRender ColorGrading 默认值。
                float expandAmount = (1.0 - exp2(-4.0 * chromaDistSqr)) * (1.0 - exp2(-4.0 * _BurtFilmExpandGamut * lumaAP1 * lumaAP1));

                // 把 Wide Gamut 矩阵转换到 AP1 空间，用来模拟 XRender 的 ExpandMat。
                float3x3 wideToAP1 = mul(BURT_XYZ_TO_AP1, BURT_WIDE_TO_XYZ);

                // 把 AP1 到 sRGB 的矩阵接到后面，得到 XRender CombineLUT 中的 ExpandMat。
                float3x3 expandMat = mul(wideToAP1, BURT_AP1_TO_SRGB);

                // 计算扩展后的 AP1 颜色。
                float3 colorExpand = mul(expandMat, colorAP1);

                // 按扩展强度混合原 AP1 和扩展 AP1。
                colorAP1 = lerp(colorAP1, colorExpand, expandAmount);

                // 把 Blue Correction 矩阵转换到 AP1 空间，和 XRender CombineLUT 的 BlueCorrectAP1 一致。
                float3x3 blueCorrectAP1 = mul(BURT_AP0_TO_AP1, mul(BURT_BLUE_CORRECT, BURT_AP1_TO_AP0));

                // 把 Blue Correction 逆矩阵转换到 AP1 空间，和 XRender CombineLUT 的 BlueCorrectInvAP1 一致。
                float3x3 blueCorrectInvAP1 = mul(BURT_AP0_TO_AP1, mul(BURT_BLUE_CORRECT_INV, BURT_AP1_TO_AP0));

                // 在 FilmToneMap 前应用蓝色修正，默认强度 0.6 对齐 XRender。
                colorAP1 = lerp(colorAP1, mul(blueCorrectAP1, colorAP1), _BurtFilmBlueCorrection);

                // 执行 UE/XRender FilmToneMap，输出仍然在 AP1 空间。
                float3 tonemappedAP1 = BurtFilmToneMapAP1(colorAP1);

                // 按 Tone Curve Amount 混合原始 AP1 和曲线结果，默认 1 表示完全使用曲线。
                colorAP1 = lerp(colorAP1, tonemappedAP1, _BurtFilmToneCurveAmount);

                // 在 FilmToneMap 后应用蓝色修正逆矩阵，用来保持白点。
                colorAP1 = lerp(colorAP1, mul(blueCorrectInvAP1, colorAP1), _BurtFilmBlueCorrection);

                // 把 AP1 转回线性 sRGB，并裁掉负值。
                float3 filmColor = max(0.0, mul(BURT_AP1_TO_SRGB, colorAP1));

                // 返回线性 LDR 颜色；BurtRP 的最终输出仍交给 FinalBlit 和 Unity 目标格式处理。
                return filmColor;
            }

            // 根据 C# 上传的模式选择具体 Tonemapping 曲线。
            float3 BurtApplyTonemapping(float3 color)
            {
                // Apply exposure before any tone curve. Exposure remains valid even when tonemapping is disabled.
                color *= _BurtPostExposure;

                // 如果模式小于 0.5，就认为是 None，直接返回原始颜色。
                if (_BurtTonemappingMode < 0.5)
                {
                    // 返回未修改的颜色，保证 No-op Copy 不改变画面。
                    return color;
                }

                // 如果模式小于 1.5，就认为是 Neutral，走中性压缩曲线。
                if (_BurtTonemappingMode < 1.5)
                {
                    // 返回 Neutral 曲线处理后的颜色。
                    return BurtTonemapNeutral(color);
                }

                // 其他当前已知模式走 XRender / UE Filmic ACES 曲线。
                return BurtTonemapXRenderUE(color);
            }

            // 定义 Color Adjustments 应用函数，让 Frag 中只关心执行顺序。
            float3 BurtApplyColorAdjustments(float3 color)
            {
                // 如果 C# 没有启用 Color Adjustments，就保持颜色原样。
                if (_BurtUseColorAdjustments < 0.5)
                {
                    // 返回未修改的颜色，保证 No-op 和纯 Tonemapping 路径不受影响。
                    return color;
                }

                // 去掉负值，避免后面的 Gamma 曲线遇到负数输入。
                color = max(color, 0.0);

                // 使用线性 Rec.709 亮度权重计算灰度，用于饱和度调整。
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));

                // 按饱和度参数在灰度和原色之间插值，1 表示不变。
                color = lerp(luma.xxx, color, _BurtColorAdjustmentsSaturation);

                // 按 0.5 作为 LDR 中点调整对比度，1 表示不变。
                color = (color - 0.5) * _BurtColorAdjustmentsContrast + 0.5;

                // 乘以颜色滤镜，白色滤镜表示不改变 RGB。
                color *= _BurtColorAdjustmentsColorFilter.rgb;

                // 给 Gamma 加安全下限，避免极端参数产生无穷指数。
                float safeGamma = max(_BurtColorAdjustmentsGamma, 0.001);

                // 应用 Gamma 曲线，Gamma 大于 1 时会让中间调变亮。
                color = pow(max(color, 0.0), 1.0 / safeGamma);

                // 返回非负结果，不强行截断高于 1 的 HDR 值，避免关闭 Tonemapping 时丢失高光。
                return max(color, 0.0);
            }

            // 定义顶点 shader，用三个程序化顶点生成覆盖全屏的大三角形。
            Varyings Vert(Attributes input)
            {
                // 创建输出结构变量，用来保存顶点 shader 的结果。
                Varyings output;

                // 根据 vertexID 生成 0..2 范围的全屏三角形 UV。
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);

                // 把 UV 转换成裁剪空间坐标，形成覆盖屏幕的三角形。
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);

                // 中间 RT 到中间 RT 的拷贝不做 Y 翻转，最终方向仍交给 FinalBlit 统一处理。
                output.uv = uv;

                // 返回顶点 shader 输出。
                return output;
            }

            float3 BurtSafeBloomHdrColor(float3 color)
            {
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                return min(max(color, 0.0), 65504.0);
            }

            // 定义片元 shader，根据当前模式原样输出或执行 Tonemapping。
            float4 Frag(Varyings input) : SV_Target
            {
                // 从当前源纹理采样颜色。
                float4 color = tex2D(_BurtPostProcessSourceTexture, input.uv);

                // Bloom 在 Tonemapping 前合回 HDR 颜色，保证现有 Tonemapping 继续处理最终高光。
                if (_BurtUseBloom > 0.5)
                {
                    float4 bloom = tex2D(_BurtBloomTexture, input.uv);
                    bloom.rgb = BurtSafeBloomHdrColor(bloom.rgb);
                    color.rgb += bloom.rgb * _BurtBloomIntensity;
                    if (_BurtUseBloomAlpha > 0.5)
                    {
                        color.a = max(color.a, saturate(bloom.a * max(_BurtBloomIntensity, 0.0)));
                    }
                }

                // 对 RGB 执行 Tonemapping，Alpha 保持原样，避免破坏后续可能依赖透明度的目标。
                color.rgb = BurtApplyTonemapping(color.rgb);

                // 对 Tonemapping 后的 RGB 执行 Color Adjustments，未启用时保持原样。
                color.rgb = BurtApplyColorAdjustments(color.rgb);

                // 返回处理后的颜色，None 模式下这里就是原样返回。
                return color;
            }

            // 结束 HLSL 程序。
            ENDHLSL
        }

        // Bloom Prefilter: writes thresholded HDR highlights into mip0.
        Pass
        {
            Name "Burt Bloom Prefilter"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            float4 _BurtBloomTexelSize;
            float _BurtBloomThreshold;
            float _BurtBloomSoftKnee;
            float _BurtBloomBypassThreshold;
            float _BurtBloomFireflyClamp;
            float _BurtUseBloomAlpha;
            float _BurtPostExposure;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float2 BurtBloomSourceUV(float2 uv)
            {
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                return uv;
            }

            float BurtBloomPerceivedLuminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float3 SafeBloomHdrColor(float3 color)
            {
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                return min(max(color, 0.0), 65504.0);
            }

            float3 ClampBloomFirefly(float3 color)
            {
                float clampLuma = max(_BurtBloomFireflyClamp, 1.0);
                float luma = BurtBloomPerceivedLuminance(color);
                float softLuma = clampLuma + (luma - clampLuma) / (1.0 + max(luma - clampLuma, 0.0) / clampLuma);
                float scale = luma > clampLuma ? softLuma / max(luma, 1e-4) : 1.0;
                return color * scale;
            }

            float3 ApplyBloomThreshold(float3 color)
            {
                if (_BurtBloomBypassThreshold > 0.5)
                {
                    return color;
                }

                float brightness = BurtBloomPerceivedLuminance(color * _BurtPostExposure);
                float knee = max(_BurtBloomThreshold * _BurtBloomSoftKnee, 0.0001);
                float soft = clamp(brightness - _BurtBloomThreshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                float contribution = max(soft, brightness - _BurtBloomThreshold);
                contribution /= max(brightness, 0.0001);
                return color * saturate(contribution);
            }

            float3 SampleBloomPrefilter(float2 uv)
            {
                return ApplyBloomThreshold(ClampBloomFirefly(SafeBloomHdrColor(tex2D(_BurtPostProcessSourceTexture, uv).rgb)));
            }

            float BloomAlphaFromColor(float3 color)
            {
                const float maxLuminance = 4.0;
                const float minLuminance = 0.01;
                float luma = BurtBloomPerceivedLuminance(color);
                return luma > minLuminance ? saturate(luma / (maxLuminance - minLuminance)) : 0.0;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 texel = _BurtBloomTexelSize.xy;
                float2 sourceUv = BurtBloomSourceUV(input.uv);
                float3 color = SampleBloomPrefilter(sourceUv) * 4.0;
                color += SampleBloomPrefilter(sourceUv + texel * float2(1.0, 0.0)) * 2.0;
                color += SampleBloomPrefilter(sourceUv + texel * float2(-1.0, 0.0)) * 2.0;
                color += SampleBloomPrefilter(sourceUv + texel * float2(0.0, 1.0)) * 2.0;
                color += SampleBloomPrefilter(sourceUv + texel * float2(0.0, -1.0)) * 2.0;
                color += SampleBloomPrefilter(sourceUv + texel * float2(1.0, 1.0));
                color += SampleBloomPrefilter(sourceUv + texel * float2(-1.0, 1.0));
                color += SampleBloomPrefilter(sourceUv + texel * float2(1.0, -1.0));
                color += SampleBloomPrefilter(sourceUv + texel * float2(-1.0, -1.0));
                color = max(color * (1.0 / 16.0), 0.0);
                return float4(color, _BurtUseBloomAlpha > 0.5 ? BloomAlphaFromColor(color) : 1.0);
            }
            ENDHLSL
        }

        // Bloom Downsample: filters a larger mip into the next smaller mip.
        Pass
        {
            Name "Burt Bloom Downsample"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            float4 _BurtBloomTexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float2 BurtBloomSourceUV(float2 uv)
            {
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                return uv;
            }

            float4 SafeBloomHdrSample(float4 color)
            {
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                color.a = color.a == color.a ? color.a : 0.0;
                color.rgb = min(max(color.rgb, 0.0), 65504.0);
                color.a = saturate(color.a);
                return color;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 texel = _BurtBloomTexelSize.xy;
                float2 sourceUv = BurtBloomSourceUV(input.uv);
                float4 color = SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv)) * 4.0;
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(1.0, 0.0))) * 2.0;
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(-1.0, 0.0))) * 2.0;
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(0.0, 1.0))) * 2.0;
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(0.0, -1.0))) * 2.0;
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(1.0, 1.0)));
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(-1.0, 1.0)));
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(1.0, -1.0)));
                color += SafeBloomHdrSample(tex2D(_BurtPostProcessSourceTexture, sourceUv + texel * float2(-1.0, -1.0)));
                return float4(max(color.rgb * (1.0 / 16.0), 0.0), saturate(color.a * (1.0 / 16.0)));
            }
            ENDHLSL
        }

        // Bloom Gaussian: PC path uses a separable Gaussian pass and optionally adds the previous smaller stage.
        Pass
        {
            Name "Burt Bloom Gaussian"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            sampler2D _BurtBloomAdditiveTexture;
            float4 _BurtBloomTexelSize;
            float4 _BurtBloomBlurDirection;
            float _BurtUseBloomAdditive;
            float _BurtUseBloomAlpha;
            float _BurtBloomSampleCount;
            float4 _BurtBloomSampleWeights[64];
            float4 _BurtBloomSampleOffsets[64];

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float2 BurtBloomSourceUV(float2 uv)
            {
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                return uv;
            }

            float4 SampleBloomGaussianSource(float2 uv)
            {
                float2 uvMin = _BurtBloomTexelSize.xy * 0.5;
                float2 uvMax = 1.0 - uvMin;
                float2 clampedUv = clamp(uv, uvMin, uvMax);
                float2 texelOffset = abs(clampedUv - uv) * _BurtBloomTexelSize.zw;
                float2 bilinearWeight = saturate(1.0 - texelOffset);
                float4 color = tex2D(_BurtPostProcessSourceTexture, clampedUv);
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                color.a = color.a == color.a ? color.a : 0.0;
                color.rgb = min(max(color.rgb, 0.0), 65504.0);
                color.a = saturate(color.a);
                return color * bilinearWeight.x * bilinearWeight.y;
            }

            float BurtBloomGaussianLuminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float BloomAlphaFromGaussianColor(float3 color)
            {
                const float maxLuminance = 4.0;
                const float minLuminance = 0.01;
                float luma = BurtBloomGaussianLuminance(color);
                return luma > minLuminance ? saturate(luma / (maxLuminance - minLuminance)) : 0.0;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 sourceUv = BurtBloomSourceUV(input.uv);
                float4 color = 0.0;
                int sampleCount = min(64, max(0, (int)_BurtBloomSampleCount));

                for (int sampleIndex = 0; sampleIndex < 64; sampleIndex++)
                {
                    if (sampleIndex < sampleCount)
                    {
                        color += SampleBloomGaussianSource(sourceUv + _BurtBloomSampleOffsets[sampleIndex].xy) * _BurtBloomSampleWeights[sampleIndex];
                    }
                }

                if (_BurtUseBloomAdditive > 0.5)
                {
                    float4 additiveColor = tex2D(_BurtBloomAdditiveTexture, sourceUv);
                    additiveColor.r = additiveColor.r == additiveColor.r ? additiveColor.r : 0.0;
                    additiveColor.g = additiveColor.g == additiveColor.g ? additiveColor.g : 0.0;
                    additiveColor.b = additiveColor.b == additiveColor.b ? additiveColor.b : 0.0;
                    additiveColor.a = additiveColor.a == additiveColor.a ? additiveColor.a : 0.0;
                    additiveColor.rgb = min(max(additiveColor.rgb, 0.0), 65504.0);
                    additiveColor.a = saturate(additiveColor.a);
                    color += additiveColor;
                }

                color.rgb = max(color.rgb, 0.0);
                if (_BurtUseBloomAlpha > 0.5)
                {
                    color.a = max(saturate(color.a), BloomAlphaFromGaussianColor(color.rgb));
                }
                else
                {
                    color.a = 1.0;
                }

                return color;
            }
            ENDHLSL
        }


        // Temporal AA resolve and helper passes. TAA-owned RTs use platform-correct screen UVs so history and velocity never pick up the post-process copy flip.
        Pass
        {
            Name "Burt Temporal AA Resolve"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            sampler2D _BurtTAAHistoryTexture;
            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAADepthHistoryTexture;
            sampler2D _BurtTAAHistoryConfidenceTexture;
            sampler2D _BurtTAAAntiFlickerHistoryTexture;
            sampler2D _BurtTAAConfidenceTexture;
            sampler2D _BurtTAARawVelocityTexture;
            sampler2D _BurtTAAVelocityTexture;
            Texture2D<int> _BurtTAAPrevUseCountTexture;
            sampler2D _BurtTAAParallaxRejectionTexture;
            sampler2D _BurtGBuffer1;
            float4x4 _BurtTAAInverseCurrentViewProjection;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAParams;
            float4 _BurtTAAParams2;
            float4 _BurtTAARejectionParams;
            float4 _BurtTAAFeedbackParams;
            float4 _BurtTAAXRenderParams;
            float4 _BurtTAAResponsiveParams;
            float4 _BurtTAAEdgeParams;
            float4 _BurtTAACurrentSampleWeights0;
            float4 _BurtTAACurrentSampleWeights1;
            float4 _BurtTAACurrentSampleWeights2;
            float _BurtTAAHasGBuffer;
            float _BurtShadingDebugEnabled;
            float _BurtShadingDebugMode;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float3 BurtTaaDebugColor(float3 color)
            {
                return color / (1.0 + max(color, 0.0));
            }

            float3 BurtTaaVelocityDebugColor(float4 velocityData)
            {
                if (velocityData.z < 0.5)
                {
                    return float3(0.22, 0.0, 0.35);
                }

                float2 motionPixels = velocityData.xy * _BurtTAATexelSize.zw;
                return float3(saturate(0.5 + motionPixels.x * 0.02), saturate(0.5 + motionPixels.y * 0.02), 0.5);
            }

            float BurtTaaLoadPrevUseCount(float2 historyUv)
            {
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                int2 textureSize = int2((int)_BurtTAATexelSize.z, (int)_BurtTAATexelSize.w);
                float useCount = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    float wy = y == 0 ? (1.0 - blend.y) : blend.y;
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float wx = x == 0 ? (1.0 - blend.x) : blend.x;
                        int2 pixel = int2(basePixel) + int2(x, y);
                        bool inBounds = pixel.x >= 0 && pixel.y >= 0 && pixel.x < textureSize.x && pixel.y < textureSize.y;
                        float weight = wx * wy * (inBounds ? 1.0 : 0.0);
                        int2 safePixel = clamp(pixel, int2(0, 0), textureSize - 1);
                        useCount += max(0, _BurtTAAPrevUseCountTexture.Load(int3(safePixel, 0))) * (1.0 / 255.0) * weight;
                        weightSum += weight;
                    }
                }

                return useCount * rcp(max(weightSum, 1e-4));
            }

            float BurtTaaHistoryUseCountDebug(float2 historyUv)
            {
                return saturate(BurtTaaLoadPrevUseCount(historyUv) * 0.5);
            }

            float BurtTaaLuminance(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float3 BurtTaaToWorkingSpace(float3 rgb)
            {
                return float3(
                    dot(rgb, float3(0.25, 0.5, 0.25)),
                    dot(rgb, float3(0.5, 0.0, -0.5)) + (128.0 / 255.0),
                    dot(rgb, float3(-0.25, 0.5, -0.25)) + (128.0 / 255.0));
            }

            float3 BurtTaaFromWorkingSpace(float3 ycocg)
            {
                float y = ycocg.x;
                float co = ycocg.y - (128.0 / 255.0);
                float cg = ycocg.z - (128.0 / 255.0);
                return float3(y + co - cg, y + cg, y - co - cg);
            }

            float3 BurtTaaToPerceptualSpace(float3 color)
            {
                return color * rcp(color.x + 1.0);
            }

            float3 BurtTaaFromPerceptualSpace(float3 color)
            {
                color.x = min(color.x, 0.999);
                return color * rcp(max(1.0 - color.x, 1e-4));
            }

            float3 BurtTaaToWorkingPerceptualSpace(float3 color)
            {
                return BurtTaaToPerceptualSpace(BurtTaaToWorkingSpace(color));
            }

            float3 BurtTaaFromWorkingPerceptualSpace(float3 color)
            {
                return max(BurtTaaFromWorkingSpace(BurtTaaFromPerceptualSpace(color)), 0.0);
            }

            float BurtTaaWorkingLuma(float3 workingColor)
            {
                return workingColor.x;
            }

            float BurtTaaHdrWeight4(float3 color)
            {
                float3 safeColor = max(color, 0.0);
                return rcp(dot(safeColor, float3(1.0, 2.0, 1.0)) + 4.0);
            }

            float BurtTaaToneWeightedHistoryFeedback(float feedback, float3 currentColor, float3 historyColor, float strength)
            {
                float currentWeight = (1.0 - feedback) * BurtTaaHdrWeight4(currentColor);
                float historyWeight = feedback * BurtTaaHdrWeight4(historyColor);
                float toneFeedback = historyWeight * rcp(max(currentWeight + historyWeight, 1e-4));
                float limitedToneFeedback = clamp(toneFeedback, max(0.0, feedback - 0.18), min(0.985, feedback + 0.24));
                return lerp(feedback, limitedToneFeedback, saturate(strength));
            }

            float BurtTaaValidSurfaceWeight(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return step(1e-6, rawDepth);
                #else
                    return 1.0 - step(1.0 - 1e-6, rawDepth);
                #endif
            }

            bool BurtTaaIsCloserDepth(float candidateDepth, float currentDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return candidateDepth > currentDepth;
                #else
                    return candidateDepth < currentDepth;
                #endif
            }

            float2 BurtTaaClipToUv(float4 clipPosition)
            {
                float2 ndc = clipPosition.xy / max(abs(clipPosition.w), 1e-6);
                float2 uv = ndc * 0.5 + 0.5;
                return uv;
            }

            float3 BurtTaaClipToAabb(float3 history, float3 minimumColor, float3 maximumColor)
            {
                float3 boxCenter = 0.5 * (maximumColor + minimumColor);
                float3 boxExtents = 0.5 * (maximumColor - minimumColor) + 1e-5;
                float3 offset = history - boxCenter;
                float3 unitOffset = abs(offset / boxExtents);
                float maxUnit = max(max(unitOffset.x, unitOffset.y), unitOffset.z);
                float3 clipped = maxUnit > 1.0 ? boxCenter + offset / maxUnit : history;
                return lerp(clipped, clamp(history, minimumColor, maximumColor), 0.2);
            }

            float3 BurtTaaDecodeOctNormal(float2 encodedNormal)
            {
                float2 f = encodedNormal * 2.0 - 1.0;
                float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
                float t = saturate(-n.z);
                n.x += n.x >= 0.0 ? -t : t;
                n.y += n.y >= 0.0 ? -t : t;
                return normalize(n + 1e-6);
            }

            float BurtTaaNormalEdgeWeight(float2 uv)
            {
                if (_BurtTAAHasGBuffer < 0.5)
                {
                    return 1.0;
                }

                float2 texel = _BurtTAATexelSize.xy;
                float3 centerNormal = BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, uv).rg);
                float minDot = 1.0;
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv + float2(texel.x, 0.0))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv - float2(texel.x, 0.0))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv + float2(0.0, texel.y))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv - float2(0.0, texel.y))).rg)));
                return lerp(0.55, 1.0, saturate((minDot - 0.75) * 4.0));
            }

            float3 BurtTaaGBufferNormalDebug(float2 uv)
            {
                if (_BurtTAAHasGBuffer < 0.5)
                {
                    return float3(0.5, 0.5, 0.5);
                }

                return BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, uv).rg) * 0.5 + 0.5;
            }

            float BurtTaaDepthDisocclusionWeight(float currentRawDepth, float historyRawDepth)
            {
                float valid = BurtTaaValidSurfaceWeight(currentRawDepth) * BurtTaaValidSurfaceWeight(historyRawDepth);
                float currentEyeDepth = LinearEyeDepth(currentRawDepth);
                float historyEyeDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(currentEyeDepth * 0.012, 0.025);
                return valid * saturate(1.0 - abs(currentEyeDepth - historyEyeDepth) / depthTolerance);
            }

            float BurtTaaDepthConfidenceValidity(float currentRawDepth, float historyRawDepth, float previousConfidence)
            {
                float depthValidity = saturate(BurtTaaDepthDisocclusionWeight(currentRawDepth, historyRawDepth));
                float confidenceDepthRelax = saturate(previousConfidence * depthValidity * 1.35);
                float confidenceDepthCeiling = lerp(0.45, 1.0, depthValidity);
                return min(max(depthValidity, confidenceDepthRelax), confidenceDepthCeiling);
            }

            float BurtTaaHistoryDepthNeighborhoodValidity(float2 historyUv, float currentRawDepth, float previousConfidence)
            {
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                float validity = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    float wy = y == 0 ? (1.0 - blend.y) : blend.y;
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float wx = x == 0 ? (1.0 - blend.x) : blend.x;
                        float2 tapUv = (basePixel + float2(x, y) + 0.5) * _BurtTAATexelSize.xy;
                        float tapInBounds = step(0.0, tapUv.x) * step(tapUv.x, 1.0) * step(0.0, tapUv.y) * step(tapUv.y, 1.0);
                        float tapWeight = wx * wy * tapInBounds;
                        float2 safeTapUv = saturate(tapUv);
                        float tapDepth = tex2D(_BurtTAADepthHistoryTexture, safeTapUv).r;
                        float tapConfidence = max(previousConfidence, tex2D(_BurtTAAHistoryConfidenceTexture, safeTapUv).r);
                        validity += BurtTaaDepthConfidenceValidity(currentRawDepth, tapDepth, tapConfidence) * tapWeight;
                        weightSum += tapWeight;
                    }
                }

                return validity * rcp(max(weightSum, 1e-4));
            }

            float BurtTaaDepthNeighborhoodWeight(float centerRawDepth, float sampleRawDepth)
            {
                float centerSurface = BurtTaaValidSurfaceWeight(centerRawDepth);
                float sampleSurface = BurtTaaValidSurfaceWeight(sampleRawDepth);
                float centerEyeDepth = LinearEyeDepth(centerRawDepth);
                float sampleEyeDepth = LinearEyeDepth(sampleRawDepth);
                float depthTolerance = max(centerEyeDepth * 0.018, 0.035);
                float surfaceDepthWeight = centerSurface * sampleSurface * saturate(1.0 - abs(centerEyeDepth - sampleEyeDepth) / depthTolerance);
                float skyWeight = (1.0 - centerSurface) * (1.0 - sampleSurface);
                return max(surfaceDepthWeight, skyWeight);
            }

            float3 BurtSampleCurrent(float2 uv)
            {
                return max(tex2D(_BurtPostProcessSourceTexture, uv).rgb, 0.0);
            }

            float3 BurtSampleHistoryCatmullRom(float2 uv)
            {
                float2 textureSize = _BurtTAATexelSize.zw;
                float2 samplePosition = uv * textureSize;
                float2 texelCenter = floor(samplePosition - 0.5) + 0.5;
                float2 f = samplePosition - texelCenter;
                float2 f2 = f * f;
                float2 f3 = f2 * f;
                float2 w0 = f2 - 0.5 * (f3 + f);
                float2 w1 = 1.5 * f3 - 2.5 * f2 + 1.0;
                float2 w2 = -1.5 * f3 + 2.0 * f2 + 0.5 * f;
                float2 w3 = 0.5 * (f3 - f2);
                float3 color = 0.0;
                [unroll]
                for (int y = 0; y < 4; y++)
                {
                    float wy = y == 0 ? w0.y : (y == 1 ? w1.y : (y == 2 ? w2.y : w3.y));
                    [unroll]
                    for (int x = 0; x < 4; x++)
                    {
                        float wx = x == 0 ? w0.x : (x == 1 ? w1.x : (x == 2 ? w2.x : w3.x));
                        float2 tapUv = (texelCenter + float2(x - 1, y - 1)) * _BurtTAATexelSize.xy;
                        color += tex2D(_BurtTAAHistoryTexture, saturate(tapUv)).rgb * (wx * wy);
                    }
                }

                float3 historyMin = tex2D(_BurtTAAHistoryTexture, uv).rgb;
                float3 historyMax = historyMin;
                [unroll]
                for (int by = -1; by <= 1; by++)
                {
                    [unroll]
                    for (int bx = -1; bx <= 1; bx++)
                    {
                        float3 sampleHistory = tex2D(_BurtTAAHistoryTexture, saturate(uv + float2(bx, by) * _BurtTAATexelSize.xy)).rgb;
                        historyMin = min(historyMin, sampleHistory);
                        historyMax = max(historyMax, sampleHistory);
                    }
                }

                float3 catmullHistory = max(clamp(color, historyMin, historyMax), 0.0);
                float3 bilinearHistory = max(tex2D(_BurtTAAHistoryTexture, uv).rgb, 0.0);
                float catmullLuma = BurtTaaLuminance(catmullHistory);
                float bilinearLuma = BurtTaaLuminance(bilinearHistory);
                float historyLumaMax = max(max(catmullLuma, bilinearLuma), 0.05);
                float hdrRinging = saturate(abs(catmullLuma - bilinearLuma) / historyLumaMax);
                float hdrRingingGuard = smoothstep(0.25, 0.85, hdrRinging) * saturate(historyLumaMax / (historyLumaMax + 1.0));
                return lerp(catmullHistory, bilinearHistory, hdrRingingGuard * 0.35);
            }

            float BurtTaaCurrentSampleWeight(int x, int y)
            {
                if (x == 0 && y == 0) return _BurtTAACurrentSampleWeights0.x;
                if (x == 0 && y == 1) return _BurtTAACurrentSampleWeights0.y;
                if (x == 1 && y == 0) return _BurtTAACurrentSampleWeights0.z;
                if (x == -1 && y == 0) return _BurtTAACurrentSampleWeights0.w;
                if (x == 0 && y == -1) return _BurtTAACurrentSampleWeights1.x;
                if (x == -1 && y == 1) return _BurtTAACurrentSampleWeights1.y;
                if (x == 1 && y == -1) return _BurtTAACurrentSampleWeights1.z;
                if (x == 1 && y == 1) return _BurtTAACurrentSampleWeights1.w;
                return _BurtTAACurrentSampleWeights2.x;
            }

            float2 BurtTaaVelocitySourceNeighborhood(float2 uv)
            {
                float trusted = 0.0;
                float untrusted = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float4 source = tex2D(_BurtTAARawVelocityTexture, saturate(uv + float2(x, y) * _BurtTAATexelSize.xy));
                        float trustedTap = step(0.75, source.w) * source.z;
                        float untrustedTap = step(0.25, source.w) * (1.0 - trustedTap) * source.z;
                        trusted = max(trusted, trustedTap);
                        untrusted = max(untrusted, untrustedTap);
                    }
                }

                return float2(trusted, untrusted);
            }

            float BurtTaaVelocityDiscontinuity(float2 uv, float4 centerVelocityData)
            {
                float centerValid = centerVelocityData.z;
                float2 centerMotionPixels = centerVelocityData.xy * _BurtTAATexelSize.zw;
                float maxVelocityDelta = 0.0;
                float maxValidityDelta = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float4 sampleVelocity = tex2D(_BurtTAAVelocityTexture, saturate(uv + float2(x, y) * _BurtTAATexelSize.xy));
                        float2 sampleMotionPixels = sampleVelocity.xy * _BurtTAATexelSize.zw;
                        maxVelocityDelta = max(maxVelocityDelta, length(sampleMotionPixels - centerMotionPixels) * centerValid * sampleVelocity.z);
                        maxValidityDelta = max(maxValidityDelta, abs(sampleVelocity.z - centerValid));
                    }
                }

                return saturate(max((maxVelocityDelta - 0.5) / 4.0, maxValidityDelta) * max(_BurtTAAEdgeParams.x, 0.0));
            }

            float BurtTaaDilatedHistoryValidity(float2 uv, float centerValidity, float edgeStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float minValidity = centerValidity;
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(texel.x, 0.0))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(texel.x, 0.0))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(0.0, texel.y))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(0.0, texel.y))).r);
                return lerp(centerValidity, minValidity, saturate(edgeStrength));
            }

            float BurtTaaDilatedHistoryCoverage(float2 uv, float centerCoverage, float edgeStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float minCoverage = centerCoverage;
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(texel.x, 0.0))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(texel.x, 0.0))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(0.0, texel.y))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(0.0, texel.y))).g);
                return lerp(centerCoverage, minCoverage, saturate(edgeStrength));
            }

            float BurtTaaPersistentAntiFlickerNeighborhood(float2 uv, float centerPersistent)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float persistent = centerPersistent;
                float2 antiFlicker = tex2D(_BurtTAAAntiFlickerHistoryTexture, saturate(uv + float2(texel.x, 0.0))).rg;
                persistent = max(persistent, min(antiFlicker.x, antiFlicker.y));
                antiFlicker = tex2D(_BurtTAAAntiFlickerHistoryTexture, saturate(uv - float2(texel.x, 0.0))).rg;
                persistent = max(persistent, min(antiFlicker.x, antiFlicker.y));
                antiFlicker = tex2D(_BurtTAAAntiFlickerHistoryTexture, saturate(uv + float2(0.0, texel.y))).rg;
                persistent = max(persistent, min(antiFlicker.x, antiFlicker.y));
                antiFlicker = tex2D(_BurtTAAAntiFlickerHistoryTexture, saturate(uv - float2(0.0, texel.y))).rg;
                persistent = max(persistent, min(antiFlicker.x, antiFlicker.y));
                return persistent;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float rawDepth = tex2D(_BurtTAACurrentDepthTexture, uv).r;
                float surfaceWeight = BurtTaaValidSurfaceWeight(rawDepth);
                float3 current = BurtSampleCurrent(uv);
                float3 currentWorking = BurtTaaToWorkingPerceptualSpace(current);
                float3 neighborhoodMin = currentWorking;
                float3 neighborhoodMax = currentWorking;
                float3 neighborhoodSum = 0.0;
                float3 neighborhoodSumSq = 0.0;
                float3 currentFilteredWorking = 0.0;
                float currentFilteredWeight = 0.0;
                float3 depthNeighborhoodMin = currentWorking;
                float3 depthNeighborhoodMax = currentWorking;
                float3 depthNeighborhoodSum = 0.0;
                float3 depthNeighborhoodSumSq = 0.0;
                float depthNeighborhoodWeightSum = 0.0;
                float depthNeighborhoodEdge = 0.0;
                float minEyeDepth = LinearEyeDepth(rawDepth);
                float maxEyeDepth = minEyeDepth;
                float2 texel = _BurtTAATexelSize.xy;
                float spatialHdrFilterStrength = saturate(_BurtTAAXRenderParams.x * 0.08);

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 sampleUv = saturate(uv + texel * float2(x, y));
                        float3 sampleColor = BurtSampleCurrent(sampleUv);
                        float3 sampleWorking = BurtTaaToWorkingPerceptualSpace(sampleColor);
                        float sampleDepth = tex2D(_BurtTAACurrentDepthTexture, sampleUv).r;
                        float sampleEyeDepth = LinearEyeDepth(sampleDepth);
                        float sampleDepthWeight = BurtTaaDepthNeighborhoodWeight(rawDepth, sampleDepth);
                        float sampleToneWeight = lerp(1.0, saturate(BurtTaaHdrWeight4(sampleColor) * 4.0), spatialHdrFilterStrength);
                        float sampleCurrentWeight = BurtTaaCurrentSampleWeight(x, y) * lerp(saturate(_BurtTAAEdgeParams.w), 1.0, sampleDepthWeight) * sampleToneWeight;
                        neighborhoodMin = min(neighborhoodMin, sampleWorking);
                        neighborhoodMax = max(neighborhoodMax, sampleWorking);
                        neighborhoodSum += sampleWorking;
                        neighborhoodSumSq += sampleWorking * sampleWorking;
                        currentFilteredWorking += sampleWorking * sampleCurrentWeight;
                        currentFilteredWeight += sampleCurrentWeight;
                        depthNeighborhoodSum += sampleWorking * sampleDepthWeight;
                        depthNeighborhoodSumSq += sampleWorking * sampleWorking * sampleDepthWeight;
                        depthNeighborhoodWeightSum += sampleDepthWeight;
                        depthNeighborhoodEdge = max(depthNeighborhoodEdge, 1.0 - sampleDepthWeight);
                        if (sampleDepthWeight > 0.05)
                        {
                            depthNeighborhoodMin = min(depthNeighborhoodMin, sampleWorking);
                            depthNeighborhoodMax = max(depthNeighborhoodMax, sampleWorking);
                        }
                        minEyeDepth = min(minEyeDepth, sampleEyeDepth);
                        maxEyeDepth = max(maxEyeDepth, sampleEyeDepth);
                    }
                }
                currentFilteredWorking *= rcp(max(currentFilteredWeight, 1e-4));

                float4 velocityData = tex2D(_BurtTAAVelocityTexture, uv);
                float closestDepth = velocityData.z > 0.5 ? velocityData.w : rawDepth;
                float2 historyUv = uv + velocityData.xy;
                float velocityValid = velocityData.z;
                float2 velocitySourceCoverage = BurtTaaVelocitySourceNeighborhood(uv);
                float trustedObjectMotion = velocitySourceCoverage.x;
                float untrustedObjectMotion = velocitySourceCoverage.y;
                float historyValid = _BurtTAAParams.z;
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float2 safeHistoryUv = saturate(historyUv);
                float3 rawHistory = BurtSampleHistoryCatmullRom(safeHistoryUv);
                float3 rawHistoryWorking = BurtTaaToWorkingPerceptualSpace(rawHistory);
                float historyRawDepth = tex2D(_BurtTAADepthHistoryTexture, safeHistoryUv).r;
                float historyConfidence = tex2D(_BurtTAAHistoryConfidenceTexture, safeHistoryUv).r * historyValid * inBounds;
                float2 antiFlickerHistory = tex2D(_BurtTAAAntiFlickerHistoryTexture, safeHistoryUv).rg * historyValid * inBounds;
                float persistentAntiFlicker = BurtTaaPersistentAntiFlickerNeighborhood(safeHistoryUv, min(antiFlickerHistory.x, antiFlickerHistory.y)) * historyValid * inBounds;
                float2 rawHistoryValidity = tex2D(_BurtTAAParallaxRejectionTexture, uv).rg;
                float rawParallaxValidity = rawHistoryValidity.r;
                float rawCoverageValidity = rawHistoryValidity.g;
                float parallaxValidity = rawParallaxValidity * historyValid * inBounds * velocityValid;
                float coverageValidity = rawCoverageValidity * historyValid * inBounds * velocityValid;
                float depthContinuity = BurtTaaHistoryDepthNeighborhoodValidity(safeHistoryUv, closestDepth, historyConfidence);

                float2 motionPixels = velocityData.xy * _BurtTAATexelSize.zw;
                float motionLength = length(motionPixels);
                float velocityEdgeResponsive = BurtTaaVelocityDiscontinuity(uv, velocityData);
                float normalWeight = BurtTaaNormalEdgeWeight(uv);
                float normalEdgeResponsive = saturate((1.0 - normalWeight) * 0.65);
                float currentFilteredLuma = BurtTaaWorkingLuma(currentFilteredWorking);
                float historyFilteredLuma = BurtTaaWorkingLuma(rawHistoryWorking);
                float temporalContrast = saturate(abs(currentFilteredLuma - historyFilteredLuma) / max(max(currentFilteredLuma, historyFilteredLuma), 0.2));
                float depthEdgeResponsive = saturate(depthNeighborhoodEdge * surfaceWeight * max(_BurtTAAEdgeParams.y, 0.0));
                float edgeResponsive = max(max(depthEdgeResponsive, velocityEdgeResponsive), normalEdgeResponsive);
                float lowMotionStability = saturate(1.0 - motionLength * 0.35);
                float rawHistoryCoverage = rawParallaxValidity * rawCoverageValidity;
                float stableAntiFlickerGuard = saturate(smoothstep(0.05, max(_BurtTAAXRenderParams.y, 0.051), temporalContrast) * historyConfidence * depthContinuity * rawHistoryCoverage * (1.0 - edgeResponsive) * lowMotionStability);
                float responsiveTemporalContrast = lerp(temporalContrast, temporalContrast * 0.35, stableAntiFlickerGuard);
                float responsivePreMask = saturate(max(max(responsiveTemporalContrast, untrustedObjectMotion), max(velocityEdgeResponsive, normalEdgeResponsive)) * saturate(_BurtTAAResponsiveParams.x) * historyValid * inBounds * velocityValid * surfaceWeight);
                persistentAntiFlicker *= 1.0 - responsivePreMask;
                float localizedAntiFlicker = lerp(_BurtTAAXRenderParams.x * 0.8, _BurtTAAXRenderParams.x * 2.25, saturate(1.0 - 2.0 * motionLength));
                float antiFlickerBoost = lerp(0.0, localizedAntiFlicker, smoothstep(0.05, max(_BurtTAAXRenderParams.y, 0.051), temporalContrast));
                antiFlickerBoost += persistentAntiFlicker * _BurtTAAXRenderParams.x;

                float dilatedParallaxValidity = BurtTaaDilatedHistoryValidity(uv, rawParallaxValidity, edgeResponsive);
                float dilatedCoverageValidity = BurtTaaDilatedHistoryCoverage(uv, rawCoverageValidity, edgeResponsive);
                float parallaxDilationBreak = saturate(rawParallaxValidity - dilatedParallaxValidity);
                float coverageBreak = saturate(1.0 - dilatedCoverageValidity);
                parallaxValidity = dilatedParallaxValidity * historyValid * inBounds * velocityValid;
                coverageValidity = dilatedCoverageValidity * historyValid * inBounds * velocityValid;
                float disocclusionContinuity = saturate(min(depthContinuity, parallaxValidity));
                disocclusionContinuity *= saturate(lerp(0.35, 1.0, coverageValidity));
                antiFlickerBoost *= disocclusionContinuity;
                float historyContinuity = saturate(disocclusionContinuity * (1.0 - edgeResponsive));
                float coverageClampTighten = coverageBreak * saturate(_BurtTAAResponsiveParams.x);
                float clampTighten = saturate(max(edgeResponsive * saturate(_BurtTAAResponsiveParams.x) * max(_BurtTAAEdgeParams.z, 0.0), coverageClampTighten));
                float clampStrength = max((_BurtTAAParams.y + antiFlickerBoost) * lerp(1.0, 0.55, clampTighten), 0.18);
                float3 neighborhoodMean = neighborhoodSum * (1.0 / 9.0);
                float3 neighborhoodVariance = max(neighborhoodSumSq * (1.0 / 9.0) - neighborhoodMean * neighborhoodMean, 0.0);
                float3 neighborhoodSigma = sqrt(neighborhoodVariance);
                float3 varianceMin = neighborhoodMean - neighborhoodSigma * clampStrength;
                float3 varianceMax = neighborhoodMean + neighborhoodSigma * clampStrength;
                float3 clampMinAll = min(max(neighborhoodMin, varianceMin), neighborhoodMean);
                float3 clampMaxAll = max(min(neighborhoodMax, varianceMax), neighborhoodMean);
                float depthWeightInv = rcp(max(depthNeighborhoodWeightSum, 1e-4));
                float3 depthNeighborhoodMean = depthNeighborhoodSum * depthWeightInv;
                float3 depthNeighborhoodVariance = max(depthNeighborhoodSumSq * depthWeightInv - depthNeighborhoodMean * depthNeighborhoodMean, 0.0);
                float3 depthNeighborhoodSigma = sqrt(depthNeighborhoodVariance);
                float3 depthVarianceMin = depthNeighborhoodMean - depthNeighborhoodSigma * clampStrength;
                float3 depthVarianceMax = depthNeighborhoodMean + depthNeighborhoodSigma * clampStrength;
                float3 clampMinDepth = min(max(depthNeighborhoodMin, depthVarianceMin), depthNeighborhoodMean);
                float3 clampMaxDepth = max(min(depthNeighborhoodMax, depthVarianceMax), depthNeighborhoodMean);
                float3 clampMin = lerp(clampMinAll, clampMinDepth, clampTighten);
                float3 clampMax = lerp(clampMaxAll, clampMaxDepth, clampTighten);
                float3 historyWorking = BurtTaaClipToAabb(rawHistoryWorking, clampMin, clampMax);
                historyWorking = lerp(historyWorking, rawHistoryWorking, saturate(persistentAntiFlicker * _BurtTAAXRenderParams.z * 0.25 * historyContinuity));
                float clipDistance = length(rawHistoryWorking - historyWorking) / max(length(currentWorking), 0.05);
                float lumaRejectStrength = max(_BurtTAARejectionParams.x, 0.001);
                float clipRejectStrength = max(_BurtTAARejectionParams.y, 0.001);
                float depthRejectStrength = max(_BurtTAARejectionParams.z, 0.001);
                float motionRejectStart = max(_BurtTAARejectionParams.w, 0.0);
                float motionRejectRange = max(_BurtTAAFeedbackParams.x, 1.0);
                float clipWeight = lerp(0.04, 1.0, saturate(1.0 - clipDistance * 2.5 * clipRejectStrength));

                float currentLuma = BurtTaaWorkingLuma(currentFilteredWorking);
                float historyLuma = BurtTaaWorkingLuma(rawHistoryWorking);
                float lumaThreshold = max(max(currentLuma, historyLuma) * 0.22, 0.06);
                float lumaWeight = lerp(0.04, 1.0, saturate(1.0 - abs(currentLuma - historyLuma) * lumaRejectStrength / lumaThreshold));
                float depthRangeWeight = lerp(0.18, 1.0, saturate(1.0 - (maxEyeDepth - minEyeDepth) * depthRejectStrength / max(minEyeDepth * 0.08, 0.05)));
                float depthWeight = pow(depthContinuity, depthRejectStrength);
                float motionWeight = saturate(1.0 - max(motionLength - motionRejectStart, 0.0) / motionRejectRange);
                float xrenderMotionWeight = rcp(1.0 + motionLength * motionLength * _BurtTAAXRenderParams.w * 0.0001);
                motionWeight = min(motionWeight, lerp(1.0, xrenderMotionWeight, saturate(_BurtTAAXRenderParams.w / 250.5)));
                float staticRelax = saturate(_BurtTAAParams2.z * historyConfidence * (1.0 - motionLength * 0.45) * historyContinuity);
                lumaWeight = max(lumaWeight, lerp(lumaWeight, 0.26, staticRelax));
                clipWeight = max(clipWeight, lerp(clipWeight, 0.28, staticRelax));
                depthWeight = max(depthWeight, lerp(depthWeight, 0.34, staticRelax));
                normalWeight = max(normalWeight, lerp(normalWeight, 0.44, staticRelax));
                float stableAntiFlickerRelax = saturate((persistentAntiFlicker + antiFlickerBoost * 0.18) * historyConfidence * historyContinuity * lowMotionStability);
                stableAntiFlickerRelax *= smoothstep(0.05, max(_BurtTAAXRenderParams.y, 0.051), temporalContrast) * (1.0 - saturate(edgeResponsive * 1.5));
                lumaWeight = max(lumaWeight, lerp(lumaWeight, 0.50, stableAntiFlickerRelax));
                clipWeight = max(clipWeight, lerp(clipWeight, 0.48, stableAntiFlickerRelax));
                float historySampleTrust = smoothstep(0.12, 0.88, historyConfidence);
                historySampleTrust *= lerp(0.55, 1.0, coverageValidity);
                float stableColorRecovery = saturate(max(persistentAntiFlicker, stableAntiFlickerGuard * 0.65) * historyConfidence * historyContinuity * lowMotionStability);
                stableColorRecovery *= (1.0 - saturate(edgeResponsive * 1.25)) * (1.0 - untrustedObjectMotion);
                stableColorRecovery *= smoothstep(0.08, max(_BurtTAAXRenderParams.y, 0.081), temporalContrast);
                stableColorRecovery *= lerp(0.55, 1.0, historySampleTrust);
                float stableColorFloor = lerp(0.56, 0.76, saturate(_BurtTAAXRenderParams.x * 0.22));
                lumaWeight = max(lumaWeight, lerp(lumaWeight, stableColorFloor, stableColorRecovery));
                clipWeight = max(clipWeight, lerp(clipWeight, stableColorFloor * 0.92, stableColorRecovery));
                float rejectionWeight = min(min(lumaWeight, clipWeight), min(min(depthRangeWeight, depthWeight), min(min(normalWeight, motionWeight), parallaxValidity)));
                float colorResponsive = max(1.0 - lumaWeight, 1.0 - clipWeight);
                float depthResponsive = max(max(1.0 - min(depthWeight, depthRangeWeight), 1.0 - parallaxValidity), depthEdgeResponsive);
                depthResponsive = max(depthResponsive, coverageBreak);
                float motionResponsive = saturate((motionLength - motionRejectStart * 0.25) / max(motionRejectRange * 0.5, 1.0));
                float taaDepthBreak = saturate(1.0 - min(depthWeight, depthRangeWeight));
                float taaParallaxBreak = saturate(1.0 - parallaxValidity);
                float taaVelocityBreak = saturate(max(velocityEdgeResponsive, motionResponsive));
                float taaClampBreak = saturate(max(1.0 - clipWeight, clampTighten));
                float edgeFeedbackGuard = saturate(edgeResponsive * max(max(max(taaParallaxBreak, coverageBreak), taaVelocityBreak), taaClampBreak) * (1.0 - historyContinuity));
                float responsiveMask = max(max(colorResponsive, depthResponsive), max(max(max(motionResponsive * 0.65, untrustedObjectMotion), velocityEdgeResponsive), responsivePreMask));
                responsiveMask = saturate(responsiveMask * saturate(_BurtTAAResponsiveParams.x) * historyValid * inBounds * velocityValid * surfaceWeight);
                responsiveMask = max(responsiveMask, edgeFeedbackGuard * historyValid * inBounds * velocityValid * surfaceWeight);
                float confidenceWeight = lerp(saturate(_BurtTAAFeedbackParams.y), 1.0, saturate(historyConfidence));
                float confidenceBoost = lerp(min(_BurtTAAFeedbackParams.z, 0.72), _BurtTAAFeedbackParams.z, saturate(historyConfidence));
                float baseFeedback = min(saturate(_BurtTAAParams.x), max(saturate(_BurtTAAParams2.w), 0.01));
                float feedback = baseFeedback * historyValid * velocityValid * inBounds * surfaceWeight * rejectionWeight * confidenceBoost;
                feedback *= confidenceWeight;
                feedback *= lerp(0.82, 1.04, historySampleTrust);
                feedback *= lerp(1.0, saturate(_BurtTAAResponsiveParams.z), responsiveMask);
                feedback *= lerp(1.0, saturate(_BurtTAAResponsiveParams.y), untrustedObjectMotion * historyValid * inBounds * velocityValid);
                feedback *= lerp(1.0, 0.55, edgeFeedbackGuard);
                feedback *= lerp(0.72, 1.0, saturate(coverageValidity));
                float geometryFeedbackValidity = min(min(depthWeight, depthRangeWeight), min(min(normalWeight, motionWeight), min(parallaxValidity, coverageValidity)));
                float settledFeedbackFloor = baseFeedback * lerp(0.56, 0.88, historySampleTrust);
                feedback = max(feedback, settledFeedbackFloor * stableColorRecovery * geometryFeedbackValidity);
                feedback = min(feedback, lerp(0.86, 0.982, historyContinuity));
                feedback = min(feedback, lerp(0.78, 0.982, saturate(coverageValidity)));
                feedback = min(feedback, lerp(0.94, 0.982, saturate(historyConfidence)));

                float edgeContrast = BurtTaaWorkingLuma(neighborhoodMax - neighborhoodMin);
                float spatialWeight = saturate(edgeContrast * 3.0) * lerp(0.35, 0.18, feedback);
                spatialWeight *= 1.0 - responsiveMask * 0.75;
                float currentFilteredDelta = abs(BurtTaaWorkingLuma(currentWorking) - BurtTaaWorkingLuma(currentFilteredWorking));
                float subpixelFlicker = saturate(currentFilteredDelta / max(BurtTaaWorkingLuma(currentWorking), 0.10));
                spatialWeight = max(spatialWeight, stableColorRecovery * subpixelFlicker * 0.34);
                currentFilteredWorking = lerp(currentWorking, currentFilteredWorking, spatialWeight);
                float toneBlendStrength = saturate(_BurtTAAXRenderParams.x * 0.12 + stableAntiFlickerRelax * 0.45);
                toneBlendStrength *= saturate(historyConfidence * historyContinuity * lowMotionStability * (1.0 - edgeResponsive));
                float finalFeedback = BurtTaaToneWeightedHistoryFeedback(
                    feedback,
                    BurtTaaFromWorkingPerceptualSpace(currentFilteredWorking),
                    BurtTaaFromWorkingPerceptualSpace(historyWorking),
                    toneBlendStrength);
                float3 resolvedWorking = lerp(currentFilteredWorking, historyWorking, finalFeedback);
                resolvedWorking += (resolvedWorking - neighborhoodMean) * _BurtTAAParams2.x;
                resolvedWorking = max(BurtTaaClipToAabb(resolvedWorking, clampMin, clampMax), 0.0);
                float3 resolved = BurtTaaFromWorkingPerceptualSpace(resolvedWorking);

                int debugMode = (int)round(_BurtShadingDebugMode);
                if (_BurtShadingDebugEnabled > 0.5 && ((debugMode >= 320 && debugMode <= 346) || debugMode == 365 || debugMode == 367))
                {
                    if (debugMode == 320) return float4(BurtTaaDebugColor(rawHistory), 1.0);
                    if (debugMode == 321) return float4(finalFeedback.xxx, 1.0);
                    if (debugMode == 322) return float4(lumaWeight, clipWeight, rejectionWeight, 1.0);
                    if (debugMode == 323) return float4(saturate(historyUv), inBounds * velocityValid, 1.0);
                    if (debugMode == 324) return float4(saturate(abs(current - rawHistory) * 4.0), 1.0);
                    if (debugMode == 325)
                    {
                        return float4(BurtTaaVelocityDebugColor(velocityData), 1.0);
                    }
                    if (debugMode == 326) return float4(historyConfidence.xxx, 1.0);
                    if (debugMode == 327) return float4(rawDepth.xxx, 1.0);
                    if (debugMode == 328) return float4(historyRawDepth.xxx, 1.0);
                    if (debugMode == 329)
                    {
                        float depthDelta = abs(LinearEyeDepth(closestDepth) - LinearEyeDepth(historyRawDepth));
                        return float4(saturate(depthDelta / max(LinearEyeDepth(closestDepth) * 0.05, 0.05)).xxx, 1.0);
                    }
                    if (debugMode == 330) return float4(BurtTaaDebugColor(current), 1.0);
                    if (debugMode == 331) return float4(BurtTaaDebugColor(resolved), 1.0);
                    if (debugMode == 332) return float4(BurtTaaVelocityDebugColor(tex2D(_BurtTAARawVelocityTexture, uv)), 1.0);
                    if (debugMode == 333) return float4(tex2D(_BurtTAAConfidenceTexture, uv).rrr, 1.0);
                    if (debugMode == 334) return float4(staticRelax.xxx, 1.0);
                    if (debugMode == 335) return float4(lumaWeight.xxx, 1.0);
                    if (debugMode == 336) return float4(clipWeight.xxx, 1.0);
                    if (debugMode == 337) return float4(depthWeight, depthRangeWeight, min(depthWeight, depthRangeWeight), 1.0);
                    if (debugMode == 338) return float4(normalWeight.xxx, 1.0);
                    if (debugMode == 339) return float4(motionWeight.xxx, 1.0);
                    if (debugMode == 340) return float4(historyConfidence, confidenceWeight, confidenceBoost, 1.0);
                    if (debugMode == 341)
                    {
                        float4 rawVelocity = tex2D(_BurtTAARawVelocityTexture, uv);
                        return float4(rawVelocity.w.xxx, 1.0);
                    }
                    if (debugMode == 342) return float4(BurtTaaGBufferNormalDebug(uv), 1.0);
                    if (debugMode == 343)
                    {
                        float historyAvailability = historyValid * inBounds * velocityValid * surfaceWeight;
                        float depthBreak = 1.0 - depthContinuity;
                        float parallaxBreak = saturate(max(parallaxDilationBreak + depthContinuity - parallaxValidity, coverageBreak));
                        float edgeBreak = saturate(min(depthContinuity, parallaxValidity) - historyContinuity);
                        float3 diagnosticColor = lerp(float3(0.18, 0.18, 0.18), float3(1.0, 1.0, 1.0), historyContinuity);
                        diagnosticColor = lerp(diagnosticColor, float3(1.0, 0.12, 0.05), saturate(depthBreak * 1.35));
                        diagnosticColor = lerp(diagnosticColor, float3(0.10, 0.35, 1.0), saturate(parallaxBreak * 1.8));
                        diagnosticColor = lerp(diagnosticColor, float3(1.0, 0.92, 0.05), saturate(edgeBreak * 1.5));
                        return float4(diagnosticColor * historyAvailability, 1.0);
                    }
                    if (debugMode == 344) return float4(antiFlickerHistory.x, antiFlickerHistory.y, persistentAntiFlicker, 1.0);
                    if (debugMode == 345)
                    {
                        float coverageAvailability = historyValid * inBounds * velocityValid * surfaceWeight;
                        float coverageDebug = BurtTaaHistoryUseCountDebug(historyUv) * coverageAvailability;
                        return float4(coverageDebug.xxx, 1.0);
                    }
                    if (debugMode == 365)
                    {
                        float reasonAvailability = historyValid * inBounds * velocityValid * surfaceWeight;
                        float depthRejectDebug = smoothstep(0.34, 0.92, 1.0 - depthContinuity) * reasonAvailability;
                        float parallaxRejectSource = max(max(1.0 - rawParallaxValidity, parallaxDilationBreak), coverageBreak);
                        float parallaxRejectDebug = smoothstep(0.36, 0.94, parallaxRejectSource) * reasonAvailability;
                        float velocityRejectDebug = smoothstep(0.22, 0.85, taaVelocityBreak) * reasonAvailability;
                        float clampRejectDebug = smoothstep(0.36, 0.90, 1.0 - clipWeight) * reasonAvailability;
                        float strongestRejectDebug = max(max(depthRejectDebug, parallaxRejectDebug), max(velocityRejectDebug, clampRejectDebug));
                        float stableReasonSurface = saturate(reasonAvailability * historyContinuity * historySampleTrust * lowMotionStability * (1.0 - edgeResponsive));
                        float3 reasonColor = lerp(float3(0.006, 0.006, 0.006), float3(0.028, 0.028, 0.028), stableReasonSurface) * reasonAvailability * (1.0 - strongestRejectDebug);
                        reasonColor += depthRejectDebug * float3(1.0, 0.05, 0.02);
                        reasonColor += parallaxRejectDebug * float3(0.05, 1.0, 0.08);
                        reasonColor += velocityRejectDebug * float3(0.10, 0.35, 1.0);
                        reasonColor += clampRejectDebug * float3(0.95, 0.0, 0.95);
                        return float4(saturate(reasonColor), 1.0);
                    }
                    if (debugMode == 367)
                    {
                        float feedbackAvailability = historyValid * inBounds * velocityValid * surfaceWeight;
                        float feedbackWeight = saturate(finalFeedback) * feedbackAvailability;
                        float3 feedbackColor = lerp(float3(0.02, 0.02, 0.02), float3(0.10, 0.65, 1.0), smoothstep(0.05, 0.45, feedbackWeight));
                        feedbackColor = lerp(feedbackColor, float3(1.0, 0.95, 0.15), smoothstep(0.45, 0.85, feedbackWeight));
                        feedbackColor = lerp(feedbackColor, float3(1.0, 1.0, 1.0), smoothstep(0.85, 0.98, feedbackWeight));
                        return float4(feedbackColor * feedbackAvailability, 1.0);
                    }
                    return float4(responsiveMask, velocityEdgeResponsive, depthEdgeResponsive, 1.0);
                }

                return float4(resolved, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Current Depth"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtCameraDepthTexture;
            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return tex2D(_BurtCameraDepthTexture, input.uv).rrrr;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Camera Velocity"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAACurrentDepthTexture;
            float4x4 _BurtTAAInverseCurrentViewProjection;
            float4x4 _BurtTAACurrentNonJitteredViewProjection;
            float4x4 _BurtTAAPreviousNonJitteredViewProjection;
            float4 _BurtTAATexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float BurtTaaValidSurfaceWeight(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return step(1e-6, rawDepth);
                #else
                    return 1.0 - step(1.0 - 1e-6, rawDepth);
                #endif
            }

            float2 BurtTaaClipToUv(float4 clipPosition)
            {
                float2 ndc = clipPosition.xy / max(abs(clipPosition.w), 1e-6);
                float2 uv = ndc * 0.5 + 0.5;
                return uv;
            }

            float4 BurtTaaScreenUvToClip(float2 uv, float rawDepth)
            {
                return float4(uv * 2.0 - 1.0, rawDepth, 1.0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = tex2D(_BurtTAACurrentDepthTexture, input.uv).r;
                float4 clip = BurtTaaScreenUvToClip(input.uv, rawDepth);
                float4 world = mul(_BurtTAAInverseCurrentViewProjection, clip);
                world.xyz /= max(abs(world.w), 1e-6);
                float4 currentClip = mul(_BurtTAACurrentNonJitteredViewProjection, float4(world.xyz, 1.0));
                float4 previousClip = mul(_BurtTAAPreviousNonJitteredViewProjection, float4(world.xyz, 1.0));
                float2 currentUv = BurtTaaClipToUv(currentClip);
                float2 previousUv = BurtTaaClipToUv(previousClip);
                float valid = BurtTaaValidSurfaceWeight(rawDepth) * step(1e-5, previousClip.w);
                valid *= step(0.0, previousUv.x) * step(previousUv.x, 1.0) * step(0.0, previousUv.y) * step(previousUv.y, 1.0);
                float2 velocity = previousUv - currentUv;
                float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
                float keepVelocity = step(0.02, max(velocityPixels.x, velocityPixels.y));
                velocity *= keepVelocity;
                return float4(velocity * valid, valid, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Velocity Dilation"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAAVelocityTexture;
            float4 _BurtTAATexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            bool BurtTaaIsCloserDepth(float candidateDepth, float currentDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return candidateDepth > currentDepth;
                #else
                    return candidateDepth < currentDepth;
                #endif
            }

            void BurtTaaTryVelocityDilationTap(float2 uv, float2 pixelOffset, float centerEyeDepth, float depthError, inout float closestDepth, inout float4 bestVelocity, inout float dilationStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float2 sampleUv = saturate(uv + pixelOffset * texel);
                float sampleDepth = tex2D(_BurtTAACurrentDepthTexture, sampleUv).r;
                float4 sampleVelocity = tex2D(_BurtTAAVelocityTexture, sampleUv);
                float sampleEyeDepth = LinearEyeDepth(sampleDepth);
                float closerEyeDelta = centerEyeDepth - sampleEyeDepth;
                float edgeCandidate = saturate((closerEyeDelta - depthError) / max(centerEyeDepth * 0.015, 0.02));
                bool centerInvalid = bestVelocity.z <= 0.5;
                bool candidateBetter = centerInvalid || BurtTaaIsCloserDepth(sampleDepth, closestDepth);
                if (sampleVelocity.z > 0.5 && candidateBetter && (edgeCandidate > 0.0 || centerInvalid))
                {
                    closestDepth = sampleDepth;
                    bestVelocity = sampleVelocity;
                    dilationStrength = max(dilationStrength, max(edgeCandidate, centerInvalid ? 0.75 : 0.0));
                }
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float closestDepth = tex2D(_BurtTAACurrentDepthTexture, uv).r;
                float4 bestVelocity = tex2D(_BurtTAAVelocityTexture, uv);
                float centerEyeDepth = LinearEyeDepth(closestDepth);
                float depthError = max(centerEyeDepth * 0.0015, 0.01);
                float dilationStrength = 0.0;
                BurtTaaTryVelocityDilationTap(uv, float2(1.0, 0.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(-1.0, 0.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(0.0, 1.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(0.0, -1.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(1.0, 1.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(-1.0, 1.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(1.0, -1.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                BurtTaaTryVelocityDilationTap(uv, float2(-1.0, -1.0), centerEyeDepth, depthError, closestDepth, bestVelocity, dilationStrength);
                bestVelocity.z = min(bestVelocity.z, lerp(1.0, 0.82, saturate(dilationStrength)));
                bestVelocity.w = closestDepth;
                return bestVelocity;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Decimate History"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAADepthHistoryTexture;
            sampler2D _BurtTAAHistoryConfidenceTexture;
            sampler2D _BurtTAAVelocityTexture;
            Texture2D<int> _BurtTAAPrevUseCountTexture;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAParams;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float BurtTaaValidSurfaceWeight(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return step(1e-6, rawDepth);
                #else
                    return 1.0 - step(1.0 - 1e-6, rawDepth);
                #endif
            }

            float BurtTaaDepthDisocclusionWeight(float currentRawDepth, float historyRawDepth)
            {
                float valid = BurtTaaValidSurfaceWeight(currentRawDepth) * BurtTaaValidSurfaceWeight(historyRawDepth);
                float currentEyeDepth = LinearEyeDepth(currentRawDepth);
                float historyEyeDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(currentEyeDepth * 0.012, 0.025);
                return valid * saturate(1.0 - abs(currentEyeDepth - historyEyeDepth) / depthTolerance);
            }

            float BurtTaaDepthConfidenceValidity(float currentRawDepth, float historyRawDepth, float previousConfidence)
            {
                float depthValidity = saturate(BurtTaaDepthDisocclusionWeight(currentRawDepth, historyRawDepth));
                float confidenceDepthRelax = saturate(previousConfidence * depthValidity * 1.35);
                float confidenceDepthCeiling = lerp(0.45, 1.0, depthValidity);
                return min(max(depthValidity, confidenceDepthRelax), confidenceDepthCeiling);
            }

            float BurtTaaHistoryDepthNeighborhoodValidity(float2 historyUv, float currentRawDepth, float previousConfidence)
            {
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                float validity = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    float wy = y == 0 ? (1.0 - blend.y) : blend.y;
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float wx = x == 0 ? (1.0 - blend.x) : blend.x;
                        float2 tapUv = (basePixel + float2(x, y) + 0.5) * _BurtTAATexelSize.xy;
                        float tapInBounds = step(0.0, tapUv.x) * step(tapUv.x, 1.0) * step(0.0, tapUv.y) * step(tapUv.y, 1.0);
                        float tapWeight = wx * wy * tapInBounds;
                        float2 safeTapUv = saturate(tapUv);
                        float tapDepth = tex2D(_BurtTAADepthHistoryTexture, safeTapUv).r;
                        float tapConfidence = max(previousConfidence, tex2D(_BurtTAAHistoryConfidenceTexture, safeTapUv).r);
                        validity += BurtTaaDepthConfidenceValidity(currentRawDepth, tapDepth, tapConfidence) * tapWeight;
                        weightSum += tapWeight;
                    }
                }

                return validity * rcp(max(weightSum, 1e-4));
            }

            float BurtTaaLoadPrevUseCount(float2 historyUv)
            {
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                int2 textureSize = int2((int)_BurtTAATexelSize.z, (int)_BurtTAATexelSize.w);
                float useCount = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    float wy = y == 0 ? (1.0 - blend.y) : blend.y;
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float wx = x == 0 ? (1.0 - blend.x) : blend.x;
                        int2 pixel = int2(basePixel) + int2(x, y);
                        bool inBounds = pixel.x >= 0 && pixel.y >= 0 && pixel.x < textureSize.x && pixel.y < textureSize.y;
                        float weight = wx * wy * (inBounds ? 1.0 : 0.0);
                        int2 safePixel = clamp(pixel, int2(0, 0), textureSize - 1);
                        useCount += max(0, _BurtTAAPrevUseCountTexture.Load(int3(safePixel, 0))) * (1.0 / 255.0) * weight;
                        weightSum += weight;
                    }
                }

                return useCount * rcp(max(weightSum, 1e-4));
            }

            float BurtTaaHistoryCoverageWeight(float2 historyUv)
            {
                float historyUseCount = BurtTaaLoadPrevUseCount(historyUv);
                float underCoverage = saturate(historyUseCount);
                float overCoverage = saturate(1.0 - max(0.0, historyUseCount - 1.0) * 0.55);
                return min(underCoverage, overCoverage);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float2 historyUv = input.uv + velocityData.xy;
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float2 safeHistoryUv = saturate(historyUv);
                float currentRawDepth = velocityData.z > 0.5 ? velocityData.w : tex2D(_BurtTAACurrentDepthTexture, input.uv).r;
                float previousConfidence = tex2D(_BurtTAAHistoryConfidenceTexture, safeHistoryUv).r;
                float depthValidity = BurtTaaHistoryDepthNeighborhoodValidity(safeHistoryUv, currentRawDepth, previousConfidence);
                float coverageValidity = BurtTaaHistoryCoverageWeight(historyUv);
                float historyValidity = _BurtTAAParams.z * inBounds * velocityData.z;
                float parallaxValidity = depthValidity * coverageValidity * historyValidity;
                return float4(saturate(parallaxValidity), saturate(coverageValidity * historyValidity), 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Confidence Update"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAADepthHistoryTexture;
            sampler2D _BurtTAAHistoryConfidenceTexture;
            sampler2D _BurtTAARawVelocityTexture;
            sampler2D _BurtTAAVelocityTexture;
            sampler2D _BurtTAAParallaxRejectionTexture;
            sampler2D _BurtGBuffer1;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAParams;
            float4 _BurtTAARejectionParams;
            float4 _BurtTAAFeedbackParams;
            float4 _BurtTAAResponsiveParams;
            float4 _BurtTAAEdgeParams;
            float _BurtTAAHasGBuffer;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float BurtTaaValidSurfaceWeight(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return step(1e-6, rawDepth);
                #else
                    return 1.0 - step(1.0 - 1e-6, rawDepth);
                #endif
            }

            float BurtTaaDepthWeight(float currentRawDepth, float historyRawDepth)
            {
                float valid = BurtTaaValidSurfaceWeight(currentRawDepth) * BurtTaaValidSurfaceWeight(historyRawDepth);
                float currentEyeDepth = LinearEyeDepth(currentRawDepth);
                float historyEyeDepth = LinearEyeDepth(historyRawDepth);
                float tolerance = max(currentEyeDepth * 0.012, 0.025);
                return valid * saturate(1.0 - abs(currentEyeDepth - historyEyeDepth) / tolerance);
            }

            float BurtTaaDepthConfidenceValidity(float currentRawDepth, float historyRawDepth, float previousConfidence)
            {
                float depthValidity = saturate(BurtTaaDepthWeight(currentRawDepth, historyRawDepth));
                float confidenceDepthRelax = saturate(previousConfidence * depthValidity * 1.35);
                float confidenceDepthCeiling = lerp(0.45, 1.0, depthValidity);
                return min(max(depthValidity, confidenceDepthRelax), confidenceDepthCeiling);
            }

            float BurtTaaHistoryDepthNeighborhoodValidity(float2 historyUv, float currentRawDepth, float previousConfidence)
            {
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                float validity = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    float wy = y == 0 ? (1.0 - blend.y) : blend.y;
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float wx = x == 0 ? (1.0 - blend.x) : blend.x;
                        float2 tapUv = (basePixel + float2(x, y) + 0.5) * _BurtTAATexelSize.xy;
                        float tapInBounds = step(0.0, tapUv.x) * step(tapUv.x, 1.0) * step(0.0, tapUv.y) * step(tapUv.y, 1.0);
                        float tapWeight = wx * wy * tapInBounds;
                        float2 safeTapUv = saturate(tapUv);
                        float tapDepth = tex2D(_BurtTAADepthHistoryTexture, safeTapUv).r;
                        float tapConfidence = max(previousConfidence, tex2D(_BurtTAAHistoryConfidenceTexture, safeTapUv).r);
                        validity += BurtTaaDepthConfidenceValidity(currentRawDepth, tapDepth, tapConfidence) * tapWeight;
                        weightSum += tapWeight;
                    }
                }

                return validity * rcp(max(weightSum, 1e-4));
            }

            float3 BurtTaaDecodeOctNormal(float2 encodedNormal)
            {
                float2 f = encodedNormal * 2.0 - 1.0;
                float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
                float t = saturate(-n.z);
                n.x += n.x >= 0.0 ? -t : t;
                n.y += n.y >= 0.0 ? -t : t;
                return normalize(n + 1e-6);
            }

            float BurtTaaNormalEdgeWeight(float2 uv)
            {
                if (_BurtTAAHasGBuffer < 0.5)
                {
                    return 1.0;
                }

                float2 texel = _BurtTAATexelSize.xy;
                float3 centerNormal = BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, uv).rg);
                float minDot = 1.0;
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv + float2(texel.x, 0.0))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv - float2(texel.x, 0.0))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv + float2(0.0, texel.y))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv - float2(0.0, texel.y))).rg)));
                return lerp(0.55, 1.0, saturate((minDot - 0.75) * 4.0));
            }

            float2 BurtTaaVelocitySourceNeighborhood(float2 uv)
            {
                float trusted = 0.0;
                float untrusted = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float4 source = tex2D(_BurtTAARawVelocityTexture, saturate(uv + float2(x, y) * _BurtTAATexelSize.xy));
                        float trustedTap = step(0.75, source.w) * source.z;
                        float untrustedTap = step(0.25, source.w) * (1.0 - trustedTap) * source.z;
                        trusted = max(trusted, trustedTap);
                        untrusted = max(untrusted, untrustedTap);
                    }
                }

                return float2(trusted, untrusted);
            }

            float BurtTaaVelocityDiscontinuity(float2 uv, float4 centerVelocityData)
            {
                float centerValid = centerVelocityData.z;
                float2 centerMotionPixels = centerVelocityData.xy * _BurtTAATexelSize.zw;
                float maxVelocityDelta = 0.0;
                float maxValidityDelta = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float4 sampleVelocity = tex2D(_BurtTAAVelocityTexture, saturate(uv + float2(x, y) * _BurtTAATexelSize.xy));
                        float2 sampleMotionPixels = sampleVelocity.xy * _BurtTAATexelSize.zw;
                        maxVelocityDelta = max(maxVelocityDelta, length(sampleMotionPixels - centerMotionPixels) * centerValid * sampleVelocity.z);
                        maxValidityDelta = max(maxValidityDelta, abs(sampleVelocity.z - centerValid));
                    }
                }

                return saturate(max((maxVelocityDelta - 0.5) / 4.0, maxValidityDelta) * max(_BurtTAAEdgeParams.x, 0.0));
            }

            float BurtTaaDilatedHistoryValidity(float2 uv, float centerValidity, float edgeStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float minValidity = centerValidity;
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(texel.x, 0.0))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(texel.x, 0.0))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(0.0, texel.y))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(0.0, texel.y))).r);
                return lerp(centerValidity, minValidity, saturate(edgeStrength));
            }

            float BurtTaaDilatedHistoryCoverage(float2 uv, float centerCoverage, float edgeStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float minCoverage = centerCoverage;
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(texel.x, 0.0))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(texel.x, 0.0))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(0.0, texel.y))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(0.0, texel.y))).g);
                return lerp(centerCoverage, minCoverage, saturate(edgeStrength));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float2 historyUv = input.uv + velocityData.xy;
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float2 safeHistoryUv = saturate(historyUv);
                float currentRawDepth = velocityData.z > 0.5 ? velocityData.w : tex2D(_BurtTAACurrentDepthTexture, input.uv).r;
                float previousConfidence = tex2D(_BurtTAAHistoryConfidenceTexture, safeHistoryUv).r;
                float2 rawHistoryValidity = tex2D(_BurtTAAParallaxRejectionTexture, input.uv).rg;
                float rawParallaxValidity = rawHistoryValidity.r;
                float rawCoverageValidity = rawHistoryValidity.g;
                float2 velocitySourceCoverage = BurtTaaVelocitySourceNeighborhood(input.uv);
                float untrustedObjectMotion = velocitySourceCoverage.y;
                float depthWeight = pow(saturate(BurtTaaHistoryDepthNeighborhoodValidity(safeHistoryUv, currentRawDepth, previousConfidence)), max(_BurtTAARejectionParams.z, 0.001));
                float speedPixels = length(velocityData.xy * _BurtTAATexelSize.zw);
                float speedWeight = saturate(1.0 - max(speedPixels - max(_BurtTAARejectionParams.w, 0.0), 0.0) / max(_BurtTAAFeedbackParams.x, 1.0));
                float velocityEdgeResponsive = BurtTaaVelocityDiscontinuity(input.uv, velocityData);
                float normalWeight = BurtTaaNormalEdgeWeight(input.uv);
                float normalEdgeResponsive = saturate((1.0 - normalWeight) * 0.65);
                float edgeResponsive = max(velocityEdgeResponsive, normalEdgeResponsive);
                float parallaxValidity = BurtTaaDilatedHistoryValidity(input.uv, rawParallaxValidity, edgeResponsive);
                float coverageValidity = BurtTaaDilatedHistoryCoverage(input.uv, rawCoverageValidity, edgeResponsive);
                float disocclusionResponsive = saturate(max(max(max(1.0 - depthWeight, 1.0 - parallaxValidity), 1.0 - coverageValidity), max(velocityEdgeResponsive, normalEdgeResponsive)) * saturate(_BurtTAAResponsiveParams.x));
                float confidenceScale = lerp(1.0, saturate(_BurtTAAResponsiveParams.z), disocclusionResponsive);
                confidenceScale *= lerp(1.0, saturate(_BurtTAAResponsiveParams.y), untrustedObjectMotion);
                float valid = _BurtTAAParams.z * inBounds * velocityData.z * depthWeight * speedWeight * parallaxValidity * lerp(0.35, 1.0, coverageValidity);
                float stableConfidence = saturate(depthWeight * speedWeight * parallaxValidity * coverageValidity * (1.0 - disocclusionResponsive) * (1.0 - untrustedObjectMotion));
                float confidenceGrowth = saturate(_BurtTAAFeedbackParams.w) * lerp(0.65, 1.45, stableConfidence);
                float confidence = lerp(previousConfidence * confidenceScale, 1.0, saturate(confidenceGrowth * confidenceScale)) * valid;
                return float4(saturate(confidence), 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Anti Flicker Update"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            sampler2D _BurtTAAHistoryTexture;
            sampler2D _BurtTAAAntiFlickerHistoryTexture;
            sampler2D _BurtTAARawVelocityTexture;
            sampler2D _BurtTAAVelocityTexture;
            sampler2D _BurtTAAParallaxRejectionTexture;
            sampler2D _BurtGBuffer1;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAParams;
            float4 _BurtTAAXRenderParams;
            float4 _BurtTAAResponsiveParams;
            float4 _BurtTAAEdgeParams;
            float _BurtTAAHasGBuffer;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float BurtTaaWorkingLuma(float3 rgb)
            {
                return dot(rgb, float3(0.25, 0.5, 0.25));
            }

            float3 BurtTaaDecodeOctNormal(float2 encodedNormal)
            {
                float2 f = encodedNormal * 2.0 - 1.0;
                float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
                float t = saturate(-n.z);
                n.x += n.x >= 0.0 ? -t : t;
                n.y += n.y >= 0.0 ? -t : t;
                return normalize(n + 1e-6);
            }

            float BurtTaaNormalEdgeWeight(float2 uv)
            {
                if (_BurtTAAHasGBuffer < 0.5)
                {
                    return 1.0;
                }

                float2 texel = _BurtTAATexelSize.xy;
                float3 centerNormal = BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, uv).rg);
                float minDot = 1.0;
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv + float2(texel.x, 0.0))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv - float2(texel.x, 0.0))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv + float2(0.0, texel.y))).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(tex2D(_BurtGBuffer1, saturate(uv - float2(0.0, texel.y))).rg)));
                return lerp(0.55, 1.0, saturate((minDot - 0.75) * 4.0));
            }

            float2 BurtTaaVelocitySourceNeighborhood(float2 uv)
            {
                float trusted = 0.0;
                float untrusted = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float4 source = tex2D(_BurtTAARawVelocityTexture, saturate(uv + float2(x, y) * _BurtTAATexelSize.xy));
                        float trustedTap = step(0.75, source.w) * source.z;
                        float untrustedTap = step(0.25, source.w) * (1.0 - trustedTap) * source.z;
                        trusted = max(trusted, trustedTap);
                        untrusted = max(untrusted, untrustedTap);
                    }
                }

                return float2(trusted, untrusted);
            }

            float BurtTaaVelocityDiscontinuity(float2 uv, float4 centerVelocityData)
            {
                float centerValid = centerVelocityData.z;
                float2 centerMotionPixels = centerVelocityData.xy * _BurtTAATexelSize.zw;
                float maxVelocityDelta = 0.0;
                float maxValidityDelta = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float4 sampleVelocity = tex2D(_BurtTAAVelocityTexture, saturate(uv + float2(x, y) * _BurtTAATexelSize.xy));
                        float2 sampleMotionPixels = sampleVelocity.xy * _BurtTAATexelSize.zw;
                        maxVelocityDelta = max(maxVelocityDelta, length(sampleMotionPixels - centerMotionPixels) * centerValid * sampleVelocity.z);
                        maxValidityDelta = max(maxValidityDelta, abs(sampleVelocity.z - centerValid));
                    }
                }

                return saturate(max((maxVelocityDelta - 0.5) / 4.0, maxValidityDelta) * max(_BurtTAAEdgeParams.x, 0.0));
            }

            float BurtTaaDilatedHistoryValidity(float2 uv, float centerValidity, float edgeStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float minValidity = centerValidity;
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(texel.x, 0.0))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(texel.x, 0.0))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(0.0, texel.y))).r);
                minValidity = min(minValidity, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(0.0, texel.y))).r);
                return lerp(centerValidity, minValidity, saturate(edgeStrength));
            }

            float BurtTaaDilatedHistoryCoverage(float2 uv, float centerCoverage, float edgeStrength)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float minCoverage = centerCoverage;
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(texel.x, 0.0))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(texel.x, 0.0))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv + float2(0.0, texel.y))).g);
                minCoverage = min(minCoverage, tex2D(_BurtTAAParallaxRejectionTexture, saturate(uv - float2(0.0, texel.y))).g);
                return lerp(centerCoverage, minCoverage, saturate(edgeStrength));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float2 historyUv = input.uv + velocityData.xy;
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float2 safeHistoryUv = saturate(historyUv);
                float3 current = max(tex2D(_BurtPostProcessSourceTexture, input.uv).rgb, 0.0);
                float3 history = max(tex2D(_BurtTAAHistoryTexture, safeHistoryUv).rgb, 0.0);
                float currentLuma = BurtTaaWorkingLuma(current);
                float historyLuma = BurtTaaWorkingLuma(history);
                float temporalContrast = saturate(abs(currentLuma - historyLuma) / max(max(currentLuma, historyLuma), 0.2));
                float2 texel = _BurtTAATexelSize.xy;
                float localLumaMin = currentLuma;
                float localLumaMax = currentLuma;
                float sampleLuma = BurtTaaWorkingLuma(max(tex2D(_BurtPostProcessSourceTexture, saturate(input.uv + float2(texel.x, 0.0))).rgb, 0.0));
                localLumaMin = min(localLumaMin, sampleLuma);
                localLumaMax = max(localLumaMax, sampleLuma);
                sampleLuma = BurtTaaWorkingLuma(max(tex2D(_BurtPostProcessSourceTexture, saturate(input.uv - float2(texel.x, 0.0))).rgb, 0.0));
                localLumaMin = min(localLumaMin, sampleLuma);
                localLumaMax = max(localLumaMax, sampleLuma);
                sampleLuma = BurtTaaWorkingLuma(max(tex2D(_BurtPostProcessSourceTexture, saturate(input.uv + float2(0.0, texel.y))).rgb, 0.0));
                localLumaMin = min(localLumaMin, sampleLuma);
                localLumaMax = max(localLumaMax, sampleLuma);
                sampleLuma = BurtTaaWorkingLuma(max(tex2D(_BurtPostProcessSourceTexture, saturate(input.uv - float2(0.0, texel.y))).rgb, 0.0));
                localLumaMin = min(localLumaMin, sampleLuma);
                localLumaMax = max(localLumaMax, sampleLuma);
                float localHighlightContrast = saturate((localLumaMax - localLumaMin) / max(localLumaMax, 0.2));
                float localHighlightEnergy = saturate((currentLuma - localLumaMin) / max(currentLuma, 0.2)) * saturate(currentLuma / (currentLuma + 0.5));
                float2 previous = tex2D(_BurtTAAAntiFlickerHistoryTexture, safeHistoryUv).rg;
                float motionLength = length(velocityData.xy * _BurtTAATexelSize.zw);
                float lowMotion = saturate(1.0 - motionLength * 0.5);
                float2 rawHistoryValidity = tex2D(_BurtTAAParallaxRejectionTexture, input.uv).rg;
                float rawParallaxValidity = rawHistoryValidity.r;
                float rawCoverageValidity = rawHistoryValidity.g;
                float2 velocitySourceCoverage = BurtTaaVelocitySourceNeighborhood(input.uv);
                float untrustedObjectMotion = velocitySourceCoverage.y;
                float velocityEdgeResponsive = BurtTaaVelocityDiscontinuity(input.uv, velocityData);
                float normalWeight = BurtTaaNormalEdgeWeight(input.uv);
                float normalEdgeResponsive = saturate((1.0 - normalWeight) * 0.65);
                float edgeResponsive = max(velocityEdgeResponsive, normalEdgeResponsive);
                float parallaxValidity = BurtTaaDilatedHistoryValidity(input.uv, rawParallaxValidity, edgeResponsive);
                float coverageValidity = BurtTaaDilatedHistoryCoverage(input.uv, rawCoverageValidity, edgeResponsive);
                float valid = _BurtTAAParams.z * inBounds * velocityData.z * parallaxValidity * lerp(0.35, 1.0, coverageValidity);
                float responsiveBlock = saturate(max(max(max(1.0 - parallaxValidity, 1.0 - coverageValidity), untrustedObjectMotion), max(velocityEdgeResponsive, normalEdgeResponsive)) * saturate(_BurtTAAResponsiveParams.x));
                float currentSignal = smoothstep(0.05, max(_BurtTAAXRenderParams.y, 0.051), temporalContrast) * lowMotion * valid * (1.0 - responsiveBlock);
                float highlightSignal = smoothstep(0.18, 0.75, max(localHighlightContrast, localHighlightEnergy)) * lowMotion * valid * (1.0 - responsiveBlock);
                currentSignal = max(currentSignal, highlightSignal * 0.65);
                return float4(saturate(currentSignal), saturate(previous.x * valid * (1.0 - responsiveBlock)), 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Copy"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return tex2D(_BurtPostProcessSourceTexture, input.uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Bloom Debug"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            float _BurtBloomDebugMode;
            float _BurtBloomDebugYFlip;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float3 SafeBloomDebugColor(float3 color)
            {
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                return min(max(color, 0.0), 65504.0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                if (_BurtBloomDebugYFlip > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                float4 color = tex2D(_BurtPostProcessSourceTexture, uv);
                color.rgb = SafeBloomDebugColor(color.rgb);
                color.a = color.a == color.a ? saturate(color.a) : 0.0;
                if (_BurtBloomDebugMode > 0.5)
                {
                    return float4(color.a.xxx, 1.0);
                }

                return float4(color.rgb / (color.rgb + 1.0), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Build Prev Use Count"
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAAVelocityTexture;
            RWTexture2D<int> _BurtTAAPrevUseCountTexture : register(u1);
            float4 _BurtTAATexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            void BurtTaaAddUseCount(int2 pixel, float weight, int2 textureSize)
            {
                bool inBounds = pixel.x >= 0 && pixel.y >= 0 && pixel.x < textureSize.x && pixel.y < textureSize.y;
                int add = inBounds ? (int)round(saturate(weight) * 255.0) : 0;
                if (add > 0)
                {
                    InterlockedAdd(_BurtTAAPrevUseCountTexture[pixel], add);
                }
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float2 historyUv = input.uv + velocityData.xy;
                float historyInBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float valid = step(1e-5, velocityData.z) * historyInBounds;
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                int2 textureSize = int2((int)_BurtTAATexelSize.z, (int)_BurtTAATexelSize.w);

                BurtTaaAddUseCount(int2(basePixel) + int2(0, 0), (1.0 - blend.x) * (1.0 - blend.y) * valid, textureSize);
                BurtTaaAddUseCount(int2(basePixel) + int2(1, 0), blend.x * (1.0 - blend.y) * valid, textureSize);
                BurtTaaAddUseCount(int2(basePixel) + int2(0, 1), (1.0 - blend.x) * blend.y * valid, textureSize);
                BurtTaaAddUseCount(int2(basePixel) + int2(1, 1), blend.x * blend.y * valid, textureSize);
                return 0.0;
            }
            ENDHLSL
        }


    }

    // 禁用 fallback，避免后处理拷贝失败时悄悄走其他管线 shader。
    Fallback Off
}
