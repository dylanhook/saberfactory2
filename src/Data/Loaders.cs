using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CustomSaber;
using SaberFactory2.DataStore;
using SaberFactory2.Helpers;
using AssetBundleLoadingTools.Utilities;
using UnityEngine;

namespace SaberFactory2.Loaders
{
    internal abstract class AssetBundleLoader
    {
        public abstract string HandledExtension { get; }
        public abstract ISet<AssetMetaPath> CollectFiles(PluginDirectories dirs);
        public abstract Task<StoreAsset> LoadStoreAssetAsync(string relativePath);
        public abstract Task<StoreAsset> LoadStoreAssetFromBundleAsync(AssetBundle bundle, string saberName);
    }

    internal class AssetMetaPath
    {
        public string Path => File.FullName;
        public bool HasMetaData => !string.IsNullOrEmpty(MetaDataPath) && System.IO.File.Exists(MetaDataPath);
        public string RelativePath => PathTools.ToRelativePath(Path);
        public string RelativeMetaDataPath => PathTools.ToRelativePath(MetaDataPath);
        public readonly FileInfo File;
        public readonly string MetaDataPath;
        public string SubDirName;
        public AssetMetaPath(FileInfo file, string metaDataPath = null)
        {
            File = file;
            MetaDataPath = metaDataPath ?? Path + ".meta";
            SubDirName = PathTools.GetSubDir(RelativePath);
        }
    }

    internal class CustomSaberAssetLoader : AssetBundleLoader
    {
        public override string HandledExtension => ".saber";
        public override ISet<AssetMetaPath> CollectFiles(PluginDirectories dirs)
        {
            var paths = new HashSet<AssetMetaPath>();
            foreach (var path in dirs.CustomSaberDir.EnumerateFiles("*.saber", SearchOption.AllDirectories))
            {
                paths.Add(new AssetMetaPath(path, dirs.Cache.GetFile(path.Name + ".meta").FullName));
            }
            return paths;
        }

        public override async Task<StoreAsset> LoadStoreAssetAsync(string relativePath)
        {
            var fullPath = PathTools.ToFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                return null;
            }
            var result = await Readers.LoadAssetFromAssetBundleAsync<GameObject>(fullPath, "_CustomSaber");
            if (result == null)
            {
                return null;
            }
            RepairShaders(result.Item1);
            return new StoreAsset(relativePath, result.Item1, result.Item2);
        }

        public override async Task<StoreAsset> LoadStoreAssetFromBundleAsync(AssetBundle bundle, string saberName)
        {
            var result = await bundle.LoadAssetFromAssetBundleAsync<GameObject>("_CustomSaber");
            if (result == null)
            {
                return null;
            }
            RepairShaders(result);
            return new StoreAsset("External\\" + saberName, result, bundle);
        }

        private static void RepairShaders(GameObject saberObject)
        {
            try
            {
                var materials = ShaderRepair.GetMaterialsFromGameObjectRenderers(saberObject);
                var trailMaterials = saberObject.GetComponentsInChildren<CustomTrail>(true)
                    .Select(t => t.TrailMaterial)
                    .Where(m => m != null && !materials.Contains(m));
                materials.AddRange(trailMaterials);
                var result = ShaderRepair.FixShadersOnMaterials(materials);
                if (!result.AllShadersReplaced)
                {
                    Plugin.Logger.Warn($"Some shaders could not be repaired: {string.Join(", ", result.MissingShaderNames)}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.Error($"Failed to repair shaders on saber: {ex}");
            }
        }
    }
}