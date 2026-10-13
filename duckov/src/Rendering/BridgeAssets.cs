using System;
using System.IO;
using UnityEngine;

namespace DuckovCraft.Rendering
{
    internal sealed class BridgeAssets : IDisposable
    {
        private readonly AssetBundle bundle;
        public readonly Material Solid, Transparent, Overlay;

        public BridgeAssets(string directory)
        {
            bundle = AssetBundle.LoadFromFile(Path.Combine(directory, "duckovcraft-assets"));
            if (bundle == null) throw new InvalidOperationException("Cannot load duckovcraft-assets beside DuckovCraft.dll.");
            Solid = bundle.LoadAsset<Material>("Assets/Materials/WorldSolid.mat");
            Transparent = bundle.LoadAsset<Material>("Assets/Materials/WorldTransparent.mat");
            Overlay = bundle.LoadAsset<Material>("Assets/Materials/Overlay.mat");
            foreach (var material in new[] { Solid, Transparent, Overlay })
                if (material == null || material.shader == null || !material.shader.isSupported)
                    throw new InvalidOperationException("A DuckovCraft material is missing or unsupported on this renderer.");
        }

        public void Dispose() { if (bundle != null) bundle.Unload(true); }
    }
}
