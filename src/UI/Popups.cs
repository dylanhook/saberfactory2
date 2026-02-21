using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using CustomSaber;
using HMUI;
using SaberFactory2.DataStore;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;
using SaberFactory2.UI.CustomSaber.CustomComponents;
using SaberFactory2.UI.Lib;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

namespace SaberFactory2.UI.CustomSaber.Popups
{
    internal class ChooseSort : Popup
    {
        public enum ESortMode
        {
            Name,
            Date,
            Size,
            Author
        }

        [UIComponent("sort-list")] private readonly CustomList _sortList = null;
        public bool ShouldScrollToTop { get; set; } = true;
        private Action<ESortMode> _onSelectionChanged;
        public async void Show(Action<ESortMode> onSelectionChanged)
        {
            _onSelectionChanged = onSelectionChanged;
            var modes = new List<SortModeItem>();
            foreach (var mode in (ESortMode[])Enum.GetValues(typeof(ESortMode)))
            {
                modes.Add(new SortModeItem(mode));
            }
            _ = Create(true);
            _sortList.OnItemSelected += SortSelected;
            _sortList.SetItems(modes);
            await AnimateIn();
        }

        private void SortSelected(ICustomListItem item)
        {
            _onSelectionChanged?.Invoke(((SortModeItem)item).SortMode);
            Exit();
        }

        private async void Exit()
        {
            _onSelectionChanged = null;
            _sortList.OnItemSelected -= SortSelected;
            await Hide(true);
        }

        [UIAction("click-cancel")]
        private void ClickSelect()
        {
            Exit();
        }

        private class SortModeItem : ICustomListItem
        {
            public readonly ESortMode SortMode;
            public SortModeItem(ESortMode sortMode)
            {
                SortMode = sortMode;
            }

            public string ListName => SortMode.ToString();
            public string ListAuthor { get; }
            public Sprite ListCover { get; }
            public bool IsFavorite { get; }
        }
    }

    internal class ChooseTrailPopup : Popup
    {
        [UIComponent("saber-list")] private readonly CustomList _saberList = null;
        [Inject] private readonly MainAssetStore _mainAssetStore = null;
        protected override string ResourceName => "SaberFactory2.UI.CustomSaber.CustomComponents.ChooseTrailPopup.bsml";
        private List<PreloadMetaData> _comps;
        private ListItemDirectoryManager _dirManager;
        private Action<TrailModel, List<CustomTrail>> _onSelectionChanged;
        private (TrailModel, List<CustomTrail>) _selectedTrailModel;
        public async void Show(IEnumerable<PreloadMetaData> comps, Action<TrailModel, List<CustomTrail>> onSelectionChanged)
        {
            _dirManager ??= new ListItemDirectoryManager(_mainAssetStore.AdditionalCustomSaberFolders);
            _onSelectionChanged = onSelectionChanged;
            _ = Create(true);
            _saberList.OnItemSelected += SaberSelected;
            _saberList.OnCategorySelected += OnDirectorySelected;
            _comps = comps.ToList();
            _saberList.SetItems(_dirManager.Process(_comps));
            await AnimateIn();
        }

        private void OnDirectorySelected(string path)
        {
            _dirManager.Navigate(path);
            _saberList.SetText(_dirManager.IsInRoot ? "Saber-Os" : _dirManager.DirectoryString);
            _saberList.Deselect();
            _saberList.SetItems(_dirManager.Process(_comps));
        }

        private async Task<(TrailModel, List<CustomTrail>)> GetTrail(PreloadMetaData metaData)
        {
            if (metaData is null)
            {
                return default;
            }
            var comp = await _mainAssetStore[metaData];
            if (comp?.GetLeft() is CustomSaberModel cs)
            {
                return (cs.GrabTrail(true), SaberHelpers.GetTrails(cs.Prefab));
            }
            return default;
        }

        private async void SaberSelected(ICustomListItem item)
        {
            if (item is PreloadMetaData metaData)
            {
                _selectedTrailModel = await GetTrail(metaData);
                _onSelectionChanged?.Invoke(_selectedTrailModel.Item1, _selectedTrailModel.Item2);
            }
        }

        public async void Exit()
        {
            if (!IsOpen)
            {
                return;
            }
            _onSelectionChanged = null;
            _saberList.OnItemSelected -= SaberSelected;
            _saberList.OnCategorySelected -= OnDirectorySelected;
            await Hide(true);
        }

