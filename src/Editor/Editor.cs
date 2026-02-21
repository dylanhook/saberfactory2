using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IPA.Loader;
using SaberFactory2.Configuration;
using SaberFactory2.HarmonyPatches;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Models;
using SaberFactory2.Serialization;
using SaberFactory2.UI.Lib;
using SiraUtil.Logging;
using TMPro;
using Tweening;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.Editor
{
    internal class Editor : IInitializable, IDisposable
    {
        public static Editor Instance;
        public bool IsActive { get; private set; }
        public bool IsSaberInHand
        {
            get => _isSaberInHand;
            set
            {
                _isSaberInHand = value;
                _editorInstanceManager.Refresh();
            }
        }

        private readonly BaseUiComposition _baseUiComposition;
        private readonly EditorInstanceManager _editorInstanceManager;
        private readonly SiraLog _logger;
        private readonly MenuSaberProvider _menuSaberProvider;
        private readonly Pedestal _pedestal;
        private readonly PlayerDataModel _playerDataModel;
        private readonly PluginConfig _pluginConfig;
        private readonly SaberGrabController _saberGrabController;
        private readonly SaberSet _saberSet;
        private readonly SFLogoAnim _sfLogoAnim;
        private bool _isFirstActivation = true;
        private bool _isSaberInHand;
        private SaberInstance _spawnedSaber;
        private readonly PluginMetadata _metaData;
        private readonly TimeTweeningManager _tweeningManager;
        private Editor(
            SiraLog logger,
            PluginConfig pluginConfig,
            BaseUiComposition baseUiComposition,
            EditorInstanceManager editorInstanceManager,
            EmbeddedAssetLoader embeddedAssetLoader,
            SaberSet saberSet,
            PlayerDataModel playerDataModel,
            SaberGrabController saberGrabController,
            MenuSaberProvider menuSaberProvider,
            PluginDirectories pluginDirs,
            [Inject(Id = nameof(SaberFactory2))] PluginMetadata metadata,
            TimeTweeningManager tweeningManager)
        {
            _logger = logger;
            _metaData = metadata;
            _tweeningManager = tweeningManager;
            _pluginConfig = pluginConfig;
            _baseUiComposition = baseUiComposition;
            _editorInstanceManager = editorInstanceManager;
            _saberSet = saberSet;
            _playerDataModel = playerDataModel;
            _saberGrabController = saberGrabController;
            _menuSaberProvider = menuSaberProvider;
            _pedestal = new Pedestal(pluginDirs.SaberFactoryDir.GetFile("pedestal"));
            _sfLogoAnim = new SFLogoAnim(embeddedAssetLoader);
            Instance = this;
            GameplaySetupViewPatch.EntryEnabled = _pluginConfig.ShowGameplaySettingsButton;
        }

        public void Dispose()
        {
            Instance = null;
            _baseUiComposition.OnClosePressed -= Close;
            _pedestal.Destroy();
        }

        public async void Initialize()
        {
            _baseUiComposition.Initialize();
            if (!Plugin.MultiPassEnabled) return;
            var pos = new Vector3(0.3f, 0, 0.9f);
            await _pedestal.Instantiate(pos, Quaternion.Euler(0, 25, 0));
            SetPedestalText(1, "<color=#ffffff70>SF2 v" + _metaData.HVersion + "</color>");
        }

        public async void Open()
        {
            if (IsActive)
            {
                return;
            }
            IsActive = true;
            _baseUiComposition.Open();
            _baseUiComposition.OnClosePressed += Close;
            if (!Plugin.MultiPassEnabled)
            {
                _menuSaberProvider.RequestSaberVisiblity(false);
                return;
            }
            _editorInstanceManager.OnModelCompositionSet += OnModelCompositionSet;
            _pedestal.IsVisible = true;
            _editorInstanceManager.Refresh();
            if (_isFirstActivation && _pluginConfig.RuntimeFirstLaunch)
            {
                await _sfLogoAnim.Instantiate(new Vector3(-1, -0.04f, 2), Quaternion.Euler(0, 45, 0));
                await _sfLogoAnim.PlayAnim();
            }
            _menuSaberProvider.RequestSaberVisiblity(false);
            _isFirstActivation = false;
        }

        public void Close()
        {
            Close(!Plugin.MultiPassEnabled);
        }

        public void Close(bool instant)
        {
            if (!IsActive)
            {
                return;
            }
            IsActive = false;
            if (Plugin.MultiPassEnabled)
            {
                _editorInstanceManager.SyncSabers();
                _editorInstanceManager.OnModelCompositionSet -= OnModelCompositionSet;
                _editorInstanceManager.DestroySaber();
                _spawnedSaber?.Destroy();
                _pedestal.IsVisible = false;
            }
            _baseUiComposition.Close(instant);
            _baseUiComposition.OnClosePressed -= Close;
            _saberGrabController.ShowHandle();
            _menuSaberProvider.RequestSaberVisiblity(true);
        }

        public void SetPedestalText(int line, string text)
        {
            _pedestal.SetText(line, text);
        }

        public void FlashPedestal(Color color)
        {
            _tweeningManager.KillAllTweens(_pedestal.SaberContainerTransform);
            _tweeningManager.AddTween(new FloatTween(1, 0, f =>
            {
                _pedestal.SetLedColor(color.ColorWithAlpha(f));
            }, 1, EaseType.InCubic), _pedestal.SaberContainerTransform);
            _pedestal.InitSpiral();
            _tweeningManager.AddTween(new FloatTween(-1, 1, f =>
            {
                _pedestal.SetSpiralLength(f);
            }, 1, EaseType.OutCubic), _pedestal.SaberContainerTransform);
        }

        private async void OnModelCompositionSet(ModelComposition composition)
        {
            _spawnedSaber?.Destroy();
            var parent = IsSaberInHand ? _saberGrabController.GrabContainer : _pedestal.SaberContainerTransform;
            _spawnedSaber = _editorInstanceManager.CreateSaber(_saberSet.LeftSaber, parent);
            if (IsSaberInHand)
            {
                _spawnedSaber.CreateTrail(true);
                _saberGrabController.HideHandle();
            }
            else
            {
                _saberGrabController.ShowHandle();
            }
            _spawnedSaber.SetColor(_playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme().saberAColor);
            _editorInstanceManager.RaiseSaberCreatedEvent();
            _editorInstanceManager.RaisePieceCreatedEvent();
            SaberFactory2.Core.EventBus.PublishSaberPreviewInstantiated(_spawnedSaber);
            await Task.Yield();
            if (_pluginConfig.AnimateSaberSelection && !IsSaberInHand)
            {
                await AnimationHelper.AsyncAnimation(0.3f, CancellationToken.None, t => { parent.localScale = new Vector3(t, t, t); });
            }
        }
    }

    internal class EditorInstanceManager
    {
        public AssetTypeDefinition SelectedDefinition { get; }
        public SaberInstance CurrentSaber { get; private set; }
        public BasePieceInstance CurrentPiece { get; private set; }
        public ModelComposition CurrentModelComposition { get; private set; }
        private readonly SiraLog _logger;
        private readonly SaberInstance.Factory _saberFactory;
        private readonly SaberSet _saberSet;
        public EditorInstanceManager(SiraLog logger, SaberSet saberSet, PresetSaveManager presetSaveManager, SaberInstance.Factory saberFactory)
        {
            _logger = logger;
            _saberSet = saberSet;
            _saberFactory = saberFactory;
            SelectedDefinition = AssetTypeDefinition.CustomSaber;
            presetSaveManager.OnSaberLoaded += delegate
            {
                if (saberSet.LeftSaber.GetCustomSaber(out var customsaber))
                {
                    SetModelComposition(customsaber.ModelComposition, false);
                }
            };

            SaberFactory2.Core.EventBus.OnPreviewSaberChanged += composition => SetModelComposition(composition, false);
            SaberFactory2.Core.EventBus.OnSaberWidthChanged += width =>
            {
                CurrentSaber?.SetSaberWidth(width);
            };
        }

        public event Action<SaberInstance> OnSaberInstanceCreated;
        public event Action<BasePieceInstance> OnPieceInstanceCreated;
        public event Action<ModelComposition> OnModelCompositionSet;
        public void SetModelComposition(ModelComposition composition, bool lazyInit = true)
        {
            if (CurrentModelComposition != null)
            {
                CurrentModelComposition.SaveAdditionalData();
                CurrentModelComposition.DestroyAdditionalInstances();
            }
            if (lazyInit && CurrentModelComposition != composition)
            {
                composition?.LazyInit();
            }
            CurrentModelComposition = composition;
            if (CurrentModelComposition != null)
            {
                _saberSet.SetModelComposition(CurrentModelComposition);
                OnModelCompositionSet?.Invoke(CurrentModelComposition);
                _logger.Info($"Selected Saber: {composition?.ListName}");
            }
        }

        public void Refresh()
        {
            if (CurrentModelComposition == null)
            {
                return;
            }
            SetModelComposition(CurrentModelComposition);
        }

        public void SyncSabers()
        {
            if (CurrentSaber == null)
            {
                return;
            }
            _saberSet.Sync(CurrentSaber.Model);
        }

        public SaberInstance CreateSaber(SaberModel model, Transform parent, bool raiseSaberEvent = false, bool raisePieceEvent = false)
        {
            CurrentSaber = _saberFactory.Create(model);
            if (parent != null)
            {
                CurrentSaber.SetParent(parent);
            }
            if (raiseSaberEvent)
            {
                RaiseSaberCreatedEvent();
            }
            CurrentPiece = GetPiece(SelectedDefinition);
            if (raisePieceEvent)
            {
                RaisePieceCreatedEvent();
            }

            return CurrentSaber;
        }

        public void RaiseSaberCreatedEvent()
        {
            if (CurrentSaber is null)
            {
                return;
            }
            OnSaberInstanceCreated?.Invoke(CurrentSaber);
        }

        public void RaisePieceCreatedEvent()
        {
            if (CurrentPiece is null)
            {
                return;
            }
            OnPieceInstanceCreated?.Invoke(CurrentPiece);
        }

        public void DestroySaber()
        {
            CurrentModelComposition?.DestroyAdditionalInstances();
            CurrentSaber?.Destroy();
            CurrentSaber = null;
            CurrentPiece = null;
        }

        public SaberInstance CreateTempSaber(SaberModel model)
        {
            return _saberFactory.Create(model);
        }

        public BasePieceInstance GetPiece(AssetTypeDefinition definition)
        {
            if (CurrentSaber == null)
            {
                return null;
            }
            if (CurrentSaber.PieceCollection.TryGetPiece(definition, out var piece))
            {
                return piece;
            }
            return null;
        }
    }

    internal class Pedestal
    {
        private static readonly string PedestalPath = String.Join(".", nameof(SaberFactory2), "Resources", "pedestal");
        public bool IsVisible
        {
            set
            {
                if (_rootTransform)
                {
                    _rootTransform.gameObject.SetActive(value);
                }
            }
        }

        public Vector3 Position
        {
            get => _rootTransform.position;
            set => _rootTransform.position = value;
        }

        public Transform SaberContainerTransform { get; private set; }
        private readonly FileInfo _customPedestalFile;
        private Transform _rootTransform;
        private TextMeshPro _textMeshPro;
        private Material _ledMat;
        private Material _spiralMat;
        private readonly string[] _lines = new string[3];
        private static readonly int LedColor = Shader.PropertyToID("_LedColor");
        private static readonly int Length = Shader.PropertyToID("_Length");
        public Pedestal(FileInfo customPedestalFile)
        {
            _customPedestalFile = customPedestalFile;
        }

        public async Task Instantiate(Vector3 pos, Quaternion rot)
        {
            if (_rootTransform)
            {
                return;
            }
            _rootTransform = new GameObject("Pedestal Container").transform;
            var prefab = await GetPedestalAsset();
            if (!prefab)
            {
                return;
            }
            var instantiated = Object.Instantiate(prefab, _rootTransform, false);
            _textMeshPro = instantiated.GetComponentsInChildren<TextMeshPro>()
                .FirstOrDefault(x => x.name == "Pedestal_Display");
            var leds = instantiated.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(x => x.name == "Leds");
            var spiral = instantiated.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(x => x.name == "Spiral");
            if (_textMeshPro)
            {
                _textMeshPro.alignment = TextAlignmentOptions.Center;
                _textMeshPro.font = UITemplateCache.GetMainFont();
                if (_textMeshPro.font != null)
                {
                    _textMeshPro.fontSharedMaterial = _textMeshPro.font.material;
                }
            }
            if (leds)
            {
                _ledMat = leds.sharedMaterial;
            }
            if (spiral)
            {
                _spiralMat = spiral.sharedMaterial;
                var clr = _spiralMat.color;
                clr.a = 0;
                _spiralMat.color = clr;
            }
            SaberContainerTransform = _rootTransform.CreateGameObject("SaberContainer").transform;
            SaberContainerTransform.localPosition += new Vector3(0, 1, 0);
            SaberContainerTransform.localEulerAngles = new Vector3(-90, 0, 0);
            _rootTransform.SetPositionAndRotation(pos, rot);
            IsVisible = false;
        }

        public void SetText(int line, string text)
        {
            if (!_textMeshPro)
            {
                return;
            }
            _lines[line] = text;
            _textMeshPro.text = string.Join("\n", _lines);
        }

        private async Task<GameObject> GetPedestalAsset()
        {
            if (_customPedestalFile.Exists)
            {
                try
                {
                    var customPedestal = await Readers.LoadAssetFromAssetBundleAsync<GameObject>(_customPedestalFile.FullName, "Pedestal");
                    customPedestal.Item2.Unload(false);
                    return customPedestal.Item1;
                }
                catch (Exception e)
                {
                    Debug.LogError("Couldn't load custom pedestal: \n" + e);
                }
            }
            var data = await Readers.ReadResourceAsync(PedestalPath);
            var bundle = await Readers.LoadAssetFromAssetBundleAsync<GameObject>(data, "Pedestal");
            bundle.Item2.Unload(false);
            return bundle.Item1;
        }

        public void Destroy()
        {
            if (_rootTransform != null)
            {
                _rootTransform.gameObject.TryDestroy();
            }
        }

        public void SetLedColor(Color color)
        {
            if (!_ledMat)
            {
                return;
            }
            _ledMat.SetColor(LedColor, color);
        }

        public void SetSpiralLength(float length)
        {
            _spiralMat.SetFloat(Length, length);
            if (length > 0.99f)
            {
                var clr = _spiralMat.color;
                clr.a = 0;
                _spiralMat.color = clr;
            }
        }

        public void InitSpiral()
        {
            var clr = _spiralMat.color;
            clr.a = 1;
            _spiralMat.color = clr;
        }
    }

    internal class SFLogoAnim
    {
        private readonly EmbeddedAssetLoader _embeddedAssetLoader;
        private Animator _animator;
        private GameObject _instance;
        public SFLogoAnim(EmbeddedAssetLoader embeddedAssetLoader)
        {
            _embeddedAssetLoader = embeddedAssetLoader;
        }

        public async Task Instantiate(Vector3 pos, Quaternion rot)
        {
            if (_instance)
            {
                return;
            }
            var prefab = await _embeddedAssetLoader.LoadAsset<GameObject>("SFLogoAnimObject");
            if (!prefab)
            {
                return;
            }
            _instance = Object.Instantiate(prefab, pos, rot);
            _animator = _instance.GetComponent<Animator>();
            _instance.SetActive(false);
        }

        public async Task PlayAnim()
        {
            if (!_instance)
            {
                return;
            }
            _instance.SetActive(true);
            _animator.speed = 0.2f;
            _animator.Play("Anim");
            await Task.Delay(2800);
            var scale = 1f;
            while (scale > 0.01f)
            {
                scale -= 0.05f;
                _instance.transform.localScale = new Vector3(scale, scale, scale);
                await Task.Delay(10);
            }
            Destroy();
        }

        public void Destroy()
        {
            _instance.TryDestroy();
        }
    }

    internal class SaberGrabController
    {
        public readonly Transform GrabContainer;
        private readonly MenuPlayerController _menuPlayerController;
        private SaberInstance _currentSaberInstancce;
        private bool _isHandleVisisble = true;
        public SaberGrabController(MenuPlayerController menuPlayerController)
        {
            _menuPlayerController = menuPlayerController;
            GrabContainer = new GameObject("SaberGrabContainer").transform;
            var parentTransform = menuPlayerController.leftController.transform.Find("MenuHandle") ?? menuPlayerController.leftController.transform;
            GrabContainer.SetParent(parentTransform, false);
        }

        public void GrabSaber(SaberInstance saberInstance)
        {
            HideHandle();
            _currentSaberInstancce = saberInstance;
            saberInstance.SetParent(GrabContainer);
        }

        public void ShowHandle()
        {
            if (_isHandleVisisble)
            {
                return;
            }
            _isHandleVisisble = true;
            SetHandleRenderers(true);
        }

        public void HideHandle()
        {
            if (!_isHandleVisisble)
            {
                return;
            }
            _isHandleVisisble = false;
            SetHandleRenderers(false);
        }

        private void SetHandleRenderers(bool visible)
        {
            if (_menuPlayerController != null && _menuPlayerController.leftController.transform.Find("MenuHandle") is { } handle)
            {
                var names = new[] { "Glowing", "Normal", "FakeGlow0", "FakeGlow1" };
                foreach (var childName in names)
                {
                    if (handle.Find(childName) is { } child && child.GetComponent<MeshRenderer>() is { } renderer)
                    {
                        renderer.enabled = visible;
                    }
                }
            }
        }
    }
}