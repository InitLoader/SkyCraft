using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildBridgeAssets
{
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Materials");
        CreateMaterial("WorldSolid", "SulfurCraft/World", false);
        CreateMaterial("WorldTransparent", "SulfurCraft/World", true);
        CreateMaterial("Overlay", "SulfurCraft/Overlay", true);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../build/assets");
        var bundle = new AssetBundleBuild
        {
            assetBundleName = "sulfurcraft-assets",
            assetNames = new[] { "Assets/Materials/WorldSolid.mat", "Assets/Materials/WorldTransparent.mat", "Assets/Materials/Overlay.mat" }
        };
        BuildPipeline.BuildAssetBundles("../build/assets", new[] { bundle }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
    }

    private static void CreateMaterial(string name, string shaderName, bool transparent)
    {
        string path = "Assets/Materials/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException("Missing shader " + shaderName);
        var material = new Material(shader) { name = name };
        if (shaderName == "SulfurCraft/World" && transparent)
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cutoff", 0.01f);
            material.renderQueue = 3000;
        }
        AssetDatabase.CreateAsset(material, path);
    }
}
