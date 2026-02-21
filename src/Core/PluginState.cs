using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IPA.Utilities.Async;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Models;
using SiraUtil.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberFactory2
{
    public class MenuSaberProvider
    {
        private readonly SaberInstance.Factory _saberInstanceFactory;
        private readonly SaberSet _saberSet;
        internal MenuSaberProvider(SaberInstance.Factory saberInstanceFactory, SaberSet saberSet)
        {
            _saberInstanceFactory = saberInstanceFactory;
            _saberSet = saberSet;
        }

        public event Action<bool> OnSaberVisibilityRequested;
        public async Task<SaberInstance> CreateSaber(Transform parent, SaberType saberType, Color color, bool createTrail)
        {
            await _saberSet.WaitForFinish();
            var saberModel = saberType == SaberType.SaberA ? _saberSet.LeftSaber : _saberSet.RightSaber;
            var saber = _saberInstanceFactory.Create(saberModel);
            saber.SetParent(parent);
            if (createTrail)
            {
                saber.CreateTrail(true);
            }
            saber.SetColor(color);
            return saber;
        }

        internal void RequestSaberVisiblity(bool visible)
        {
            OnSaberVisibilityRequested?.Invoke(visible);
        }
    }

    public class SaberFileWatcher
    {
        private const string Filter = "*.saber";
        public bool IsWatching { get; private set; }
        private readonly DirectoryInfo _dir;
        private FileSystemWatcher _watcher;
        public SaberFileWatcher(PluginDirectories dirs)
        {
            _dir = dirs.CustomSaberDir;
        }

        public event Action<string> OnSaberUpdate;
        public void Watch()
        {
            if (_watcher != null)
            {
                StopWatching();
            }
            _watcher = new FileSystemWatcher(_dir.FullName, Filter);
            _watcher.NotifyFilter = NotifyFilters.LastWrite;
            _watcher.Changed += WatcherOnCreated;
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
            IsWatching = true;
        }

        private void WatcherOnCreated(object sender, FileSystemEventArgs e)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var runner = new GameObject("SF_FileWatchRunner");
                var component = runner.AddComponent<CoroutineRunner>();
                component.StartCoroutine(Initiate(e.FullPath, runner));
            });
        }

        private IEnumerator Initiate(string filename, GameObject runner)
        {
            var seconds = 0f;
            while (seconds < 10)
            {
                if (File.Exists(filename))
                {
                    yield return new WaitForSeconds(0.5f);
                    OnSaberUpdate?.Invoke(filename);
                    Object.Destroy(runner);
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
                seconds += 0.5f;
            }
            Object.Destroy(runner);
        }

        public void StopWatching()
        {
            if (_watcher is null)
            {
                return;
            }
            _watcher.Changed -= WatcherOnCreated;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            IsWatching = false;
        }
    }

    internal class EmbeddedAssetLoader : IDisposable
    {
        public static readonly string BUNDLE_PATH = "SaberFactory2.Resources.assets";
        private readonly SiraLog _logger;
        private AssetBundle _assetBundle;
        private Task<bool> _loadingTask;
        private EmbeddedAssetLoader(SiraLog logger)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            if (_assetBundle)
            {
                _assetBundle.Unload(true);
            }
        }

        public async Task<T> LoadAsset<T>(string name) where T : Object
        {
            if (!await CheckLoaded())
            {
                return null;
            }
            return await _assetBundle.LoadAssetFromAssetBundleAsync<T>(name);
        }

        public async Task<List<T>> LoadAssets<T>(params string[] names) where T : Object
        {
            if (!await CheckLoaded())
            {
                return null;
            }
            var assets = new List<T>();
            foreach (var name in names)
            {
                var asset = await _assetBundle.LoadAssetFromAssetBundleAsync<T>(name);
                if (asset)
                {
                    assets.Add(asset);
                }
            }
            return assets;
        }

        private async Task<bool> CheckLoaded()
        {
            if (_assetBundle)
            {
                return true;
            }
            _loadingTask ??= LoadBundle();
            await _loadingTask;
            return true;
        }

        private async Task<bool> LoadBundle()
        {
            var data = await Readers.ReadResourceAsync(BUNDLE_PATH);
            if (data == null)
            {
                _logger.Error($"Resource at {BUNDLE_PATH} doesn't exist");
                return false;
            }
            _assetBundle = await Readers.LoadAssetBundleAsync(data);
            if (_assetBundle == null)
            {
                _logger.Error("Couldn't load embedded AssetBundle");
                return false;
            }
#if false
            foreach (var assetName in _assetBundle.GetAllAssetNames())
            {
                var obj = _assetBundle.LoadAsset(assetName);
            }
#endif
            return true;
        }
    }
}