        [UIAction("click-select")]
        private void ClickSelect()
        {
            Exit();
        }

        [UIAction("click-original")]
        private void ClickOriginal()
        {
            _onSelectionChanged?.Invoke(null, null);
            Exit();
        }

        [UIAction("click-cancel")]
        private void ClickCancel()
        {
            Exit();
        }
    }

    internal class LoadingPopup : Popup
    {
        [UIValue("text")] private string _text;
        [UIValue("text-active")] private bool _isTextActive => !string.IsNullOrEmpty(_text);
        public void Show()
        {
            ShowInteral(string.Empty);
        }

        public void Show(string text)
        {
            ShowInteral(text);
        }

        private void ShowInteral(string text)
        {
            _text = text;
            _ = Create(false);
        }

        public async void Hide()
        {
            await Hide(false);
        }
    }

    internal class MaterialEditor : Popup
    {
        [UIComponent("material-dropdown")] private readonly DropDownListSetting _materialDropDown = null;
        [UIComponent("prop-list")] private readonly PropList _propList = null;
        [UIValue("materials")] private readonly List<object> _materials = new List<object>();
        [Inject] private readonly ShaderPropertyCache _shaderPropertyCache = null;
        public async void Show(MaterialDescriptor materialDescriptor)
        {
            if (materialDescriptor == null || materialDescriptor.Material == null)
            {
                return;
            }
            _ = Create(false);
            _cachedTransform.localScale = Vector3.zero;
            _materialDropDown.transform.parent.gameObject.SetActive(false);
            SetMaterial(materialDescriptor.Material);
            await Task.Delay(100);
            await AnimateIn();
        }

        public async void Show(IEnumerable<MaterialDescriptor> materialDescriptors)
        {
            _ = Create(true);
            _cachedTransform.localScale = Vector3.zero;
            var descriptorArray = materialDescriptors.ToArray();
            _materials.Clear();
            _materials.Add(descriptorArray.Where(x => x.Material != null).Select(x => x.Material.name));
            _materialDropDown.transform.parent.gameObject.SetActive(true);
            SetMaterial(descriptorArray.First().Material);
            await Task.Delay(100);
            await AnimateIn();
        }

        public async void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            await Hide(true);
        }

        private void SetMaterial(Material material)
        {
            var props = new List<PropertyDescriptor>();
            var shaderPropertyInfo = _shaderPropertyCache[material.shader];
            foreach (var prop in shaderPropertyInfo.GetAll())
            {
                EPropertyType type;
                if (prop.HasAttribute(MaterialAttributes.HideInSf))
                {
                    continue;
                }
                if (prop.Attributes.Contains("MaterialToggle") || prop.Name == "_CustomColors")
                {
                    var floatProp = (ShaderPropertyInfo.ShaderFloat)prop;
                    type = EPropertyType.Bool;
                    var propObject = (float)floatProp.GetValue(material) > 0;
                    void Callback(object obj)
                    {
                        material.SetFloat(prop.PropId, obj.Cast<bool>() ? 1 : 0);
                    }
                    props.Add(new PropertyDescriptor(prop.Description, type, propObject, Callback));
                }
                else
                {
                    type = GetTypeFromShaderType(prop.Type);
                    if (type == EPropertyType.Unhandled)
                    {
                        continue;
                    }
                    var propObject = GetPropObject(prop.Type, prop.PropId, material);
                    var callback = ConstructCallback(prop.Type, prop.PropId, material);
                    var propertyDescriptor = new PropertyDescriptor(prop.Description, type, propObject, callback);
                    if (prop is ShaderPropertyInfo.ShaderRange range)
                    {
                        propertyDescriptor.AddtionalData = new Vector2(range.Min, range.Max);
                    }
                    else if (prop is ShaderPropertyInfo.ShaderTexture)
                    {
                        propertyDescriptor.AddtionalData = !prop.HasAttribute(MaterialAttributes.SfNoPreview);
                    }
                    props.Add(propertyDescriptor);
                }
            }
            _propList.SetItems(props);
        }

        private EPropertyType GetTypeFromShaderType(ShaderPropertyType type)
        {
            return type switch
            {
                ShaderPropertyType.Float => EPropertyType.Float,
                ShaderPropertyType.Range => EPropertyType.Float,
                ShaderPropertyType.Color => EPropertyType.Color,
                ShaderPropertyType.Texture => EPropertyType.Texture,
                _ => EPropertyType.Unhandled
            };
        }

