using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Tests
{
    public sealed class BurtRenderGraphCompilerTests
    {
        [Test]
        public void CompileBuildsRawWarAndWawDependencies()
        {
            var target = new BurtRenderTargetHandle("A", new RenderTargetIdentifier(101));
            var usages = new List<BurtRenderPassResourceUsage>
            {
                CreateUsage(0, "Write A", write: target),
                CreateUsage(1, "Read A", read: target),
                CreateUsage(2, "Rewrite A", write: target),
            };

            var result = Compile(usages, null);

            CollectionAssert.AreEqual(new[] { 0 }, result.Dependencies[1], "RAW dependency was not recorded.");
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result.Dependencies[2], "WAR/WAW dependencies were not recorded.");
            Assert.AreEqual(3, result.DependencyCount);
        }

        [Test]
        public void CompileCullsDeadBranchAndKeepsTerminalWriteChain()
        {
            var dead = new BurtRenderTargetHandle("Dead", new RenderTargetIdentifier(201));
            var intermediate = new BurtRenderTargetHandle("Intermediate", new RenderTargetIdentifier(202));
            var output = new BurtRenderTargetHandle("Output", new RenderTargetIdentifier(203));
            var usages = new List<BurtRenderPassResourceUsage>
            {
                CreateCullableUsage(0, "Dead producer", write: dead),
                CreateCullableUsage(1, "Live producer", write: intermediate),
                CreateCullableUsage(2, "Terminal output", read: intermediate, write: output),
            };
            usages[2].AllowUnconsumedWriteResource(output.Name);

            var result = Compile(usages, null);

            Assert.IsFalse(result.ShouldExecute(0));
            Assert.IsTrue(result.ShouldExecute(1));
            Assert.IsTrue(result.ShouldExecute(2));
            Assert.AreEqual(1, result.CulledPassCount);
        }

        [Test]
        public void RegistryRejectsStaleVersionedHandle()
        {
            var resources = new BurtRenderGraphResourceRegistry();
            var stale = resources.RegisterRenderTarget("Versioned", new RenderTargetIdentifier(301));
            var current = resources.RegisterRenderTarget("Versioned", new RenderTargetIdentifier(302));

            Assert.IsFalse(resources.IsCurrent(stale));
            Assert.IsTrue(resources.IsCurrent(current));
            Assert.Greater(current.Version, stale.Version);
        }

        [Test]
        public void CompileReusesAliasSlotForNonOverlappingBuffers()
        {
            var bufferA = new BurtRenderBufferHandle("A");
            var bufferB = new BurtRenderBufferHandle("B");
            var usages = new List<BurtRenderPassResourceUsage>
            {
                CreateBufferUsage(0, "Write A", write: bufferA),
                CreateBufferUsage(1, "Read A", read: bufferA),
                CreateBufferUsage(2, "Write B", write: bufferB),
                CreateBufferUsage(3, "Read B", read: bufferB),
            };

            var result = Compile(usages, null);
            var lifetimeA = FindLifetime(result, "BUF:A");
            var lifetimeB = FindLifetime(result, "BUF:B");

            Assert.NotNull(lifetimeA);
            Assert.NotNull(lifetimeB);
            Assert.AreEqual(lifetimeA.AliasSlot, lifetimeB.AliasSlot);
        }

        [Test]
        public void RegistryReusesReleasedRenderTextureWithMatchingDescriptor()
        {
            var resources = new BurtRenderGraphResourceRegistry();
            try
            {
                resources.RegisterRenderTarget("Pooled", new RenderTargetIdentifier(401));
                var descriptor = new RenderTextureDescriptor(64, 64, RenderTextureFormat.ARGB32, 0);
                resources.SetRenderTargetDescriptor("Pooled", descriptor, FilterMode.Bilinear, "Pooled Test");
                resources.AllocateRenderTarget("Pooled");
                Assert.IsTrue(resources.TryGetAllocatedRenderTexture("Pooled", out var first));

                resources.ReleaseRenderTarget("Pooled");
                Assert.IsFalse(resources.IsRenderTargetAllocated("Pooled"));
                resources.AllocateRenderTarget("Pooled");
                Assert.IsTrue(resources.TryGetAllocatedRenderTexture("Pooled", out var second));

                Assert.AreSame(first, second);
            }
            finally
            {
                resources.DisposeResources();
            }
        }

        [Test]
        public void RegistryDoesNotAliasIncompatibleRenderTextureDescriptors()
        {
            var resources = new BurtRenderGraphResourceRegistry();
            try
            {
                resources.RegisterRenderTarget("Pooled", new RenderTargetIdentifier(402));
                var descriptor = new RenderTextureDescriptor(64, 64, RenderTextureFormat.ARGB32, 0);
                resources.SetRenderTargetDescriptor("Pooled", descriptor, FilterMode.Bilinear);
                resources.AllocateRenderTarget("Pooled");
                Assert.IsTrue(resources.TryGetAllocatedRenderTexture("Pooled", out var first));

                resources.ReleaseRenderTarget("Pooled");
                descriptor.width = 128;
                resources.SetRenderTargetDescriptor("Pooled", descriptor, FilterMode.Bilinear);
                resources.AllocateRenderTarget("Pooled");
                Assert.IsTrue(resources.TryGetAllocatedRenderTexture("Pooled", out var second));

                Assert.AreNotSame(first, second);
            }
            finally
            {
                resources.DisposeResources();
            }
        }

        private static BurtRenderGraph.BurtRenderGraphCompileResult Compile(
            IReadOnlyList<BurtRenderPassResourceUsage> usages,
            BurtRenderGraphResourceRegistry resources)
        {
            var passes = new List<BurtRenderPass>(usages.Count);
            for (var passIndex = 0; passIndex < usages.Count; passIndex++)
            {
                passes.Add(null);
            }

            return new BurtRenderGraph.BurtRenderGraphCompiler().Compile(passes, usages, resources);
        }

        private static BurtRenderPassResourceUsage CreateUsage(
            int passIndex,
            string name,
            BurtRenderTargetHandle read = default,
            BurtRenderTargetHandle write = default)
        {
            var usage = new BurtRenderPassResourceUsage(passIndex, name);
            AddAccesses(usage, read, write);
            return usage;
        }

        private static BurtRenderPassResourceUsage CreateCullableUsage(
            int passIndex,
            string name,
            BurtRenderTargetHandle read = default,
            BurtRenderTargetHandle write = default)
        {
            var usage = new BurtRenderPassResourceUsage(
                passIndex,
                name,
                BurtRenderPassKind.Generic,
                false,
                true);
            AddAccesses(usage, read, write);
            return usage;
        }

        private static BurtRenderPassResourceUsage CreateBufferUsage(
            int passIndex,
            string name,
            BurtRenderBufferHandle read = default,
            BurtRenderBufferHandle write = default)
        {
            var usage = new BurtRenderPassResourceUsage(passIndex, name);
            if (read.IsValid)
            {
                usage.AddReadBuffer(read);
            }

            if (write.IsValid)
            {
                usage.AddWriteBuffer(write);
            }

            return usage;
        }

        private static BurtRenderGraph.BurtRenderGraphResourceLifetime FindLifetime(
            BurtRenderGraph.BurtRenderGraphCompileResult result,
            string resourceKey)
        {
            for (var index = 0; index < result.ResourceLifetimes.Count; index++)
            {
                var lifetime = result.ResourceLifetimes[index];
                if (lifetime.ResourceKey == resourceKey)
                {
                    return lifetime;
                }
            }

            return null;
        }

        private static void AddAccesses(
            BurtRenderPassResourceUsage usage,
            BurtRenderTargetHandle read,
            BurtRenderTargetHandle write)
        {
            if (read.IsValid)
            {
                usage.AddReadRenderTarget(read);
            }

            if (write.IsValid)
            {
                usage.AddWriteRenderTarget(write);
            }
        }
    }
}
