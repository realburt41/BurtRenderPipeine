using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEditor;

public class BurtDeferredRenderPipeline : RenderPipeline
{

    RenderTexture gdepth;                                               // depth attachment
    RenderTargetIdentifier gdepthID;
    RenderTexture[] gbuffers = new RenderTexture[4];                    // color attachments 
    RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[4]; // tex ID 

    RenderTexture lightPassTex;                                         // 存储 light pass 的结果

    const string bufferName = "Render Camera";

    Matrix4x4 vpMatrix;
    Matrix4x4 vpMatrixInv;
    Matrix4x4 vpMatrixPrev;     // 上一帧的 vp 矩阵
    Matrix4x4 vpMatrixInvPrev;

    public BurtDeferredRenderPipeline()
    {
        QualitySettings.vSyncCount = 0;     // 关闭垂直同步
        Application.targetFrameRate = 60;   // 帧率

        // 创建纹理
        gdepth = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

        gbuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        gbuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
        gbuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        gbuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);


        // 给纹理 ID 赋值
        gdepthID = gdepth;
        for (int i = 0; i < 4; i++)
            gbufferID[i] = gbuffers[i];

    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // 主相机
        Camera camera = cameras[0];

        bool isEditor = Handles.ShouldRenderGizmos();

        //  gbuffer 
        Shader.SetGlobalTexture("_gdepth", gdepth);
        for (int i = 0; i < 4; i++)
            Shader.SetGlobalTexture("_GT" + i, gbuffers[i]);

        context.SetupCameraProperties(camera);
        // -------------------------------
        if (!isEditor)
        {
            Profiler.BeginSample("gbufferDraw");

            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "gbuffer";

            // 清屏
            cmd.SetRenderTarget(gbufferID, gdepthID);
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // 剔除
            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = context.Cull(ref cullingParameters);

            // config settings
            ShaderTagId shaderTagId = new ShaderTagId("BurtGBuffer");   // 使用 LightMode 为 BurtGBuffer 的 shader
            SortingSettings sortingSettings = new SortingSettings(camera);
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
            FilteringSettings filteringSettings = FilteringSettings.defaultValue;

            // 绘制GBuffer
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            context.Submit();
            Profiler.EndSample();

        }

        // 绘制光照
        CommandBuffer cmd2 = new CommandBuffer();
        cmd2.name = "lightpass";
        Material mat = new Material(Shader.Find("BurtRP/LightPass"));
        cmd2.Blit(gbufferID[0], BuiltinRenderTextureType.CameraTarget, mat);
        context.ExecuteCommandBuffer(cmd2);
        context.Submit();


        // -------------------------------

        // skybox and Gizmos
        context.DrawSkybox(camera);
        if (isEditor)
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        // 提交绘制命令
        context.Submit();
    }
}
