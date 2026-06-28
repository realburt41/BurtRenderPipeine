Shader "Hidden/BurtRP/PostProcessCopy"
{
    // 定义 Shader 在 Unity 内部查找时使用的隐藏路径。
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

            Texture2D _BurtPostProcessSourceTexture;
            Texture2D _BurtTAAHistoryTexture;
            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAADepthHistoryTexture;
            sampler2D _BurtTAARawVelocityTexture;
            sampler2D _BurtTAAVelocityTexture;
            Texture2D<float> _BurtTAAPrevUseCountTexture;
            sampler2D _BurtTAAMetadataTexture;
            sampler2D _BurtTAAParallaxRejectionTexture;
            Texture2D<float> _BurtTAAStencilMaskTexture;
            Texture2D _BurtGBuffer1;
            SamplerState sampler_PointClamp;
            SamplerState sampler_LinearClamp;
            float4x4 _BurtTAAInverseCurrentViewProjection;
            float4x4 _BurtTAAInverseCurrentNonJitteredViewProjection;
            float _BurtTAAHistoryExposureCorrection;
            float4 _BurtTAAJitter;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAParams;
            float4 _BurtTAAParams2;
            float4 _BurtTAAResponsiveParams;
            float4 _BurtTAAEdgeParams;
            float4 _BurtTAACurrentSampleWeights0;
            float4 _BurtTAACurrentSampleWeights1;
            float4 _BurtTAACurrentSampleWeights2;
            float4 _BurtTAAUpscaleParams;
            float4 _BurtTAAStencilTexelSize;
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
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                output.uv = uv;
                return output;
            }

            float2 BurtTaaExternalUv(float2 uv)
            {
                return uv;
            }

            float3 BurtTaaDebugColor(float3 color)
            {
                return color / (1.0 + max(color, 0.0));
            }

            float3 BurtTaaDebugHeatmap(float value)
            {
                value = saturate(value);
                float3 cold = float3(0.02, 0.04, 0.20);
                float3 cyan = float3(0.00, 0.65, 1.00);
                float3 yellow = float3(1.00, 0.88, 0.08);
                float3 white = float3(1.00, 1.00, 1.00);
                float3 low = lerp(cold, cyan, smoothstep(0.0, 0.45, value));
                float3 high = lerp(yellow, white, smoothstep(0.65, 1.0, value));
                return value < 0.65 ? lerp(low, yellow, smoothstep(0.45, 0.65, value)) : high;
            }

            bool BurtTaaAnyNonFinite(float3 value)
            {
                return any((asuint(value) & 0x7F800000u) == 0x7F800000u);
            }

            float3 BurtTaaSanitizeRawColor(float3 color, float3 fallback)
            {
                float3 safeFallback = BurtTaaAnyNonFinite(fallback) ? float3(0.0, 0.0, 0.0) : clamp(fallback, 0.0, 65504.0);
                return BurtTaaAnyNonFinite(color) ? safeFallback : clamp(color, 0.0, 65504.0);
            }

            float3 BurtTaaSanitizeRawColor(float3 color)
            {
                return BurtTaaSanitizeRawColor(color, float3(0.0, 0.0, 0.0));
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
                        useCount += max(0.0, _BurtTAAPrevUseCountTexture.Load(int3(safePixel, 0))) * weight;
                        weightSum += weight;
                    }
                }

                return useCount * rcp(max(weightSum, 1e-4));
            }

            float BurtTaaHistoryUseCountDebug(float2 historyUv)
            {
                return saturate(BurtTaaLoadPrevUseCount(historyUv) * 0.5);
            }

            uint BurtTaaLoadStencil(float2 uv)
            {
                int2 size = max(int2(_BurtTAAStencilTexelSize.zw), int2(1, 1));
                int2 pixel = clamp(int2(uv * size), int2(0, 0), size - 1);
                return (uint)round(max(0.0, _BurtTAAStencilMaskTexture.Load(int3(pixel, 0))));
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
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                return uv;
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
                float2 gbufferUv = BurtTaaExternalUv(uv);
                float3 centerNormal = BurtTaaDecodeOctNormal(_BurtGBuffer1.SampleLevel(sampler_PointClamp, gbufferUv, 0.0).rg);
                float minDot = 1.0;
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(_BurtGBuffer1.SampleLevel(sampler_PointClamp, saturate(gbufferUv + float2(texel.x, 0.0)), 0.0).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(_BurtGBuffer1.SampleLevel(sampler_PointClamp, saturate(gbufferUv - float2(texel.x, 0.0)), 0.0).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(_BurtGBuffer1.SampleLevel(sampler_PointClamp, saturate(gbufferUv + float2(0.0, texel.y)), 0.0).rg)));
                minDot = min(minDot, dot(centerNormal, BurtTaaDecodeOctNormal(_BurtGBuffer1.SampleLevel(sampler_PointClamp, saturate(gbufferUv - float2(0.0, texel.y)), 0.0).rg)));
                return lerp(0.55, 1.0, saturate((minDot - 0.75) * 4.0));
            }

            float3 BurtTaaGBufferNormalDebug(float2 uv)
            {
                if (_BurtTAAHasGBuffer < 0.5)
                {
                    return float3(0.5, 0.5, 0.5);
                }

                return BurtTaaDecodeOctNormal(_BurtGBuffer1.SampleLevel(sampler_PointClamp, BurtTaaExternalUv(uv), 0.0).rg) * 0.5 + 0.5;
            }

            float BurtTaaDepthDisocclusionWeight(float currentRawDepth, float historyRawDepth)
            {
                float currentSurface = BurtTaaValidSurfaceWeight(currentRawDepth);
                float historySurface = BurtTaaValidSurfaceWeight(historyRawDepth);
                float valid = currentSurface * historySurface;
                float skyContinuity = (1.0 - currentSurface) * (1.0 - historySurface);
                float currentEyeDepth = LinearEyeDepth(currentRawDepth);
                float historyEyeDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(currentEyeDepth * 0.012, 0.025);
                return max(skyContinuity, valid * saturate(1.0 - abs(currentEyeDepth - historyEyeDepth) / depthTolerance));
            }

            float4 BurtSampleCurrentRaw(float2 uv)
            {
                float2 pixelFloat = uv * _BurtTAATexelSize.zw - 0.5;
                int2 textureSize = int2((int)_BurtTAATexelSize.z, (int)_BurtTAATexelSize.w);
                int2 pixel = clamp(int2(round(pixelFloat)), int2(0, 0), textureSize - 1);
                float4 color = _BurtPostProcessSourceTexture.Load(int3(pixel, 0));
                color.rgb = BurtTaaSanitizeRawColor(color.rgb);
                return color;
            }

            float3 BurtSampleCurrent(float2 uv)
            {
                return BurtSampleCurrentRaw(uv).rgb;
            }

            float3 BurtTaaApplyHistoryExposureCorrection(float3 history)
            {
                float correction = max(_BurtTAAHistoryExposureCorrection, 0.0);
                correction = correction > 0.0 ? correction : 1.0;
                return history * correction;
            }

            float3 BurtSampleHistoryLinear(float2 uv)
            {
                float3 history = BurtTaaApplyHistoryExposureCorrection(BurtTaaSanitizeRawColor(_BurtTAAHistoryTexture.SampleLevel(sampler_LinearClamp, saturate(uv), 0.0).rgb));
                return BurtTaaSanitizeRawColor(history);
            }

            float3 BurtSampleHistoryCatmullRom(float2 uv)
            {
                float2 textureSize = _BurtTAATexelSize.zw;
                float2 samplePosition = uv * textureSize;
                float2 texPos1 = floor(samplePosition - 0.5) + 0.5;
                float2 f = samplePosition - texPos1;
                float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
                float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
                float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
                float2 w3 = f * f * (-0.5 + 0.5 * f);
                float2 w12 = w1 + w2;
                float2 offset12 = w2 / max(w12, float2(1e-5, 1e-5));
                float2 texPos0 = (texPos1 - 1.0) * _BurtTAATexelSize.xy;
                float2 texPos3 = (texPos1 + 2.0) * _BurtTAATexelSize.xy;
                float2 texPos12 = (texPos1 + offset12) * _BurtTAATexelSize.xy;

                float3 result = 0.0;
                result += BurtSampleHistoryLinear(float2(texPos0.x, texPos0.y)) * w0.x * w0.y;
                result += BurtSampleHistoryLinear(float2(texPos12.x, texPos0.y)) * w12.x * w0.y;
                result += BurtSampleHistoryLinear(float2(texPos3.x, texPos0.y)) * w3.x * w0.y;
                result += BurtSampleHistoryLinear(float2(texPos0.x, texPos12.y)) * w0.x * w12.y;
                result += BurtSampleHistoryLinear(float2(texPos12.x, texPos12.y)) * w12.x * w12.y;
                result += BurtSampleHistoryLinear(float2(texPos3.x, texPos12.y)) * w3.x * w12.y;
                result += BurtSampleHistoryLinear(float2(texPos0.x, texPos3.y)) * w0.x * w3.y;
                result += BurtSampleHistoryLinear(float2(texPos12.x, texPos3.y)) * w12.x * w3.y;
                result += BurtSampleHistoryLinear(float2(texPos3.x, texPos3.y)) * w3.x * w3.y;
                return BurtTaaSanitizeRawColor(result);
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

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float rawDepth = tex2D(_BurtTAACurrentDepthTexture, uv).r;
                float3 current = BurtSampleCurrent(uv);
                float3 currentWorking = BurtTaaToWorkingPerceptualSpace(current);
                float3 neighborhoodSum = 0.0;
                float3 neighborhoodSumSq = 0.0;
                float3 currentFilteredWorking = 0.0;
                float2 texel = _BurtTAATexelSize.xy;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 sampleUv = saturate(uv + texel * float2(x, y));
                        float3 sampleColor = BurtSampleCurrent(sampleUv);
                        float3 sampleWorking = BurtTaaToWorkingPerceptualSpace(sampleColor);
                        float sampleCurrentWeight = BurtTaaCurrentSampleWeight(x, y);
                        neighborhoodSum += sampleWorking;
                        neighborhoodSumSq += sampleWorking * sampleWorking;
                        currentFilteredWorking += sampleWorking * sampleCurrentWeight;
                    }
                }

                float4 velocityData = tex2D(_BurtTAAVelocityTexture, uv);
                float2 historyUv = uv - velocityData.xy;
                float historyValid = _BurtTAAParams.z;
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float finalHistoryAvailability = historyValid * inBounds;
                float2 safeHistoryUv = saturate(historyUv);
                float3 rawHistory = BurtSampleHistoryCatmullRom(safeHistoryUv);
                float3 historyWorking = BurtTaaToWorkingPerceptualSpace(rawHistory);
                float historyRawDepth = tex2D(_BurtTAADepthHistoryTexture, safeHistoryUv).r;
                float closestDepth = BurtTaaValidSurfaceWeight(velocityData.w) > 0.5 ? velocityData.w : rawDepth;
                float depthContinuity = BurtTaaDepthDisocclusionWeight(closestDepth, historyRawDepth);
                float historyValidity = tex2D(_BurtTAAParallaxRejectionTexture, uv).r;
                float historyUseCount = BurtTaaLoadPrevUseCount(historyUv);
                float historyCoverage = saturate(1.0 - abs(historyUseCount - 1.0));
                float finalRejection = saturate(min(historyValidity, depthContinuity) * finalHistoryAvailability);
                uint stencil = BurtTaaLoadStencil(uv);
                float4 taaMetadata = tex2D(_BurtTAAMetadataTexture, uv);
                float outOfBoundsBreak = saturate(1.0 - inBounds);
                float depthBreak = saturate(1.0 - depthContinuity);
                float parallaxBreak = saturate(1.0 - historyValidity);
                float coverageBreak = saturate(1.0 - historyCoverage);
                float geometryBreak = saturate(max(taaMetadata.a, max(max(max(parallaxBreak, coverageBreak), outOfBoundsBreak), depthBreak)));
                float motionPixels = length(velocityData.xy * _BurtTAATexelSize.zw);
                float stencilResponsive = ((stencil & 16u) != 0u ? 1.0 : 0.0) * BurtTaaValidSurfaceWeight(closestDepth) * smoothstep(0.35, 2.50, motionPixels);
                float responsiveStrength = saturate(max(stencilResponsive, taaMetadata.g));
                float responsiveMask = responsiveStrength * finalHistoryAvailability;

                float3 moment1 = neighborhoodSum * (1.0 / 9.0);
                float3 moment2 = neighborhoodSumSq * (1.0 / 9.0);
                float3 standardDeviation = sqrt(abs(moment2 - moment1 * moment1));
                float3 clampMin = moment1 - standardDeviation * 1.5;
                float3 clampMax = moment1 + standardDeviation * 1.5;
                float3 boxCenter = 0.5 * (clampMax + clampMin);
                float3 boxExtents = max(0.5 * (clampMax - clampMin), float3(1e-5, 1e-5, 1e-5));
                float3 historyOffset = historyWorking - boxCenter;
                float3 clampUnitVector = abs(historyOffset) / boxExtents;
                float clampUnit = max(max(clampUnitVector.x, clampUnitVector.y), clampUnitVector.z);
                float3 clippedHistoryWorking = clampUnit > 1.0 ? boxCenter + historyOffset / clampUnit : historyWorking;

                float minBoundLuma = BurtTaaWorkingLuma(clampMin);
                float maxBoundLuma = BurtTaaWorkingLuma(clampMax);
                float historyLuma = BurtTaaWorkingLuma(clippedHistoryWorking);
                float lumaContrast = saturate(0.25 * rcp(1.0 + max(maxBoundLuma - minBoundLuma, 0.0) / max(historyLuma, 1e-4)));
                float xrenderBaseBlend = max(0.05, lumaContrast);
                float currentBlend = lerp(1.0, xrenderBaseBlend, finalRejection);
                float motionResponsive = saturate(max(responsiveStrength, smoothstep(0.10, 1.50, motionPixels)));
                float softGeometryBlend = lerp(0.06, 0.22, saturate(max(responsiveStrength, motionPixels / 12.0)));
                float hardGeometryBreak = smoothstep(0.70, 0.96, max(max(parallaxBreak, depthBreak), coverageBreak)) * finalHistoryAvailability * motionResponsive;
                currentBlend = max(currentBlend, responsiveMask * 0.25);
                currentBlend = max(currentBlend, geometryBreak * softGeometryBlend * finalHistoryAvailability);
                currentBlend = lerp(currentBlend, 1.0, hardGeometryBreak);
                currentBlend = saturate(currentBlend);
                currentBlend = lerp(1.0, currentBlend, finalHistoryAvailability);

                float3 resolvedWorking = lerp(clippedHistoryWorking, currentFilteredWorking, currentBlend);
                resolvedWorking = lerp(currentFilteredWorking, resolvedWorking, finalHistoryAvailability);
                float3 resolved = BurtTaaFromWorkingPerceptualSpace(max(resolvedWorking, 0.0));
                float finalFeedback = saturate((1.0 - currentBlend) * finalHistoryAvailability);

                int debugMode = (int)round(_BurtShadingDebugMode);
                if (_BurtShadingDebugEnabled > 0.5 && ((debugMode >= 320 && debugMode <= 346) || debugMode == 365 || debugMode == 367 || debugMode == 376 || (debugMode >= 489 && debugMode <= 491)))
                {
                    float clipWeight = saturate(rcp(max(clampUnit, 1.0)));
                    float normalWeight = BurtTaaNormalEdgeWeight(uv);
                    float velocityWeight = velocityData.z;
                    float2 velocitySourceCoverage = BurtTaaVelocitySourceNeighborhood(uv);
                    float trustedObjectMotion = max(velocitySourceCoverage.x, taaMetadata.r);
                    float untrustedObjectMotion = max(velocitySourceCoverage.y, taaMetadata.b);
                    float velocityBreak = saturate(1.0 - velocityWeight);
                    float clampBreak = saturate((clampUnit - 1.0) * 0.25);

                    if (debugMode == 320) return float4(BurtTaaDebugColor(rawHistory), 1.0);
                    if (debugMode == 321) return float4(finalFeedback.xxx, 1.0);
                    if (debugMode == 322) return float4(lumaContrast, clipWeight, finalRejection, 1.0);
                    if (debugMode == 323) return float4(saturate(historyUv), finalHistoryAvailability, 1.0);
                    if (debugMode == 324) return float4(saturate(abs(resolved - current) * 8.0), 1.0);
                    if (debugMode == 325) return float4(BurtTaaVelocityDebugColor(velocityData), 1.0);
                    if (debugMode == 326) return float4(finalHistoryAvailability.xxx, 1.0);
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
                    if (debugMode == 333) return float4(finalRejection.xxx, 1.0);
                    if (debugMode == 334) return float4(depthContinuity.xxx, 1.0);
                    if (debugMode == 335) return float4(lumaContrast.xxx, 1.0);
                    if (debugMode == 336) return float4(clipWeight.xxx, 1.0);
                    if (debugMode == 337) return float4(depthContinuity, historyValidity, min(depthContinuity, historyValidity), 1.0);
                    if (debugMode == 338) return float4(normalWeight.xxx, 1.0);
                    if (debugMode == 339) return float4(saturate(rcp(1.0 + motionPixels)).xxx, 1.0);
                    if (debugMode == 340) return float4(currentBlend, finalFeedback, finalRejection, 1.0);
                    if (debugMode == 341) return float4(tex2D(_BurtTAARawVelocityTexture, uv).w.xxx, 1.0);
                    if (debugMode == 342) return float4(BurtTaaGBufferNormalDebug(uv), 1.0);
                    if (debugMode == 343)
                    {
                        float3 diagnosticColor = lerp(float3(0.18, 0.18, 0.18), float3(1.0, 1.0, 1.0), finalRejection);
                        diagnosticColor = lerp(diagnosticColor, float3(1.0, 0.12, 0.05), depthBreak);
                        diagnosticColor = lerp(diagnosticColor, float3(0.10, 0.35, 1.0), parallaxBreak);
                        diagnosticColor = lerp(diagnosticColor, float3(1.0, 0.92, 0.05), velocityBreak);
                        diagnosticColor = lerp(diagnosticColor, float3(0.95, 0.0, 0.95), coverageBreak);
                        return float4(diagnosticColor, 1.0);
                    }
                    if (debugMode == 344) return float4(historyValidity.xxx, 1.0);
                    if (debugMode == 345) return float4(historyCoverage.xxx, 1.0);
                    if (debugMode == 346) return float4(responsiveMask, responsiveStrength, geometryBreak, 1.0);
                    if (debugMode == 376) return float4(BurtTaaHistoryUseCountDebug(uv).xxx, 1.0);
                    if (debugMode == 489) return float4(saturate(float3(0.06, 0.06, 0.06) + taaMetadata.rgb * 0.94 + taaMetadata.a * float3(0.12, 0.12, 0.0)), 1.0);
                    if (debugMode == 490) return float4(saturate(float3(0.04, 0.04, 0.04) + float3(trustedObjectMotion, untrustedObjectMotion, velocityWeight) * 0.96), 1.0);
                    if (debugMode == 491)
                    {
                        float upscaleActive = step(1.0001, max(_BurtTAAUpscaleParams.z, _BurtTAAUpscaleParams.w));
                        return float4(upscaleActive, saturate((_BurtTAAUpscaleParams.z - 1.0) * 1.0), saturate((_BurtTAAUpscaleParams.w - 1.0) * 1.0), 1.0);
                    }
                    if (debugMode == 365)
                    {
                        float strongestRejectDebug = max(max(depthBreak, parallaxBreak), max(velocityBreak, clampBreak));
                        float3 reasonColor = float3(0.035, 0.035, 0.035) * (1.0 - strongestRejectDebug);
                        reasonColor += depthBreak * float3(1.0, 0.05, 0.02);
                        reasonColor += parallaxBreak * float3(0.05, 1.0, 0.08);
                        reasonColor += coverageBreak * float3(1.0, 0.80, 0.05);
                        reasonColor += velocityBreak * float3(0.10, 0.35, 1.0);
                        reasonColor += clampBreak * float3(0.95, 0.0, 0.95);
                        reasonColor += outOfBoundsBreak * float3(0.0, 0.85, 1.0);
                        return float4(saturate(reasonColor), 1.0);
                    }
                    if (debugMode == 367)
                    {
                        float3 feedbackColor = BurtTaaDebugHeatmap(finalFeedback);
                        return float4(feedbackColor, 1.0);
                    }

                    return float4(responsiveMask, velocityBreak, depthBreak, 1.0);
                }

                return float4(BurtTaaSanitizeRawColor(resolved, current), saturate(BurtSampleCurrentRaw(uv).a));
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
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                output.uv = uv;
                return output;
            }

            float2 BurtTaaExternalUv(float2 uv)
            {
                return uv;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return tex2D(_BurtCameraDepthTexture, BurtTaaExternalUv(input.uv)).rrrr;
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
            float4x4 _BurtTAAInverseCurrentNonJitteredViewProjection;
            float4x4 _BurtTAACurrentNonJitteredViewProjection;
            float4x4 _BurtTAAPreviousNonJitteredViewProjection;
            float4 _BurtTAAJitter;
            float4 _BurtTAATexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                return uv;
            }

            float4 BurtTaaScreenUvToClip(float2 uv, float rawDepth)
            {
                float2 clipXY = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                return float4(clipXY, rawDepth, 1.0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = tex2D(_BurtTAACurrentDepthTexture, input.uv).r;
                float4 clip = BurtTaaScreenUvToClip(input.uv, rawDepth);
                float2 currentFrameJitter = _BurtTAAJitter.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    currentFrameJitter.y = -currentFrameJitter.y;
                #endif
                clip.xy -= currentFrameJitter;
                float4 world = mul(_BurtTAAInverseCurrentNonJitteredViewProjection, clip);
                world.xyz /= max(abs(world.w), 1e-6);
                float4 previousClip = mul(_BurtTAAPreviousNonJitteredViewProjection, float4(world.xyz, 1.0));
                float2 currentUv = BurtTaaClipToUv(clip);
                float2 previousUv = BurtTaaClipToUv(previousClip);
                float surfaceValid = BurtTaaValidSurfaceWeight(rawDepth);
                float previousAvailable = step(1e-5, previousClip.w);
                previousAvailable *= step(0.0, previousUv.x) * step(previousUv.x, 1.0) * step(0.0, previousUv.y) * step(previousUv.y, 1.0);
                float2 velocity = currentUv - previousUv;
                float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
                float keepVelocity = step(0.02, max(velocityPixels.x, velocityPixels.y));
                velocity *= keepVelocity;
                velocity = lerp(velocity * surfaceValid, float2(2.0, 2.0), surfaceValid * (1.0 - previousAvailable));
                return float4(velocity, surfaceValid * previousAvailable, 0.0);
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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAAVelocityTexture;
            Texture2D<float> _BurtTAAStencilMaskTexture;
            float4x4 _BurtTAAInverseCurrentNonJitteredViewProjection;
            float4x4 _BurtTAAPreviousNonJitteredViewProjection;
            float4 _BurtTAAJitter;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAStencilTexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                return uv;
            }

            float4 BurtTaaScreenUvToClip(float2 uv, float rawDepth)
            {
                float2 clipXY = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                return float4(clipXY, rawDepth, 1.0);
            }

            float2 BurtTaaComputeStaticVelocity(float2 uv, float rawDepth, out float available)
            {
                float4 clip = BurtTaaScreenUvToClip(uv, rawDepth);
                float2 currentFrameJitter = _BurtTAAJitter.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    currentFrameJitter.y = -currentFrameJitter.y;
                #endif
                clip.xy -= currentFrameJitter;
                float4 world = mul(_BurtTAAInverseCurrentNonJitteredViewProjection, clip);
                world.xyz /= max(abs(world.w), 1e-6);
                float4 previousClip = mul(_BurtTAAPreviousNonJitteredViewProjection, float4(world.xyz, 1.0));
                float2 currentUv = BurtTaaClipToUv(clip);
                float2 previousUv = BurtTaaClipToUv(previousClip);
                available = BurtTaaValidSurfaceWeight(rawDepth) * step(1e-5, previousClip.w);
                available *= step(0.0, previousUv.x) * step(previousUv.x, 1.0) * step(0.0, previousUv.y) * step(previousUv.y, 1.0);
                float2 velocity = currentUv - previousUv;
                float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
                float keepVelocity = step(0.02, max(velocityPixels.x, velocityPixels.y));
                return velocity * keepVelocity;
            }

            uint BurtTaaLoadStencil(float2 uv)
            {
                int2 size = max(int2(_BurtTAAStencilTexelSize.zw), int2(1, 1));
                int2 pixel = clamp(int2(uv * size), int2(0, 0), size - 1);
                return (uint)round(max(0.0, _BurtTAAStencilMaskTexture.Load(int3(pixel, 0))));
            }

            void BurtTaaSelectClosestDepthTap(float2 uv, float2 pixelOffset, float centerDepth, float centerDeviceZError, inout float closestDepth, inout float2 closestOffset)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float2 sampleUv0 = saturate(uv + pixelOffset * texel);
                float2 sampleUv1 = saturate(uv - pixelOffset * texel);
                float depth0 = tex2D(_BurtTAACurrentDepthTexture, sampleUv0).r;
                float depth1 = tex2D(_BurtTAACurrentDepthTexture, sampleUv1).r;
                float depthDiff = abs(depth0 - depth1);
                float depthVariation = 0.5 * (depth0 + depth1) - centerDepth;

                #if defined(UNITY_REVERSED_Z)
                    bool edgeCandidate = depthVariation > max(depthDiff * 0.25, centerDeviceZError);
                #else
                    bool edgeCandidate = -depthVariation > max(depthDiff * 0.25, centerDeviceZError);
                #endif

                if (edgeCandidate && BurtTaaIsCloserDepth(depth0, closestDepth))
                {
                    closestDepth = depth0;
                    closestOffset = pixelOffset;
                }

                if (edgeCandidate && BurtTaaIsCloserDepth(depth1, closestDepth))
                {
                    closestDepth = depth1;
                    closestOffset = -pixelOffset;
                }
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float centerDepth = tex2D(_BurtTAACurrentDepthTexture, uv).r;
                float closestDepth = centerDepth;
                float2 closestOffset = 0.0;
                float centerEyeDepth = LinearEyeDepth(centerDepth);
                float centerDeviceZError = max(abs(centerDepth) * 1e-5, max(centerEyeDepth * 1e-7, 1e-7));

                BurtTaaSelectClosestDepthTap(uv, float2(1.0, 0.0), centerDepth, centerDeviceZError, closestDepth, closestOffset);
                BurtTaaSelectClosestDepthTap(uv, float2(1.0, 1.0), centerDepth, centerDeviceZError, closestDepth, closestOffset);
                BurtTaaSelectClosestDepthTap(uv, float2(0.0, 1.0), centerDepth, centerDeviceZError, closestDepth, closestOffset);
                BurtTaaSelectClosestDepthTap(uv, float2(-1.0, 1.0), centerDepth, centerDeviceZError, closestDepth, closestOffset);

                float staticAvailable = 0.0;
                float2 finalVelocity = BurtTaaComputeStaticVelocity(uv, closestDepth, staticAvailable);
                float2 closestUv = saturate(uv + closestOffset * _BurtTAATexelSize.xy);
                float4 objectVelocity = tex2D(_BurtTAAVelocityTexture, closestUv);
                float stencilObjectMotion = (BurtTaaLoadStencil(closestUv) & 8u) != 0u ? 1.0 : 0.0;
                float rawObjectMotion = step(0.75, objectVelocity.w);
                float objectVelocityValid = max(stencilObjectMotion, rawObjectMotion) * objectVelocity.z * rawObjectMotion;
                finalVelocity = lerp(finalVelocity, objectVelocity.xy, objectVelocityValid);
                float available = max(staticAvailable, objectVelocityValid);
                float2 historyUv = uv - finalVelocity;
                float historyInBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                return float4(finalVelocity, saturate(available * historyInBounds), closestDepth);
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
            sampler2D _BurtTAAVelocityTexture;
            Texture2D<float> _BurtTAAPrevUseCountTexture;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAParams;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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
                float currentSurface = BurtTaaValidSurfaceWeight(currentRawDepth);
                float historySurface = BurtTaaValidSurfaceWeight(historyRawDepth);
                float valid = currentSurface * historySurface;
                float skyContinuity = (1.0 - currentSurface) * (1.0 - historySurface);
                float currentEyeDepth = LinearEyeDepth(currentRawDepth);
                float historyEyeDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(currentEyeDepth * 0.012, 0.025);
                return max(skyContinuity, valid * saturate(1.0 - abs(currentEyeDepth - historyEyeDepth) / depthTolerance));
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
                        useCount += max(0.0, _BurtTAAPrevUseCountTexture.Load(int3(safePixel, 0))) * weight;
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

            float BurtTaaLinearDepth(float rawDepth)
            {
                return max(LinearEyeDepth(rawDepth), 1e-4);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float2 historyUv = input.uv - velocityData.xy;
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float currentRawDepth = BurtTaaValidSurfaceWeight(velocityData.w) > 0.5 ? velocityData.w : tex2D(_BurtTAACurrentDepthTexture, input.uv).r;
                float currentLinearDepth = max(LinearEyeDepth(currentRawDepth), 1e-4);
                float2 samplePosition = historyUv * _BurtTAATexelSize.zw - 0.5;
                float2 basePixel = floor(samplePosition);
                float2 blend = saturate(samplePosition - basePixel);
                int2 textureSize = int2((int)_BurtTAATexelSize.z, (int)_BurtTAATexelSize.w);
                float finalRejection = 0.0;
                float historyGhosting = 0.0;
                float depthRejectionSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    float wy = y == 0 ? (1.0 - blend.y) : blend.y;
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float wx = x == 0 ? (1.0 - blend.x) : blend.x;
                        int2 pixel = int2(basePixel) + int2(x, y);
                        bool pixelInBounds = pixel.x >= 0 && pixel.y >= 0 && pixel.x < textureSize.x && pixel.y < textureSize.y;
                        float weight = wx * wy * (pixelInBounds ? 1.0 : 0.0) * inBounds;
                        int2 safePixel = clamp(pixel, int2(0, 0), textureSize - 1);
                        float previousUseCount = max(0.0, _BurtTAAPrevUseCountTexture.Load(int3(safePixel, 0)));
                        float historyGhostingRejection = saturate(1.0 - abs(previousUseCount - 1.0));
                        float previousRawDepth = tex2D(_BurtTAADepthHistoryTexture, (float2(safePixel) + 0.5) * _BurtTAATexelSize.xy).r;
                        float previousLinearDepth = BurtTaaLinearDepth(previousRawDepth);
                        float depthDelta = abs(currentLinearDepth - previousLinearDepth);
                        float depthRejection = saturate(2.0 - 4.0 * depthDelta / previousLinearDepth);
                        float tapRejection = max(historyGhostingRejection, depthRejection);
                        finalRejection += tapRejection * weight;
                        historyGhosting += historyGhostingRejection * weight;
                        depthRejectionSum += depthRejection * weight;
                    }
                }

                return float4(saturate(finalRejection), saturate(max(historyGhosting, depthRejectionSum)), 0.0, 1.0);
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
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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
            float _BurtBloomThreshold;
            float _BurtBloomSoftKnee;
            float _BurtBloomBypassThreshold;
            float _BurtBloomFireflyClamp;
            float _BurtPostExposure;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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

            float BurtBloomDebugLuminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float3 ClampBloomDebugFirefly(float3 color)
            {
                float clampLuma = max(_BurtBloomFireflyClamp, 1.0);
                float luma = BurtBloomDebugLuminance(color);
                float softLuma = clampLuma + (luma - clampLuma) / (1.0 + max(luma - clampLuma, 0.0) / clampLuma);
                float scale = luma > clampLuma ? softLuma / max(luma, 1e-4) : 1.0;
                return color * scale;
            }

            float BloomThresholdMask(float3 color)
            {
                if (_BurtBloomBypassThreshold > 0.5)
                {
                    return 1.0;
                }

                float brightness = BurtBloomDebugLuminance(color * _BurtPostExposure);
                float knee = max(_BurtBloomThreshold * _BurtBloomSoftKnee, 0.0001);
                float soft = clamp(brightness - _BurtBloomThreshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                float contribution = max(soft, brightness - _BurtBloomThreshold);
                contribution /= max(brightness, 0.0001);
                return saturate(contribution);
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
                if (_BurtBloomDebugMode > 1.5)
                {
                    float mask = BloomThresholdMask(ClampBloomDebugFirefly(color.rgb));
                    return float4(mask.xxx, 1.0);
                }

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
            Name "Burt Temporal AA Closest Depth Copy"
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

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float closestDepth = BurtTaaValidSurfaceWeight(velocityData.w) > 0.5 ? velocityData.w : tex2D(_BurtTAACurrentDepthTexture, input.uv).r;
                return float4(closestDepth, closestDepth, closestDepth, closestDepth);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Build Prev Use Count"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAAVelocityTexture;
            float4 _BurtTAATexelSize;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                output.uv = uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, input.uv);
                float2 historyUv = input.uv - velocityData.xy;
                float historyInBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                return float4(historyInBounds, historyInBounds, historyInBounds, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Auto Exposure Log Luminance Reduce"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            float4 _BurtAutoExposureTexelSize;
            float _BurtInvPreExposure;

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

            float BurtAutoExposureLuminance(float3 color)
            {
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                return max(dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722)), 1e-6);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 texel = _BurtAutoExposureTexelSize.xy;
                float2 uv = input.uv;
                float sum = 0.0;
                float invPreExposure = max(_BurtInvPreExposure, 0.0);
                sum += log2(BurtAutoExposureLuminance(tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(-0.5, -0.5))).rgb * invPreExposure));
                sum += log2(BurtAutoExposureLuminance(tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(0.5, -0.5))).rgb * invPreExposure));
                sum += log2(BurtAutoExposureLuminance(tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(-0.5, 0.5))).rgb * invPreExposure));
                sum += log2(BurtAutoExposureLuminance(tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(0.5, 0.5))).rgb * invPreExposure));
                return float4(sum * 0.25, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Auto Exposure Final Reduce"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            float4 _BurtAutoExposureTexelSize;

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
                float2 texel = _BurtAutoExposureTexelSize.xy;
                float2 uv = input.uv;
                float sum = 0.0;
                sum += tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(-0.5, -0.5))).r;
                sum += tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(0.5, -0.5))).r;
                sum += tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(-0.5, 0.5))).r;
                sum += tex2D(_BurtPostProcessSourceTexture, saturate(uv + texel * float2(0.5, 0.5))).r;
                return float4(clamp(sum * 0.25, -20.0, 16.0), 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Auto Exposure Debug"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            float4 _BurtAutoExposureTexelSize;
            float _BurtAutoExposureDebugMode;
            float4 _BurtAutoExposureDebugParams;
            float _BurtInvPreExposure;

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

            float BurtAutoExposureDebugLuminance(float3 color)
            {
                color.r = color.r == color.r ? color.r : 0.0;
                color.g = color.g == color.g ? color.g : 0.0;
                color.b = color.b == color.b ? color.b : 0.0;
                return max(dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722)), 1e-6);
            }

            float3 BurtAutoExposureHeatmap(float t)
            {
                t = saturate(t);
                float3 cold = float3(0.02, 0.05, 0.45);
                float3 cyan = float3(0.0, 0.75, 1.0);
                float3 green = float3(0.1, 0.9, 0.2);
                float3 yellow = float3(1.0, 0.85, 0.05);
                float3 red = float3(1.0, 0.08, 0.02);
                float3 a = lerp(cold, cyan, saturate(t * 4.0));
                float3 b = lerp(green, yellow, saturate((t - 0.5) * 4.0));
                float3 c = lerp(b, red, saturate((t - 0.75) * 4.0));
                return t < 0.5 ? lerp(a, green, saturate((t - 0.25) * 4.0)) : c;
            }

            float BurtAutoExposureMeterWeight(float2 uv)
            {
                float width = max(1.0, _BurtAutoExposureTexelSize.z);
                float height = max(1.0, _BurtAutoExposureTexelSize.w);
                float2 centered = (uv - 0.5) * 2.0;
                centered.x *= width / height;
                float radius = length(centered);
                float t = smoothstep(0.35, 0.9, radius);
                return lerp(1.0, 0.2, t);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 color = tex2D(_BurtPostProcessSourceTexture, input.uv).rgb * max(_BurtInvPreExposure, 0.0);
                float logLum = clamp(log2(BurtAutoExposureDebugLuminance(color)), -20.0, 16.0);
                float minLogLum = _BurtAutoExposureDebugParams.x;
                float maxLogLum = max(minLogLum + 0.001, _BurtAutoExposureDebugParams.y);
                float normalized = saturate((logLum - minLogLum) / (maxLogLum - minLogLum));

                if (_BurtAutoExposureDebugMode < 1.5)
                {
                    return float4(BurtAutoExposureHeatmap(normalized), 1.0);
                }

                if (_BurtAutoExposureDebugMode < 2.5)
                {
                    float weight = BurtAutoExposureMeterWeight(input.uv);
                    return float4(weight.xxx, 1.0);
                }

                if (logLum < minLogLum)
                {
                    return float4(0.05, 0.2, 1.0, 1.0);
                }

                if (logLum > maxLogLum)
                {
                    return float4(1.0, 0.08, 0.02, 1.0);
                }

                float avgLine = 1.0 - smoothstep(0.03, 0.08, abs(normalized - saturate((_BurtAutoExposureDebugParams.w - minLogLum) / (maxLogLum - minLogLum))));
                float3 rangeColor = lerp(float3(normalized, normalized, normalized), float3(1.0, 0.9, 0.1), avgLine);
                return float4(rangeColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Metadata"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtTAARawVelocityTexture;
            sampler2D _BurtTAAVelocityTexture;
            sampler2D _BurtTAACurrentDepthTexture;
            sampler2D _BurtTAAParallaxRejectionTexture;
            Texture2D<float> _BurtTAAStencilMaskTexture;
            float4 _BurtTAATexelSize;
            float4 _BurtTAAStencilTexelSize;
            float4 _BurtTAAResponsiveParams;
            float4 _BurtTAAEdgeParams;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
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

            float BurtTaaDepthWeight(float currentRawDepth, float sampleRawDepth)
            {
                float currentSurface = BurtTaaValidSurfaceWeight(currentRawDepth);
                float sampleSurface = BurtTaaValidSurfaceWeight(sampleRawDepth);
                float currentEyeDepth = LinearEyeDepth(currentRawDepth);
                float sampleEyeDepth = LinearEyeDepth(sampleRawDepth);
                float tolerance = max(currentEyeDepth * 0.018, 0.035);
                float surfaceWeight = currentSurface * sampleSurface * saturate(1.0 - abs(currentEyeDepth - sampleEyeDepth) / tolerance);
                float skyWeight = (1.0 - currentSurface) * (1.0 - sampleSurface);
                return max(surfaceWeight, skyWeight);
            }

            uint BurtTaaLoadStencil(float2 uv)
            {
                int2 size = max(int2(_BurtTAAStencilTexelSize.zw), int2(1, 1));
                int2 pixel = clamp(int2(uv * size), int2(0, 0), size - 1);
                return (uint)round(max(0.0, _BurtTAAStencilMaskTexture.Load(int3(pixel, 0))));
            }

            float BurtTaaDepthEdgeResponsive(float2 uv, float centerRawDepth)
            {
                float2 texel = _BurtTAATexelSize.xy;
                float centerSurface = BurtTaaValidSurfaceWeight(centerRawDepth);
                float maxBreak = 0.0;
                float sampleDepth = tex2D(_BurtTAACurrentDepthTexture, saturate(uv + float2(texel.x, 0.0))).r;
                maxBreak = max(maxBreak, 1.0 - BurtTaaDepthWeight(centerRawDepth, sampleDepth));
                sampleDepth = tex2D(_BurtTAACurrentDepthTexture, saturate(uv - float2(texel.x, 0.0))).r;
                maxBreak = max(maxBreak, 1.0 - BurtTaaDepthWeight(centerRawDepth, sampleDepth));
                sampleDepth = tex2D(_BurtTAACurrentDepthTexture, saturate(uv + float2(0.0, texel.y))).r;
                maxBreak = max(maxBreak, 1.0 - BurtTaaDepthWeight(centerRawDepth, sampleDepth));
                sampleDepth = tex2D(_BurtTAACurrentDepthTexture, saturate(uv - float2(0.0, texel.y))).r;
                maxBreak = max(maxBreak, 1.0 - BurtTaaDepthWeight(centerRawDepth, sampleDepth));
                return saturate(maxBreak * centerSurface * max(_BurtTAAEdgeParams.y, 0.0));
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

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float4 rawVelocity = tex2D(_BurtTAARawVelocityTexture, uv);
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, uv);
                float rawDepth = tex2D(_BurtTAACurrentDepthTexture, uv).r;
                float closestDepth = BurtTaaValidSurfaceWeight(velocityData.w) > 0.5 ? velocityData.w : rawDepth;
                float surfaceWeight = BurtTaaValidSurfaceWeight(closestDepth);
                float2 sourceCoverage = BurtTaaVelocitySourceNeighborhood(uv);
                float trustedObjectMotion = max(sourceCoverage.x, step(0.75, rawVelocity.w) * rawVelocity.z);
                float untrustedObjectMotion = max(sourceCoverage.y, step(0.25, rawVelocity.w) * (1.0 - step(0.75, rawVelocity.w)) * rawVelocity.z);
                uint stencil = BurtTaaLoadStencil(uv);
                float stencilObjectMotion = ((stencil & 8u) != 0u ? 1.0 : 0.0) * step(0.75, rawVelocity.w) * rawVelocity.z;
                trustedObjectMotion = max(trustedObjectMotion, stencilObjectMotion);

                float historyValidity = tex2D(_BurtTAAParallaxRejectionTexture, uv).r;
                float2 historyUv = uv - velocityData.xy;
                float parallaxBreak = saturate(1.0 - historyValidity);
                float coverageBreak = parallaxBreak;
                float depthEdgeResponsive = BurtTaaDepthEdgeResponsive(uv, closestDepth);
                float velocityEdgeResponsive = BurtTaaVelocityDiscontinuity(uv, velocityData);
                float inBounds = step(0.0, historyUv.x) * step(historyUv.x, 1.0) * step(0.0, historyUv.y) * step(historyUv.y, 1.0);
                float outOfBoundsBreak = 1.0 - inBounds;
                float motionPixels = length(velocityData.xy * _BurtTAATexelSize.zw);
                float movingObjectResponsive = trustedObjectMotion * smoothstep(0.75, 6.0, motionPixels);
                float geometryBreak = saturate(max(max(parallaxBreak, coverageBreak), outOfBoundsBreak) * surfaceWeight);
                float edgeResponsive = saturate(max(depthEdgeResponsive, velocityEdgeResponsive) * geometryBreak);
                float responsive = saturate(max(movingObjectResponsive, untrustedObjectMotion * saturate(_BurtTAAResponsiveParams.y)));
                responsive = max(responsive, edgeResponsive);
                float stencilResponsive = ((stencil & 16u) != 0u ? 1.0 : 0.0) * surfaceWeight * saturate(max(edgeResponsive, smoothstep(0.35, 2.50, motionPixels)));
                responsive = max(responsive, stencilResponsive);

                return float4(saturate(trustedObjectMotion), saturate(responsive), saturate(untrustedObjectMotion), saturate(geometryBreak));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Temporal AA Upscale"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BurtPostProcessSourceTexture;
            sampler2D _BurtTAAUpscaleCurrentTexture;
            sampler2D _BurtTAAMetadataTexture;
            sampler2D _BurtTAAParallaxRejectionTexture;
            sampler2D _BurtTAAVelocityTexture;
            float4 _BurtTAAUpscaleTexelSize;
            float4 _BurtTAAUpscaleParams;
            float4 _BurtTAAParams2;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                output.uv = uv;
                return output;
            }

            float3 BurtTaaUpscaleSampleResolved(float2 uv)
            {
                return max(tex2D(_BurtPostProcessSourceTexture, saturate(uv)).rgb, 0.0);
            }

            float3 BurtTaaUpscaleSampleCurrent(float2 uv)
            {
                return max(tex2D(_BurtTAAUpscaleCurrentTexture, saturate(uv)).rgb, 0.0);
            }

            float BurtTaaUpscaleLuma(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float BurtTaaUpscaleHdrWeight4(float3 color)
            {
                return rcp(dot(max(color, 0.0), float3(1.0, 2.0, 1.0)) + 4.0);
            }

            float BurtTaaUpscaleMitchell(float d)
            {
                d = abs(d);
                const float b = 1.0 / 3.0;
                const float c = 1.0 / 3.0;

                if (d < 1.0)
                {
                    return ((12.0 - 9.0 * b - 6.0 * c) * d * d * d + (-18.0 + 12.0 * b + 6.0 * c) * d * d + (6.0 - 2.0 * b)) / 6.0;
                }

                if (d < 2.0)
                {
                    return ((-b - 6.0 * c) * d * d * d + (6.0 * b + 30.0 * c) * d * d + (-12.0 * b - 48.0 * c) * d + (8.0 * b + 24.0 * c)) / 6.0;
                }

                return 0.0;
            }

            float3 BurtTaaUpscaleMitchellResolve(float2 uv, out float3 sourceMin, out float3 sourceMax)
            {
                float2 sourceTexel = _BurtTAAUpscaleTexelSize.xy;
                float2 sourceSize = max(_BurtTAAUpscaleTexelSize.zw, float2(1.0, 1.0));
                float2 historyPixelPos = uv * sourceSize;
                float2 topLeftKernel = floor(historyPixelPos - 1.5) + 0.5;
                float2 uvMin = 0.5 * sourceTexel;
                float2 uvMax = 1.0 - uvMin;
                float3 color = 0.0;
                float colorWeight = 0.0;
                sourceMin = float3(1e20, 1e20, 1e20);
                sourceMax = 0.0;

                [unroll]
                for (int y = 0; y < 4; y++)
                {
                    [unroll]
                    for (int x = 0; x < 4; x++)
                    {
                        float2 samplePixelPos = topLeftKernel + float2(x, y);
                        float2 sampleUv = clamp(samplePixelPos * sourceTexel, uvMin, uvMax);
                        float3 sampleColor = BurtTaaUpscaleSampleResolved(sampleUv);
                        float2 pixelOffset = abs(samplePixelPos - historyPixelPos);
                        float kernelWeight = BurtTaaUpscaleMitchell(pixelOffset.x) * BurtTaaUpscaleMitchell(pixelOffset.y);
                        float sampleWeight = kernelWeight * BurtTaaUpscaleHdrWeight4(sampleColor);
                        sourceMin = min(sourceMin, sampleColor);
                        sourceMax = max(sourceMax, sampleColor);
                        color += sampleColor * sampleWeight;
                        colorWeight += sampleWeight;
                    }
                }

                return clamp(max(color * rcp(max(colorWeight, 1e-4)), 0.0), sourceMin, sourceMax);
            }

            void BurtTaaUpscaleCurrentNeighborhood(float2 uv, out float3 currentCenter, out float3 currentMean, out float3 currentMin, out float3 currentMax, out float edge)
            {
                float2 currentTexel = rcp(max(_BurtTAAUpscaleParams.xy, float2(1.0, 1.0)));
                currentCenter = BurtTaaUpscaleSampleCurrent(uv);
                currentMean = 0.0;
                currentMin = currentCenter;
                currentMax = currentCenter;
                float centerLuma = BurtTaaUpscaleLuma(currentCenter);
                float maxLumaDelta = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float3 sampleColor = BurtTaaUpscaleSampleCurrent(uv + float2(x, y) * currentTexel);
                        currentMean += sampleColor;
                        currentMin = min(currentMin, sampleColor);
                        currentMax = max(currentMax, sampleColor);
                        maxLumaDelta = max(maxLumaDelta, abs(BurtTaaUpscaleLuma(sampleColor) - centerLuma));
                    }
                }

                currentMean *= 1.0 / 9.0;
                edge = saturate(maxLumaDelta / max(max(centerLuma, BurtTaaUpscaleLuma(currentMean)), 0.05));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 sourceTexel = _BurtTAAUpscaleTexelSize.xy;
                float3 sourceMin;
                float3 sourceMax;
                float3 mitchell = BurtTaaUpscaleMitchellResolve(uv, sourceMin, sourceMax);
                float3 center = BurtTaaUpscaleSampleResolved(uv);
                float3 left = BurtTaaUpscaleSampleResolved(uv - float2(sourceTexel.x, 0.0));
                float3 right = BurtTaaUpscaleSampleResolved(uv + float2(sourceTexel.x, 0.0));
                float3 down = BurtTaaUpscaleSampleResolved(uv - float2(0.0, sourceTexel.y));
                float3 up = BurtTaaUpscaleSampleResolved(uv + float2(0.0, sourceTexel.y));

                float3 laplacian = center * 4.0 - left - right - down - up;
                float sourceEdge = saturate(abs(BurtTaaUpscaleLuma(laplacian)) * 1.6);
                float upscaleStrength = saturate((max(_BurtTAAUpscaleParams.z, _BurtTAAUpscaleParams.w) - 1.0) * 0.95);

                float3 current;
                float3 currentMean;
                float3 currentMin;
                float3 currentMax;
                float currentEdge;
                BurtTaaUpscaleCurrentNeighborhood(uv, current, currentMean, currentMin, currentMax, currentEdge);

                float4 metadata = tex2D(_BurtTAAMetadataTexture, uv);
                float historyValidity = tex2D(_BurtTAAParallaxRejectionTexture, uv).r;
                float4 velocityData = tex2D(_BurtTAAVelocityTexture, uv);
                float motionPixels = length(velocityData.xy * _BurtTAAUpscaleTexelSize.zw);
                float geometryBreak = saturate(max(metadata.a, 1.0 - historyValidity));
                float responsive = saturate(max(metadata.g, metadata.b * 0.65));
                float motionResponsive = smoothstep(1.0, 8.0, motionPixels);
                float currentGuide = saturate(max(max(geometryBreak, responsive), max(currentEdge * 0.35, motionResponsive * 0.18)));

                float stableReconstruction = saturate((1.0 - geometryBreak) * (1.0 - responsive) * (1.0 - motionResponsive * 0.55));
                float3 currentHighFrequency = current - currentMean;
                float highFrequencyScale = saturate(0.14 + _BurtTAAParams2.x * 0.45) * upscaleStrength * stableReconstruction * (1.0 - sourceEdge * 0.55);
                float3 reconstructed = mitchell + currentHighFrequency * highFrequencyScale;

                float3 currentSpan = max(currentMax - currentMin, 0.0);
                float3 sourceSpan = max(sourceMax - sourceMin, 0.0);
                float3 clampPadding = max(currentSpan, sourceSpan) * lerp(0.05, 0.18, saturate(sourceEdge + currentEdge));
                float3 clampMin = min(currentMin, sourceMin) - clampPadding;
                float3 clampMax = max(currentMax, sourceMax) + clampPadding;
                reconstructed = clamp(reconstructed, clampMin, clampMax);

                float currentBlend = upscaleStrength * (geometryBreak * 0.34 + responsive * 0.20 + currentEdge * 0.04 + motionResponsive * 0.05);
                currentBlend = saturate(currentBlend * lerp(0.45, 1.0, currentGuide));
                float3 color = lerp(reconstructed, current, currentBlend);
                return float4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }


    }

    // 禁用 fallback，避免后处理拷贝失败时悄悄走其他管线 shader。
    Fallback Off
}