        private object GetPropObject(ShaderPropertyType type, int propId, Material material)
        {
            return type switch
            {
                ShaderPropertyType.Float => material.GetFloat(propId),
                ShaderPropertyType.Range => material.GetFloat(propId),
                ShaderPropertyType.Color => material.GetColor(propId),
                ShaderPropertyType.Texture => material.GetTexture(propId),
                _ => null
            };
        }

        private Action<object> ConstructCallback(ShaderPropertyType type, int propId, Material material)
        {
            return type switch
            {
                ShaderPropertyType.Float => obj => { material.SetFloat(propId, (float)obj); }
                ,
                ShaderPropertyType.Range => obj => { material.SetFloat(propId, (float)obj); }
                ,
                ShaderPropertyType.Color => obj => { material.SetColor(propId, (Color)obj); }
                ,
                ShaderPropertyType.Texture => obj => { material.SetTexture(propId, (Texture2D)obj); }
                ,
                _ => null
            };
        }

        [UIAction("click-close")]
        private void ClickClose()
        {
            Close();
        }
    }

    internal class MessagePopup : Popup
    {
        [UIValue("message")] protected string _message;
        [UIValue("no-button-active")] protected bool _noButtonActive;
        [UIValue("yes-button-active")] protected bool _yesButtonActive;
        [UIValue("yes-button-text")] protected string _yesButtonText;
        private TaskCompletionSource<bool> _taskCompletionSource;
        public async Task<bool> Show(string message, bool yesNoOption = false)
        {
            _taskCompletionSource = new TaskCompletionSource<bool>();
            _noButtonActive = yesNoOption;
            _yesButtonActive = true;
            _yesButtonText = yesNoOption ? "Yes" : "Ok";
            _message = message;
            await Create(true);
            return await _taskCompletionSource.Task;
        }

        public async Task ShowPermanent(string message)
        {
            _yesButtonActive = false;
            _noButtonActive = false;
            _message = message;
            await Create(false);
        }

        [UIAction("yes-click")]
        private async void OnYesClick()
        {
            await Hide(true);
            _taskCompletionSource.SetResult(true);
        }

        [UIAction("no-click")]
        private async void OnNoClick()
        {
            await Hide(true);
            _taskCompletionSource.SetResult(false);
        }
    }

    internal class TexturePickerPopup : Popup
    {
        [UIComponent("item-list")] private readonly BeatSaberMarkupLanguage.Components.CustomListTableData _itemList = null;
        protected override string ResourceName => "SaberFactory2.UI.CustomSaber.CustomComponents.TexturePickerPopup.bsml";
        [Inject] private readonly TextureStore _textureStore = null;
        private Action _onCancelCallback;
        private Action<Texture2D> _onSelectedCallback;
        private TextureAsset _selectedTextureAsset;
        private List<TextureAsset> _textureAssets;
        protected override void Awake()
        {
            base.Awake();
            _textureAssets = new List<TextureAsset>();
        }

        public async void Show(Action<Texture2D> onSelected, Action onCancel = null)
        {
            ParentToViewController();
            _onSelectedCallback = onSelected;
            _onCancelCallback = onCancel;
            await _textureStore.LoadAllTexturesAsync();
            _ = Create(false, false);
            RefreshList(_textureStore.GetAllTextures().ToList());
            await AnimateIn();
        }

        public void RefreshList(List<TextureAsset> items)
        {
            _textureAssets = items;
            var cells = new List<BeatSaberMarkupLanguage.Components.CustomListTableData.CustomCellInfo>();
            foreach (var textureAsset in _textureAssets)
            {
                var cell = new BeatSaberMarkupLanguage.Components.CustomListTableData.CustomCellInfo(textureAsset.Name, null, textureAsset.Sprite);
                cells.Add(cell);
            }
            _itemList.Data = cells;
            _itemList.TableView.ReloadData();
        }

        [UIAction("click-cancel")]
        private async void ClickCancel()
        {
            await Hide(false);
            _onCancelCallback?.Invoke();
        }

        [UIAction("click-select")]
        private async void ClickSelect()
        {
            await Hide(false);
            _onSelectedCallback?.Invoke(_selectedTextureAsset?.Texture);
        }

        [UIAction("item-selected")]
        private void ItemSelected(TableView _, int row)
        {
            _selectedTextureAsset = _textureAssets[row];
        }
    }
}