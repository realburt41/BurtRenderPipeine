using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Validation
{
    internal sealed class CodexTAAValidationRuntime : MonoBehaviour
    {
        private const string EnableArgument = "-burtTaaValidation";
        private const string QuitArgument = "-burtTaaValidationQuit";
        private const string OutputArgument = "-burtTaaValidationOutput";
        private const string ProceduralSkinnedArgument = "-burtTaaValidationProceduralSkinned";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // This runner changes cameras and exits the process when finished.
            // Never install it for normal Editor Play mode or unrelated builds.
            // Keep the existing output argument as an opt-in for CLI workflows.
            if (!IsValidationRequested())
            {
                return;
            }

            var host = new GameObject("Codex TAA Validation");
            DontDestroyOnLoad(host);
            host.AddComponent<CodexTAAValidationRuntime>();
        }

        private IEnumerator Start()
        {
            // Also guard an old runner serialized into a scene or added manually.
            if (!IsValidationRequested())
            {
                yield break;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

            var outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            Camera camera = null;
            for (var frame = 0; frame < 120 && camera == null; ++frame)
            {
                camera = Camera.main;
                if (camera == null)
                {
                    var cameras = FindObjectsOfType<Camera>();
                    for (var index = 0; index < cameras.Length; ++index)
                    {
                        if (cameras[index] != null && cameras[index].enabled && cameras[index].cameraType == CameraType.Game)
                        {
                            camera = cameras[index];
                            break;
                        }
                    }
                }

                yield return null;
            }

            if (camera == null)
            {
                Debug.LogError("[CodexTAAValidation] No enabled Game camera found.");
                Exit(2);
                yield break;
            }

            var cameraData = camera.GetComponent<BurtCameraData>();
            var originalTarget = camera.targetTexture;
            var validationTarget = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32)
            {
                name = "Codex TAA PlayMode Validation Target",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            validationTarget.Create();
            camera.targetTexture = validationTarget;
            Debug.Log(
                $"[CodexTAAValidation] Scene={SceneManager.GetActiveScene().path} Camera={camera.name} " +
                $"Resolution={validationTarget.width}x{validationTarget.height} AA={(cameraData != null ? cameraData.AntialiasingMode.ToString() : "NoBurtCameraData")} " +
                $"RenderScale={(cameraData != null ? cameraData.RenderScale.ToString("0.###") : "n/a")} Output={outputDirectory}");

            var proceduralSkinned = HasCommandLineFlag(ProceduralSkinnedArgument);
            if (proceduralSkinned)
            {
                DisableTemporalValidationDistractors();
            }
            var skinnedValidation = proceduralSkinned ? CreateProceduralSkinnedValidation(camera) : null;

            // Let shader compilation, exposure, and temporal history settle before measuring TAA.
            yield return WaitFrames(proceduralSkinned ? 12 : 96);
            yield return Capture(validationTarget, outputDirectory, "baseline.png");

            var startPosition = camera.transform.position;
            if (skinnedValidation != null)
            {
                for (var frame = 1; frame <= 24; ++frame)
                {
                    var t = frame / 24.0f;
                    t = t * t * (3.0f - 2.0f * t);
                    skinnedValidation.AnimatedBone.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Lerp(-35.0f, 35.0f, t));
                    yield return null;
                    if (frame == 1 || frame == 12 || frame == 24)
                    {
                        yield return Capture(validationTarget, outputDirectory, $"skinned_moving_{frame:00}.png");
                    }
                }
            }
            else
            {
                var displacement = camera.transform.right * 0.20f;
                for (var frame = 1; frame <= 12; ++frame)
                {
                    var t = frame / 12.0f;
                    t = t * t * (3.0f - 2.0f * t);
                    camera.transform.position = startPosition + displacement * t;
                    yield return null;
                }
            }

            yield return Capture(validationTarget, outputDirectory, "motion_end.png");

            var checkpoints = new[] { 0, 1, 2, 4, 8, 16, 32, 64 };
            var elapsed = 0;
            for (var index = 0; index < checkpoints.Length; ++index)
            {
                var target = checkpoints[index];
                yield return WaitFrames(target - elapsed);
                elapsed = target;
                yield return Capture(validationTarget, outputDirectory, $"settle_{target:00}.png");
            }

            for (var index = 0; index < 8; ++index)
            {
                yield return Capture(validationTarget, outputDirectory, $"static_{index:00}.png");
            }

            File.WriteAllText(Path.Combine(outputDirectory, "complete.txt"), "ok\n");
            Debug.Log("[CodexTAAValidation] Capture complete.");
            if (skinnedValidation != null)
            {
                Destroy(skinnedValidation.Root);
            }
            camera.targetTexture = originalTarget;
            validationTarget.Release();
            Destroy(validationTarget);
            yield return null;
            Exit(0);
        }

        private static SkinnedValidation CreateProceduralSkinnedValidation(Camera camera)
        {
            var root = new GameObject("Codex Procedural Skinned TAA Validation");
            root.transform.position = camera.transform.position + camera.transform.forward * 4.0f;
            root.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);

            var lowerBone = new GameObject("Lower Bone").transform;
            lowerBone.SetParent(root.transform, false);
            lowerBone.localPosition = new Vector3(0.0f, -0.75f, 0.0f);
            var upperBone = new GameObject("Animated Upper Bone").transform;
            upperBone.SetParent(lowerBone, false);
            upperBone.localPosition = new Vector3(0.0f, 1.0f, 0.0f);
            upperBone.localRotation = Quaternion.Euler(0.0f, 0.0f, -35.0f);

            const int columns = 7;
            const int rows = 5;
            var vertices = new Vector3[columns * rows];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var weights = new BoneWeight[vertices.Length];
            for (var y = 0; y < rows; ++y)
            {
                var fy = y / (float)(rows - 1);
                for (var x = 0; x < columns; ++x)
                {
                    var fx = x / (float)(columns - 1);
                    var index = y * columns + x;
                    vertices[index] = new Vector3(Mathf.Lerp(-0.75f, 0.75f, fx), Mathf.Lerp(-1.0f, 1.0f, fy), 0.0f);
                    normals[index] = Vector3.back;
                    uv[index] = new Vector2(fx, fy);
                    var upperWeight = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.25f, 0.75f, fy));
                    weights[index] = new BoneWeight
                    {
                        boneIndex0 = 0,
                        weight0 = 1.0f - upperWeight,
                        boneIndex1 = 1,
                        weight1 = upperWeight
                    };
                }
            }

            var triangles = new List<int>((columns - 1) * (rows - 1) * 6);
            for (var y = 0; y < rows - 1; ++y)
            {
                for (var x = 0; x < columns - 1; ++x)
                {
                    var a = y * columns + x;
                    var b = a + 1;
                    var c = a + columns;
                    var d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = "Codex Procedural Skinned Mesh" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.boneWeights = weights;
            mesh.triangles = triangles.ToArray();
            mesh.bindposes = new[]
            {
                lowerBone.worldToLocalMatrix * root.transform.localToWorldMatrix,
                upperBone.worldToLocalMatrix * root.transform.localToWorldMatrix
            };
            mesh.RecalculateBounds();

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.rootBone = lowerBone;
            renderer.bones = new[] { lowerBone, upperBone };
            renderer.localBounds = new Bounds(Vector3.zero, new Vector3(4.0f, 4.0f, 1.0f));
            renderer.updateWhenOffscreen = true;
            renderer.skinnedMotionVectors = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;

            var shader = Shader.Find("BurtRP/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("BurtRP/Lit shader was not found.");
            }

            var material = new Material(shader) { name = "Codex Procedural Skinned Lit" };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.8f, 0.08f, 0.03f, 1.0f));
            }
            renderer.sharedMaterial = material;

            Debug.Log($"[CodexTAAValidation] ProceduralSkinned enabled MotionMode={renderer.motionVectorGenerationMode} SkinnedMotion={renderer.skinnedMotionVectors}");
            return new SkinnedValidation(root, upperBone);
        }

        private static void DisableTemporalValidationDistractors()
        {
            var volumes = FindObjectsOfType<Volume>(true);
            for (var volumeIndex = 0; volumeIndex < volumes.Length; ++volumeIndex)
            {
                var profile = volumes[volumeIndex] != null ? volumes[volumeIndex].sharedProfile : null;
                if (profile == null)
                {
                    continue;
                }

                if (profile.TryGet<ScreenSpaceGlobalIlluminationVolumeComponent>(out var gi))
                {
                    gi.active = false;
                }
                if (profile.TryGet<ScreenSpaceReflectionVolumeComponent>(out var ssr))
                {
                    ssr.active = false;
                }
            }
            Debug.Log("[CodexTAAValidation] Procedural skinned isolation disabled GI and SSR volume components.");
        }

        private static bool IsValidationRequested()
        {
            return HasCommandLineFlag(EnableArgument) || HasCommandLineFlag(OutputArgument);
        }

        private static bool HasCommandLineFlag(string flag)
        {
            var arguments = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; ++index)
            {
                if (arguments[index] == flag)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class SkinnedValidation
        {
            public GameObject Root { get; }
            public Transform AnimatedBone { get; }

            public SkinnedValidation(GameObject root, Transform animatedBone)
            {
                Root = root;
                AnimatedBone = animatedBone;
            }
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (var frame = 0; frame < count; ++frame)
            {
                yield return null;
            }
        }

        private static IEnumerator Capture(RenderTexture target, string outputDirectory, string fileName)
        {
            var path = Path.Combine(outputDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            yield return new WaitForEndOfFrame();
            var previousActive = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                Destroy(pixels);
            }

            Debug.Log($"[CodexTAAValidation] Captured {fileName} Exists={File.Exists(path)}");
        }

        private static string ResolveOutputDirectory()
        {
            var arguments = System.Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (arguments[index] == OutputArgument)
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
            }

            return Path.Combine(Application.persistentDataPath, "CodexTAAValidation");
        }

        private static void Exit(int code)
        {
#if UNITY_EDITOR
            // Interactive validation may stop Play mode, but must not close the
            // user's Editor. Process exit is reserved for explicitly run CLI jobs.
            if (IsValidationRequested() && (Application.isBatchMode || HasCommandLineFlag(QuitArgument)))
            {
                UnityEditor.EditorApplication.Exit(code);
            }
            else
            {
                UnityEditor.EditorApplication.ExitPlaymode();
            }
#else
            if (IsValidationRequested())
            {
                Application.Quit(code);
            }
#endif
        }
    }
}
