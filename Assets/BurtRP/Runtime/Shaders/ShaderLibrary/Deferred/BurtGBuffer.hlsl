// BurtRP Deferred GBuffer 缂傚倷鐒﹂幏婵堢不閹达附鍋╁Δ锝呭暞閸ゆ帒顭跨捄渚剾闁衡偓閹惰姤鐓ユ繛鎴炵懆婢规鎲搁幎濠傛噳閸嬫挾鎲撮崟顓犲彎缂?shader 濠电偞鎸婚惃婊堝礋椤掍緡鍟€闂備胶顢婇鏍窗閺嶎偆鍗氬瀣凹濞岊亪鏌嶈閸撶喎鐣烽鍕殕闁逞屽墰濡叉劙顢旈崱妯烘瀭闂佹寧绻傚ú銊╁垂閹惰姤鐓ユ繛鎴灻〃娆戠磼濡も偓椤﹁京妲愰幒妤€绠ｆ繝闈涚墛濮?RenderTarget 闂備焦鐪归崹濠氬窗鎼淬劌绠犻柨鐔哄Т瀹告繈鏌曟繝蹇涙闁?#ifndef BURT_GBUFFER_INCLUDED // 闁诲孩顔栭崰鎺楀磻閹剧粯鐓?include guard闂備焦瀵х粙鎴︽儗閸屾锝囨嫚瀹割喗妗ㄩ梺闈涚箳婵敻宕愰崡鐐╂闁圭虎鍨版禍楣冩⒑?shader 缂傚倸鍊搁崐褰掓偋閻愮儤鍎婄憸鏂跨暦閿熺姴鍗抽柣鎰絻缁楁岸姊绘担鐟扮祷闁活厼鍊垮畷鎶藉川婵犲倻绐為悗骞垮劚閹虫劙寮虫导瀛樼叆?GBuffer 闁诲氦顫夐幃鍫曞磿闁秴鐭?#define BURT_GBUFFER_INCLUDED // 闂備礁鎼粔鏉懨洪埡鍜佹晩?BurtGBuffer.hlsl 闁诲氦顫夐悺鏇犱焊濞嗘垵鍨濋柨娑樺閸嬫牗銇勯幇鈺佺仼闁伙箑鐖奸弻娑橆潩閻撳簼鍑界紓浣筋嚙閸熸挳寮鍥︽勃闁兼亽鍎遍埀顒傛暩缁辨挻绗熼崶褍绫嶉梺闈╃稻閹倿寮?include 濠电偞娼欓崥瀣┍濞差亷缍栭柨鏃傚亾瀹曞鎮楅崷顓炐ョ紒鐘崇墵閺?
#ifndef BURT_GBUFFER_INCLUDED
#define BURT_GBUFFER_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtBRDF.hlsl" // 闁诲孩顔栭崰妤€煤閺嶎厼鐭?BurtSurfaceData闂備線娼уΛ鏃傛導婵狀椄 闂備礁鎲￠崹闈浳涘Δ鍚藉洭顢楅崒婊咃紲闂佽鍎抽顓㈠焵椤掑鐏﹂柡?XRender 濠碉紕鍋涢鍛偓娑掓櫊閹?reflectance/F0/roughness 闁诲氦顫夐幃鍫曞磿闁秴鐭楅柛褎顨嗛弲?
// Authoritative BurtRP deferred GBuffer layout contract, aligned to the XRender PC-style fixed slots.
// GBuffer0: normal payload. rgb = RGB888-packed normalWS, a = perceptual roughness. Backed by R8G8B8A8_UNorm.
// GBuffer1: base payload. rgb = baseColor, a = occlusion. Backed by R8G8B8A8_SRGB/UNorm.
// GBuffer2: property payload. r = packed(shadingModelID, material channel), g = metallic, b = smoothness, a = reflectance.
// GBuffer3: low-precision per-model custom payload. Every channel must be 0..1 and safe for R8G8B8A8_UNorm.
//   Default/ClearCoat: clearCoatNormal.xy, clearCoatMask, clearCoatRoughness.
//   Hair/Fur: primarySpecularColor.rgb, pack(secondaryRoughness, shadowFillStrength).
//   Subsurface: geometryNormal.xy, reserved, pack(power, ambient) or 3S curvature.
//   Fabric/Silk: fuzzColor.rgb, fuzzWeight.
//   Foliage/Grass: transmissionColor.rgb, transmissionWeight or grass transmissionWeight * 0.1.
//   Eye: irisNormal.xy, irisMask, reserved.
// GBuffer4: emission.rgb, reserved alpha. Backed by ARGBHalf.
// GBuffer5: higher-precision per-model custom payload. Direction/profile/anisotropy packing lives here until it is proven R8-safe.
//   Default/ClearCoat: tangent.xy, anisotropy, reserved.
//   Hair/Fur: secondarySpecularColor.rgb, pack(primaryShift, secondaryShift, backLight).
//   Subsurface: tangent.xy, pack(distortion, mode), pack(thickness, profileIndex).
//   Fabric/Silk: tangent.xy, anisotropy, pack(fuzzRoughness, isSilk).
//   Foliage/Grass: screenSpaceShadowIntensity, reserved, pack(backLight, transmissionNdotL), thickness.
//   Eye: causticNormal.xy, reserved, reserved.

// 濠电儑绲藉ú锔炬崲閸岀偞鍋?Deferred 闂佽崵鍠愰悷杈╁緤閸ф鍋夋繝濠傜墕鐟欙箓骞栨潏鍓хɑ闁伙綁浜堕弻鈩冨緞鐎ｎ亜顫戦悗鐟版啞缁诲牆顕ｉ锔藉亱闁割偅绻勯妶锕傛⒑閹稿海鈯曠紒瀣灴閹缚顦查柍缁樻崌濡啫鈽夊Ο濠氭暘闂佽崵鍠愬ú鏍涘☉妯?PBR shading 闂備礁婀遍。浠嬪磻閹剧粯鈷掗柛銉到娴滈箖姊哄Ч鍥у閻庢凹鍘奸妴鎺楀礈娴ｉ鍓ㄩ棅顐㈡处缁嬫帡鎳栭悩缁樼厱婵﹩鍓涙晶宕囩磼鏉堛劎绠炵€殿噮鍓熼弻鍛槈濮橈絾鏅欓梻浣告啞閼规儳霉閻戣姤鍋ㄥ┑鐘宠壘閸楁娊鏌熺粙鍧楊€楅幖鏉戯躬閺岋綁顢旈崱妤佺€婚梺?GBuffer RT
struct BurtGBufferData
{
    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濞炬櫅缁狙囨煙闁箑鐏犵紒鎰洴閺屾盯鍩￠崟顒変哗濡炪們鍨归幊姗€骞婂┑鍫熷珰鐟滃繒绮堟径鎰叆?Forward 闂?surfaceData.BaseColor.rgb 濠电儑绲藉ú锔炬崲閸愵亖鍋撻崹顐ｂ拹缂佸顦甸崺鈧い鎺戝閸?
    float3 BaseColor;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濠忓閳绘棃鏌″搴′簼婵炲懏娲滅槐鎺斾沪閻愵兙浠㈠┑鈽嗗亜濞尖€愁嚕椤掑嫭鍋￠柣妤€鐗嗛埀顒€顭烽弻銊モ槈濡偐鍔悶姘懇閺岋綁顢欓懡銈庢М闂佽鍠楅〃澶岀不濞戙垹宸濇繛锝庡厴閸嬫捁绠涘☉妯哄挤濡炪倖鐗楅懝鍓х矆鐎ｎ€㈠綊鏁愰崨顖呫垽鏌熼鐣岀煉闁哄苯鐭佺粻娑㈠Ψ瑜嶉懓绶縠fault Lit=normalWS闂備焦瀵х粙鎴︽偤濞屾r=strandDirectionWS
float3 NormalWS;

    float3 ClearCoatNormalWS;

    float3 TangentWS;

    float Anisotropy;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濞炬櫅缁狙囨煙闁箑鐏犵紒鎰洴濮婃椽顢曢敐鍛懙婵炲瓨绮撶粻鏍极瀹ュ憛褔篓閻滎枾ult Lit=metallic闂備焦瀵х粙鎴︽偤濞屾r=packed(scatter, shift)闂備線娼уΛ妤呭磹娴犲鏁婇柡宥庡幗閳锋帗銇勯弮鍥撴い蟻鍏犲綊宕楀ù搴邯瀹曘垹鈽夊▎鎰€撻柣鐘充航閸斿秹寮宠箛鎾闁圭偓鍓氶崕鏃€绻涢梻鏉戝祮闁诡垰鍟村畷鐔碱敋閸涱厽顓鹃梻?helper闂備焦瀵х粙鎴︽儗閸屾稑顕遍柍鍝勬噹缁€鍌溾偓骞垮劚濡粎绮氶崸妤佺厽?
    float Metallic;

