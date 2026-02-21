using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SaberFactory2.Helpers;
using UnityEngine;
using Zenject;
[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace SaberFactory2.Configuration
{
    internal enum EAssetTypeConfiguration
    {
        None,
        SaberFactory2,
        CustomSaber
    }

    internal class LaunchOptions
    {
        public bool FPFC;
    }

    public abstract class ConfigBase : IInitializable, IDisposable
    {
        [JsonIgnore] public bool Exists => ConfigFile.Exists;
        [JsonIgnore] public bool LoadOnInit = true;
        [JsonIgnore] public bool SaveOnDispose = true;
        protected readonly FileInfo ConfigFile;
        private readonly Dictionary<PropertyInfo, object> _originalValues = new Dictionary<PropertyInfo, object>();
        private bool _didLoadingFail;
        protected ConfigBase(PluginDirectories pluginDirs, string fileName)
        {
            ConfigFile = pluginDirs.SaberFactoryDir.GetFile(fileName);
        }

        public void Dispose()
        {
            if (SaveOnDispose && !_didLoadingFail)
            {
                Save();
            }
        }

        public void Initialize()
        {
            foreach (var propertyInfo in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                _originalValues.Add(propertyInfo, propertyInfo.GetValue(this));
            }
            if (LoadOnInit)
            {
                Load();
            }
        }

        public void Revert()
        {
            foreach (var originalValue in _originalValues)
            {
                originalValue.Key.SetValue(this, originalValue.Value);
            }
        }

        public void Load()
        {
            if (!Exists)
            {
                return;
            }
            try
            {
                JsonConvert.PopulateObject(ConfigFile.ReadText(), this);
            }
            catch (Exception)
            {
                _didLoadingFail = true;
                Debug.LogError($"[Saber Factory 2 Configs] Failed to load config file {ConfigFile.Name}");
            }
        }

        public void Save()
        {
            ConfigFile.WriteText(JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }

    internal class PluginConfig : INotifyPropertyChanged
    {
        private bool _enabled = true;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                OnPropertyChanged();
            }
        }

        public bool FirstLaunch { get; set; } = true;
        public bool EnableEvents { get; set; } = true;
        public bool OverrideSongSaber { get; set; } = false;
        public bool RandomSaber { get; set; } = false;
        public bool AnimateSaberSelection { get; set; } = true;
        public float TrailWidthMax { get; set; } = 1;
        public float GlobalSaberWidthMax { get; set; } = 3;
        public bool ShowAdvancedTrailSettings { get; set; } = false;
        public bool AutoUpdateTrail { get; set; } = true;
        public bool ShowGameplaySettingsButton { get; set; } = true;
        public bool ControlTrailWithThumbstick { get; set; } = true;
        public float SaberAudioVolumeMultiplier { get; set; } = 1;
        [UseConverter(typeof(HexColorConverter))]
        public Color ListCellColor0 { get; set; } = new Color(0.047f, 0.471f, 0.949f);
        [UseConverter(typeof(HexColorConverter))]
        public Color ListCellColor1 { get; set; } = new Color(0.875f, 0.086f, 0.435f);
        public bool ReloadOnSaberUpdate { get; set; } = false;
        public float SwingSoundVolume { get; set; } = 1;

        [Ignore] public int LoadingThreads { get; set; } = 2;
        [UseConverter(typeof(EnumConverter<EAssetTypeConfiguration>))]
        public EAssetTypeConfiguration AssetType { get; set; } = EAssetTypeConfiguration.None;
        [UseConverter(typeof(ListConverter<string>))]
        public List<string> Favorites { get; set; } = new List<string>();
        [Ignore] public bool RuntimeFirstLaunch;
        public void AddFavorite(string path)
        {
            if (!IsFavorite(path))
            {
                Favorites.Add(path);
            }
        }

        public void RemoveFavorite(string path)
        {
            Favorites.Remove(path);
        }

        public bool IsFavorite(string path)
        {
            return Favorites.Contains(path);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public class TrailConfig : ConfigBase
    {
        public int Granularity { get; set; } = 70;
        public int SamplingFrequency { get; set; } = 90;
        public bool OnlyUseVertexColor { get; set; } = true;
        public TrailConfig(PluginDirectories pluginDirs) : base(pluginDirs, "TrailConfig.json")
        { }
    }
}