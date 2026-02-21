using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using HMUI;
using IPA.Utilities;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using SaberFactory2.UI.Lib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using CustomListTableData = SaberFactory2.UI.Lib.CustomListTableData;

namespace SaberFactory2.UI.CustomSaber.CustomComponents
{
    internal class CustomCellList : CustomUiComponent
    {
        [UIComponent("item-list")] private readonly CustomCellListTableData _customList = null;
        [UIComponent("header-text")] private readonly TextMeshProUGUI _headerText = null;
        public object CurrentSelectedItem { get; private set; }
        public event Action<ICustomListItem> OnItemSelected;
        public void SetItems(IEnumerable<ICustomListItem> listItems)
        {
            var content = new List<object>();
            foreach (var listItem in listItems)
            {
                content.Add(new ListItem(listItem, listItem.ListName, listItem.ListAuthor, listItem.ListCover));
            }
            _customList.Data = content;
            _customList.TableView.ReloadData();
        }

        public void SetHeader(string header)
        {
            UITemplateCache.AssignValidFont(_headerText);
            _headerText.text = header;
        }

        private void SelectItem(ICustomListItem item)
        {
            if (CurrentSelectedItem == item)
            {
                return;
            }
            CurrentSelectedItem = item;
            OnItemSelected?.Invoke(item);
        }

        [UIAction("item-selected")]
        private void Item_Selected(TableView _, ListItem item)
        {
            SelectItem((ICustomListItem)item.ContainingObject);
        }

        internal class ListItem
        {
            [UIValue("name")] public string Name { get; }
            [UIValue("author")] public string Author { get; }
            [UIComponent("cover")] private readonly ImageView _cover = null;
            public object ContainingObject { get; }
            private readonly Sprite _coverSprite;
            public ListItem(object containingObject, string name, string author, Sprite cover)
            {
                ContainingObject = containingObject;
                Name = name;
                Author = author;
                _coverSprite = cover;
            }

            [UIAction("#post-parse")]
            private void Setup()
            {
                if (_cover)
                {
                    _cover.sprite = _coverSprite;
                }
            }
        }

        [ComponentHandler(typeof(CustomCellList))]
        internal class TypeHander : TypeHandler<CustomCellList>
        {
            public override Dictionary<string, string[]> Props => new Dictionary<string, string[]>
            {
                { "title", new[] { "title", "header" } }
            };
            public override Dictionary<string, Action<CustomCellList, string>> Setters =>
                new Dictionary<string, Action<CustomCellList, string>>
                {
                    { "title", (list, val) => list.SetHeader(val) }
                };
        }
    }

    internal class CustomList : CustomUiComponent
    {
        [UIComponent("root-vertical")] private readonly LayoutElement _layoutElement = null;
        [UIComponent("item-list")] private readonly CustomListTableData _list = null;
        [UIComponent("header-text")] private readonly TextMeshProUGUI _textMesh = null;
        [Inject] private readonly IVRPlatformHelper _platformHelper = null;
        [Inject] private readonly PluginConfig _config = null;
        private int _currentIdx = -1;
        private List<ICustomListItem> _listObjects;
        public event Action<ICustomListItem> OnItemSelected;
        public event Action<string> OnCategorySelected;
        public void SetItems(IEnumerable<ICustomListItem> items)
        {
            var listItems = new List<ICustomListItem>();
            var data = new List<CustomListTableData.CustomCellInfo>();
            foreach (var item in items)
            {
                var cell = new CustomListTableData.CustomCellInfo
                {
                    Text = item.ListName,
                    Subtext = item.ListAuthor,
                    Icon = item.ListCover,
                    IsFavorite = item.IsFavorite,
                    IsCategory = item is ListDirectory,
                    Color0 = _config.ListCellColor0,
                    Color1 = _config.ListCellColor1
                };
                data.Add(cell);
                listItems.Add(item);
            }
            _list.Data = data;
            _list.TableView.ReloadData();
            _listObjects = listItems;
            _currentIdx = -1;
        }

        public void Reload()
        {
            SetItems(_listObjects);
        }

        public void Select(ICustomListItem item, bool scroll = true)
        {
            if (item == null || _listObjects == null)
            {
                return;
            }
            var idx = _listObjects.IndexOf(item);
            Select(idx, scroll);
        }