    float MaterialChannel;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濞炬櫅缁€鍌氼熆鐠轰警鍎戦柟鏋姂楠炴牗娼忛幑鎰靛悈缂備浇椴稿鎻僽ffer 濠电儑绲藉ú锔炬崲閸曨垰姹查柍褜鍓熷濠氬醇濞戞浠惧┑鐘灪椤洭骞忛悩娲绘晣婵犻潧鐗忛弸鈧梻浣瑰缁嬫垿鎳熼鐐茬畺婵°倕鍟扮壕鍏笺亜閹捐泛啸闁糕晝濮撮埥澶愬箻椤栨矮澹曢梻?smoothness -> perceptual roughness
float Smoothness;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濞炬櫅缁犳盯鏌ｉ弮鍥仩闁告瑢鍋撶紓鍌欑贰閸犳銆冮崼銏″厹缂備焦蓱閸庣喖鏌ㄩ弮鍥т汗缂佲偓婢舵劖鍋℃繛鍡樼懅閹ジ鏌ｉ弽銊х煉鐎规洏鍎查幆鏃堟晲閸パ呯С闂備礁鎲￠〃鍛存偪閸ヮ剦鏁囬柛婵勫劤娑撳秵淇婇妶鍌氫壕闂佹悶鍊ら崣鍐嚕閸洖唯妞ゆ牗绮庨ˇ顕€姊洪崫鍕偓鍧楀焵椤掍胶銆掗柍?Debug 闂備胶鎳撻悺銊╂偋閺囥垹绠栨俊銈呮噺閺?shading 闂備胶鍎甸弲娑㈡偤閵娧勬殰閻庨潧鎲＄€氭岸鎮归崶銊ョ祷缂?
    float PerceptualRoughness;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋?XRender 濠碉紕鍋涢鍛偓娑掓櫊閹?reflectance闂備焦瀵х粙鎴︽儗閸屾稑顕遍柍鍝勬噹缁€鍌溾偓骞垮劚濡鍎?F0 闂備礁鎼Λ娆撳箰閸愯鑰块柛顐ｆ礀缁狅綁鏌熼柇锕€骞楃紓宥呯箻閹綊骞囬埡浣割潊缂備線顤傞崣鍐ㄧ暦濡ゅ懎唯闁挎洍鍋撻柣蹇旑殜閺岋綁鍩℃担鐑樻喖闁汇埄鍨遍幐鍐茬暦濡　鏋庨煫鍥ㄦ尭濮?GBuffer
float Reflectance;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濞炬櫆閸嬫繃銇勯弽銊ュ毈闁哄棙妫冨娲敆婢跺﹥鐏曢梺鍝勫€甸崑鎾绘⒑閹稿海鈽夐柣顒€銈稿顐﹀Χ閸℃瑯娲搁柟鍏肩暘閸斿秹鏁嶉悢鍏肩厵闁诡厽甯掗崝婊堟煛鐎ｂ晝绐旂€规洩绲介濂稿川椤栨侗鍟呴梻浣告啞缁矂鎯岄崒姣兼盯宕稿Δ鈧粻鎶芥煏婢跺棙娅囩憸閭﹀灦閺屾稖绠涢幙鍐┾枅婵炲鍘х€氫即骞?
    float Occlusion;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋ら柕濞炬櫆閸ゅ﹥銇勮箛鎾愁仼缂佸弶妞介弻娑滅疀閹垮啯鈻堝銈冨€撶欢姘跺箠濠靛牊瀚氱憸蹇涚嵁閵忕姭鏀介柍銉ュ暱閳ь剙缍婇幃妯诲緞閹邦剝袝闁硅壈鎻徊鍧楀极妤ｅ啯鈷掗柛銉到娴滈箖姊?HDR emission闂備焦瀵х粙鎴︽偤濡ゎ槢ffer4 闂?RT 闂備礁鎼粔鍫曞储瑜忓Σ鎰版晬閸曨剙鏆繛鎾村焹閸嬫捇鏌涘Ο绋库偓婵嗙暦閵夆晛閱囬柕澶堝灩娴滄儳顭跨捄渚剰妞?
    float3 Emission;

    // 濠电儑绲藉ú锔炬崲閸岀偞鍋?shading model id闂?=Default Lit闂?=Hair闂備線娼уΛ妤呭磹閹间焦鍋╅柣妯款嚙缁€鍐煕閹邦垰鐨洪柡?vector/material 濠电偞鍨堕幐鍫曞磹閹剧粯鐓傛繝濠傚缁剁偟鈧箍鍎卞ú锕傚汲韫囨柧绻嗛柤纰卞墯濞呮洘绻涢崼鐔风仼闁归濞€椤㈡ê鈹戦崼銏＄€?
    float ShadingModelID;

    float ClearCoatMask;

    float ClearCoatRoughness;

    float SubsurfaceThickness;

    float SubsurfacePower;

    float SubsurfaceDistortion;

    float SubsurfaceAmbient;

    float SubsurfaceScatteringMode;

    float Subsurface3SCurvature;

    float SubsurfaceProfileIndex;

    float3 SubsurfaceGeometryNormalWS;

    float HairSecondaryRoughness;

    float HairBackLight;

    float HairShadowFillStrength;

    float3 HairGeometryNormalWS;

    float HairSpecularShift;

    float HairSecondarySpecularShift;

    float3 HairSpecularColor;

    float3 HairSecondarySpecularColor;

    float FabricIsSilk;

    float FabricFuzzWeight;

    float FabricFuzzRoughness;

    float3 FabricFuzzColor;

    float3 FoliageTransmissionColor;

    float FoliageTransmissionWeight;

    float FoliageThickness;

    float FoliageBackLight;

    float FoliageTransmissionNdotL;

    float FoliageSpecularScale;

    float FoliageUseSpecularColor;

    float FoliageScreenSpaceShadowIntensity;

    float FoliageIsGrass;

    float EyeIrisMask;

    float3 EyeIrisNormalWS;

    float3 EyeCausticNormalWS;
};

// Stores the six GBuffer color payloads; RT creation/lifetime is handled by the C# render graph.
struct BurtEncodedGBuffer
{
    // GBuffer0: normal.rgb + perceptual roughness
float4 GBuffer0;

    // GBuffer1: baseColor.rgb + occlusion
float4 GBuffer1;

    // GBuffer2: packed(shadingModelID, material channel), metallic, smoothness, reflectance
float4 GBuffer2;

    float4 GBuffer3;

    float4 GBuffer4;

    float4 GBuffer5;
};

// Octahedron normal 缂傚倸鍊搁崐褰掓偋濠婂牊鍋夋繝濠傜墛閸庡秹鏌涢弴銊ュ妞ゎ偅鍨块弻娑樷枎閹存粳銏ゆ煕閵堝鎲剧€殿噮鍋嗛弫顕€顢欓幆褑鈧潡姊虹涵鍛【闁挎洏鍔戝畷鏇炍旈崨顓狀攨婵犵數濮寸€氼參宕愰弽顓熺厵闁肩绶遍鍔芥椽寮介妸褜娲搁悗鍏夊亾闁逞屽墮鑿愭い鏇楀亾闁绘侗鍣ｉ獮搴ㄦ嚍閵夛负鈧啴姊洪幐搴ｂ槈闁活厺鐒︽穱濠囶敇閵忊剝娅?GBuffer normal 闂傚倷绶￠崑鍛暆閸涘﹦顩?
float2 BurtWrapOctahedronNormal(float2 Value)
{
    // 闂備礁鎲＄敮鎺懳涘┑瀣棷闁惧繐婀辩粻鏃堟煥閺囨浜鹃悷婊勬緲濞尖€崇暦閿熺姴鍗抽柣妯烘▕娴犻箖鏌ｉ悩杈╃瓘缂佽鲸娲熼幃鐐節濮橆厽娅栭悗鍏夊亾闁告洦浜炵槐姘舵⒑?HLSL 闂備礁鎲＄喊宥夊垂閸ф闂柣鎴烆焽閳绘柨顭跨捄鐑樻拱缂佹劖顨婇幃璺衡槈閺嵮冾潊闂佽妞挎禍婊堫敋閿濆牏鐤€闁瑰墽顥愰崥顐ｇ箾閹寸偞灏紒澶嬫尦楠炲啴骞樼拠鑼缎曢柟鑹版彧缂嶅棙瀵奸崒姣懓鈹冮悩鍙夊櫤闁哄懏鎮傞弻娑滅疀閺囩偛浠樺銈嗘⒐閸旀瑩鐛埀顒傛喐閺傝娑㈡嚋绾板鐭?
    float2 SignNotZero = float2(Value.x >= 0.0f ? 1.0f : -1.0f, Value.y >= 0.0f ? 1.0f : -1.0f);

    // 闂備胶鍘ч崯鍧楁嚐椤栨氨鏆﹂柟鎵閸嬬娀鏌涢幇銊︽珔妞ゎ偅鍨块弻娑樷枎閹存粳銏ゆ煕濞嗘劖绀€妞ゆ洩缍€缁犳盯濡疯閻涖儵姊?1 - abs(yx) 濠电儑绲藉ú锔炬崲閸曨垰姹查柍褜鍓熷鍫曞醇濠靛洩纭€闂佸搫鎷嬮崑鍛焽婵犳碍鍊烽柣銏犵仛閺嗕即姊虹拠鈥冲姱鐎广儱娲ㄩˇ顕€姊洪崨濠傚缂佸纾划缁樼節濮橆剛鍊為梺缁樺姈瑜板啴寮閳规垿顢欑喊鍗炲壋濡炪倖鍨崇欢姘暦?
    return (1.0f - abs(Value.yx)) * SignNotZero;
}

// Pack a world-space unit vector into two 0..1 channels; BurtRP stores it as RGB888 in GBuffer0.rgb.
float2 BurtEncodeNormalWSForGBuffer(float3 NormalWS)
{
    // 闂備胶顭堢换鎰版偋閸℃稒鍋╅柕濞炬櫅缁€鍌炴煏婵炲灝鍔ょ紒澶婃惈閳藉骞橀姘闂備礁鎲￠悧妤呮偋椤撶姵顫曟繝闈涱儐閻掑ジ鏌涢…鎴濇灈閻㈩垵娅曠换娑欏緞鐎ｎ偆顦梺鎼炲€曞ù鐑藉箯瑜版帒绾ч柟瀵稿仧椤╊參姊洪悷鎵憼闁绘顨夐妵鎰板礃椤旇偐锛欓梺鐓庣秺閸嬪﹪宕甸弽顐熷亾閻熸澘顥忛柛鐘崇墬缁轰粙寮介鐔告?octahedron 闂備胶顢婇崺鏍洪弽銊ч檮?
    float3 N = BurtSafeNormalize(NormalWS);

    // 闂備胶顢婇崺鏍洪弽銊ч檮闁哄稁鍘介弲?L1 闂備礁鎲￠〃鍡椕洪幋鐘电闁告稒娼欑粈鍌涖亜閹板爼妾俊妞煎妼闇夐柨婵嗘閹牏绱掗柆宥勬喚鐎规洘宀稿畷鍗炍熼崗澶规捇姊虹粙娆惧剱闁活収鍠氶幑銏狀潩鐠鸿櫣顔呴梺闈涚墕缁绘帞绮堟径鎰拺妞ゆ巻鍋撻柣蹇旂箞瀹曘垽濡堕崪浣告闂佺鏈銊╁春濡ゅ懏鈷掗柛鈩冭壘閻撴劗鐥鐐差暢闁汇儺浜濆鍕箹娴ｅ搫绀堥梻?NaN
    float InvL1 = rcp(max(abs(N.x) + abs(N.y) + abs(N.z), BURT_EPSILON));
    float2 Encoded = N.xy * InvL1;

    // 闂備胶鍘ч崯鍧楁嚐椤栨氨鏆﹂柟鎵閸嬬娀鏌涢幇闈涙灍濞寸姵锕㈤幃鐑藉即濮樺崬濡芥繝娈垮枛閻倸鐣烽敐澶嬪亹闁告瑥顦遍ˇ銉︾箾鐎涙鐭婇柣顒傚帶鑿愭い鏇楀亾闁绘侗鍣ｉ獮搴ㄦ嚍閵夛负鈧啴姊洪幐搴ｂ槈濠靛倹姊婚幑銏狀潩鏉堛劌鐝版繛杈剧悼閺屽鍩涢崼婵冩闁瑰灝鍟╅柇顖炴煃瑜滈崜姘暆閸涘﹦顩插Δ锝呭暞閸ゅ嫰鏌ら幁鎺戝姍闁告帗甯掗…璺ㄦ崉閾忓墣銏ゆ煟閿濆骸澧寸€殿噮鍋嗛埀顒佺⊕钃遍柣鎾寸懇閺?
    if (N.z < 0.0f)
    {
        Encoded = BurtWrapOctahedronNormal(Encoded);
    }

    // 闂?[-1, 1] 闂備礁鎼€氼喗鎱ㄩ幘顔藉剭闁绘绮弲?[0, 1]闂備焦瀵х粙鎴﹀嫉椤掑嫬钃熼柣鏂挎憸閻熷綊鏌涢…鎴濇灈闁哄懏鎮傞弻娑滅疀鐎ｎ亜濮庨梺缁樼墪婢у酣骞夐幘顔肩妞ゆ牭绲惧鎺楁⒑?RT
    return Encoded * 0.5f + 0.5f;
}

