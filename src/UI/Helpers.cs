using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Tags;
using HarmonyLib;
using IPA.Utilities;
using SaberFactory2.Helpers;
using SaberFactory2.Instances.Trail;
using SaberFactory2.Models;
using SaberFactory2.UI.CustomSaber.CustomComponents;
using SaberFactory2.UI.CustomSaber.Views;
using SaberFactory2.UI.Lib;
using SiraUtil.Logging;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.UI
{
    internal class ListItemDirectoryManager
    {
        private const string UpDirIndicator = "<";
        public string DirectoryString { get; private set; } = "";
        public bool IsInRoot => string.IsNullOrEmpty(_currentDirectory);
        private readonly List<string> _additionalFolderPool;
        private string _currentDirectory = "";
        public ListItemDirectoryManager(List<string> additionalFolderPool)
        {
            _additionalFolderPool = additionalFolderPool;
        }

        public void GoBack()
        {
            if (!_currentDirectory.Contains("\\"))
            {
                _currentDirectory = "";
            }
            else
            {
                _currentDirectory = _currentDirectory.Substring(0, _currentDirectory.LastIndexOf('\\'));
            }
        }

        public void Navigate(string path)
        {
            if (path == UpDirIndicator)
            {
                GoBack();
                return;
            }
            _currentDirectory += (IsInRoot ? "" : "\\") + path;
            RefreshDirectoryString();
        }

        public List<ICustomListItem> Process(IEnumerable<ICustomListItem> items)
        {
            var itemsList = FilterForDir(items, _currentDirectory).ToList();
            var addedFolders = new HashSet<string>();
            foreach (var folder in _additionalFolderPool)
            {
                if (!folder.StartsWith(_currentDirectory))
                {
                    continue;
                }
                var d = _currentDirectory == string.Empty ? folder : folder.Replace(_currentDirectory, "");
                if (d.Length > 0 && d[0] == '\\')
                {
                    d = d.Substring(1);
                }
                d = d.Contains('\\') ? d.Substring(0, d.IndexOf('\\')) : d;
                if (d != string.Empty)
                {
                    addedFolders.Add(d);
                }
            }
            itemsList.InsertRange(0, addedFolders.Select(x => new CustomList.ListDirectory(x)));
            if (!IsInRoot)
            {
                itemsList.Insert(0, new CustomList.ListDirectory(UpDirIndicator));
            }
            return itemsList;
        }

        public IEnumerable<ICustomListItem> FilterForDir(IEnumerable<ICustomListItem> items, string dir)
        {
            foreach (var item in items)
            {
                if (item is PreloadMetaData preloadMetaData)
                {
                    if (preloadMetaData.AssetMetaPath.SubDirName == dir)
                    {
                        yield return item;
                    }
                }
                else if (item is ModelComposition comp)
                {
                    if (comp.GetLeft().StoreAsset.SubDirName == dir)
                    {
                        yield return item;
                    }
                }
                else
                {
                    yield return item;
                }
            }
        }

        private void RefreshDirectoryString()
        {
            DirectoryString = _currentDirectory;
        }
    }

    internal class SFPauseMenuManager : IInitializable
    {
        [Inject] private readonly PauseMenuManager _pauseMenuManager = null;
        public void Initialize()
        {
            try
            {
                CreateCheckbox();
            }
            catch (Exception)
            { }
        }

        public void CreateCheckbox()
        {
            var canvas = _pauseMenuManager.GetField<LevelBar, PauseMenuManager>("_levelBar")
                .transform
                .parent
                .parent
                .GetComponent<Canvas>();
            if (!canvas)
            {
                return;
            }
            var buttonObj = new ButtonTag().CreateObject(canvas.transform);
            (buttonObj.transform as RectTransform).anchoredPosition = new Vector2(26, -15);
            (buttonObj.transform as RectTransform).sizeDelta = new Vector2(-130, 7);
            buttonObj.GetComponent<Button>().onClick.AddListener(ButtonClick);
        }

        private void ButtonClick()
        {
            Editor.Editor.Instance?.Open();
        }
    }

    internal class SaberFactoryMenuButton : MenuButtonRegistrar
    {
        private readonly Editor.Editor _editor;
        protected SaberFactoryMenuButton(Editor.Editor editor) : base("Saber Factory 2", "Good quality sabers")
        {
            _editor = editor;
        }

        protected override void OnClick()
        {
            _editor.Open();
        }
    }

    internal class TrailPreviewer
    {
        public Material Material
        {
            get => GetMaterial();
            set => SetMaterial(value);
        }

        public float Length
        {
            set => SetLength(value);
        }

        public bool OnlyColorVertex
        {
            set { _sections.Do(x => x.OnlyColorVertex = value); }
        }

        private readonly SiraLog _logger;
        private readonly List<TrailPreviewSection> _sections = new List<TrailPreviewSection>();
        private GameObject _prefab;
        public TrailPreviewer(SiraLog logger, EmbeddedAssetLoader assetLoader)
        {
            _logger = logger;
            LoadPrefab(assetLoader);
        }

        private async void LoadPrefab(EmbeddedAssetLoader assetLoader)
        {
            try
            {
                _prefab = await assetLoader.LoadAsset<GameObject>("TrailPlane");
            }
            catch (Exception)
            {
                _logger.Error("Couldn't load trail plane");
            }
        }

        public void Create(Transform parent, InstanceTrailData trailData, bool onlyColorVertex)
        {
            _sections.Clear();
            var (pointStart, pointEnd) = trailData.GetPoints();
            _sections.Add(new TrailPreviewSection(0, parent, pointStart, pointEnd, _prefab) { OnlyColorVertex = onlyColorVertex });
            for (var i = 0; i < trailData.SecondaryTrails.Count; i++)
            {
                var trail = trailData.SecondaryTrails[i];
                if (trail.Trail.PointStart is null || trail.Trail.PointEnd is null)
                {
                    continue;
                }
                _sections.Add(new TrailPreviewSection(i + 1, parent, trail.Trail.PointStart, trail.Trail.PointEnd, _prefab, trail)
                { OnlyColorVertex = onlyColorVertex });
            }
            Material = trailData.Material.Material;
            Length = trailData.Length;
            UpdateWidth();
        }

        public void SetMaterial(Material mat)
        {
            if (_sections.Count < 1)
            {
                return;
            }
            _sections[0].SetMaterial(mat);
        }

        public void SetColor(Color color)
        {
            _sections.Do(x => x.SetColor(color));
        }

        public void UpdateWidth()
        {
            _sections.Do(x => x.UpdateWidth());
        }

        public Material GetMaterial()
        {
            if (_sections.Count < 1)
            {
                return null;
            }
            return _sections[0].GetMaterial();
        }

        public void SetLength(float val)
        {
            _sections.Do(x => x.SetLength(val));
        }

        public void Destroy()
        {
            _sections.Do(x => x.Destroy());
            _sections.Clear();
        }

        private class TrailPreviewSection
        {
            public int TrailIdx { get; }
            public bool IsPrimaryTrail => TrailIdx == 0;
            public bool OnlyColorVertex;
            private readonly GameObject _instance;
            private readonly Mesh _mesh;
            private readonly Transform _pointEnd;
            private readonly Transform _pointStart;
            private readonly Renderer _renderer;
            private readonly InstanceTrailData.SecondaryTrailHandler _trailHandler;
            private readonly Transform _transform;
            public TrailPreviewSection(
                int idx,
                Transform parent,
                Transform pointStart,
                Transform pointEnd,
                GameObject prefab,
                InstanceTrailData.SecondaryTrailHandler trailHandler = null)
            {
                TrailIdx = idx;
                _trailHandler = trailHandler;
                _pointStart = pointStart;
                _pointEnd = pointEnd;
                _instance = Object.Instantiate(prefab, _pointEnd.position, Quaternion.Euler(-90, 25, 0), parent);
                _instance.name = "Trail preview " + idx;
                _transform = _instance.transform;
                _renderer = _instance.GetComponentInChildren<Renderer>();
                _mesh = _instance.GetComponentInChildren<MeshFilter>().sharedMesh;
                _renderer.sortingOrder = 3;
                if (trailHandler != null)
                {
                    SetMaterial(trailHandler.Trail.TrailMaterial);
                }
            }

            public void SetMaterial(Material mat)
            {
                if (_renderer == null)
                {
                    return;
                }
                _renderer.sharedMaterial = mat;
            }

            public Material GetMaterial()
            {
                return _renderer != null ? _renderer.sharedMaterial : null;
            }

            public void SetColor(Color color)
            {
                var mat = _renderer.sharedMaterial;
                if (mat != null && !OnlyColorVertex && MaterialHelpers.IsMaterialColorable(mat))
                {
                    _renderer.SetPropertyBlock(MaterialHelpers.ColorBlock(color));
                }
                var newColors = new Color[4];
                for (var i = 0; i < newColors.Length; i++)
                {
                    newColors[i] = color;
                }
                _mesh.colors = newColors;
            }

            public void UpdateWidth()
            {
                var locPosStart = _instance.transform.InverseTransformPoint(_pointStart.position);
                var locPosEnd = _instance.transform.InverseTransformPoint(_pointEnd.position);
                var newVerts = new Vector3[4];
                newVerts[0] = new Vector3(0, 0, locPosStart.z);
                newVerts[1] = new Vector3(0, 0, locPosEnd.z);
                newVerts[2] = new Vector3(1, 0, locPosEnd.z);
                newVerts[3] = new Vector3(1, 0, locPosStart.z);
                _mesh.vertices = newVerts;
            }

            public void SetLength(float val)
            {
                if (_trailHandler is null)
                {
                    SetLengthInternal(val);
                    return;
                }
                _trailHandler.UpdateLength((int)val);
                SetLengthInternal(_trailHandler.Trail.Length);
            }

            private void SetLengthInternal(float val)
            {
                var currentScale = _transform.localScale;
                currentScale.x = val * 0.05f;
                _transform.localScale = currentScale;
            }

            public void Destroy()
            {
                _instance.TryDestroy();
            }
        }
    }

    internal class CustomSaberUiComposition : BaseUiComposition
    {
        private readonly SaberSet _saberSet;
        protected CustomSaberUiComposition(
            SiraLog logger,
            CustomScreen.Factory screenFactory,
            BaseGameUiHandler baseGameUiHandler,
            PhysicsRaycasterWithCache physicsRaycaster,
            BsmlDecorator bsmlDecorator,
            SaberSet saberSet)
            : base(logger, screenFactory, baseGameUiHandler, physicsRaycaster, bsmlDecorator)
        {
            _saberSet = saberSet;
        }

        protected override void SetupUI()
        {
            var mainScreenInitData = new CustomScreen.InitData
            (
                "Main Screen",
                new Vector3(-25, -7, 0),
                Quaternion.identity,
                new Vector2(105, 140),
                true
            );
            var navigationInitData = new CustomScreen.InitData(
                "Navigation Screen",
                new Vector3(-95, 0, 0),
                Quaternion.identity,
                new Vector2(30, 70),
                true
            );
            _mainView = AddScreen(mainScreenInitData).CreateViewController<MainView>();
            _navigationView = AddScreen(navigationInitData).CreateViewController<NavigationView>();
        }

        protected override void DidOpen()
        {
            base.DidOpen();
            _navigationView.OnExit += ClosePressed;
            _navigationView.OnCategoryChanged += _mainView.ChangeCategory;
        }

        protected override void DidClose()
        {
            base.DidClose();
            _navigationView.OnExit -= ClosePressed;
            _navigationView.OnCategoryChanged -= _mainView.ChangeCategory;
            _ = _saberSet.Save();
        }

        protected override void SetupTemplates()
        {
            base.SetupTemplates();
            BsmlDecorator.AddTemplate("NavHeight", "70");
        }
        #region Views
        private MainView _mainView;
        private NavigationView _navigationView;
        #endregion
    }

    internal enum ENavigationCategory
    {
        Saber,
        Trail,
        Settings,
        Transform,
        Modifier
    }

    internal interface INavigationCategoryView
    {
        ENavigationCategory Category { get; }
    }

    internal class NavButtonWrapper
    {
        [UIComponent("button")] public readonly NavButton NavButton = null;
        [UIValue("hover-hint")] private readonly string _hoverHint;
        [UIValue("icon")] private readonly string _iconResource;
        private readonly Action<NavButton, ENavigationCategory> _callback;
        private readonly ENavigationCategory _category;
        public NavButtonWrapper(ENavigationCategory category, string iconResource, Action<NavButton, ENavigationCategory> callback,
            string hoverHint = "")
        {
            _category = category;
            _iconResource = iconResource;
            _callback = callback;
            _hoverHint = hoverHint;
        }

        [UIAction("selected")]
        private void OnSelect(NavButton button, string id)
        {
            _callback?.Invoke(NavButton, _category);
        }
    }
}