        public void Select(string listName, bool scroll = true)
        {
            if (string.IsNullOrEmpty(listName))
            {
                return;
            }
            var item = _listObjects.FirstOrDefault(x => x.ListName == listName);
            if (item == null)
            {
                return;
            }
            Select(item, scroll);
        }

        public void Select(int idx, bool scroll = true)
        {
            if (idx == -1 || idx == _currentIdx)
            {
                return;
            }
            _list.TableView.SelectCellWithIdx(idx, true);
            _currentIdx = idx;
            if (scroll)
            {
                _list.TableView.ScrollToCellWithIdx(idx, TableView.ScrollPositionType.Beginning, false);
            }
        }

        public void Deselect()
        {
            _currentIdx = -1;
            _list.TableView.ClearSelection();
        }

        public void ScrollTo(int idx)
        {
            if (idx == -1)
            {
                return;
            }
            _list.TableView.ScrollToCellWithIdx(idx, TableView.ScrollPositionType.Beginning, false);
        }

        private void SetWidth(float width)
        {
            _layoutElement.preferredWidth = width;
        }

        private void SetHeight(float height)
        {
            _layoutElement.preferredHeight = height;
        }

        public void SetText(string text)
        {
            UITemplateCache.AssignValidFont(_textMesh);
            _textMesh.text = text;
        }

        private void SetHeaderSize(float size)
        {
            _textMesh.fontSize = size;
        }

        private void SetBgColor(Color color)
        {
            _layoutElement.gameObject.GetComponent<Backgroundable>().Background.color = color;
        }

        [UIAction("#post-parse")]
        private void Setup()
        {
            _list.TableView.GetField<ScrollView, TableView>("_scrollView").SetField("_platformHelper", _platformHelper);
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                UITemplateCache.AssignValidFont(text);
            }
        }

        [UIAction("item-selected")]
        private void ItemSelected(TableView _, int row)
        {
            var obj = _listObjects[row];
            if (obj is ListDirectory dir)
            {
                OnCategorySelected?.Invoke(dir.ListName);
                return;
            }
            _currentIdx = row;
            OnItemSelected?.Invoke(obj);
        }

        internal class ListDirectory : ICustomListItem
        {
            public ListDirectory(string listName)
            {
                ListName = listName;
            }

            public string ListName { get; }
            public string ListAuthor => "";
            public Sprite ListCover => null;
            public bool IsFavorite => false;
        }

