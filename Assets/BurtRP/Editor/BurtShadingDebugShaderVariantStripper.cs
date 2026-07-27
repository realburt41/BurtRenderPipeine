using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    internal sealed class BurtShadingDebugShaderVariantStripper : IPreprocessShaders
    {
        private const string MainKeyword = "BURT_SHADING_DEBUG";
        private const string LightingKeyword = "BURT_FORWARD_SHADING_DEBUG_LIGHTING";
        private const string BrdfKeyword = "BURT_FORWARD_SHADING_DEBUG_BRDF";
        private const string ShadowKeyword = "BURT_FORWARD_SHADING_DEBUG_SHADOW";
        private const string TransmissionKeyword = "BURT_FORWARD_SHADING_DEBUG_TRANSMISSION";

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
#if BURT_KEEP_SHADING_DEBUG_IN_RELEASE
            return;
#else
            if (!BuildPipeline.isBuildingPlayer || EditorUserBuildSettings.development)
            {
                return;
            }

            for (var index = data.Count - 1; index >= 0; index--)
            {
                if (ContainsShadingDebugKeyword(data[index].shaderKeywordSet))
                {
                    data.RemoveAt(index);
                }
            }
#endif
        }

        private static bool ContainsShadingDebugKeyword(ShaderKeywordSet keywordSet)
        {
            var keywords = keywordSet.GetShaderKeywords();
            for (var index = 0; index < keywords.Length; index++)
            {
                switch (keywords[index].name)
                {
                    case MainKeyword:
                    case LightingKeyword:
                    case BrdfKeyword:
                    case ShadowKeyword:
                    case TransmissionKeyword:
                        return true;
                }
            }

            return false;
        }
    }
}