// Decode the octahedral unit vector used by the GBuffer normal/custom direction payloads.
float3 BurtDecodeNormalWSFromGBuffer(float2 EncodedNormal)
{
    // 闂?[0, 1] 闂佸搫顦弲婊堟晪闁汇埄鍨奸崑濠囧极?octahedron 婵°倗濮烽崑鐐寸箾婵犲偆鐒介柣妤€鐗忛埢鏂库攽閻樻彃顏柣?[-1, 1]
    float2 F = EncodedNormal * 2.0f - 1.0f;

    // 闂備胶顭堢换鎰版偋婵犲嫧鍋撻崹顐ょ煉鐎规洘绮岄濂稿川椤撶媴绱梻浣藉吹閸嬫盯宕幘顔奸棷闁归棿鐒﹂弲?z闂備焦瀵х粙鎴︽嚐椤栫偛鐤柍褜鍓熷娲敃閿濆嫪瀛╃紓浣筋嚙閸燁垳绮欐径灞稿亾閿濆骸鏋熸俊妞煎姂閺岋綁濡搁妷銉还闂侀€涚┒閸斿矂锝為姀銈嗘櫜闁告粈鐒﹁ⅸ闂備浇宕甸崑娑樜涘鍫濈？婵°倕鎳庣涵鈧繝鐢靛Т鐎氼參宕愰弽顓熺厵闁肩绶遍鍥ヤ汗?
    float3 N = float3(F.x, F.y, 1.0f - abs(F.x) - abs(F.y));

    // Z 濠电偞鍨堕幑鍥╃磽濮樿京鐭嗛悗锝庡墯閸嬫﹢鏌曟繛褌妞掔划鐢告⒑閸濆嫷妲堕柛搴㈡尦瀹曢潧顭ㄩ崼婵堫唴濠碘槅鍨伴幖顐ょ礊瀹€鍕厱闁哄诞鍕創闂佺粯鎸婚悷鈺呭极瀹ュ懐鏆嗛柛鏇ㄥ厸缁垶鏌ｉ悢鍛婄；闁绘帪闄勬穱?xy 婵犵數鍋涢惌浣糕枖閺囥埄鏁勫〒姘ｅ亾鐎规洩缍侀弻銊р偓锝庡亞閸樼敻姊洪崨濠勵暡闁搞劌澧庨弫顔裤亹閹烘垹鍊為梺缁樺姈瑜板啰绮?
    float T = saturate(-N.z);
    N.x += N.x >= 0.0f ? -T : T;
    N.y += N.y >= 0.0f ? -T : T;

    // 闂備礁鎼悧鍐磻閹剧粯鐓曟慨姗嗗墴椤庢鏌ｉ敐鍐ㄥ鐎规洘顨婃俊鎼佸Ψ瑜忔濠电偞鍨堕幐鎾磻閹剧粯鐓曢柡宥冨妿婢瑰嫮绱掓潏銊х疄妤犵偛閰ｅ畷褰掝敃閵?RT 闂傚倷鐒﹁ぐ鍐崲閹邦喒鍋撻崹顐ゅ弨鐎规洦浜炴禒锕傚箚瑜忕粊鐑芥⒑缁嬪尅鍔熼柛妯犲洦鍋ㄦい鎰剁畱缁狙囨煏婢诡垰瀚弳鐘绘⒒閸屾浜鹃梺鍓茬厛閸犳牠顢欐繝鍥ㄥ仯闁搞儴娉涢悘鐘充繆?
    return BurtSafeNormalize(N);
}

uint3 BurtPackFloat2To888UInt(float2 Value)
{
    uint2 Quantized = (uint2)(saturate(Value) * 4095.5f);
    uint2 Hi = Quantized >> 8;
    uint2 Lo = Quantized & 255u;
    return uint3(Lo, Hi.x | (Hi.y << 4));
}

float3 BurtPackFloat2To888(float2 Value)
{
    return (float3)BurtPackFloat2To888UInt(Value) / 255.0f;
}

float2 BurtUnpack888UIntToFloat2(uint3 Value)
{
    uint Hi = Value.z >> 4;
    uint Lo = Value.z & 15u;
    uint2 Packed = Value.xy | uint2(Lo << 8, Hi << 8);
    return (float2)Packed / 4095.0f;
}

float2 BurtUnpack888ToFloat2(float3 Value)
{
    uint3 Quantized = (uint3)(saturate(Value) * 255.5f);
    return BurtUnpack888UIntToFloat2(Quantized);
}

float3 BurtEncodeNormalWS888ForGBuffer(float3 NormalWS)
{
    float3 N = BurtSafeNormalize(NormalWS);
    float Z = max(abs(N.z), 1.0f / 1024.0f);
    N.z = N.z < 0.0f ? -Z : Z;
    return BurtPackFloat2To888(BurtEncodeNormalWSForGBuffer(N));
}

float3 BurtDecodeNormalWS888FromGBuffer(float3 EncodedNormal)
{
    return BurtDecodeNormalWSFromGBuffer(BurtUnpack888ToFloat2(EncodedNormal));
}

// Deferred lighting uses XRender-style high stencil bits for the authoritative
// shading model id. GBuffer2.r keeps this compatibility pack for fullscreen
// consumers that still need to branch per pixel.
// Keep each model bucket away from both edges: Fabric/Silk at metallic=0
// otherwise lands on the 4/5 boundary, and half/UNorm RT quantization can
// decode it as the previous shading model.
float BurtEncodeMetallicAndShadingModelForGBuffer(float MetallicOrScatter, float ShadingModelID)
{
    float ModelID = clamp(BurtResolveSurfaceShadingModel(ShadingModelID), 0.0f, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - 1.0f);
    float Material = BURT_GBUFFER_SHADING_MODEL_PACK_BIAS + saturate(MetallicOrScatter) * BURT_GBUFFER_SHADING_MODEL_PACK_SCALE;
    return (ModelID + Material) / BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
}

float BurtDecodeMetallicAndShadingModelFromGBuffer(float PackedValue, out float ShadingModelID)
{
    float Scaled = saturate(PackedValue) * BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
    ShadingModelID = floor(min(Scaled, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - BURT_EPSILON));
    return saturate((Scaled - ShadingModelID - BURT_GBUFFER_SHADING_MODEL_PACK_BIAS) / BURT_GBUFFER_SHADING_MODEL_PACK_SCALE);
}

#define BURT_HAIR_SCATTER_PACK_DIMENSION (32.0f)
#define BURT_HAIR_SHIFT_PACK_DIMENSION (16.0f)
#define BURT_HAIR_SCATTER_PACK_MAX_BUCKET (BURT_HAIR_SCATTER_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_SHIFT_PACK_MAX_BUCKET (BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_MATERIAL_PACK_MAX_VALUE (BURT_HAIR_SCATTER_PACK_DIMENSION * BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_CONTROL_PACK_DIMENSION (64.0f)
#define BURT_HAIR_CONTROL_PACK_MAX_BUCKET (BURT_HAIR_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_CONTROL_PACK_MAX_VALUE (BURT_HAIR_CONTROL_PACK_DIMENSION * BURT_HAIR_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION (16.0f)
#define BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN (-2.60f)
#define BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX (5.32f)
#define BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN (-5.10f)
#define BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX (8.22f)
float BurtQuantizeHairMaterialValue(float Value, float MaxBucket)
{
    return floor(saturate(Value) * MaxBucket + 0.5f);
}

float BurtEncodeHairMaterialChannel(float HairScatter, float HairShiftScale)
{
    // Hair only has one Material scalar inside GBuffer2.r; pack scatter and the longitudinal lobe shift scale together.
    float ScatterBucket = BurtQuantizeHairMaterialValue(HairScatter, BURT_HAIR_SCATTER_PACK_MAX_BUCKET);
    float ShiftBucket = BurtQuantizeHairMaterialValue(HairShiftScale, BURT_HAIR_SHIFT_PACK_MAX_BUCKET);
    return (ShiftBucket * BURT_HAIR_SCATTER_PACK_DIMENSION + ScatterBucket) / BURT_HAIR_MATERIAL_PACK_MAX_VALUE;
}

void BurtDecodeHairMaterialChannel(float PackedHairMaterial, out float HairScatter, out float HairShiftScale)
{
    float PackedBucket = floor(saturate(PackedHairMaterial) * BURT_HAIR_MATERIAL_PACK_MAX_VALUE + 0.5f);
    float ShiftBucket = floor(PackedBucket / BURT_HAIR_SCATTER_PACK_DIMENSION);
    float ScatterBucket = PackedBucket - ShiftBucket * BURT_HAIR_SCATTER_PACK_DIMENSION;

    HairScatter = saturate(ScatterBucket / BURT_HAIR_SCATTER_PACK_MAX_BUCKET);
    HairShiftScale = saturate(ShiftBucket / BURT_HAIR_SHIFT_PACK_MAX_BUCKET);
}

