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
        private const string ForwardKeyword = "BURT_USE_DEBUG_MODE_FORWARD";
        private const string DeferredKeyword = "BURT_USE_DEBUG_MODE_DEFERRED";

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
                    case ForwardKeyword:
                    case DeferredKeyword:
                        return true;
                }
            }

            return false;
        }
    }
}
