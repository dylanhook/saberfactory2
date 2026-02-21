using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using SaberFactory2.Loaders;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;
using SiraUtil.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberFactory2.DataStore
{
    internal enum EAssetOrigin
    {
        FileSystem,
        AssetBundle
    }

    internal interface ILoadingTask
    {
        public Task CurrentTask { get; }
    }

    public class MainAssetStore : IDisposable, ILoadingTask
    {
        public List<string> AdditionalCustomSaberFolders { get; } = new List<string>();
        public Task<ModelComposition> this[string path] => GetCompositionByPath(path);
        public Task<ModelComposition> this[PreloadMetaData metaData] => GetCompositionByPath(metaData.AssetMetaPath.RelativePath);
        private readonly PluginConfig _config;
        private readonly CustomSaberAssetLoader _customSaberAssetLoader;
        private readonly CustomSaberModelLoader _customSaberModelLoader;
        private readonly SiraLog _logger;
        private readonly Dictionary<string, PreloadMetaData> _metaData;
        private readonly Dictionary<string, ModelComposition> _modelCompositions;
        private readonly PluginDirectories _pluginDirs;
        private MainAssetStore(
            PluginConfig config,
            SiraLog logger,
            CustomSaberModelLoader customSaberModelLoader,
            PluginDirectories pluginDirs)
        {
            _config = config;
            _logger = logger;
            _pluginDirs = pluginDirs;
            _customSaberAssetLoader = new CustomSaberAssetLoader();
            _customSaberModelLoader = customSaberModelLoader;
            _modelCompositions = new Dictionary<string, ModelComposition>();
            _metaData = new Dictionary<string, PreloadMetaData>();
            foreach (var directory in pluginDirs.CustomSaberDir.GetDirectories("*", SearchOption.AllDirectories))
            {
                var relPath = PathTools.ToRelativePath(directory.FullName);
                relPath = PathTools.CorrectRelativePath(relPath);
                relPath = relPath.Substring(relPath.IndexOf('\\') + 1);
                AdditionalCustomSaberFolders.Add(relPath);
            }
        }

        public void Dispose()
        {
            UnloadAll();
        }

        public Task CurrentTask { get; private set; }
        public async Task<ModelComposition> GetCompositionByPath(string relativePath)
        {
            if (_modelCompositions.TryGetValue(relativePath, out var result))
            {
                return result;
            }
            return await LoadComposition(relativePath);
        }

        public async Task<ModelComposition> GetCompositionByMeta(PreloadMetaData meta)
        {
            return await this[PathTools.ToRelativePath(meta.AssetMetaPath.Path)];
        }

        internal async Task LoadAllMetaAsync(EAssetTypeConfiguration assetType)
        {
            await LoadAllCustomSaberMetaDataAsync();
        }

        public async Task LoadAllCustomSaberMetaDataAsync()
        {
            if (CurrentTask == null)
            {
                CurrentTask = LoadAllMetaDataForLoader(_customSaberAssetLoader, true);
            }
            await CurrentTask;
            CurrentTask = null;
        }

        public IEnumerable<PreloadMetaData> GetAllMetaData()
        {
            return _metaData.Values;
        }

        public IEnumerable<ModelComposition> GetAllCompositions()
        {
            return _modelCompositions.Values;
        }

        public PreloadMetaData GetMetaDataForComposition(ModelComposition comp)
        {
            var path = comp.GetLeft().StoreAsset.RelativePath + ".meta";
            if (_metaData.TryGetValue(path, out var preloadMetaData))
            {
                return preloadMetaData;
            }
            return null;
        }

        public IEnumerable<PreloadMetaData> GetAllMetaData(AssetTypeDefinition assetType)
        {
            return _metaData.Values.Where(x => x.AssetTypeDefinition.Equals(assetType));
        }

        public void UnloadAll()
        {
            foreach (var modelCompositions in _modelCompositions.Values)
            {
                modelCompositions.Dispose();
            }
            _modelCompositions.Clear();
            _metaData.Clear();
        }

        public void Unload(string path)
        {
            if (!_modelCompositions.TryGetValue(path, out var comp))
            {
                return;
            }
            comp.Dispose();
            _modelCompositions.Remove(path);
            _metaData.Remove(path + ".meta");
            SaberFactory2.Core.EventBus.PublishSettingsChanged();
        }

        public async Task Reload(string path)
        {
            Unload(path);
            LoadMetaData(path);
            await LoadComposition(path);
        }

        public async Task ReloadAll()
        {
            UnloadAll();
            await LoadAllCustomSaberMetaDataAsync();
        }

        public void Delete(string path)
        {
            if (_metaData.TryGetValue(path + ".meta", out var meta) && meta.AssetMetaPath.HasMetaData)
            {
                File.Delete(meta.AssetMetaPath.MetaDataPath);
            }
            Unload(path);
            var filePath = PathTools.ToFullPath(path);
            File.Delete(filePath);
        }

        private async Task LoadAllMetaDataForLoader(AssetBundleLoader loader, bool createIfNotExisting = false)
        {
            var sw = DebugTimer.StartNew("Loading Metadata");
            foreach (var assetMetaPath in loader.CollectFiles(_pluginDirs))
            {
                if (_metaData.TryGetValue(assetMetaPath.RelativePath + ".meta", out _))
                {
                    continue;
                }
                if (!assetMetaPath.HasMetaData)
                {
                    if (createIfNotExisting)
                    {
                        var comp = await this[PathTools.ToRelativePath(assetMetaPath.Path)];
                        if (comp == null)
                        {
                            continue;
                        }
                        var metaData = new PreloadMetaData(assetMetaPath, comp, comp.AssetTypeDefinition);
                        metaData.SaveToFile();
                        _metaData.Add(assetMetaPath.RelativePath + ".meta", metaData);
                    }
                }
                else
                {
                    var metaData = new PreloadMetaData(assetMetaPath);
                    metaData.LoadFromFile();
                    metaData.IsFavorite = _config.IsFavorite(PathTools.ToRelativePath(assetMetaPath.Path));
                    _metaData.Add(assetMetaPath.RelativePath + ".meta", metaData);
                }
            }
            sw.Print(_logger);
        }

        internal async Task<ModelComposition> CreateMetaData(AssetMetaPath assetMetaPath)
        {
            var relativePath = assetMetaPath.RelativePath + ".meta";
            if (_metaData.TryGetValue(relativePath, out _))
            {
                return null;
            }
            var comp = await this[PathTools.ToRelativePath(assetMetaPath.Path)];
            if (comp == null)
            {
                return null;
            }
            var metaData = new PreloadMetaData(assetMetaPath, comp, comp.AssetTypeDefinition);
            metaData.SaveToFile();
            _metaData.Add(relativePath, metaData);
            return comp;
        }

        private void LoadMetaData(string pieceRelativePath)
        {
            var assetMetaPath = new AssetMetaPath(new FileInfo(PathTools.ToFullPath(pieceRelativePath)), _pluginDirs.Cache.GetFile(Path.GetFileName(pieceRelativePath) + ".meta").FullName);
            if (_metaData.TryGetValue(assetMetaPath.RelativePath + ".meta", out _))
            {
                return;
            }
            if (!File.Exists(assetMetaPath.MetaDataPath))
            {
                return;
            }
            var metaData = new PreloadMetaData(assetMetaPath);
            metaData.IsFavorite = _config.IsFavorite(assetMetaPath.RelativePath);
            metaData.LoadFromFile();
            _metaData.Add(assetMetaPath.RelativePath + ".meta", metaData);
        }

        private void AddModelComposition(string key, ModelComposition modelComposition)
        {
            _modelCompositions.TryAdd(key, modelComposition);
        }

        private (AssetBundleLoader loader, IStoreAssetParser creator) GetLoaderAndCreatorForCurrentSystem()
        {
            return (_customSaberAssetLoader, _customSaberModelLoader);
        }

        private async Task<ModelComposition> LoadModelCompositionFromFileAsync(string relativeBundlePath)
        {
            var (loader, modelCreator) = GetLoaderAndCreatorForCurrentSystem();
            var storeAsset = await loader.LoadStoreAssetAsync(relativeBundlePath);
            if (storeAsset == null)
            {
                return null;
            }
            var model = modelCreator.GetComposition(storeAsset);
            return model;
        }

        private async Task<ModelComposition> LoadModelCompositionFromBundleAsync(AssetBundle bundle, string saberName)
        {
            if (string.IsNullOrWhiteSpace(saberName))
            {
                _logger.Warn("SaberName needs to be unique and non-empty");
                return null;
            }
            var (loader, modelCreator) = GetLoaderAndCreatorForCurrentSystem();
            var storeAsset = await loader.LoadStoreAssetFromBundleAsync(bundle, saberName);
            if (storeAsset == null)
            {
                return null;
            }
            var model = modelCreator.GetComposition(storeAsset);
            return model;
        }

        private async Task<ModelComposition> LoadComposition(string relativePath)
        {
            var composition = await LoadModelCompositionFromFileAsync(relativePath);
            if (composition != null)
            {
                _modelCompositions.Add(relativePath, composition);
                SaberFactory2.Core.EventBus.PublishSaberLoaded(composition);
            }
            return composition;
        }
    }

    public class StoreAsset
    {
        public readonly AssetBundle AssetBundle;
        public readonly string Extension;
        public readonly string Name;
        public readonly string NameWithoutExtension;
        public readonly string RelativePath;
        public readonly string SubDirName;
        public readonly bool IsStoredOnDisk;
        public GameObject Prefab;
        public StoreAsset(string relativePath, GameObject prefab, AssetBundle assetBundle)
        {
            RelativePath = relativePath;
            IsStoredOnDisk = !relativePath.StartsWith("External");
            Name = Path.GetFileName(RelativePath);
            NameWithoutExtension = Path.GetFileNameWithoutExtension(Name);
            Extension = Path.GetExtension(Name);
            SubDirName = PathTools.GetSubDir(relativePath);
            Prefab = prefab;
            AssetBundle = assetBundle;
        }

        public void Unload()
        {
            AssetBundle.Unload(true);
        }
    }

    internal class TextureAsset : IDisposable
    {
        public Sprite Sprite
        {
            get
            {
                if (_cachedSprite == null)
                {
                    _cachedSprite = CreateSprite();
                }
                return _cachedSprite;
            }
        }

        public bool IsInUse;
        public string Name;
        public EAssetOrigin Origin;
        public string Path;
        public Texture2D Texture;
        private Sprite _cachedSprite;
        public TextureAsset(string name, string path, Texture2D texture, EAssetOrigin origin)
        {
            Name = name;
            Path = path;
            Texture = texture;
            if (name.ToLower().Contains("_clamp"))
            {
                Texture.wrapMode = TextureWrapMode.Clamp;
            }
            Origin = origin;
        }

        public void Dispose()
        {
            Object.Destroy(Texture);
            Object.Destroy(_cachedSprite);
        }

        private Sprite CreateSprite()
        {
            return SaberFactory2.Helpers.TextureUtilities.LoadSpriteFromTexture(Texture);
        }

        public void Dispose(bool forced)
        {
            if (forced)
            {
                Dispose();
            }
            else
            {
                if (!IsInUse)
                {
                    Dispose();
                }
            }
        }
    }

    internal class TextureStore : ILoadingTask
    {
        public Task<TextureAsset> this[string path] => GetTexture(path);
        private readonly Dictionary<string, TextureAsset> _textureAssets;
        private readonly DirectoryInfo _textureDirectory;
        private TextureStore(PluginDirectories pluginDirs)
        {
            _textureAssets = new Dictionary<string, TextureAsset>();
            _textureDirectory = pluginDirs.SaberFactoryDir.CreateSubdirectory("Textures");
        }

        public Task CurrentTask { get; private set; }
        public async Task<TextureAsset> GetTexture(string path)
        {
            return await LoadTexture(path);
        }

        public TextureAsset GetTextureEndsWith(string path)
        {
            return _textureAssets.Values.FirstOrDefault(x => x.Name.EndsWith(path));
        }

        public bool HasTexture(string path)
        {
            return _textureAssets.ContainsKey(path);
        }

        public IEnumerable<TextureAsset> GetAllTextures()
        {
            return _textureAssets.Values;
        }

        public async Task LoadAllTexturesAsync()
        {
            if (CurrentTask is null)
            {
                CurrentTask = LoadAllTexturesAsyncInternal();
            }
            await CurrentTask;
            CurrentTask = null;
        }

        public void UnloadAll()
        {
            foreach (var textureAsset in _textureAssets.Values)
            {
                textureAsset.Dispose();
            }
            _textureAssets.Clear();
        }

        private async Task<TextureAsset> LoadTexture(string path)
        {
            if (HasTexture(path))
            {
                return _textureAssets[path];
            }
            var fullPath = PathTools.ToFullPath(path);
            if (!File.Exists(fullPath))
            {
                return null;
            }
            var tex = await Readers.ReadTexture(path);
            if (!tex)
            {
                return null;
            }
            tex.name = path;
            var texAsset = new TextureAsset(Path.GetFileName(path), path, tex, EAssetOrigin.FileSystem);
            _textureAssets.Add(texAsset.Path, texAsset);
            return texAsset;
        }

        private async Task LoadAllTexturesAsyncInternal()
        {
            foreach (var texFile in _textureDirectory.EnumerateFiles("*.png", SearchOption.AllDirectories))
            {
                await LoadTexture(PathTools.ToRelativePath(texFile.FullName));
            }
        }
    }
}