float BurtEncodeHairRoughnessFillForGBuffer(float SecondaryRoughness, float ShadowFillStrength)
{
    float RoughnessBucket = BurtQuantizeHairMaterialValue(SecondaryRoughness, BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    float FillBucket = BurtQuantizeHairMaterialValue(ShadowFillStrength, BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    return (FillBucket * BURT_HAIR_CONTROL_PACK_DIMENSION + RoughnessBucket) / BURT_HAIR_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeHairRoughnessFillFromGBuffer(float PackedValue, out float SecondaryRoughness, out float ShadowFillStrength)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_HAIR_CONTROL_PACK_MAX_VALUE + 0.5f);
    float FillBucket = floor(PackedBucket / BURT_HAIR_CONTROL_PACK_DIMENSION);
    float RoughnessBucket = PackedBucket - FillBucket * BURT_HAIR_CONTROL_PACK_DIMENSION;
    SecondaryRoughness = saturate(RoughnessBucket / BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    ShadowFillStrength = saturate(FillBucket / BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeHairShiftBackLightForGBuffer(float SpecularShift, float SecondarySpecularShift, float BackLight)
{
    float PrimaryBucket = floor(saturate((SpecularShift - BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN) / max(BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX - BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_EPSILON)) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET + 0.5f);
    float SecondaryBucket = floor(saturate((SecondarySpecularShift - BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN) / max(BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX - BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_EPSILON)) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET + 0.5f);
    float BackLightBucket = BurtQuantizeHairMaterialValue(BackLight, BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    return (BackLightBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION + SecondaryBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION + PrimaryBucket) / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeHairShiftBackLightFromGBuffer(float PackedValue, out float SpecularShift, out float SecondarySpecularShift, out float BackLight)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE + 0.5f);
    float BackLightBucket = floor(PackedBucket / (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION));
    float RemainingBucket = PackedBucket - BackLightBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION;
    float SecondaryBucket = floor(RemainingBucket / BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION);
    float PrimaryBucket = RemainingBucket - SecondaryBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION;
    SpecularShift = lerp(BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX, PrimaryBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    SecondarySpecularShift = lerp(BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX, SecondaryBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    BackLight = saturate(BackLightBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeSubsurfacePowerForGBuffer(float Power)
{
    return saturate((BurtClampSubsurfacePower(Power) - BURT_SUBSURFACE_POWER_MIN) / max(BURT_SUBSURFACE_POWER_MAX - BURT_SUBSURFACE_POWER_MIN, BURT_EPSILON));
}

float BurtDecodeSubsurfacePowerFromGBuffer(float EncodedPower)
{
    return BurtClampSubsurfacePower(lerp(BURT_SUBSURFACE_POWER_MIN, BURT_SUBSURFACE_POWER_MAX, saturate(EncodedPower)));
}

#define BURT_SUBSURFACE_CONTROL_PACK_DIMENSION (32.0f)
#define BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET (BURT_SUBSURFACE_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE (BURT_SUBSURFACE_CONTROL_PACK_DIMENSION * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION (64.0f)
#define BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET (BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_PROFILE_PACK_DIMENSION (BURT_SUBSURFACE_PROFILE_COUNT)
#define BURT_SUBSURFACE_PROFILE_PACK_MAX_BUCKET (BURT_SUBSURFACE_PROFILE_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE (BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION * BURT_SUBSURFACE_PROFILE_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT (3.0f)
#define BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE (0.999f)
float BurtQuantizeSubsurfaceControlValue(float Value)
{
    return floor(saturate(Value) * BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET + 0.5f);
}

float BurtEncodeSubsurfacePowerAmbientForGBuffer(float Power, float Ambient)
{
    float PowerBucket = BurtQuantizeSubsurfaceControlValue(BurtEncodeSubsurfacePowerForGBuffer(Power));
    float AmbientBucket = BurtQuantizeSubsurfaceControlValue(Ambient);
    return (AmbientBucket * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION + PowerBucket) / BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeSubsurfacePowerAmbientFromGBuffer(float PackedControl, out float Power, out float Ambient)
{
    float PackedBucket = floor(saturate(PackedControl) * BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE + 0.5f);
    float AmbientBucket = floor(PackedBucket / BURT_SUBSURFACE_CONTROL_PACK_DIMENSION);
    float PowerBucket = PackedBucket - AmbientBucket * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION;
    Power = BurtDecodeSubsurfacePowerFromGBuffer(PowerBucket / BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET);
    Ambient = saturate(AmbientBucket / BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeSubsurfaceThicknessProfileForGBuffer(float Thickness, float ProfileIndex)
{
    float ThicknessBucket = floor(saturate(Thickness) * BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET + 0.5f);
    float ProfileBucket = BurtClampSubsurfaceProfileIndex(ProfileIndex);
    return (ProfileBucket * BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION + ThicknessBucket) / BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE;
}

void BurtDecodeSubsurfaceThicknessProfileFromGBuffer(float PackedValue, out float Thickness, out float ProfileIndex)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE + 0.5f);
    float ProfileBucket = floor(PackedBucket / BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION);
    float ThicknessBucket = PackedBucket - ProfileBucket * BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION;
    Thickness = saturate(ThicknessBucket / BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET);
    ProfileIndex = BurtClampSubsurfaceProfileIndex(ProfileBucket);
}

float BurtEncodeSubsurfaceDistortionModeForGBuffer(float Distortion, float ScatteringMode)
{
    float Mode = BurtClampSubsurfaceScatteringMode(ScatteringMode);
    return (Mode + saturate(Distortion) * BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE) / BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT;
}

void BurtDecodeSubsurfaceDistortionModeFromGBuffer(float PackedValue, out float Distortion, out float ScatteringMode)
{
    float Scaled = saturate(PackedValue) * BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT;
    ScatteringMode = floor(min(Scaled, BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT - BURT_EPSILON));
    Distortion = saturate((Scaled - ScatteringMode) / BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE);
    ScatteringMode = BurtClampSubsurfaceScatteringMode(ScatteringMode);
}

#define BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE (0.499f)
float BurtEncodeFabricRoughnessSilkForGBuffer(float FuzzRoughness, float IsSilk)
{
    float PackedRoughness = saturate(FuzzRoughness) * BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE;
    return IsSilk > 0.5f ? 0.5f + PackedRoughness : PackedRoughness;
}

void BurtDecodeFabricRoughnessSilkFromGBuffer(float PackedValue, out float FuzzRoughness, out float IsSilk)
{
    float Packed = saturate(PackedValue);
    IsSilk = Packed >= 0.5f ? 1.0f : 0.0f;
    float LocalRoughness = IsSilk > 0.5f ? Packed - 0.5f : Packed;
    FuzzRoughness = ClampPerceptualRoughness(LocalRoughness / BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE);
}

#define BURT_FOLIAGE_SPECULAR_PACK_SCALE (0.499f)
float BurtEncodeFoliageSpecularTypeForGBuffer(float SpecularScale, float UseSpecularColor)
{
    float PackedSpecular = saturate(SpecularScale) * BURT_FOLIAGE_SPECULAR_PACK_SCALE;
    return UseSpecularColor > 0.5f ? 0.5f + PackedSpecular : PackedSpecular;
}

void BurtDecodeFoliageSpecularTypeFromGBuffer(float PackedValue, out float SpecularScale, out float UseSpecularColor)
{
    float Packed = saturate(PackedValue);
    UseSpecularColor = Packed >= 0.5f ? 1.0f : 0.0f;
    float LocalSpecular = UseSpecularColor > 0.5f ? Packed - 0.5f : Packed;
    SpecularScale = saturate(LocalSpecular / BURT_FOLIAGE_SPECULAR_PACK_SCALE);
}

#define BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION (32.0f)
#define BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET (BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION - 1.0f)
#define BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE (BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION - 1.0f)
float BurtEncodeFoliageBackLightNdotLForGBuffer(float BackLight, float TransmissionNdotL)
{
    float BackLightBucket = floor(saturate(BackLight) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET + 0.5f);
    float NdotLBucket = floor(saturate(TransmissionNdotL) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET + 0.5f);
    return (NdotLBucket * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION + BackLightBucket) / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE;
}

void BurtDecodeFoliageBackLightNdotLFromGBuffer(float PackedValue, out float BackLight, out float TransmissionNdotL)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE + 0.5f);
    float NdotLBucket = floor(PackedBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION);
    float BackLightBucket = PackedBucket - NdotLBucket * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION;
    BackLight = saturate(BackLightBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET);
    TransmissionNdotL = saturate(NdotLBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET);
}

// Creates semantic GBuffer Data from Material inputs. Hair passes use NormalWS as the stored strand direction.
BurtGBufferData BurtCreateGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    BurtGBufferData Data;

    Data.BaseColor = SurfaceData.BaseColor.rgb;

    // Vector slot stores NormalWS for Lit/ClearCoat/Subsurface and strand direction for Hair.
    Data.NormalWS = BurtSafeNormalize(NormalWS);
    Data.ClearCoatNormalWS = Data.NormalWS;
    Data.TangentWS = BurtOrthonormalizeTangentWS(Data.NormalWS, TangentWS.xyz);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Anisotropy = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    Data.Anisotropy = (BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID) || BurtIsActiveFoliageShadingModel(SurfaceData.ShadingModelID) || BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID)) ? 0.0f : clamp(SurfaceData.Anisotropy, -1.0f, 1.0f);
#else
    Data.Anisotropy = clamp(SurfaceData.Anisotropy, -1.0f, 1.0f);
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Metallic = 0.0f;
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    Data.MaterialChannel = BurtEncodeFoliageSpecularTypeForGBuffer(SurfaceData.FoliageSpecularScale, SurfaceData.FoliageUseSpecularColor);
#elif BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.MaterialChannel = 1.0f;
#else
    Data.MaterialChannel = 0.0f;
#endif
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID) || BurtIsActiveFoliageShadingModel(SurfaceData.ShadingModelID) || BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID))
    {
        Data.Metallic = 0.0f;
        Data.MaterialChannel = BurtIsActiveFoliageShadingModel(SurfaceData.ShadingModelID)
            ? BurtEncodeFoliageSpecularTypeForGBuffer(SurfaceData.FoliageSpecularScale, SurfaceData.FoliageUseSpecularColor)
            : (BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID) ? 1.0f : 0.0f);
    }
    else
    {
        Data.Metallic = saturate(SurfaceData.Metallic);
        Data.MaterialChannel = Data.Metallic;
    }
#else
    Data.Metallic = saturate(SurfaceData.Metallic);
    Data.MaterialChannel = Data.Metallic;
#endif
    Data.Smoothness = saturate(SurfaceData.Smoothness);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    Data.Reflectance = BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : saturate(SurfaceData.Reflectance);
#else
    Data.Reflectance = saturate(SurfaceData.Reflectance);
#endif
    Data.Occlusion = saturate(SurfaceData.Occlusion);

    Data.PerceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(Data.Smoothness));

    Data.Emission = max(Emission, float3(0.0f, 0.0f, 0.0f));

    Data.ShadingModelID = BurtResolveSurfaceShadingModel(SurfaceData.ShadingModelID);
    Data.ClearCoatMask = 0.0f;
    Data.ClearCoatRoughness = 0.2f;
    Data.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    Data.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    Data.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    Data.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    Data.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    Data.SubsurfaceGeometryNormalWS = Data.NormalWS;
    Data.HairSecondaryRoughness = 0.5f;
    Data.HairBackLight = 0.0f;
    Data.HairShadowFillStrength = 0.0f;
    Data.HairGeometryNormalWS = Data.NormalWS;
    Data.HairSpecularShift = 0.0f;
    Data.HairSecondarySpecularShift = 0.0f;
    Data.HairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    Data.HairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    Data.FabricIsSilk = 0.0f;
    Data.FabricFuzzWeight = 0.0f;
    Data.FabricFuzzRoughness = 0.75f;
    Data.FabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
    Data.FoliageTransmissionColor = float3(0.55f, 0.85f, 0.35f);
    Data.FoliageTransmissionWeight = 0.0f;
    Data.FoliageThickness = 0.5f;
    Data.FoliageBackLight = 0.5f;
    Data.FoliageTransmissionNdotL = 0.5f;
    Data.FoliageSpecularScale = 1.0f;
    Data.FoliageUseSpecularColor = 0.0f;
    Data.FoliageScreenSpaceShadowIntensity = 0.0f;
    Data.FoliageIsGrass = 0.0f;
    Data.EyeIrisMask = 0.0f;
    Data.EyeIrisNormalWS = Data.NormalWS;
    Data.EyeCausticNormalWS = Data.NormalWS;

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    Data.ClearCoatMask = saturate(SurfaceData.ClearCoatMask);
    Data.ClearCoatRoughness = ClampPerceptualRoughness(SurfaceData.ClearCoatRoughness);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        Data.ClearCoatMask = saturate(SurfaceData.ClearCoatMask);
        Data.ClearCoatRoughness = ClampPerceptualRoughness(SurfaceData.ClearCoatRoughness);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.SubsurfaceThickness = saturate(SurfaceData.SubsurfaceThickness);
    Data.SubsurfacePower = BurtClampSubsurfacePower(SurfaceData.SubsurfacePower);
    Data.SubsurfaceDistortion = saturate(SurfaceData.SubsurfaceDistortion);
    Data.SubsurfaceAmbient = saturate(SurfaceData.SubsurfaceAmbient);
    Data.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(SurfaceData.SubsurfaceScatteringMode);
    Data.Subsurface3SCurvature = saturate(SurfaceData.Subsurface3SCurvature);
    Data.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(SurfaceData.SubsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        Data.SubsurfaceThickness = saturate(SurfaceData.SubsurfaceThickness);
        Data.SubsurfacePower = BurtClampSubsurfacePower(SurfaceData.SubsurfacePower);
        Data.SubsurfaceDistortion = saturate(SurfaceData.SubsurfaceDistortion);
        Data.SubsurfaceAmbient = saturate(SurfaceData.SubsurfaceAmbient);
        Data.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(SurfaceData.SubsurfaceScatteringMode);
        Data.Subsurface3SCurvature = saturate(SurfaceData.Subsurface3SCurvature);
        Data.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(SurfaceData.SubsurfaceProfileIndex);
    }
