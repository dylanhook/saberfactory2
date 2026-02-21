using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using CustomSaber;
using HMUI;
using SaberFactory2.Configuration;
using SaberFactory2.DataStore;
using SaberFactory2.Editor;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Instances.Trail;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;
using SaberFactory2.Modifiers;
using SaberFactory2.UI.CustomSaber.CustomComponents;
using SaberFactory2.UI.CustomSaber.Popups;
using SaberFactory2.UI.Lib;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SaberFactory2.UI.CustomSaber.Views
{
    internal class MainModifierPanelView : SubView, INavigationCategoryView
    {
        public ENavigationCategory Category => ENavigationCategory.Modifier;
        [UIObject("container")] private readonly GameObject _container = null;
        [UIComponent("component-list")] private readonly SaberFactory2.UI.Lib.CustomListTableData _componentList = null;
        [Inject] private readonly BsmlDecorator _decorator = null;
        [Inject] private readonly EditorInstanceManager _instanceManager = null;
        [Inject] private readonly GizmoAssets _gizmoAssets = null;
        private bool IsNotCustomizable { get => _isNotCustomizable; set => SetProperty(ref _isNotCustomizable, value); }

        protected override string _resourceName => "SaberFactory2.UI.CustomSaber.Views.Modifiers.MainModifierPanelView.bsml";
        private ModifyableComponentManager _modifyableComponentManager;
        private bool IsAvailable => _modifyableComponentManager?.IsAvailable ?? false;
        private List<BaseModifierImpl> _items;
        private BaseModifierImpl _currentItem;
        private bool _isNotCustomizable;
        public override void DidOpen()
        {
#pragma warning disable CS0162
            _modifyableComponentManager = _instanceManager.CurrentPiece?.Model.ModifyableComponentManager;
            if (IsAvailable)
            {
                IsNotCustomizable = false;
                _gizmoAssets.Activate();
                SetupMod();
            }
            else
            {
                IsNotCustomizable = true;
            }
#pragma warning restore CS0162
        }

        public override void DidClose()
        {
#pragma warning disable CS0162
            _modifyableComponentManager = null;
            _componentList.Data = new List<SaberFactory2.UI.Lib.CustomListTableData.CustomCellInfo>();
            _componentList.TableView.ReloadData();
            ClearCurrentView();
            GizmoDrawer.Deactivate();
#pragma warning restore CS0162
        }

        public void SetupMod()
        {
            var list = new List<SaberFactory2.UI.Lib.CustomListTableData.CustomCellInfo>();
            _items = _modifyableComponentManager.GetAllMods();
            foreach (var mod in _items)
            {
                list.Add(new SaberFactory2.UI.Lib.CustomListTableData.CustomCellInfo(mod.Name, mod.TypeName));
            }
            _componentList.Data = list;
            _componentList.TableView.ReloadData();
            if (_items.Count > 0)
            {
                _componentList.TableView.SelectCellWithIdx(0, true);
            }
        }

        private void ClearCurrentView()
        {
            for (var i = 0; i < _container.transform.childCount; i++)
            {
                Destroy(_container.transform.GetChild(0).gameObject);
            }
        }

        [UIAction("component-selected")]
        private void ComponentSelected(TableView table, int idx)
        {
            if (!IsAvailable)
            {
                return;
            }
            _currentItem = _items[idx];
            ClearCurrentView();
            _currentItem.ParserParams = _decorator.ParseFromString(_currentItem.DrawUi(), _container, _currentItem);
            _currentItem.WasSelected();
        }

        [UIAction("reset-click")]
        private void ResetClick()
        {
            if (!IsAvailable)
            {
                return;
            }
            if (_modifyableComponentManager is null)
            {
                return;
            }
            _modifyableComponentManager.Reset(_currentItem.Id);
            ReloadSaber();
        }

        [UIAction("reset-all-click")]
        private void ResetAllClick()
        {
            if (!IsAvailable)
            {
                return;
            }
            if (_modifyableComponentManager is null)
            {
                return;
            }
            _modifyableComponentManager.ResetAll();
            ReloadSaber();
        }

        private void ReloadSaber()
        {
            _instanceManager.Refresh();
            DidClose();
            ClearCurrentView();
            DidOpen();
        }

        private void Update()
        {
            if (_currentItem == null)
            {
                return;
            }
            _currentItem.OnTick();
        }
    }

    internal class MainView : CustomViewController
    {
        [UIComponent("SubViewContainer")] private readonly Transform _subViewContainer = null;
        private Dictionary<ENavigationCategory, INavigationCategoryView> _navViews;
        [UIAction("#post-parse")]
        private void Setup()
        {
            _navViews = new Dictionary<ENavigationCategory, INavigationCategoryView>();
            _saberSelectorView = AddView<SaberSelectorView>(true);
            _trailSettingsView = AddView<TrailSettingsView>();
            _settingsView = AddView<SettingsView>();
            _transformSettingsView = AddView<TransformSettingsView>();
            _modifiersSelectionView = AddView<MainModifierPanelView>();
        }

        public void ChangeCategory(ENavigationCategory category)
        {
            if (_navViews.TryGetValue(category, out var view))
            {
                if (view is SubView subView)
                {
                    SubViewSwitcher.SwitchView(subView);
                }
            }
        }

        private T AddView<T>(bool switchToView = false, Transform container = null) where T : SubView
        {
            var view = CreateSubView<T>(container != null ? container : _subViewContainer, switchToView);
            if (view is INavigationCategoryView navView)
            {
                _navViews.Add(navView.Category, navView);
            }
            if (!switchToView)
            {
                view.gameObject.SetActive(false);
            }
            return view;
        }
        #region SubViews
        private SaberSelectorView _saberSelectorView;
        private TrailSettingsView _trailSettingsView;
        private SettingsView _settingsView;
        private TransformSettingsView _transformSettingsView;
        private MainModifierPanelView _modifiersSelectionView;
        #endregion
    }

    internal class NavigationView : CustomViewController
    {
        [UIValue("nav-buttons")] private List<object> _navButtons;
        public override IAnimatableUi.EAnimationType AnimationType => IAnimatableUi.EAnimationType.Vertical;
        private NavButton _currentSelectedNavButton;
        private void Awake()
        {
            _navButtons = new List<object>();
            var saberButton = new NavButtonWrapper(
                ENavigationCategory.Saber,
                "SaberFactory2.Resources.Icons.customsaber-icon.png",
                ClickedCategory,
                "Select a saber");
            var trailButton = new NavButtonWrapper(
                ENavigationCategory.Trail,
                "SaberFactory2.Resources.Icons.trail-icon.png",
                ClickedCategory,
                "Edit the trail");
            var transformButton = new NavButtonWrapper(
                ENavigationCategory.Transform,
                "SaberFactory2.Resources.Icons.transform-icon.png",
                ClickedCategory,
                "Transform settings");
            var propButton = new NavButtonWrapper(
                ENavigationCategory.Modifier,
                "SaberFactory2.Resources.Icons.wrench.png",
                ClickedCategory,
                "Saber modifier");
            _navButtons.Add(saberButton);
            _navButtons.Add(trailButton);
            _navButtons.Add(transformButton);
            _navButtons.Add(propButton);
        }

        public event Action<ENavigationCategory> OnCategoryChanged;
        public event Action OnExit;
        [UIAction("#post-parse")]
        private void Setup()
        {
            try
            {
                if (_navButtons != null && _navButtons.Count > 0)
                {
                    _currentSelectedNavButton = ((NavButtonWrapper)_navButtons[0]).NavButton;
                    _currentSelectedNavButton.SetState(true, false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error($"NavigationView.Setup failed: {ex}");
            }
        }

        private void ClickedCategory(NavButton button, ENavigationCategory category)
        {
            if (_currentSelectedNavButton == button)
            {
                return;
            }
            if (_currentSelectedNavButton != null)
            {
                _currentSelectedNavButton.Deselect();
            }
            _currentSelectedNavButton = button;
            OnCategoryChanged?.Invoke(category);
        }

        [UIAction("clicked-settings")]
        private void ClickSettings(NavButton button, string _)
        {
            ClickedCategory(button, ENavigationCategory.Settings);
        }

        [UIAction("clicked-exit")]
        private void ClickExit()
        {
            OnExit?.Invoke();
        }
    }

    internal class SaberSelectorView : SubView, INavigationCategoryView
    {
        private static readonly string MODELSABER_LINK = "https://modelsaber.com/Sabers/?pc";
        [UIComponent("choose-sort-popup")] private readonly ChooseSort _chooseSortPopup = null;
        [UIComponent("loading-popup")] private readonly LoadingPopup _loadingPopup = null;
        [UIComponent("message-popup")] private readonly MessagePopup _messagePopup = null;
        [UIComponent("saber-list")] private readonly CustomList _saberList = null;
        [UIComponent("toggle-favorite")] private readonly IconToggleButton _toggleButtonFavorite = null;
        [UIValue("global-saber-width-max")] private float GlobalSaberWidthMax => _pluginConfig.GlobalSaberWidthMax;
        [UIValue("saber-width")]
        private float SaberWidth
        {
            set => _mainViewModel.SaberWidth = value;
            get => _mainViewModel.SaberWidth;
        }

        public bool IsReloading { get; private set; }
        [Inject] private readonly Editor.Editor _editor = null;
        [Inject] private readonly SaberFactory2.UI.ViewModels.MainViewModel _mainViewModel = null;
        [Inject] private readonly EditorInstanceManager _editorInstanceManager = null;
        [Inject] private readonly MainAssetStore _mainAssetStore = null;
        [Inject] private readonly PluginConfig _pluginConfig = null;
        [Inject] private readonly SaberFileWatcher _saberFileWatcher = null;
        [Inject] private readonly SaberSet _saberSet = null;
        private ModelComposition _currentComposition;
        private PreloadMetaData _currentPreloadMetaData;
        private ListItemDirectoryManager _dirManager;
        private string _listTitle;
        private ChooseSort.ESortMode _sortMode = ChooseSort.ESortMode.Name;
        public ENavigationCategory Category => ENavigationCategory.Saber;
        public override void DidOpen()
        {
            _editorInstanceManager.OnModelCompositionSet += CompositionDidChange;
            if (_pluginConfig.ReloadOnSaberUpdate)
            {
                _saberFileWatcher.OnSaberUpdate += OnSaberFileUpdate;
                _saberFileWatcher.Watch();
            }
        }

        public override void DidClose()
        {
            _editorInstanceManager.OnModelCompositionSet -= CompositionDidChange;
            if (_pluginConfig.ReloadOnSaberUpdate)
            {
                _saberFileWatcher.OnSaberUpdate -= OnSaberFileUpdate;
            }
            _saberFileWatcher.StopWatching();
        }

        [UIAction("#post-parse")]
        private async void Setup()
        {
            _dirManager = new ListItemDirectoryManager(_mainAssetStore.AdditionalCustomSaberFolders);
            _saberList.OnItemSelected += SaberSelected;
            _saberList.OnCategorySelected += DirectorySelected;
            _listTitle = "";
            _saberList.SetText(_listTitle);
            if (!Plugin.MultiPassEnabled)
            {
                await _messagePopup.ShowPermanent(
                    "Multi-pass rendering is required\n" +
                    "for Saber Factory 2.\n\n" +
                    "Enable it in Mod Settings \u2192 Asset Bundles.");
                return;
            }
            await LoadSabers();
        }

        private async void DirectorySelected(string dir)
        {
            _dirManager.Navigate(dir);
            _saberList.SetText(_dirManager.IsInRoot ? _listTitle : _dirManager.DirectoryString);
            _saberList.Deselect();
            await ShowSabers(true);
        }

        public async Task LoadSabers()
        {
            _loadingPopup.Show();
            await _mainAssetStore.LoadAllMetaAsync(_pluginConfig.AssetType);
            await ShowSabers(false, 500);
            _loadingPopup.Hide();
        }

        private async Task ShowSabers(bool scrollToTop = false, int delay = 0)
        {
            var metaEnumerable = from meta in _mainAssetStore.GetAllMetaData()
                                 orderby meta.IsFavorite descending
                                 select meta;
            switch (_sortMode)
            {
                case ChooseSort.ESortMode.Name:
                    metaEnumerable = metaEnumerable.ThenBy(x => x.ListName);
                    break;
                case ChooseSort.ESortMode.Date:
                    metaEnumerable = metaEnumerable.ThenByDescending(x => x.AssetMetaPath.File.LastWriteTime);
                    break;
                case ChooseSort.ESortMode.Size:
                    metaEnumerable = metaEnumerable.ThenByDescending(x => x.AssetMetaPath.File.Length);
                    break;
                case ChooseSort.ESortMode.Author:
                    metaEnumerable = metaEnumerable.ThenBy(x => x.ListAuthor);
                    break;
            }
            if (delay > 0)
            {
                await Task.Delay(delay);
            }
            var items = new List<ICustomListItem>(metaEnumerable);
            var loadedNames = items.Select(x => x.ListName).ToList();
            _saberList.SetItems(_dirManager.Process(items));
            _currentComposition = _mainViewModel.PreviewSaber ?? _editorInstanceManager.CurrentModelComposition;
            if (_currentComposition != null)
            {
                _mainViewModel.PreviewSaber = _currentComposition;
                _saberList.Select(_mainAssetStore.GetMetaDataForComposition(_currentComposition)?.ListName, !scrollToTop);
                UpdatePedestalText(_currentComposition);
            }
            if (scrollToTop)
            {
                _saberList.ScrollTo(0);
            }
            UpdateUi();
        }

        public void OnSaberFileUpdate(string filename)
        {
            var currentSaberPath = _currentComposition.GetLeft().StoreAsset.RelativePath;
            if (!filename.Contains(currentSaberPath))
            {
                return;
            }
            if (File.Exists(filename))
            {
                ClickedReload();
            }
        }

        private async void SaberSelected(object item)
        {
            var reloadList = false;
            if (item is PreloadMetaData metaData)
            {
                _currentPreloadMetaData = metaData;
                var relativePath = PathTools.ToRelativePath(metaData.AssetMetaPath.Path);
                _currentComposition = await _mainAssetStore[relativePath];
            }
            else if (item is ModelComposition comp)
            {
                _currentComposition = comp;
            }
            else
            {
                return;
            }
            if (_currentComposition == null)
            {
                _loadingPopup.Hide();
                return;
            }
            _mainViewModel.PreviewSaber = _currentComposition;
            UpdateUi();
            if (reloadList)
            {
                await ShowSabers();
            }
            _editor.FlashPedestal(new Color(0.24f, 0.77f, 1f));
        }

        private void CompositionDidChange(ModelComposition comp)
        {
            _currentComposition = comp;
            UpdatePedestalText(comp);
            _saberList.Select(comp);
        }

        private void UpdatePedestalText(ICustomListItem item)
        {
            var saberName = item.ListName;
            if (saberName.Length > 12)
            {
                saberName = saberName.Substring(0, 9) + "...";
            }
            _editor.SetPedestalText(0, saberName);
        }

        private void UpdateUi()
        {
            if (_currentComposition == null)
            {
                return;
            }
            _toggleButtonFavorite.SetState(_currentComposition.IsFavorite, false);
        }

        [UIAction("toggled-favorite")]
        private async void ToggledFavorite(bool isOn)
        {
            if (_currentComposition == null)
            {
                return;
            }
            _currentComposition.SetFavorite(isOn);
            _currentPreloadMetaData?.SetFavorite(isOn);
            if (isOn)
            {
                _pluginConfig.AddFavorite(_currentComposition.GetLeft().StoreAsset.RelativePath);
            }
            else
            {
                _pluginConfig.RemoveFavorite(_currentComposition.GetLeft().StoreAsset.RelativePath);
            }
            await ShowSabers();
        }

        [UIAction("select-sort")]
        private void SelectSort()
        {
            _chooseSortPopup.Show(async sortMode =>
            {
                _sortMode = sortMode;
                await ShowSabers(_chooseSortPopup.ShouldScrollToTop);
            });
        }

        [UIAction("toggled-grab-saber")]
        private void ToggledGrabSaber(bool isOn)
        {
            _editor.IsSaberInHand = isOn;
        }

        [UIAction("clicked-reload")]
        private async void ClickedReload()
        {
            if (IsReloading)
            {
                return;
            }
            IsReloading = true;
            if (_currentComposition == null)
            {
                return;
            }
            _loadingPopup.Show();
            try
            {
                await _saberSet.Save();
                _editorInstanceManager.DestroySaber();
                await _mainAssetStore.Reload(_currentComposition.GetLeft().StoreAsset.RelativePath);
                await _saberSet.Load();
                await ShowSabers();
            }
            catch (Exception ex)
            { Plugin.Logger.Error($"Error loading sabers: {ex}"); }
            _loadingPopup.Hide();
            IsReloading = false;
        }

        [UIAction("clicked-reloadall")]
        private async void ClickedReloadAll()
        {
            if (IsReloading)
            {
                return;
            }
            IsReloading = true;
            _loadingPopup.Show();
            try
            {
                await _saberSet.Save();
                _editorInstanceManager.DestroySaber();
                await _mainAssetStore.ReloadAll();
                await _saberSet.Load();
                await ShowSabers();
            }
            catch (Exception ex)
            { Plugin.Logger.Error($"Error reloading sabers: {ex}"); }
            _loadingPopup.Hide();
            IsReloading = false;
        }

        [UIAction("clicked-delete")]
        private async void ClickedDelete()
        {
            if (_currentComposition == null)
            {
                return;
            }
            var result = await _messagePopup.Show("Do you really want to delete this saber?", true);
            if (!result)
            {
                return;
            }
            _editorInstanceManager.DestroySaber();
            _mainAssetStore.Delete(_currentComposition.GetLeft().StoreAsset.RelativePath);
            await ShowSabers();
        }

        [UIAction("open-modelsaber")]
        private void OpenModelsaber()
        {
            Process.Start(MODELSABER_LINK);
        }
    }

    internal class SettingsView : SubView, INavigationCategoryView
    {
        private const string ProfileUrl = "https://github.com/ToniMacaroni";
        private const string DiscordUrl = "https://discord.com/invite/enTn4MgbJT";
        [UIValue("mod-enabled")] private bool ModEnabled { get => _pluginConfig.Enabled; set { _pluginConfig.Enabled = value; OnPropertyChanged(); } }
        [UIValue("events-enabled")] private bool EventsEnabled { get => _pluginConfig.EnableEvents; set { _pluginConfig.EnableEvents = value; OnPropertyChanged(); } }
        [UIValue("random-sabers")] private bool RandomSabers { get => _pluginConfig.RandomSaber; set { _pluginConfig.RandomSaber = value; OnPropertyChanged(); } }
        [UIValue("override-song-saber")] private bool OverrideSongSaber { get => _pluginConfig.OverrideSongSaber; set { _pluginConfig.OverrideSongSaber = value; OnPropertyChanged(); } }
        private float SwingSoundVolume { get => _pluginConfig.SwingSoundVolume; set { _pluginConfig.SwingSoundVolume = value; SaberFactory2.Core.EventBus.PublishSwingSoundVolumeChanged(value); OnPropertyChanged(); } }

        [Inject] private readonly PluginConfig _pluginConfig = null;
        public ENavigationCategory Category => ENavigationCategory.Settings;

        public override void DidClose()
        {
            _pluginConfig.PropertyChanged -= OnConfigChanged;
        }

        public override void DidOpen()
        {
            _pluginConfig.PropertyChanged += OnConfigChanged;
            SaberFactory2.Core.EventBus.PublishSwingSoundVolumeChanged(_pluginConfig.SwingSoundVolume);
        }

        private void OnConfigChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PluginConfig.Enabled):
                    OnPropertyChanged(nameof(ModEnabled));
                    break;
                case nameof(PluginConfig.EnableEvents):
                    OnPropertyChanged(nameof(EventsEnabled));
                    break;
                case nameof(PluginConfig.RandomSaber):
                    OnPropertyChanged(nameof(RandomSabers));
                    break;
                case nameof(PluginConfig.OverrideSongSaber):
                    OnPropertyChanged(nameof(OverrideSongSaber));
                    break;
                case nameof(PluginConfig.SwingSoundVolume):
                    OnPropertyChanged(nameof(SwingSoundVolume));
                    break;

            }
        }

        [UIAction("profile-clicked")]
        private void ProfileClicked()
        {
            try { Process.Start(ProfileUrl); } catch (Exception ex) { Plugin.Logger.Error($"Failed to open ProfileUrl: {ex}"); }
        }

        [UIAction("discord-clicked")]
        private void DiscordClicked()
        {
            try { Process.Start(DiscordUrl); } catch (Exception ex) { Plugin.Logger.Error($"Failed to open DiscordUrl: {ex}"); }
        }
    }

    internal class TrailSettingsView : SubView, INavigationCategoryView
    {
        [UIObject("advanced-container")] private readonly GameObject _advancedContainer = null;
        [UIComponent("choose-trail-popup")] private readonly ChooseTrailPopup _chooseTrailPopup = null;
        [UIObject("main-container")] private readonly GameObject _mainContainer = null;
        [UIObject("no-trail-container")] private readonly GameObject _noTrailContainer = null;
        [UIComponent("material-editor")] private readonly MaterialEditor _materialEditor = null;
        [UIValue("trail-width-max")] private float _trailWidthMax => _pluginConfig.TrailWidthMax;
        [UIValue("granularity-value")] private int GranularityValue { get => _trailConfig.Granularity; set => _trailConfig.Granularity = value; }
        [UIValue("sampling-frequency-value")] private int SamplingFrequencyValue { get => _trailConfig.SamplingFrequency; set => _trailConfig.SamplingFrequency = value; }

        [UIValue("refresh-button-active")] private bool RefreshButtonActive { get => _refreshButtonActive; set => SetProperty(ref _refreshButtonActive, value); }
        private bool UseVertexColorOnly { get => _trailConfig.OnlyUseVertexColor; set { _trailConfig.OnlyUseVertexColor = value; _trailPreviewer.OnlyColorVertex = value; } }
        private bool NoTrailViewActive { get => _noTrailContainer.activeSelf; set { _noTrailContainer.SetActive(value); _mainContainer.SetActive(!value); } }

        private bool ShowThumbstickMessage => _pluginConfig.ControlTrailWithThumbstick;
        [Inject] private readonly SaberFactory2.UI.ViewModels.MainViewModel _mainViewModel = null;
        [Inject] private readonly EditorInstanceManager _editorInstanceManager = null;
        [Inject] private readonly MainAssetStore _mainAssetStore = null;
        [Inject] private readonly PlayerDataModel _playerDataModel = null;
        [Inject] private readonly PluginConfig _pluginConfig = null;
        [Inject] private readonly TrailConfig _trailConfig = null;
        [Inject] private readonly TrailPreviewer _trailPreviewer = null;
        private bool _dirty;
        private InstanceTrailData _instanceTrailData;
        private bool _refreshButtonActive;
        private float _time;
        private float _trailFloatLength;
        public ENavigationCategory Category => ENavigationCategory.Trail;
        private void Update()
        {
            if (_time < 0.3)
            {
                _time += Time.deltaTime;
                return;
            }
            _time = 0;
            if (!_dirty || _instanceTrailData == null || !_refreshButtonActive)
            {
                return;
            }
            _dirty = false;
            RefreshTrail();
        }

        [UIAction("#post-parse")]
        private void Setup()
        {
            _mainContainer.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _advancedContainer.SetActive(_pluginConfig.ShowAdvancedTrailSettings);
        }

        public override void DidOpen()
        {
            CreateTrail(_editorInstanceManager.CurrentSaber);
            SaberFactory2.Core.EventBus.OnSaberPreviewInstantiated += CreateTrail;
            if (_instanceTrailData != null && _pluginConfig.ControlTrailWithThumbstick)
            {
                _trailFloatLength = _instanceTrailData.Length;
            }
        }

        public override void DidClose()
        {
            if (_instanceTrailData != null && _pluginConfig.ControlTrailWithThumbstick)
            {
            }
            SaberFactory2.Core.EventBus.OnSaberPreviewInstantiated -= CreateTrail;
            _materialEditor.Close();
            _chooseTrailPopup.Exit();
            _instanceTrailData = null;
            _trailPreviewer.Destroy();
        }

        private void OnjoystickWasNotCenteredThisFrameEvent(Vector2 deltaPos)
        {
            WidthValue = Mathf.Clamp(_instanceTrailData.Width + deltaPos.y * -0.005f, 0, _trailWidthMax);
            LengthValue = Mathf.Clamp(_trailFloatLength + deltaPos.x * 0.1f, 0, 30);
            ParserParams.EmitEvent("update-proportions");
        }

        private void LoadFromModel(InstanceTrailData trailData)
        {
            _instanceTrailData = trailData;
            _trailFloatLength = _instanceTrailData?.Length ?? 0;
            UpdateProps();
        }

        private void SetTrailModel(TrailModel trailModel)
        {
            if (_mainViewModel.PreviewSaber?.GetLeft() is CustomSaberModel model)
            {
                model.TrailModel = trailModel;
            }
        }

        private bool CopyFromTrailModel(TrailModel trailModel, List<CustomTrail> trailList)
        {
            if (_mainViewModel.PreviewSaber?.GetLeft() is CustomSaberModel model)
            {
                if (model.TrailModel == null)
                {
                    model.TrailModel = new TrailModel(
                        Vector3.zero,
                        0.5f,
                        12,
                        new MaterialDescriptor(null),
                        0f,
                        TextureWrapMode.Clamp)
                    { TrailOriginTrails = trailList };
                    model.TrailModel.CopyFrom(trailModel);
                    model.TrailModel.Material.UpdateBackupMaterial(false);
                    return true;
                }
                model.TrailModel.CopyFrom(trailModel);
                model.TrailModel.TrailOriginTrails = trailList;
            }
            return false;
        }

        private void ResetTrail()
        {
            if (_mainViewModel.PreviewSaber?.GetLeft() is CustomSaberModel model)
            {
                _instanceTrailData.RevertMaterialForCustomSaber(model);
                var tm = model.GrabTrail(false);
                if (tm != null)
                {
                    SetTrailModel(tm);
                }
            }
        }

        private void CreateTrail(SaberInstance saberInstance)
        {
            _dirty = false;
            _trailPreviewer.Destroy();
            var trailData = saberInstance?.GetTrailData(out _);
            if (trailData is null)
            {
                NoTrailViewActive = true;
                return;
            }
            NoTrailViewActive = false;
            if (saberInstance.TrailHandler != null)
            {
                LoadFromModel(trailData);
                RefreshButtonActive = true;
            }
            else
            {
                _trailPreviewer.Create(saberInstance.GameObject.transform.parent, trailData, UseVertexColorOnly);
                LoadFromModel(trailData);
                _trailPreviewer.SetColor(_playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme().saberAColor);
                RefreshButtonActive = false;
            }
        }

        private void TrailPopupSelectionChanged(TrailModel trailModel, List<CustomTrail> trailList)
        {
            if (trailModel is null)
            {
                ResetTrail();
            }
            else
            {
                CopyFromTrailModel(trailModel, trailList);
            }
            SaberFactory2.Core.EventBus.PublishPreviewSaberChanged(_mainViewModel.PreviewSaber);
        }

        [UIAction("edit-material")]
        private void EditMaterial()
        {
            _materialEditor.Show(_instanceTrailData.Material);
        }

        [UIAction("revert-trail")]
        private void ClickRevertTrail()
        {
            ResetTrail();
            SaberFactory2.Core.EventBus.PublishPreviewSaberChanged(_mainViewModel.PreviewSaber);
        }

        [UIAction("choose-trail")]
        private void ClickChooseTrail()
        {
            _chooseTrailPopup.Show(
                from meta in _mainAssetStore.GetAllMetaData(AssetTypeDefinition.CustomSaber)
                orderby meta.IsFavorite descending
                select meta,
                TrailPopupSelectionChanged);
        }

        [UIAction("refresh-trail")]
        private void RefreshTrail()
        {
            SaberFactory2.Core.EventBus.PublishPreviewSaberChanged(_mainViewModel.PreviewSaber);
        }

        [UIAction("revert-advanced")]
        private void RevertAdvanced()
        {
            _trailConfig.Revert();
            ParserParams.EmitEvent("get-advanced");
        }
        #region Values
        private float LengthValue { get => _trailFloatLength; set { _trailFloatLength = value; if (_instanceTrailData != null) { _instanceTrailData.Length = (int)value; _dirty = true; if (!_refreshButtonActive) { _trailPreviewer.SetLength(value); OnPropertyChanged(); } } } }
        private float WidthValue { get { return _instanceTrailData?.Width ?? 1; } set { if (_instanceTrailData != null) { _instanceTrailData.Width = value; if (!_refreshButtonActive) { _trailPreviewer.UpdateWidth(); OnPropertyChanged(); } } } }
        private float OffsetValue { get { return _instanceTrailData?.Offset ?? 0; } set { if (_instanceTrailData != null) { _instanceTrailData.Offset = value; if (!_refreshButtonActive) { _trailPreviewer.UpdateWidth(); OnPropertyChanged(); } } } }
        private float WhitestepValue { get { return _instanceTrailData?.WhiteStep ?? 0; } set { if (_instanceTrailData != null) { _instanceTrailData.WhiteStep = value; OnPropertyChanged(); } } }
        private bool ClampValue { get { return _instanceTrailData?.ClampTexture ?? false; } set { if (_instanceTrailData != null) { _instanceTrailData.ClampTexture = value; OnPropertyChanged(); } } }
        private bool FlipValue { get { return _instanceTrailData?.Flip ?? false; } set { if (_instanceTrailData != null) { _instanceTrailData.Flip = value; if (_refreshButtonActive) RefreshTrail(); else SaberFactory2.Core.EventBus.PublishPreviewSaberChanged(_mainViewModel.PreviewSaber); OnPropertyChanged(); } } }
        #endregion
    }

    internal class TransformSettingsView : SubView, INavigationCategoryView
    {
        public float RotationAmount { get => _transformDataSetter?.Rotation ?? 0f; set { if (_transformDataSetter != null) _transformDataSetter.Rotation = value; } }
        public float OffsetAmount { get => _transformDataSetter?.Offset ?? 0f; set { if (_transformDataSetter != null) _transformDataSetter.Offset = value; } }

        public float SaberLength
        {
            get => _editorInstanceManager.CurrentSaber?.Model.SaberLength ?? 1;
            set => _editorInstanceManager.CurrentSaber?.SetSaberLength(value);
        }

        [Inject] private readonly EditorInstanceManager _editorInstanceManager = null;
        private TransformDataSetter _transformDataSetter;
        public ENavigationCategory Category => ENavigationCategory.Transform;
        public override void DidOpen()
        {
            if (_editorInstanceManager.CurrentPiece is CustomSaberInstance cs)
            {
                _transformDataSetter = cs.PropertyBlockSetterHandler.Cast<CustomSaberPropertyBlockSetterHandler>()
                    .TransformDataSetter;
            }
            ParserParams.EmitEvent("update-props");
        }

        public override void DidClose()
        {
            _transformDataSetter = null;
        }
    }
}