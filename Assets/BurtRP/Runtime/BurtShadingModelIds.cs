using UnityEngine;

namespace Burt.RenderPipeline
{
    public static class BurtShadingModelIds
    {
        public const int XRenderSemanticInvalid = 0x00;
        public const int XRenderSemanticDefaultLit = 0x01;
        public const int XRenderSemanticSubsurface = 0x02;
        public const int XRenderSemanticTransmission = 0x03;
        public const int XRenderSemanticCoat = 0x04;
        public const int XRenderSemanticClearCoat = XRenderSemanticCoat;
        public const int XRenderSemanticFabric = 0x05;
        public const int XRenderSemanticFoliage = 0x06;
        public const int XRenderSemanticFur = 0x07;
        public const int XRenderSemanticEye = 0x08;
        public const int XRenderSemanticHair = 0x09;
        public const int XRenderSemanticSingleLayerWater = 0x0A;
        public const int XRenderSemanticHexagon = 0x0B;
        public const int XRenderSemanticEmissive = 0x0C;
        public const int XRenderSemanticComplex = 0x0D;
        public const int XRenderSemanticHexaLighting = 0x0E;
        public const int XRenderSemanticMedium = 0x0F;

        public const int BurtPackedDefaultLit = 0;
        public const int BurtPackedHair = 1;
        public const int BurtPackedClearCoat = 2;
        public const int BurtPackedSubsurface = 3;
        public const int BurtPackedFabric = 4;
        public const int BurtPackedFoliage = 5;
        public const int BurtPackedFur = 6;
        public const int BurtPackedEye = 7;
        public const int BurtPackedMaxEncoded = BurtPackedEye;
        public const int BurtPackedCount = BurtPackedMaxEncoded + 1;

        public const int SssProfileTypeNone = 0x00;
        public const int SssProfileTypeBurley = 0x40;
        public const int SssProfileTypeSeparable = 0x80;
        public const int SssProfileTypeMask = 0xC0;
        public const int SssProfileIdMask = 0x3F;

        public const int DeferredStencilObjectMotionBit = 0x08;
        public const int DeferredStencilResponsiveAABit = 0x10;
        public const int DeferredStencilShadingModelMask = 0xE0;
        public const int DeferredStencilDefaultLitRef = 0x20;
        public const int DeferredStencilSubsurfaceRef = 0x40;

        // Burt currently reuses XRender's deferred Transmission bucket for deferred hair compatibility.
        public const int DeferredStencilHairRef = 0x60;

        public const int DeferredStencilClearCoatRef = 0x80;
        public const int DeferredStencilFabricRef = 0xA0;
        public const int DeferredStencilFoliageRef = 0xC0;
        public const int DeferredStencilFurRef = 0xE0;

        public static readonly int MotionVectorsStencilRefId = Shader.PropertyToID("_MotionVectorsStencilRef");
        public static readonly int MotionVectorsStencilMaskId = Shader.PropertyToID("_MotionVectorsStencilMask");
        public static readonly int ResponsiveAAId = Shader.PropertyToID("_ResponsiveAA");
        public static readonly int DeferredStencilDefaultLitRefId = Shader.PropertyToID("_BurtDeferredStencilDefaultLitRef");
        public static readonly int DeferredStencilSubsurfaceRefId = Shader.PropertyToID("_BurtDeferredStencilSubsurfaceRef");
        public static readonly int DeferredStencilHairRefId = Shader.PropertyToID("_BurtDeferredStencilHairRef");
        public static readonly int DeferredStencilClearCoatRefId = Shader.PropertyToID("_BurtDeferredStencilClearCoatRef");
        public static readonly int DeferredStencilFabricRefId = Shader.PropertyToID("_BurtDeferredStencilFabricRef");
        public static readonly int DeferredStencilFoliageRefId = Shader.PropertyToID("_BurtDeferredStencilFoliageRef");
        public static readonly int DeferredStencilFurRefId = Shader.PropertyToID("_BurtDeferredStencilFurRef");
        public static readonly int DeferredStencilShadingModelMaskId = Shader.PropertyToID("_BurtDeferredStencilShadingModelMask");

        public static void ApplyMotionVectorStencilProperties(Material material)
        {
            var responsive = material != null && material.HasProperty(ResponsiveAAId) && material.GetFloat(ResponsiveAAId) >= 0.5f;
            var stencilRef = DeferredStencilObjectMotionBit | (responsive ? DeferredStencilResponsiveAABit : 0);
            var stencilMask = DeferredStencilObjectMotionBit | (responsive ? DeferredStencilResponsiveAABit : 0);
            SetFloatIfPropertyExists(material, MotionVectorsStencilRefId, stencilRef);
            SetFloatIfPropertyExists(material, MotionVectorsStencilMaskId, stencilMask);
        }

        public static void ApplyDeferredLightingStencilProperties(Material material)
        {
            SetFloatIfPropertyExists(material, DeferredStencilDefaultLitRefId, DeferredStencilDefaultLitRef);
            SetFloatIfPropertyExists(material, DeferredStencilSubsurfaceRefId, DeferredStencilSubsurfaceRef);
            SetFloatIfPropertyExists(material, DeferredStencilHairRefId, DeferredStencilHairRef);
            SetFloatIfPropertyExists(material, DeferredStencilClearCoatRefId, DeferredStencilClearCoatRef);
            SetFloatIfPropertyExists(material, DeferredStencilFabricRefId, DeferredStencilFabricRef);
            SetFloatIfPropertyExists(material, DeferredStencilFoliageRefId, DeferredStencilFoliageRef);
            SetFloatIfPropertyExists(material, DeferredStencilFurRefId, DeferredStencilFurRef);
            SetFloatIfPropertyExists(material, DeferredStencilShadingModelMaskId, DeferredStencilShadingModelMask);
        }

        public static void ApplyScreenSpaceSubsurfaceStencilProperties(Material material)
        {
            SetFloatIfPropertyExists(material, DeferredStencilSubsurfaceRefId, DeferredStencilSubsurfaceRef);
            SetFloatIfPropertyExists(material, DeferredStencilShadingModelMaskId, DeferredStencilShadingModelMask);
        }

        private static void SetFloatIfPropertyExists(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }
    }
}