#endif

#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    Data.FabricIsSilk = saturate(SurfaceData.FabricIsSilk);
    Data.FabricFuzzWeight = saturate(SurfaceData.FabricFuzzWeight);
    Data.FabricFuzzRoughness = ClampPerceptualRoughness(SurfaceData.FabricFuzzRoughness);
    Data.FabricFuzzColor = max(SurfaceData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        Data.FabricIsSilk = saturate(SurfaceData.FabricIsSilk);
        Data.FabricFuzzWeight = saturate(SurfaceData.FabricFuzzWeight);
        Data.FabricFuzzRoughness = ClampPerceptualRoughness(SurfaceData.FabricFuzzRoughness);
        Data.FabricFuzzColor = max(SurfaceData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    Data.FoliageTransmissionColor = max(SurfaceData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
    Data.FoliageTransmissionWeight = SurfaceData.FoliageIsGrass > 0.5f
        ? max(SurfaceData.FoliageTransmissionWeight, 0.0f)
        : saturate(SurfaceData.FoliageTransmissionWeight);
    Data.FoliageThickness = saturate(SurfaceData.FoliageThickness);
    Data.FoliageBackLight = saturate(SurfaceData.FoliageBackLight);
    Data.FoliageTransmissionNdotL = saturate(SurfaceData.FoliageTransmissionNdotL);
    Data.FoliageSpecularScale = saturate(SurfaceData.FoliageSpecularScale);
    Data.FoliageUseSpecularColor = saturate(SurfaceData.FoliageUseSpecularColor);
    Data.FoliageScreenSpaceShadowIntensity = max(SurfaceData.FoliageScreenSpaceShadowIntensity, 0.0f);
    Data.FoliageIsGrass = saturate(SurfaceData.FoliageIsGrass);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        Data.FoliageTransmissionColor = max(SurfaceData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
        Data.FoliageTransmissionWeight = SurfaceData.FoliageIsGrass > 0.5f
            ? max(SurfaceData.FoliageTransmissionWeight, 0.0f)
            : saturate(SurfaceData.FoliageTransmissionWeight);
        Data.FoliageThickness = saturate(SurfaceData.FoliageThickness);
        Data.FoliageBackLight = saturate(SurfaceData.FoliageBackLight);
        Data.FoliageTransmissionNdotL = saturate(SurfaceData.FoliageTransmissionNdotL);
        Data.FoliageSpecularScale = saturate(SurfaceData.FoliageSpecularScale);
        Data.FoliageUseSpecularColor = saturate(SurfaceData.FoliageUseSpecularColor);
        Data.FoliageScreenSpaceShadowIntensity = max(SurfaceData.FoliageScreenSpaceShadowIntensity, 0.0f);
        Data.FoliageIsGrass = saturate(SurfaceData.FoliageIsGrass);
    }
#endif

#if BURT_ACTIVE_EYE_SHADING_MODEL
    Data.EyeIrisMask = saturate(SurfaceData.EyeIrisMask);
    Data.EyeIrisNormalWS = BurtSafeNormalize(SurfaceData.EyeIrisNormalWS);
    Data.EyeCausticNormalWS = BurtSafeNormalize(SurfaceData.EyeCausticNormalWS);
#elif BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data.EyeIrisMask = saturate(SurfaceData.EyeIrisMask);
        Data.EyeIrisNormalWS = BurtSafeNormalize(SurfaceData.EyeIrisNormalWS);
        Data.EyeCausticNormalWS = BurtSafeNormalize(SurfaceData.EyeCausticNormalWS);
    }
#endif

    return Data;
}

// Hair GBuffer keeps one scalar Material channel: Packed(scatter, lobe shift scale).
BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData SurfaceData, float HairScatter, float HairShiftScale)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_HAIR;
    SurfaceData.Metallic = BurtEncodeHairMaterialChannel(HairScatter, HairShiftScale);
    return SurfaceData;
}

BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData SurfaceData, float HairScatter)
{
    return BurtApplyHairGBufferSurfaceSemantics(SurfaceData, HairScatter, 1.0f);
}

BurtSurfaceData BurtApplyFurGBufferSurfaceSemantics(BurtSurfaceData SurfaceData)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FUR;
    SurfaceData.Metallic = 0.0f;
    return SurfaceData;
}

BurtSurfaceData BurtApplyEyeGBufferSurfaceSemantics(BurtSurfaceData SurfaceData)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_EYE;
    SurfaceData.Metallic = 0.0f;
    SurfaceData.Anisotropy = 0.0f;
    return SurfaceData;
}

BurtGBufferData BurtCreateGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 Emission)
{
    float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
    return BurtCreateGBufferData(SurfaceData, SafeNormalWS, float4(BurtCreateFallbackTangentWS(SafeNormalWS), 1.0f), Emission);
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData SurfaceData, float3 StrandDirectionWS, float3 HairNormalWS, float3 HairGeometryNormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_HAIR;
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, StrandDirectionWS, Emission);
    Data.ClearCoatNormalWS = BurtSafeNormalize(HairNormalWS);
    Data.HairGeometryNormalWS = BurtSafeNormalize(HairGeometryNormalWS);
    Data.HairSecondaryRoughness = ClampPerceptualRoughness(SurfaceData.HairSecondaryRoughness);
    Data.HairBackLight = saturate(SurfaceData.HairBackLight);
    Data.HairShadowFillStrength = saturate(SurfaceData.HairShadowFillStrength);
    Data.HairSpecularShift = clamp(SurfaceData.HairSpecularShift, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX);
    Data.HairSecondarySpecularShift = clamp(SurfaceData.HairSecondarySpecularShift, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX);
    Data.HairSpecularColor = max(SurfaceData.HairSpecularColor, float3(0.0f, 0.0f, 0.0f));
    Data.HairSecondarySpecularColor = max(SurfaceData.HairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f));
    return Data;
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData SurfaceData, float3 StrandDirectionWS, float3 Emission)
{
    return BurtCreateHairGBufferData(SurfaceData, StrandDirectionWS, StrandDirectionWS, StrandDirectionWS, Emission);
}

BurtGBufferData BurtCreateFurGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData = BurtApplyFurGBufferSurfaceSemantics(SurfaceData);
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

BurtGBufferData BurtCreateEyeGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 IrisNormalWS, float3 CausticNormalWS, float3 Emission)
{
    SurfaceData = BurtApplyEyeGBufferSurfaceSemantics(SurfaceData);
    SurfaceData.EyeIrisNormalWS = BurtSafeNormalize(IrisNormalWS);
    SurfaceData.EyeCausticNormalWS = BurtSafeNormalize(CausticNormalWS);
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
    Data.EyeIrisMask = saturate(SurfaceData.EyeIrisMask);
    Data.EyeIrisNormalWS = BurtSafeNormalize(IrisNormalWS);
    Data.EyeCausticNormalWS = BurtSafeNormalize(CausticNormalWS);
    return Data;
}

BurtGBufferData BurtCreateEyeGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    return BurtCreateEyeGBufferData(SurfaceData, NormalWS, TangentWS, SurfaceData.EyeIrisNormalWS, SurfaceData.EyeCausticNormalWS, Emission);
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 ClearCoatNormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
    Data.ClearCoatNormalWS = BurtSafeNormalize(ClearCoatNormalWS);
    return Data;
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ClearCoatNormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, NormalWS, Emission);
    Data.ClearCoatNormalWS = BurtSafeNormalize(ClearCoatNormalWS);
    return Data;
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 Emission)
{
    return BurtCreateClearCoatGBufferData(SurfaceData, NormalWS, NormalWS, Emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return BurtCreateGBufferData(SurfaceData, NormalWS, Emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 GeometryNormalWS, float3 Emission)
{
    BurtGBufferData Data = BurtCreateSubsurfaceGBufferData(SurfaceData, NormalWS, Emission);
    Data.SubsurfaceGeometryNormalWS = BurtSafeNormalize(GeometryNormalWS);
    return Data;
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 GeometryNormalWS, float4 TangentWS, float3 Emission)
{
    BurtGBufferData Data = BurtCreateSubsurfaceGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
    Data.SubsurfaceGeometryNormalWS = BurtSafeNormalize(GeometryNormalWS);
    return Data;
}

BurtGBufferData BurtCreateFabricGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FABRIC;
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

BurtGBufferData BurtCreateFoliageGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FOLIAGE;
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

float3 BurtGetGBufferDirectionWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}

bool BurtIsSubsurface3SGBuffer(BurtGBufferData GBufferData)
{
#if BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) &&
        BurtIsSubsurface3SPreIntegratedMode(GBufferData.SubsurfaceScatteringMode);
#else
    return false;
#endif
}

float3 BurtGetSubsurfaceGeometryNormalWS(BurtGBufferData GBufferData)
{
    return BurtSafeNormalize(GBufferData.SubsurfaceGeometryNormalWS);
}

float3 BurtGetDeferredSurfaceNormalWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}

float3 BurtGetDefaultLitNormalWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}

float3 BurtGetClearCoatNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return GBufferData.ClearCoatNormalWS;
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(GBufferData.ShadingModelID) ? GBufferData.ClearCoatNormalWS : GBufferData.NormalWS;
#else
    return GBufferData.NormalWS;
#endif
}

float3 BurtGetHairStrandDirectionWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}

float3 BurtGetHairShadingNormalWS(BurtGBufferData GBufferData)
{
    return BurtSafeNormalize(GBufferData.ClearCoatNormalWS);
}

float3 BurtGetHairGeometryNormalWS(BurtGBufferData GBufferData)
{
    return BurtSafeNormalize(GBufferData.HairGeometryNormalWS);
}

float BurtGetDefaultLitMetallic(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    return 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_EYE_SHADING
    return (BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) || BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID)) ? 0.0f : saturate(GBufferData.Metallic);
#else
    return saturate(GBufferData.Metallic);
#endif
}

float BurtGetEyeIrisMask(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_EYE_SHADING_MODEL
    return saturate(GBufferData.EyeIrisMask);
#elif BURT_ENABLE_EYE_SHADING
    return BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.EyeIrisMask) : 0.0f;
#else
    return 0.0f;
#endif
}

float3 BurtGetEyeIrisNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_EYE_SHADING_MODEL
    return BurtSafeNormalize(GBufferData.EyeIrisNormalWS);
#elif BURT_ENABLE_EYE_SHADING
    return BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID) ? BurtSafeNormalize(GBufferData.EyeIrisNormalWS) : GBufferData.NormalWS;
#else
    return GBufferData.NormalWS;
#endif
}

float3 BurtGetEyeCausticNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_EYE_SHADING_MODEL
    return BurtSafeNormalize(GBufferData.EyeCausticNormalWS);
#elif BURT_ENABLE_EYE_SHADING
    return BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID) ? BurtSafeNormalize(GBufferData.EyeCausticNormalWS) : GBufferData.NormalWS;
#else
    return GBufferData.NormalWS;
#endif
}

float BurtGetClearCoatMask(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return saturate(GBufferData.ClearCoatMask);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.ClearCoatMask) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetClearCoatRoughness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return ClampPerceptualRoughness(GBufferData.ClearCoatRoughness);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(GBufferData.ShadingModelID) ? ClampPerceptualRoughness(GBufferData.ClearCoatRoughness) : 0.2f;
#else
    return 0.2f;
#endif
}

float3 BurtGetReflectionNormalWS(BurtGBufferData GBufferData)
{
    float ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    return BurtSafeNormalize(lerp(BurtGetDeferredSurfaceNormalWS(GBufferData), BurtGetClearCoatNormalWS(GBufferData), ClearCoatMask));
}

float BurtGetReflectionRoughness(BurtGBufferData GBufferData)
{
    float ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    return saturate(lerp(GBufferData.PerceptualRoughness, BurtGetClearCoatRoughness(GBufferData), ClearCoatMask));
}

float BurtGetSubsurfaceStrength(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return 1.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetSubsurfaceThickness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(GBufferData.SubsurfaceThickness);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.SubsurfaceThickness) : BURT_SUBSURFACE_DEFAULT_THICKNESS;
#else
    return BURT_SUBSURFACE_DEFAULT_THICKNESS;
#endif
}

float BurtGetSubsurfacePower(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfacePower(GBufferData.SubsurfacePower);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BurtClampSubsurfacePower(GBufferData.SubsurfacePower) : BURT_SUBSURFACE_DEFAULT_POWER;
#else
    return BURT_SUBSURFACE_DEFAULT_POWER;
#endif
}

float BurtGetSubsurfaceDistortion(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(GBufferData.SubsurfaceDistortion);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.SubsurfaceDistortion) : BURT_SUBSURFACE_DEFAULT_DISTORTION;
#else
    return BURT_SUBSURFACE_DEFAULT_DISTORTION;
#endif
}

float BurtGetSubsurfaceScatteringMode(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfaceScatteringMode(GBufferData.SubsurfaceScatteringMode);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BurtClampSubsurfaceScatteringMode(GBufferData.SubsurfaceScatteringMode) : BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
#else
    return BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
#endif
}

float BurtGetSubsurfaceAmbient(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(GBufferData.SubsurfaceAmbient);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.SubsurfaceAmbient) : BURT_SUBSURFACE_DEFAULT_AMBIENT;
#else
    return BURT_SUBSURFACE_DEFAULT_AMBIENT;
#endif
}

float BurtGetSubsurfaceProfileIndex(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfaceProfileIndex(GBufferData.SubsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BurtClampSubsurfaceProfileIndex(GBufferData.SubsurfaceProfileIndex) : BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#else
    return BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
}

float BurtGetHairScatter(BurtGBufferData GBufferData)
{
    float HairScatter;
    float HairShiftScale;
    BurtDecodeHairMaterialChannel(GBufferData.MaterialChannel, HairScatter, HairShiftScale);
    return HairScatter;
}

float BurtGetHairLongitudinalShiftScale(BurtGBufferData GBufferData)
{
    float HairScatter;
    float HairShiftScale;
    BurtDecodeHairMaterialChannel(GBufferData.MaterialChannel, HairScatter, HairShiftScale);
    return HairShiftScale;
}

float BurtGetHairSecondaryRoughness(BurtGBufferData GBufferData)
{
    return ClampPerceptualRoughness(GBufferData.HairSecondaryRoughness);
}

float BurtGetHairBackLight(BurtGBufferData GBufferData)
{
    return saturate(GBufferData.HairBackLight);
}

float BurtGetHairShadowFillStrength(BurtGBufferData GBufferData)
{
    return saturate(GBufferData.HairShadowFillStrength);
}

float BurtGetHairSpecularShift(BurtGBufferData GBufferData)
{
    return clamp(GBufferData.HairSpecularShift, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX);
}

float BurtGetHairSecondarySpecularShift(BurtGBufferData GBufferData)
{
    return clamp(GBufferData.HairSecondarySpecularShift, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX);
}

float3 BurtGetHairSpecularColor(BurtGBufferData GBufferData)
{
    return max(GBufferData.HairSpecularColor, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtGetHairSecondarySpecularColor(BurtGBufferData GBufferData)
{
    return max(GBufferData.HairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f));
}

float BurtGetFabricFuzzWeight(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return saturate(GBufferData.FabricFuzzWeight);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FabricFuzzWeight) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFabricFuzzRoughness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return ClampPerceptualRoughness(GBufferData.FabricFuzzRoughness);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? ClampPerceptualRoughness(GBufferData.FabricFuzzRoughness) : 0.75f;
#else
    return 0.75f;
#endif
}

float3 BurtGetFabricFuzzColor(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return max(GBufferData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? max(GBufferData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f)) : float3(1.0f, 1.0f, 1.0f);
#else
    return float3(1.0f, 1.0f, 1.0f);
#endif
}

float BurtGetFabricIsSilk(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return saturate(GBufferData.FabricIsSilk);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FabricIsSilk) : 0.0f;
#else
    return 0.0f;
#endif
}

float3 BurtGetFoliageTransmissionColor(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return max(GBufferData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? max(GBufferData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)) : float3(0.0f, 0.0f, 0.0f);
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float BurtGetFoliageTransmissionWeight(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return GBufferData.FoliageIsGrass > 0.5f ? max(GBufferData.FoliageTransmissionWeight, 0.0f) : saturate(GBufferData.FoliageTransmissionWeight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID)
        ? (GBufferData.FoliageIsGrass > 0.5f ? max(GBufferData.FoliageTransmissionWeight, 0.0f) : saturate(GBufferData.FoliageTransmissionWeight))
        : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageThickness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageThickness);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageThickness) : 0.5f;
#else
    return 0.5f;
#endif
}

float BurtGetFoliageBackLight(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageBackLight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageBackLight) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageTransmissionNdotL(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageTransmissionNdotL);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageTransmissionNdotL) : 0.5f;
#else
    return 0.5f;
#endif
}

float BurtGetFoliageSpecularScale(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageSpecularScale);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageSpecularScale) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageUseSpecularColor(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageUseSpecularColor);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageUseSpecularColor) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageIsGrass(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageIsGrass);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageIsGrass) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageScreenSpaceShadowIntensity(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return max(GBufferData.FoliageScreenSpaceShadowIntensity, 0.0f);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? max(GBufferData.FoliageScreenSpaceShadowIntensity, 0.0f) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetGBufferMaterialChannel(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    return BurtGetHairScatter(GBufferData);
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        return BurtGetHairScatter(GBufferData);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtGetSubsurfaceStrength(GBufferData);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID))
    {
        return BurtGetSubsurfaceStrength(GBufferData);
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return BurtGetFoliageSpecularScale(GBufferData);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID))
    {
        return BurtGetFoliageSpecularScale(GBufferData);
    }
#endif

    return BurtGetDefaultLitMetallic(GBufferData);
}

float4 BurtEncodeClearCoatOrDefaultGBuffer3(BurtGBufferData Data)
{
    float2 EncodedClearCoatNormalWS = BurtEncodeNormalWSForGBuffer(Data.ClearCoatNormalWS);

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return float4(
        EncodedClearCoatNormalWS,
        saturate(Data.ClearCoatMask),
        ClampPerceptualRoughness(Data.ClearCoatRoughness));
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        return float4(
            EncodedClearCoatNormalWS,
            saturate(Data.ClearCoatMask),
            ClampPerceptualRoughness(Data.ClearCoatRoughness));
    }
#endif

    return float4(EncodedClearCoatNormalWS, 0.0f, 0.0f);
}

float4 BurtEncodeSubsurfaceGBuffer3(BurtGBufferData Data)
{
    float2 EncodedGeometryNormalWS = BurtEncodeNormalWSForGBuffer(Data.SubsurfaceGeometryNormalWS);
    float SubsurfaceControl = BurtIsSubsurface3SPreIntegratedMode(Data.SubsurfaceScatteringMode)
        ? saturate(Data.Subsurface3SCurvature)
        : BurtEncodeSubsurfacePowerAmbientForGBuffer(Data.SubsurfacePower, Data.SubsurfaceAmbient);
    return float4(EncodedGeometryNormalWS, 1.0f, SubsurfaceControl);
}