        [ComponentHandler(typeof(CustomList))]
        internal class TypeHandler : TypeHandler<CustomList>
        {
            public override Dictionary<string, string[]> Props => new Dictionary<string, string[]>
            {
                { "title", new[] { "title", "header" } },
                { "width", new[] { "width" } },
                { "height", new[] { "height" } },
                { "bgColor", new[] { "bg-color" } },
                { "titleSize", new[] { "title-size" } }
            };
            public override Dictionary<string, Action<CustomList, string>> Setters =>
                new Dictionary<string, Action<CustomList, string>>
                {
                    { "title", (list, val) => list.SetText(val) },
                    { "width", (list, val) => list.SetWidth(float.Parse(val, CultureInfo.InvariantCulture)) },
                    { "height", (list, val) => list.SetHeight(float.Parse(val, CultureInfo.InvariantCulture)) },
                    { "bgColor", SetBgColor },
                    { "titleSize", (list, val) => list.SetHeaderSize(float.Parse(val, CultureInfo.InvariantCulture)) }
                };
            private void SetBgColor(CustomList list, string hex)
            {
                if (!ThemeManager.GetColor(hex, out var color))
                {
                    return;
                }
                list.SetBgColor(color);
            }
        }
    }

    internal class IconToggleButton : CustomUiComponent
    {
        [UIComponent("icon-button")] private readonly ButtonImageController _iconButtonImageController = null;
        [UIValue("hover-hint")] private string _hoverHint = "";
        public Color OnColor { get; set; }
        public Color OffColor { get; set; }
        public bool IsOn { get; private set; }
        public event Action<bool> OnStateChanged;
        public void SetIcon(string path)
        {
            _iconButtonImageController.SetIcon(path);
        }

        public void SetState(bool state, bool fireEvent)
        {
            IsOn = state;
            UpdateColor();
            if (fireEvent)
            {
                OnStateChanged?.Invoke(state);
            }
        }

        public void SetHoverHint(string text)
        {
            _hoverHint = text;
        }

        private void UpdateColor()
        {
            var image = _iconButtonImageController.ForegroundImage;
            if (!image)
            {
                return;
            }
            image.color = IsOn ? OnColor : OffColor;
        }

        [UIAction("clicked")]
        private void ClickedButton()
        {
            SetState(!IsOn, true);
        }

        [UIAction("#post-parse")]
        private void Setup()
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                UITemplateCache.AssignValidFont(text);
            }
        }

        [ComponentHandler(typeof(IconToggleButton))]
        internal class TypeHandler : TypeHandler<IconToggleButton>
        {
            public override Dictionary<string, string[]> Props => new Dictionary<string, string[]>
            {
                { "icon", new[] { "icon" } },
                { "onColor", new[] { "on-color" } },
                { "offColor", new[] { "off-color" } },
                { "onToggle", new[] { "on-toggle" } },
                { "hoverhint", new[] { "hover-hint" } }
            };
            public override Dictionary<string, Action<IconToggleButton, string>> Setters =>
                new Dictionary<string, Action<IconToggleButton, string>>
                {
                    { "icon", (button, val) => button.SetIcon(val) },
                    { "onColor", SetOnColor },
                    { "offColor", SetOffColor },
                    { "hoverhint", (button, val) => button.SetHoverHint(val) }
                };
            public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
            {
                try
                {
                    var button = componentType.Component as IconToggleButton;
                    if (componentType.Data.TryGetValue("onToggle", out var onToggle))
                    {
                        if (parserParams.Actions.TryGetValue(onToggle, out var onToggleAction))
                        {
                            button.OnStateChanged += val => { onToggleAction.Invoke(val); };
                        }
                    }
                    base.HandleType(componentType, parserParams);
                }
                catch (Exception ex)
                { Plugin.Logger.Error($"Error handling IconToggleButton type: {ex}"); }
            }

            private void SetOnColor(IconToggleButton button, string hexString)
            {
                if (hexString == "none")
                {
                    return;
                }
                ColorUtility.TryParseHtmlString(hexString, out var color);
                button.OnColor = color;
            }

            private void SetOffColor(IconToggleButton button, string hexString)
            {
                if (hexString == "none")
                {
                    return;
                }
                ColorUtility.TryParseHtmlString(hexString, out var color);
                button.OffColor = color;
            }
        }
    }

    internal class NavButton : CustomUiComponent
    {
        [UIComponent("icon-button")] private readonly ButtonImageController _iconButton = null;
        [UIValue("hover-hint")] private string _hoverHint;
        public Color OnColor
        {
            get => _onColor;
            set
            {
                _onColor = value;
                UpdateColor();
            }
        }

        public Color OffColor
        {
            get => _offColor;
            set
            {
                _offColor = value;
                UpdateColor();
            }
        }

        public bool IsOn { get; private set; }
        public string CategoryId { get; private set; }
        public Action<NavButton, string> OnSelect;
        private readonly Color _iconShadedColor = new Color(1, 1, 1, 0.6f);
        private ButtonStateColors _buttonStateColors;
        private Color _hoverColor;
        private Color _offColor;
        private Color _onColor;
        public void SetState(bool state, bool fireEvent)
        {
            IsOn = state;
            UpdateColor();
            if (fireEvent)
            {
                OnSelect?.Invoke(this, CategoryId);
            }
        }

        public void Deselect()
        {
            SetState(false, false);
        }

        [UIAction("clicked")]
        private void Clicked()
        {
            SetState(true, true);
        }

        private void UpdateColor()
        {
            if (_buttonStateColors is null)
            {
                return;
            }
            _buttonStateColors.NormalColor = IsOn ? OnColor : Color.clear;
            _buttonStateColors.HoveredColor = IsOn ? OnColor : _hoverColor;
            _iconButton.ForegroundImage.color = IsOn ? Color.white : _iconShadedColor;
            _buttonStateColors.Image.gradient = !IsOn;
            _buttonStateColors.UpdateSelectionState();
        }

        public void SetIcon(string path)
        {
            _iconButton.ForegroundImage.sprite = TextureUtilities.LoadSpriteFromResource(path);
        }

        public void SetHoverHint(string hoverHint)
        {
            _hoverHint = hoverHint;
        }

        public void SetCategoryId(string id)
        {
            CategoryId = id;
        }

        [UIAction("#post-parse")]
        private void Setup()
        {
            _buttonStateColors = GetComponentsInChildren<ButtonStateColors>().First();
            _hoverColor = _buttonStateColors.HoveredColor;
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                UITemplateCache.AssignValidFont(text);
            }
        }

        [ComponentHandler(typeof(NavButton))]
        internal class TypeHandler : TypeHandler<NavButton>
        {
            public override Dictionary<string, string[]> Props => new Dictionary<string, string[]>
            {
                { "icon", new[] { "icon" } },
                { "onColor", new[] { "on-color" } },
                { "offColor", new[] { "off-color" } },
                { "onSelected", new[] { "on-selected" } },
                { "hoverhint", new[] { "hover-hint" } },
                { "catId", new[] { "category" } }
            };
            public override Dictionary<string, Action<NavButton, string>> Setters =>
                new Dictionary<string, Action<NavButton, string>>
                {
                    { "icon", (button, val) => button.SetIcon(val) },
                    { "onColor", SetOnColor },
                    { "offColor", SetOffColor },
                    { "hoverhint", (button, val) => button.SetHoverHint(val) },
                    { "category", (button, val) => button.SetCategoryId(val) }
                };
            public override void HandleType(BSMLParser.ComponentTypeWithData componentType,
                BSMLParserParams parserParams)
            {
                try
                {
                    var button = componentType.Component as NavButton;
                    if (componentType.Data.TryGetValue("onSelected", out var onToggle))
                    {
                        if (parserParams.Actions.TryGetValue(onToggle, out var onToggleAction))
                        {
                            button.OnSelect += (btn, val) => { onToggleAction.Invoke(btn, val); };
                        }
                    }
                    base.HandleType(componentType, parserParams);
                }
                catch (Exception ex)
                { Plugin.Logger.Error($"Error handling NavButton type: {ex}"); }
            }

            private void SetOnColor(NavButton button, string hexString)
            {
                if (hexString == "none")
                {
                    return;
                }
                ColorUtility.TryParseHtmlString(hexString, out var color);
                button.OnColor = color;
            }

            private void SetOffColor(NavButton button, string hexString)
            {
                if (hexString == "none")
                {
                    return;
                }
                ColorUtility.TryParseHtmlString(hexString, out var color);
                button.OffColor = color;
            }
        }
    }

    internal class PropList : CustomUiComponent
    {
        [UIComponent("item-container")] private readonly ScrollView _scrollView = null;
        private readonly List<BasePropCell> _cells = new List<BasePropCell>();
        private RectTransform _layoutContainer;
        private RectTransform GetLayoutContainer()
        {
            if (_layoutContainer != null)
            {
                return _layoutContainer;
            }
            var content = _scrollView.GetField<RectTransform, ScrollView>("_contentRectTransform");
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.GetComponent<VerticalLayoutGroup>() != null)
                {
                    _layoutContainer = child as RectTransform;
                    return _layoutContainer;
                }
            }
            _layoutContainer = content;
            return _layoutContainer;
        }

        public void SetItems(IEnumerable<PropertyDescriptor> props)
        {
            Clear();
            foreach (var propertyDescriptor in props)
            {
                AddCell(propertyDescriptor);
            }
        }

        public void Clear()
        {
            var container = GetLayoutContainer();
            foreach (Transform t in container)
            {
                t.gameObject.TryDestroy();
            }
            _cells.Clear();
        }

        public void AddCell(PropertyDescriptor data)
        {
            if (data.PropObject == null)
            {
                return;
            }
            var go = new GameObject("PropCell");
            var container = GetLayoutContainer();
            go.transform.SetParent(container, false);
            go.AddComponent<RectTransform>();
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 16;
            layoutElement.preferredWidth = 70;
            var contentSizeFitter = go.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var cellType = data.Type switch
            {
                EPropertyType.Float => typeof(FloatPropCell),
                EPropertyType.Bool => typeof(BoolPropCell),
                EPropertyType.Color => typeof(ColorPropCell),
                EPropertyType.Texture => typeof(TexturePropCell),
                _ => throw new ArgumentOutOfRangeException(nameof(data), "cell type not handled")
            };
            var comp = (BasePropCell)go.AddComponent(cellType);
            BsmlDecorator.ParseFromResource(comp.ContentLocation, go, comp);
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                UITemplateCache.AssignValidFont(text);
                text.enableWordWrapping = false;
                text.enableAutoSizing = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.fontSize *= 0.85f;
            }

            comp.SetData(data);
            _cells.Add(comp);
        }
    }
}