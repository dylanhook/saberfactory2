using System;
using System.Linq;

using IPA.Loader;
using ModestTree;
using SaberFactory2.Configuration;
using SaberFactory2.DataStore;
using SaberFactory2.Editor;
using SaberFactory2.Game;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Instances.Trail;
using SaberFactory2.Misc;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;
using SaberFactory2.Serialization;
using SaberFactory2.UI;
using SaberFactory2.UI.Lib;
using SiraUtil.Sabers;
using Zenject;
using Logger = IPA.Logging.Logger;

namespace SaberFactory2.Installers
{
    internal class PluginAppInstaller : Installer
    {
        private readonly PluginConfig _config;
        private readonly Logger _logger;
        private readonly PluginMetadata _metadata;
        private PluginAppInstaller(Logger logger, PluginConfig config, PluginMetadata metadata)
        {
            _logger = logger;
            _config = config;
            _metadata = metadata;
        }

        public override void InstallBindings()
        {
            if (_config.FirstLaunch)
            {
                _config.FirstLaunch = false;
                _config.RuntimeFirstLaunch = true;
            }
            var rtOptions = new LaunchOptions();
            if (Environment.GetCommandLineArgs().Any(x => x.ToLower() == "fpfc"))
            {
                rtOptions.FPFC = true;
            }
            Container.BindInstance(rtOptions).AsSingle();
            Container.Bind<PluginDirectories>().AsSingle();
            Container.BindInstance(_metadata).WithId(nameof(SaberFactory2)).AsCached();
            Container.BindInstance(_config).AsSingle();
            Serializer.Install(Container);
            Container.Bind<ShaderPropertyCache>().AsSingle();
            Container.Bind<PresetSaveManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<TrailConfig>().AsSingle();
            Container.BindInterfacesAndSelfTo<EmbeddedAssetLoader>().AsSingle();
            Container.Bind<CustomSaberModelLoader>().AsSingle();
            Container.Bind<TextureStore>().AsSingle();
            Container.BindInterfacesAndSelfTo<MainAssetStore>().AsSingle()
                .OnInstantiated<MainAssetStore>(OnMainAssetStoreInstantiated);
            Container.Bind<SaberModel>().WithId(ESaberSlot.Left).AsCached().WithArguments(ESaberSlot.Left);
            Container.Bind<SaberModel>().WithId(ESaberSlot.Right).AsCached().WithArguments(ESaberSlot.Right);
            Container.Bind<SaberInstanceList>().AsSingle();
            Container.Bind<SaberSet>().AsSingle();
            Container.Bind<SaberFileWatcher>().AsSingle();
            Container.Bind<RandomUtil>().AsSingle();
            Container.BindInterfacesAndSelfTo<SaberClashCustomizer>().AsSingle();
            Container.Bind<SaberSettableSettings>().AsSingle();

            InstallFactories();
            InstallMiddlewares();
        }

        private async void OnMainAssetStoreInstantiated(InjectContext ctx, MainAssetStore mainAssetStore)
        {
            await mainAssetStore.LoadAllMetaAsync(_config.AssetType);
        }

        private void InstallMiddlewares()
        {
            Container.Bind<ISaberPostProcessor>().To(typeof(MainSaberPostProcessor)).AsSingle();
        }

        private void InstallFactories()
        {
            Container.BindFactory<StoreAsset, CustomSaberModel, CustomSaberModel.Factory>();
            Container.BindFactory<BasePieceModel, BasePieceInstance, BasePieceInstance.Factory>()
                .FromFactory<InstanceFactory>();
            Container.BindFactory<SaberModel, SaberInstance, SaberInstance.Factory>();
        }
    }

    internal class PluginMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind<SaberFactory2.UI.ViewModels.MainViewModel>().AsSingle();
            Container.Bind<EditorInstanceManager>().AsSingle();
            MainUIInstaller.Install(Container);
            Container.Bind<BaseUiComposition>().To<CustomSaberUiComposition>().AsSingle();
            Container.Bind<SaberGrabController>().AsSingle();
            Container.BindInterfacesAndSelfTo<Editor.Editor>().AsSingle();
            Container.BindInterfacesAndSelfTo<SaberFactoryMenuButton>().AsSingle();
            Container.Bind<TrailPreviewer>().AsSingle();
            Container.Bind<MenuSaberProvider>().AsSingle();
            Container.BindInterfacesAndSelfTo<GizmoAssets>().AsSingle();
        }
    }

    internal class PluginGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            if (Container.TryResolve<PlayerTransforms>() is { } playerTransforms &&
                Container.TryResolve<SaberInstanceList>() is { } saberInstanceList)
            {
                saberInstanceList.PlayerTransforms = playerTransforms;
            }
            var config = Container.Resolve<PluginConfig>();
            if (!config.Enabled || !Plugin.MultiPassEnabled || Container.Resolve<SaberSet>().IsEmpty)
            {
                return;
            }
            Container.BindInterfacesAndSelfTo<EventPlayer>().AsTransient();
            if (!Container.HasBinding<ObstacleSaberSparkleEffectManager>())
            {
                Container.Bind<ObstacleSaberSparkleEffectManager>().FromMethod(ObstanceSaberSparkleEffectManagerGetter).AsSingle();
            }
            Container.BindInterfacesAndSelfTo<GameSaberSetup>().AsSingle();
            Container.BindInstance(SaberModelRegistration.Create<SfSaberModelController>(300));
#if DEBUG && TEST_TRAIL
            if (Container.TryResolve<LaunchOptions>()?.FPFC ?? false)
            {
                var testerInitData = new SaberMovementTester.InitData { CreateTestingSaber = true };
                Container.BindInterfacesAndSelfTo<SaberMovementTester>().AsSingle().WithArguments(testerInitData);
            }
#endif
        }

        private ObstacleSaberSparkleEffectManager ObstanceSaberSparkleEffectManagerGetter(InjectContext ctx)
        {
            var playerSpaceConverter = Container.TryResolve<PlayerSpaceConvertor>();
            Assert.IsNotNull(playerSpaceConverter, $"{nameof(playerSpaceConverter)} was null");
            return playerSpaceConverter.GetComponentInChildren<ObstacleSaberSparkleEffectManager>();
        }
    }

    internal class SaberSettableSettings
    {
        public SettableSettingAbstraction<bool> RelativeTrailMode = new SettableSettingAbstraction<bool>();
        public class SettableSettingAbstraction<T>
        {
            public event Action ValueChanged;
            public T Value
            {
                get => _value;
                set
                {
                    _value = value;
                    ValueChanged?.Invoke();
                }
            }

            private T _value;
        }
    }
}