#define BURT_ENCODE_GBUFFER3_SHADING_MODEL(ShadingModelName, Data) \
    BURT_TOKEN_PASTE2(BurtEncodeGBuffer3_, ShadingModelName)(Data)
#define BURT_ENCODE_GBUFFER4_SHADING_MODEL(ShadingModelName, Data) \
    BURT_TOKEN_PASTE2(BurtEncodeGBuffer4_, ShadingModelName)(Data)

float4 BurtEncodeGBuffer3_DefaultLit(BurtGBufferData Data)
{
    return BurtEncodeClearCoatOrDefaultGBuffer3(Data);
}

float4 BurtEncodeGBuffer3_Hair(BurtGBufferData Data)
{
    return float4(
        max(Data.HairSpecularColor, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeHairRoughnessFillForGBuffer(Data.HairSecondaryRoughness, Data.HairShadowFillStrength));
}

float4 BurtEncodeGBuffer3_ClearCoat(BurtGBufferData Data)
{
    return BurtEncodeClearCoatOrDefaultGBuffer3(Data);
}

float4 BurtEncodeGBuffer3_Subsurface(BurtGBufferData Data)
{
    return BurtEncodeSubsurfaceGBuffer3(Data);
}

float4 BurtEncodeGBuffer3_Fabric(BurtGBufferData Data)
{
    return float4(max(Data.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f)), saturate(Data.FabricFuzzWeight));
}

float4 BurtEncodeGBuffer3_Foliage(BurtGBufferData Data)
{
    float EncodedFoliageWeight = Data.FoliageIsGrass > 0.5f
        ? saturate(Data.FoliageTransmissionWeight * 0.1f)
        : saturate(Data.FoliageTransmissionWeight);
    return float4(max(Data.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)), EncodedFoliageWeight);
}

float4 BurtEncodeGBuffer3_Fur(BurtGBufferData Data)
{
    return BurtEncodeClearCoatOrDefaultGBuffer3(Data);
}

float4 BurtEncodeGBuffer3_Eye(BurtGBufferData Data)
{
    float2 EncodedIrisNormalWS = BurtEncodeNormalWSForGBuffer(Data.EyeIrisNormalWS);
    return float4(EncodedIrisNormalWS, saturate(Data.EyeIrisMask), 0.0f);
}

float4 BurtEncodeDefaultOrClearCoatGBuffer4(BurtGBufferData Data)
{
    float2 EncodedTangentWS = BurtEncodeNormalWSForGBuffer(Data.TangentWS);
    return float4(
        EncodedTangentWS,
        clamp(Data.Anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
        0.0f);
}

float4 BurtEncodeGBuffer4_DefaultLit(BurtGBufferData Data)
{
    return BurtEncodeDefaultOrClearCoatGBuffer4(Data);
}

float4 BurtEncodeGBuffer4_Hair(BurtGBufferData Data)
{
    return float4(
        max(Data.HairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeHairShiftBackLightForGBuffer(Data.HairSpecularShift, Data.HairSecondarySpecularShift, Data.HairBackLight));
}

float4 BurtEncodeGBuffer4_ClearCoat(BurtGBufferData Data)
{
    return BurtEncodeDefaultOrClearCoatGBuffer4(Data);
}

float4 BurtEncodeGBuffer4_Subsurface(BurtGBufferData Data)
{
    float2 EncodedTangentWS = BurtEncodeNormalWSForGBuffer(Data.TangentWS);
    return float4(
        EncodedTangentWS,
        BurtEncodeSubsurfaceDistortionModeForGBuffer(Data.SubsurfaceDistortion, Data.SubsurfaceScatteringMode),
        BurtEncodeSubsurfaceThicknessProfileForGBuffer(Data.SubsurfaceThickness, Data.SubsurfaceProfileIndex));
}

float4 BurtEncodeGBuffer4_Fabric(BurtGBufferData Data)
{
    float2 EncodedTangentWS = BurtEncodeNormalWSForGBuffer(Data.TangentWS);
    return float4(
        EncodedTangentWS,
        clamp(Data.Anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
        BurtEncodeFabricRoughnessSilkForGBuffer(Data.FabricFuzzRoughness, Data.FabricIsSilk));
}

float4 BurtEncodeGBuffer4_Foliage(BurtGBufferData Data)
{
    return float4(
        max(Data.FoliageScreenSpaceShadowIntensity, 0.0f),
        0.0f,
        BurtEncodeFoliageBackLightNdotLForGBuffer(Data.FoliageBackLight, Data.FoliageTransmissionNdotL),
        saturate(Data.FoliageThickness));
}

float4 BurtEncodeGBuffer4_Fur(BurtGBufferData Data)
{
    return BurtEncodeDefaultOrClearCoatGBuffer4(Data);
}

float4 BurtEncodeGBuffer4_Eye(BurtGBufferData Data)
{
    float2 EncodedCausticNormalWS = BurtEncodeNormalWSForGBuffer(Data.EyeCausticNormalWS);
    return float4(EncodedCausticNormalWS, 0.0f, 0.0f);
}

float4 BurtEncodeGBuffer3(BurtGBufferData Data)
{
#if BURT_STATIC_SHADING_MODEL
    return BURT_ENCODE_GBUFFER3_SHADING_MODEL(BURT_STATIC_SHADING_MODEL_NAME, Data);
#else
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Hair(Data);
    }
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Subsurface(Data);
    }
#endif

#if BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Fabric(Data);
    }
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Foliage(Data);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Eye(Data);
    }
#endif

    return BurtEncodeGBuffer3_DefaultLit(Data);
#endif
}

float4 BurtEncodeGBuffer4(BurtGBufferData Data)
{
#if BURT_STATIC_SHADING_MODEL
    return BURT_ENCODE_GBUFFER4_SHADING_MODEL(BURT_STATIC_SHADING_MODEL_NAME, Data);
#else
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Hair(Data);
    }
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Subsurface(Data);
    }
#endif

#if BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Fabric(Data);
    }
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Foliage(Data);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Eye(Data);
    }
#endif

    return BurtEncodeGBuffer4_DefaultLit(Data);
#endif
}

float4 BurtClampGBuffer3LowPrecisionPayload(float4 Payload)
{
    return saturate(Payload);
}

// Encodes semantic GBuffer Data into the five MRT payloads.
BurtEncodedGBuffer BurtEncodeGBuffer(BurtGBufferData Data)
{
    BurtEncodedGBuffer Encoded;

    Encoded.GBuffer0 = float4(BurtEncodeNormalWS888ForGBuffer(Data.NormalWS), ClampPerceptualRoughness(Data.PerceptualRoughness));

    Encoded.GBuffer1 = float4(saturate(Data.BaseColor), saturate(Data.Occlusion));

    Encoded.GBuffer2 = float4(
        BurtEncodeMetallicAndShadingModelForGBuffer(Data.MaterialChannel, Data.ShadingModelID),
        saturate(Data.Metallic),
        saturate(Data.Smoothness),
        saturate(Data.Reflectance));

    Encoded.GBuffer3 = BurtClampGBuffer3LowPrecisionPayload(BurtEncodeGBuffer3(Data));
    Encoded.GBuffer4 = float4(max(Data.Emission, float3(0.0f, 0.0f, 0.0f)), 0.0f);
    Encoded.GBuffer5 = BurtEncodeGBuffer4(Data);

    return Encoded;
}

#define BURT_DECODE_GBUFFER_CUSTOM_SHADING_MODEL(ShadingModelName, Encoded, Data) \
    BURT_TOKEN_PASTE2(BurtDecodeGBufferCustom_, ShadingModelName)(Encoded, Data)

