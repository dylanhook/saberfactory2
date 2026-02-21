using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Loader;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using SaberFactory2.Installers;
using SiraUtil.Zenject;
using UnityEngine.XR.OpenXR;
using IPALogger = IPA.Logging.Logger;

namespace SaberFactory2
{
    [Plugin(RuntimeOptions.SingleStartInit), NoEnableDisable]
    public class Plugin
    {
        private const string HarmonyId = "com.dylan.saberfactory2";
        private Harmony _harmony;
        private PluginConfig _config;
        public static IPALogger Logger { get; private set; }
        public static bool MultiPassEnabled
        {
            get
            {
                try
                {
                    if (Environment.GetCommandLineArgs().Any(x => x.Equals("fpfc", StringComparison.OrdinalIgnoreCase)))
                        return true;
                    var openXrSettings = OpenXRSettings.Instance;
                    return openXrSettings != null && openXrSettings.renderMode == OpenXRSettings.RenderMode.MultiPass;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        [Init]
        public async void Init(IPALogger logger, Config conf, Zenjector zenjector, PluginMetadata metadata)
        {
            Logger = logger;
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            _config = conf.Generated<PluginConfig>();

            if (!MultiPassEnabled)
            {
                Logger.Warn("Multi-pass rendering is not enabled. Saber Factory 2 requires multi-pass rendering to function. Enable it in Mod Settings → Asset Bundles.");
            }

            if (!await LoadCsDescriptors())
            {
                return;
            }
            zenjector.UseLogger(logger);
            zenjector.Install<PluginAppInstaller>(Location.App, logger, _config, metadata);
            zenjector.Install<PluginMenuInstaller>(Location.Menu);
            zenjector.Install<PluginGameInstaller>(Location.Player | Location.MultiPlayer);
        }

        private async Task<bool> LoadCsDescriptors()
        {
            try
            {
                Assembly.Load(await Readers.ReadResourceAsync("SaberFactory2.Resources.CustomSaberComponents.dll"));
                return true;
            }
            catch (Exception)
            {
                Logger.Info("Couldn't load custom saber descriptors");
                return false;
            }
        }

        [OnEnable]
        public void OnEnable()
        {
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += MainMenuInitializing;
        }

        [OnDisable]
        public void OnDisable()
        {
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing -= MainMenuInitializing;
        }

        private void MainMenuInitializing()
        {
            BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance.AddSettingsMenu("Saber Factory 2", "SaberFactory2.UI.BSML.SaberFactorySettings.bsml", _config);
        }
    }
}