BurtGBufferData BurtDecodeGBufferCustom_DefaultLit(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_Hair(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.HairSpecularColor = max(Encoded.GBuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeHairRoughnessFillFromGBuffer(Encoded.GBuffer3.a, Data.HairSecondaryRoughness, Data.HairShadowFillStrength);
    Data.HairSecondarySpecularColor = max(Encoded.GBuffer5.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeHairShiftBackLightFromGBuffer(Encoded.GBuffer5.a, Data.HairSpecularShift, Data.HairSecondarySpecularShift, Data.HairBackLight);
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_ClearCoat(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.ClearCoatMask = saturate(Encoded.GBuffer3.b);
    Data.ClearCoatRoughness = ClampPerceptualRoughness(Encoded.GBuffer3.a);
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_Subsurface(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    BurtDecodeSubsurfacePowerAmbientFromGBuffer(Encoded.GBuffer3.a, Data.SubsurfacePower, Data.SubsurfaceAmbient);
    BurtDecodeSubsurfaceDistortionModeFromGBuffer(Encoded.GBuffer5.b, Data.SubsurfaceDistortion, Data.SubsurfaceScatteringMode);
    BurtDecodeSubsurfaceThicknessProfileFromGBuffer(Encoded.GBuffer5.a, Data.SubsurfaceThickness, Data.SubsurfaceProfileIndex);
    Data.SubsurfaceGeometryNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
    Data.Subsurface3SCurvature = BurtIsSubsurface3SPreIntegratedMode(Data.SubsurfaceScatteringMode)
        ? saturate(Encoded.GBuffer3.a)
        : saturate(1.0f - Data.SubsurfaceThickness);
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_Fabric(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.FabricFuzzColor = max(Encoded.GBuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    Data.FabricFuzzWeight = saturate(Encoded.GBuffer3.a);
    BurtDecodeFabricRoughnessSilkFromGBuffer(Encoded.GBuffer5.a, Data.FabricFuzzRoughness, Data.FabricIsSilk);
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_Foliage(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.FoliageTransmissionColor = max(Encoded.GBuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeFoliageSpecularTypeFromGBuffer(Data.MaterialChannel, Data.FoliageSpecularScale, Data.FoliageUseSpecularColor);
    Data.FoliageIsGrass = 1.0f - saturate(Data.FoliageUseSpecularColor);
    Data.FoliageTransmissionWeight = Data.FoliageIsGrass > 0.5f
        ? max(Encoded.GBuffer3.a * 10.0f, 0.0f)
        : saturate(Encoded.GBuffer3.a);
    Data.FoliageScreenSpaceShadowIntensity = max(Encoded.GBuffer5.r, 0.0f);
    Data.TangentWS = BurtCreateFallbackTangentWS(Data.NormalWS);
    BurtDecodeFoliageBackLightNdotLFromGBuffer(Encoded.GBuffer5.b, Data.FoliageBackLight, Data.FoliageTransmissionNdotL);
    Data.FoliageThickness = saturate(Encoded.GBuffer5.a);
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_Fur(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    return Data;
}

BurtGBufferData BurtDecodeGBufferCustom_Eye(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.EyeIrisNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
    Data.EyeIrisMask = saturate(Encoded.GBuffer3.b);
    Data.EyeCausticNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer5.rg);
    return Data;
}

// Decodes the five MRT payloads back into semantic GBuffer Data.
BurtGBufferData BurtDecodeGBufferInternal(BurtEncodedGBuffer Encoded, float OverrideShadingModelID, bool UseOverrideShadingModel)
{
    BurtGBufferData Data;

    Data.BaseColor = max(Encoded.GBuffer1.rgb, float3(0.0f, 0.0f, 0.0f));
    Data.Occlusion = saturate(Encoded.GBuffer1.a);

    Data.NormalWS = BurtDecodeNormalWS888FromGBuffer(Encoded.GBuffer0.rgb);
    float DecodedShadingModelID = 0.0f;
    Data.MaterialChannel = BurtDecodeMetallicAndShadingModelFromGBuffer(Encoded.GBuffer2.r, DecodedShadingModelID);
    Data.ShadingModelID = UseOverrideShadingModel ? BurtResolveSurfaceShadingModel(OverrideShadingModelID) : DecodedShadingModelID;
    Data.SubsurfaceGeometryNormalWS = Data.NormalWS;
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    Data.ClearCoatNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        Data.ClearCoatNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
    }
    else
    {
        Data.ClearCoatNormalWS = Data.NormalWS;
    }
#else
    Data.ClearCoatNormalWS = Data.NormalWS;
#endif
    Data.TangentWS = BurtOrthonormalizeTangentWS(Data.NormalWS, BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer5.rg));
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID) || BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID) || BurtIsActiveFoliageShadingModel(Data.ShadingModelID) || BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data.Anisotropy = 0.0f;
    }
    else
    {
        Data.Anisotropy = clamp(Encoded.GBuffer5.b * 2.0f - 1.0f, -1.0f, 1.0f);
    }
#else
    Data.Anisotropy = clamp(Encoded.GBuffer5.b * 2.0f - 1.0f, -1.0f, 1.0f);
#endif
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Metallic = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID) || BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID) || BurtIsActiveFoliageShadingModel(Data.ShadingModelID) || BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data.Metallic = 0.0f;
    }
    else
    {
        Data.Metallic = saturate(Encoded.GBuffer2.g);
    }
#else
    Data.Metallic = saturate(Encoded.GBuffer2.g);
#endif
    Data.ClearCoatMask = 0.0f;
    Data.ClearCoatRoughness = 0.2f;
    Data.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    Data.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    Data.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    Data.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    Data.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    Data.HairSecondaryRoughness = 0.5f;
    Data.HairBackLight = 0.0f;
    Data.HairShadowFillStrength = 0.0f;
    Data.HairGeometryNormalWS = Data.NormalWS;
    Data.HairSpecularShift = 0.0f;
    Data.HairSecondarySpecularShift = 0.0f;
    Data.HairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    Data.HairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    Data.FabricIsSilk = 0.0f;
    Data.FabricFuzzWeight = 0.0f;
    Data.FabricFuzzRoughness = 0.75f;
    Data.FabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
    Data.FoliageTransmissionColor = float3(0.0f, 0.0f, 0.0f);
    Data.FoliageTransmissionWeight = 0.0f;
    Data.FoliageThickness = 0.5f;
    Data.FoliageBackLight = 0.0f;
    Data.FoliageTransmissionNdotL = 0.5f;
    Data.FoliageSpecularScale = 0.0f;
    Data.FoliageUseSpecularColor = 0.0f;
    Data.FoliageScreenSpaceShadowIntensity = 0.0f;
    Data.FoliageIsGrass = 0.0f;
    Data.EyeIrisMask = 0.0f;
    Data.EyeIrisNormalWS = Data.NormalWS;
    Data.EyeCausticNormalWS = Data.NormalWS;
    Data.Smoothness = saturate(Encoded.GBuffer2.b);
    Data.PerceptualRoughness = ClampPerceptualRoughness(Encoded.GBuffer0.a);
    Data.Emission = max(Encoded.GBuffer4.rgb, float3(0.0f, 0.0f, 0.0f));
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    Data.Reflectance = BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : saturate(Encoded.GBuffer2.a);
#else
    Data.Reflectance = saturate(Encoded.GBuffer2.a);
#endif

#if BURT_STATIC_SHADING_MODEL
    Data = BURT_DECODE_GBUFFER_CUSTOM_SHADING_MODEL(BURT_STATIC_SHADING_MODEL_NAME, Encoded, Data);
#else
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Hair(Encoded, Data);
    }
#endif

#if BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_ClearCoat(Encoded, Data);
    }
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Subsurface(Encoded, Data);
    }
#endif

#if BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Fabric(Encoded, Data);
    }
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Foliage(Encoded, Data);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Eye(Encoded, Data);
    }
#endif
#endif

    return Data;
}

BurtGBufferData BurtDecodeGBuffer(BurtEncodedGBuffer Encoded)
{
    return BurtDecodeGBufferInternal(Encoded, BURT_SHADING_MODEL_DEFAULT_LIT, false);
}

BurtGBufferData BurtDecodeGBufferWithShadingModel(BurtEncodedGBuffer Encoded, float ShadingModelID)
{
    return BurtDecodeGBufferInternal(Encoded, ShadingModelID, true);
}

// Prepares PBR Material Data from decoded GBuffer Data.
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtGBufferData GBufferData)
{
    BurtPBRMaterialData MaterialData;

    MaterialData.BaseColor = GBufferData.BaseColor;
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    MaterialData.Metallic = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID) || BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) || BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        MaterialData.Metallic = 0.0f;
    }
    else
    {
        MaterialData.Metallic = BurtGetDefaultLitMetallic(GBufferData);
    }
#else
    MaterialData.Metallic = BurtGetDefaultLitMetallic(GBufferData);
#endif
    MaterialData.ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    MaterialData.ClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
    MaterialData.SubsurfaceActive = BurtIsSubsurfaceShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
    MaterialData.SubsurfaceThickness = BurtGetSubsurfaceThickness(GBufferData);
    MaterialData.SubsurfacePower = BurtGetSubsurfacePower(GBufferData);
    MaterialData.SubsurfaceDistortion = BurtGetSubsurfaceDistortion(GBufferData);
    MaterialData.SubsurfaceAmbient = BurtGetSubsurfaceAmbient(GBufferData);
    MaterialData.SubsurfaceScatteringMode = BurtGetSubsurfaceScatteringMode(GBufferData);
    MaterialData.Subsurface3SCurvature = saturate(GBufferData.Subsurface3SCurvature);
    MaterialData.SubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(GBufferData);
    MaterialData.FabricActive = BurtIsFabricShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
    MaterialData.FabricIsSilk = BurtGetFabricIsSilk(GBufferData);
    MaterialData.FabricFuzzWeight = BurtGetFabricFuzzWeight(GBufferData);
    MaterialData.FabricFuzzRoughness = BurtGetFabricFuzzRoughness(GBufferData);
    MaterialData.FabricFuzzColor = BurtGetFabricFuzzColor(GBufferData);
    MaterialData.FoliageActive = BurtIsFoliageShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
    MaterialData.FoliageTransmissionColor = BurtGetFoliageTransmissionColor(GBufferData);
    MaterialData.FoliageTransmissionWeight = BurtGetFoliageTransmissionWeight(GBufferData);
    MaterialData.FoliageThickness = BurtGetFoliageThickness(GBufferData);
    MaterialData.FoliageBackLight = BurtGetFoliageBackLight(GBufferData);
    MaterialData.FoliageTransmissionNdotL = BurtGetFoliageTransmissionNdotL(GBufferData);
    MaterialData.FoliageSpecularScale = BurtGetFoliageSpecularScale(GBufferData);
    MaterialData.FoliageUseSpecularColor = BurtGetFoliageUseSpecularColor(GBufferData);
    MaterialData.FoliageScreenSpaceShadowIntensity = BurtGetFoliageScreenSpaceShadowIntensity(GBufferData);
    MaterialData.FoliageIsGrass = BurtGetFoliageIsGrass(GBufferData);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    MaterialData.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    MaterialData.Reflectance = BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : GBufferData.Reflectance;
#else
    MaterialData.Reflectance = GBufferData.Reflectance;
#endif
    MaterialData.Occlusion = GBufferData.Occlusion;
    MaterialData.Smoothness = GBufferData.Smoothness;
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    MaterialData.Anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID) || BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) || BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) || BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        MaterialData.Anisotropy = 0.0f;
    }
    else
    {
        MaterialData.Anisotropy = clamp(GBufferData.Anisotropy, -1.0f, 1.0f);
    }
#else
    MaterialData.Anisotropy = clamp(GBufferData.Anisotropy, -1.0f, 1.0f);
#endif

    MaterialData.PerceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(MaterialData.Smoothness));
    MaterialData.LinearRoughness = PerceptualRoughnessToLinearRoughness(MaterialData.PerceptualRoughness);
    MaterialData.A2 = LinearRoughnessToA2(MaterialData.LinearRoughness);

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT) && BURT_ENABLE_SUBSURFACE_SHADING
    float3 DiffuseBaseColor = BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) && !BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode) ? float3(1.0f, 1.0f, 1.0f) : MaterialData.BaseColor;
#else
    float3 DiffuseBaseColor = MaterialData.BaseColor;
#endif

    MaterialData.DiffuseColor = DiffuseColorFromBaseColor(DiffuseBaseColor, MaterialData.Metallic);
    MaterialData.F0 = DielectricReflectanceToF0(MaterialData.BaseColor, MaterialData.Reflectance, MaterialData.Metallic);
    MaterialData.F90 = ApproximateF90(MaterialData.F0);
    if (MaterialData.FoliageActive > 0.5f)
    {
        MaterialData.F90 = MaterialData.FoliageUseSpecularColor > 0.5f
            ? saturate(MaterialData.BaseColor * MaterialData.FoliageSpecularScale)
            : saturate((MaterialData.BaseColor * 0.9f + 0.1f) * MaterialData.FoliageSpecularScale * 3.0f);
    }

    return MaterialData;
}

// Prepares PBR geometry Data from decoded GBuffer Data and reconstructed view direction.
BurtPBRGeometryData BurtPreparePBRGeometryData(BurtGBufferData GBufferData, float3 ViewDirectionWS)
{
    return BurtPreparePBRGeometryData(BurtGetDeferredSurfaceNormalWS(GBufferData), GBufferData.TangentWS, ViewDirectionWS);
}

#endif // BURT_GBUFFER_INCLUDED // 缂傚倸鍊烽悞锕傚箰鐠囧樊鐒?BurtGBuffer.hlsl 闂?include guard闂?
