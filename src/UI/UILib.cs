using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Macros;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Tags;
using BeatSaberMarkupLanguage.TypeHandlers;
using BGLib.Polyglot;
using HMUI;
using IPA.Utilities;
using JetBrains.Annotations;
using SaberFactory2.Helpers;
using SaberFactory2.UI.CustomSaber.Popups;
using SiraUtil.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRUIControls;
using Zenject;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Screen = HMUI.Screen;

namespace SaberFactory2.UI.Lib
{
    internal static partial class UITemplateCache
    {
        private static TMP_FontAsset _cachedFont;
        public static TMP_FontAsset GetMainFont()
        {
            if (_cachedFont != null) return _cachedFont;
            var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().LastOrDefault(f => f.name == "Teko-Medium SDF No Glow");
            if (font == null) font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().LastOrDefault(f => f.name == "Teko-Medium SDF");
            if (font == null) font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            _cachedFont = font;
            return font;
        }

        private static Material _cachedMaterial;
        public static Material GetMainMaterial()
        {
            if (_cachedMaterial != null) return _cachedMaterial;
            var font = GetMainFont();
            if (font != null && font.material != null) _cachedMaterial = font.material;
            if (_cachedMaterial == null) _cachedMaterial = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == "Teko-Medium SDF No Glow");
            if (_cachedMaterial == null) _cachedMaterial = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault();
            return _cachedMaterial;
        }

        public static void AssignValidFont(TextMeshProUGUI textMesh)
        {
            if (textMesh == null) return;

            if (textMesh.font == null)
            {
                textMesh.font = GetMainFont();
            }

            if (textMesh.fontSharedMaterial == null)
            {
                textMesh.fontSharedMaterial = GetMainMaterial();
            }
        }

        public static void AssignValidFont(TextMeshPro textMesh)
        {
            if (textMesh == null) return;

            if (textMesh.font == null)
            {
                textMesh.font = GetMainFont();
            }

            if (textMesh.fontSharedMaterial == null)
            {
                textMesh.fontSharedMaterial = GetMainMaterial();
            }
        }

        private static Button _practiceButtonTemplate;
        public static Button PracticeButtonTemplate
        {
            get
            {
                if (_practiceButtonTemplate == null)
                {
                    _practiceButtonTemplate = Resources.FindObjectsOfTypeAll<Button>().LastOrDefault(x => x.name == "PracticeButton");
                    if (_practiceButtonTemplate == null)
                    {
                        _practiceButtonTemplate = Resources.FindObjectsOfTypeAll<Button>().LastOrDefault(x => x.name == "PlayButton");
                    }
                }
                return _practiceButtonTemplate;
            }
        }
    }

    public class ButtonWithIconTag : BSMLTag
    {
        public override string[] Aliases => new[] { CustomComponentHandler.ComponentPrefix + ".icon-button" };
        public override GameObject CreateObject(Transform parent)
        {
            var template = UITemplateCache.PracticeButtonTemplate;
            if (template == null) return new GameObject("ButtonWithIcon_Fallback");
            var button = Object.Instantiate(template, parent, false);
            button.name = "CustomIconButton";
            button.interactable = true;
            var hoverHint = button.GetComponent<HoverHint>();
            if (hoverHint) Object.Destroy(hoverHint);
            var localizedHoverHint = button.GetComponent<LocalizedHoverHint>();
            if (localizedHoverHint) localizedHoverHint.enabled = false;
            button.GetComponent<ButtonStaticAnimations>().TryDestroy();
            button.gameObject.AddComponent<ExternalComponents>().Components
                .Add(button.GetComponentsInChildren<LayoutGroup>().First(x => x.name == "Content"));
            var contentTransform = button.transform.Find("Content");
            contentTransform.GetComponent<LayoutElement>().minWidth = 0;
            var textObj = contentTransform.Find("Text");
            if (textObj) Object.Destroy(textObj.gameObject);
            var iconImage = new GameObject("Icon").AddComponent<ImageView>();
            iconImage.material = Utilities.ImageResources.NoGlowMat;
            iconImage.rectTransform.SetParent(contentTransform, false);
            iconImage.rectTransform.sizeDelta = new Vector2(20f, 20f);
            iconImage.sprite = Utilities.ImageResources.BlankSprite;
            iconImage.preserveAspect = true;
            var btnImageController = button.gameObject.AddComponent<ButtonImageController>();
            btnImageController.ForegroundImage = iconImage;
            btnImageController.BackgroundImage = button.transform.Find("BG").GetComponent<ImageView>();
            btnImageController.LineImage = button.transform.Find("Underline").GetComponent<ImageView>();
            btnImageController.BackgroundImage.color0 = new Color(1, 1, 1, 1);
            btnImageController.BackgroundImage.color1 = new Color(1, 1, 1, 0f);
            var noTransitionsButton = button.gameObject.GetComponent<NoTransitionsButton>();
            var buttonStateColors = button.gameObject.AddComponent<ButtonStateColors>();
            buttonStateColors.Image = btnImageController.BackgroundImage;
            buttonStateColors.UnderlineImage = btnImageController.LineImage;
            noTransitionsButton.selectionStateDidChangeEvent += buttonStateColors.SelectionDidChange;

            var externalComponents = button.gameObject.GetComponent<ExternalComponents>();
            externalComponents.Components.Add(btnImageController);
            externalComponents.Components.Add(buttonStateColors);

            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            return button.gameObject;
        }
    }

    public class CustomButtonTag : BSMLTag
    {
        private static readonly Color _defaultNormalColor = new Color(0.086f, 0.090f, 0.101f, 0.8f);
        private static readonly Color _defaultHoveredColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        public override string[] Aliases => new[] { CustomComponentHandler.ComponentPrefix + ".button" };

        public override GameObject CreateObject(Transform parent)
        {
            var template = UITemplateCache.PracticeButtonTemplate;
            if (template == null) return new GameObject("CustomButton_Fallback");

            var buttonGo = Object.Instantiate(template.gameObject, parent, false);
            buttonGo.name = "BSMLButton";
            var button = buttonGo.GetComponent<NoTransitionsButton>();
            if (button == null)
            {
                var oldBtn = buttonGo.GetComponent<Button>();
                if (oldBtn) Object.DestroyImmediate(oldBtn);
                button = buttonGo.AddComponent<NoTransitionsButton>();
            }

            button.interactable = true;
            buttonGo.GetComponentInChildren<BGLib.Polyglot.LocalizedTextMeshProUGUI>(true).TryDestroy();

            var externalComponents = buttonGo.AddComponent<ExternalComponents>();
            var textMesh = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
            textMesh.richText = true;

            UITemplateCache.AssignValidFont(textMesh);

            externalComponents.Components.Add(textMesh);

            var contentTransform = buttonGo.transform.Find("Content");
            if (contentTransform != null)
            {
                var layoutElement = contentTransform.GetComponent<LayoutElement>();
                if (layoutElement) Object.Destroy(layoutElement);
            }

            var bgImage = buttonGo.transform.Find("BG").gameObject.GetComponent<ImageView>();
            ButtonStaticAnimations staticAnimations = buttonGo.GetComponent<ButtonStaticAnimations>();
            if (staticAnimations != null) staticAnimations.TryDestroy();

            var buttonStateColors = buttonGo.AddComponent<ButtonStateColors>();
            externalComponents.Components.Add(buttonStateColors);
            buttonStateColors.Image = bgImage;
            buttonStateColors.NormalColor = _defaultNormalColor;
            buttonStateColors.HoveredColor = _defaultHoveredColor;
            buttonStateColors.SelectedColor = _defaultHoveredColor;

            buttonStateColors.SelectionDidChange(NoTransitionsButton.SelectionState.Normal);
            button.selectionStateDidChangeEvent += buttonStateColors.SelectionDidChange;

            var buttonImageController = buttonGo.AddComponent<ButtonImageController>();
            externalComponents.Components.Add(buttonImageController);
            buttonImageController.BackgroundImage = bgImage;

            var underlineTransform = buttonGo.transform.Find("Underline");
            if (underlineTransform != null)
            {
                buttonImageController.LineImage = underlineTransform.gameObject.GetComponent<ImageView>();
                buttonStateColors.UnderlineImage = buttonImageController.LineImage;
            }
            buttonImageController.ShowLine(false);

            bgImage.SetSkew(0);

            var buttonSizeFitter = buttonGo.AddComponent<ContentSizeFitter>();
            buttonSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layoutElem = buttonGo.GetComponent<LayoutElement>();
            if (layoutElem == null) layoutElem = buttonGo.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 10;

            var stackLayoutGroup = buttonGo.GetComponentInChildren<LayoutGroup>();
            if (stackLayoutGroup != null)
            {
                externalComponents.Components.Add(stackLayoutGroup);
            }

            if (!buttonGo.activeSelf)
            {
                buttonGo.SetActive(true);
            }
            return buttonGo;
        }
    }

    [ComponentHandler(typeof(CustomListTableData))]
    public class CustomListTableDataHandler : TypeHandler
    {
        private static readonly Dictionary<string, string[]> _props = new Dictionary<string, string[]>
        {
            { "selectCell", new[] { "select-cell" } },
            { "visibleCells", new[] { "visible-cells" } },
            { "cellSize", new[] { "cell-size" } },
            { "id", new[] { "id" } },
            { "data", new[] { "data", "content" } },
            { "listWidth", new[] { "list-width" } },
            { "listHeight", new[] { "list-height" } },
            { "expandCell", new[] { "expand-cell" } },
            { "listStyle", new[] { "list-style" } },
            { "listDirection", new[] { "list-direction" } },
            { "alignCenter", new[] { "align-to-center" } }
        };
        public override Dictionary<string, string[]> Props => _props;
        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
            var tableData = componentType.Component as CustomListTableData;
            if (componentType.Data.TryGetValue("selectCell", out var selectCell))
            {
                tableData.TableView.didSelectCellWithIdxEvent += delegate (TableView table, int index)
                {
                    if (!parserParams.Actions.TryGetValue(selectCell, out var action))
                    {
                        throw new Exception("select-cell action '" + componentType.Data["onClick"] + "' not found");
                    }
                    action.Invoke(table, index);
                };
            }
            if (componentType.Data.TryGetValue("listDirection", out var listDirection))
            {
                tableData.TableView.SetField("_tableType", (TableView.TableType)Enum.Parse(typeof(TableView.TableType), listDirection));
            }
            if (componentType.Data.TryGetValue("listStyle", out var listStyle))
            {
                tableData.Style = (CustomListTableData.ListStyle)Enum.Parse(typeof(CustomListTableData.ListStyle), listStyle);
            }
            if (componentType.Data.TryGetValue("cellSize", out var cellSize))
            {
                tableData.cellSize = Parse.Float(cellSize);
            }
            if (componentType.Data.TryGetValue("expandCell", out var expandCell))
            {
                tableData.ExpandCell = Parse.Bool(expandCell);
            }
            if (componentType.Data.TryGetValue("alignCenter", out var alignCenter))
            {
                tableData.TableView.SetField("_alignToCenter", Parse.Bool(alignCenter));
            }
            if (componentType.Data.TryGetValue("data", out var value))
            {
                if (!parserParams.Values.TryGetValue(value, out var contents))
                {
                    throw new Exception("value '" + value + "' not found");
                }
                tableData.Data = contents.GetValue() as List<CustomListTableData.CustomCellInfo>;
                tableData.TableView.ReloadData();
            }
            switch (tableData.TableView.tableType)
            {
                case TableView.TableType.Vertical:
                    (componentType.Component.gameObject.transform as RectTransform).sizeDelta = new Vector2(
                        componentType.Data.TryGetValue("listWidth", out var vListWidth) ? Parse.Float(vListWidth) : 60,
                        tableData.cellSize * (componentType.Data.TryGetValue("visibleCells", out var vVisibleCells)
                            ? Parse.Float(vVisibleCells)
                            : 7));
                    break;
                case TableView.TableType.Horizontal:
                    (componentType.Component.gameObject.transform as RectTransform).sizeDelta = new Vector2(
                        tableData.cellSize * (componentType.Data.TryGetValue("visibleCells", out var hVisibleCells) ? Parse.Float(hVisibleCells) : 4),
                        componentType.Data.TryGetValue("listHeight", out var hListHeight) ? Parse.Float(hListHeight) : 40);
                    break;
            }
            componentType.Component.gameObject.GetComponent<LayoutElement>().preferredHeight =
                (componentType.Component.gameObject.transform as RectTransform).sizeDelta.y;
            componentType.Component.gameObject.GetComponent<LayoutElement>().preferredWidth =
                (componentType.Component.gameObject.transform as RectTransform).sizeDelta.x;
            tableData.TableView.gameObject.SetActive(true);
            tableData.TableView.LazyInit();
            if (componentType.Data.TryGetValue("id", out var id))
            {
                var scroller = tableData.TableView.GetField<ScrollView, TableView>("_scrollView");
                parserParams.AddEvent(id + "#PageUp", scroller.PageUpButtonPressed);
                parserParams.AddEvent(id + "#PageDown", scroller.PageDownButtonPressed);
            }
        }
    }

    internal static partial class UITemplateCache
    {
        private static Canvas _dropdownTableViewTemplate;
        public static Canvas DropdownTableViewTemplate
        {
            get
            {
                if (_dropdownTableViewTemplate == null)
                {
                    _dropdownTableViewTemplate = Resources.FindObjectsOfTypeAll<Canvas>().First(x => x.name == "DropdownTableView");
                }
                return _dropdownTableViewTemplate;
            }
        }
    }

    public class CustomListTag : BSMLTag
    {
        public override string[] Aliases => new[] { CustomComponentHandler.ComponentPrefix + ".list" };
        public override GameObject CreateObject(Transform parent)
        {
            var rootGO = new GameObject("CustomListContainer");
            var container = rootGO.AddComponent<RectTransform>();
            container.gameObject.AddComponent<LayoutElement>();
            container.SetParent(parent, false);
            var gameObject = new GameObject();
            gameObject.transform.SetParent(container, false);
            gameObject.name = "CustomList";
            gameObject.SetActive(false);
            gameObject.AddComponent<ScrollRect>();
            var template = UITemplateCache.DropdownTableViewTemplate;
            if (template != null) gameObject.AddComponent(template);
            gameObject.AddComponent<VRGraphicRaycaster>().SetField("_physicsRaycaster", BeatSaberUI.PhysicsRaycasterWithCache);
            gameObject.AddComponent<Touchable>();
            gameObject.AddComponent<EventSystemListener>();
            var scrollView = gameObject.AddComponent<ScrollView>();
            var platformHelper = UITemplateCache.PlatformHelper;
            if (platformHelper != null)
            {
                scrollView.SetField<ScrollView, IVRPlatformHelper>("_platformHelper", platformHelper);
            }
            TableView tableView = gameObject.AddComponent<BSMLTableView>();
            var tableData = container.gameObject.AddComponent<CustomListTableData>();
            tableData.TableView = tableView;
            tableView.SetField("_preallocatedCells", new TableView.CellsGroup[0]);
            tableView.SetField("_isInitialized", false);
            tableView.SetField("_scrollView", scrollView);
            var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
            viewport.SetParent(gameObject.GetComponent<RectTransform>(), false);
            viewport.gameObject.AddComponent<RectMask2D>();
            gameObject.GetComponent<ScrollRect>().viewport = viewport;
            var content = new GameObject("Content").AddComponent<RectTransform>();
            content.SetParent(viewport, false);
            scrollView.SetField("_contentRectTransform", content);
            scrollView.SetField("_viewport", viewport);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.sizeDelta = new Vector2(0f, 0f);
            viewport.anchoredPosition = new Vector3(0f, 0f);
            var tableviewRect = (RectTransform)tableView.transform;
            tableviewRect.anchorMin = new Vector2(0f, 0f);
            tableviewRect.anchorMax = new Vector2(1f, 1f);
            tableviewRect.sizeDelta = new Vector2(0f, 0f);
            tableviewRect.anchoredPosition = new Vector3(0f, 0f);
            tableView.SetDataSource(tableData, false);
            return container.gameObject;
        }
    }

    internal static partial class UITemplateCache
    {
        private static TextPageScrollView _textPageScrollViewTemplate;
        public static TextPageScrollView TextPageScrollViewTemplate
        {
            get
            {
                if (_textPageScrollViewTemplate == null)
                {
                    _textPageScrollViewTemplate = Resources.FindObjectsOfTypeAll<TextPageScrollView>().First(x => x.name == "TextPageScrollView");
                }
                return _textPageScrollViewTemplate;
            }
        }

        private static IVRPlatformHelper _platformHelper;
        public static IVRPlatformHelper PlatformHelper
        {
            get
            {
                if (_platformHelper == null)
                {
                    foreach (var sv in Resources.FindObjectsOfTypeAll<ScrollView>())
                    {
                        var helper = sv.GetField<IVRPlatformHelper, ScrollView>("_platformHelper");
                        if (helper != null)
                        {
                            _platformHelper = helper;
                            break;
                        }
                    }
                }
                return _platformHelper;
            }
        }
    }

    public class CustomScrollViewTag : BSMLTag
    {
        public override string[] Aliases => new[] { CustomComponentHandler.ComponentPrefix + ".scroll-view" };
        public override GameObject CreateObject(Transform parent)
        {
            var template = UITemplateCache.TextPageScrollViewTemplate;
            if (template == null) return new GameObject("CustomScrollView_Fallback");
            var textScrollView = Object.Instantiate(template, parent);
            textScrollView.name = "BSMLScrollView";
            var pageUpButton = textScrollView.GetField<Button, ScrollView>("_pageUpButton");
            var pageDownButton = textScrollView.GetField<Button, ScrollView>("_pageDownButton");
            var verticalScrollIndicator = textScrollView.GetField<VerticalScrollIndicator, ScrollView>("_verticalScrollIndicator");
            var viewport = textScrollView.GetField<RectTransform, ScrollView>("_viewport");
            viewport.gameObject.AddComponent<VRGraphicRaycaster>().SetField("_physicsRaycaster", BeatSaberUI.PhysicsRaycasterWithCache);
            Object.Destroy(textScrollView.GetField<TextMeshProUGUI, TextPageScrollView>("_text").gameObject);
            var gameObject = textScrollView.gameObject;
            Object.Destroy(textScrollView);
            gameObject.SetActive(false);
            var scrollView = gameObject.AddComponent<BSMLScrollView>();
            var platformHelper = UITemplateCache.PlatformHelper;
            if (platformHelper != null)
            {
                scrollView.SetField<ScrollView, IVRPlatformHelper>("_platformHelper", platformHelper);
            }
            scrollView.SetField<ScrollView, Button>("_pageUpButton", pageUpButton);
            scrollView.SetField<ScrollView, Button>("_pageDownButton", pageDownButton);
            scrollView.SetField<ScrollView, VerticalScrollIndicator>("_verticalScrollIndicator", verticalScrollIndicator);
            scrollView.SetField<ScrollView, RectTransform>("_viewport", viewport);
            viewport.anchorMin = new Vector2(0, 0);
            viewport.anchorMax = new Vector2(1, 1);
            var parentObj = new GameObject();
            parentObj.name = "BSMLScrollViewContent";
            parentObj.transform.SetParent(viewport, false);
            var contentSizeFitter = parentObj.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var verticalLayout = parentObj.AddComponent<VerticalLayoutGroup>();
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childForceExpandWidth = false;
            verticalLayout.childControlHeight = true;
            verticalLayout.childControlWidth = true;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            var rectTransform = parentObj.transform.AsRectTransform();
            parentObj.AddComponent<LayoutElement>();
            parentObj.AddComponent<ScrollViewContent>().ScrollView = scrollView;
            var child = new GameObject();
            child.name = "BSMLScrollViewContentContainer";
            child.transform.SetParent(rectTransform, false);
            var layoutGroup = child.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.spacing = 0.5f;

            parentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var childFitter = child.AddComponent<ContentSizeFitter>();
            childFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            child.AddComponent<LayoutElement>();

            var externalComponents = child.AddComponent<ExternalComponents>();
            externalComponents.Components.Add(scrollView);
            externalComponents.Components.Add(scrollView.transform);

            var childRect = child.transform.AsRectTransform();
            childRect.anchorMin = new Vector2(0, 1);
            childRect.anchorMax = new Vector2(1, 1);
            childRect.pivot = new Vector2(0.5f, 1);
            childRect.sizeDelta = new Vector2(0, 0);

            var rootRect = gameObject.transform.AsRectTransform();
            rootRect.anchorMin = new Vector2(0, 0);
            rootRect.anchorMax = new Vector2(1, 1);
            rootRect.sizeDelta = new Vector2(-4, -4);
            rootRect.anchoredPosition = new Vector2(0, 0);

            scrollView.SetField<ScrollView, RectTransform>("_contentRectTransform", parentObj.transform as RectTransform);
            var runner = new GameObject("SF_ScrollViewRunner");
            var component = runner.AddComponent<CoroutineRunner>();
            component.StartCoroutine(SetupScrollViewCoroutine(gameObject, rectTransform, runner));
            return child;
        }

        private IEnumerator SetupScrollViewCoroutine(GameObject gameObject, RectTransform rectTransform, GameObject runner)
        {
            gameObject.SetActive(true);
            yield return new WaitForEndOfFrame();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.sizeDelta = new Vector2(0, 0);
            rectTransform.pivot = new Vector2(0.5f, 1);
            Object.Destroy(runner);
        }
    }

    internal class CustomUiComponentTag : BSMLTag
    {
        public override string[] Aliases => new[] { CustomComponentHandler.ComponentPrefix + "." + BSMLTools.GetKebabCaseName(_type) };
        private readonly CustomUiComponent.Factory _factory;
        private readonly Type _type;
        public CustomUiComponentTag(Type type, CustomUiComponent.Factory factory)
        {
            _type = type;
            _factory = factory;
        }

        public override GameObject CreateObject(Transform parent)
        {
            var go = parent.CreateGameObject(_type.Name);
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>();
            var contentSizeFitter = go.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.AddComponent<StackLayoutGroup>();
            var comp = _factory.Create(go, _type);
            comp.Parse();
            if (!comp.gameObject.activeSelf)
            {
                comp.gameObject.SetActive(true);
            }
            return go;
        }
    }

    internal class PopupTag : BSMLTag
    {
        public override string[] Aliases => new[] { CustomComponentHandler.ComponentPrefix + "." + BSMLTools.GetKebabCaseName(_type) };
        private readonly Popup.Factory _factory;
        private readonly Type _type;
        public PopupTag(Type type, Popup.Factory factory)
        {
            _type = type;
            _factory = factory;
        }

        public override GameObject CreateObject(Transform parent)
        {
            var go = parent.CreateGameObject(_type.Name);
            go.AddComponent<RectTransform>();
            go.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            var comp = _factory.Create(go, _type);
            return go;
        }
    }

    [ComponentHandler(typeof(Backgroundable))]
    public class SaberFactoryBackgroundableHandler : TypeHandler<Backgroundable>
    {
        private static readonly Dictionary<string, string[]> _props = new Dictionary<string, string[]>
        {
            { "border", new[] { "border" } },
            { "raycast", new[] { "raycast", "block" } },
            { "skew", new[] { "skew" } },
            { "customColor", new[] { "custom-color" } },
            { "customBg", new[] { "custom-bg" } }
        };
        private static readonly Dictionary<string, Action<Backgroundable, string>> _setters = new Dictionary<string, Action<Backgroundable, string>>();
        public override Dictionary<string, string[]> Props => _props;
        public override Dictionary<string, Action<Backgroundable, string>> Setters => _setters;
        private readonly Sprite _borderSprite;
        private Material _bgMaterial;
        private Sprite _bgSprite;
        private ImageView _imageViewPrefab;
        public SaberFactoryBackgroundableHandler()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(Utilities.GetResourceAsync(Assembly.GetExecutingAssembly(),
                "SaberFactory2.Resources.UI.border.png").Result);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            _borderSprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0f, 32f), 100, 1, SpriteMeshType.FullRect,
                new Vector4(0, 7, 7, 0));
        }

        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
            base.HandleType(componentType, parserParams);
            var backgroundable = (Backgroundable)componentType.Component;
            if (componentType.Data.TryGetValue("customBg", out var customBg))
            {
                InitSprite();
                var imageview = GetOrAddImageView(backgroundable);
                if (imageview != null)
                {
                    imageview.overrideSprite = _bgSprite;
                    backgroundable.Background = imageview;
                }
            }
            if (componentType.Data.TryGetValue("customColor", out var customColor))
            {
                TrySetBackgroundColor(backgroundable, customColor);
            }
            if (componentType.Data.TryGetValue("border", out var borderAttr))
            {
                AddBorder(backgroundable.gameObject, borderAttr == "square");
            }
            if (componentType.Data.TryGetValue("raycast", out var raycastAttr))
            {
                if (backgroundable.Background != null)
                {
                    backgroundable.Background.raycastTarget = bool.Parse(raycastAttr);
                }
            }
            if (componentType.Data.TryGetValue("skew", out var skew))
            {
                if (backgroundable.Background is ImageView imageView)
                {
                    imageView.SetSkew(float.Parse(skew, CultureInfo.InvariantCulture));
                }
            }
        }

        internal static partial class UITemplateCache
        {
            private static GameObject _borderTemplate;
            public static GameObject BorderTemplate
            {
                get
                {
                    if (_borderTemplate == null)
                    {
                        var button = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(x => x.name == "ActionButton");
                        var borderTransform = button != null ? button.transform.Find("Border") : null;
                        if (borderTransform != null)
                        {
                            _borderTemplate = borderTransform.gameObject;
                        }
                    }
                    return _borderTemplate;
                }
            }

            private static ImageView _keyboardWrapperTemplate;
            public static ImageView KeyboardWrapperTemplate
            {
                get
                {
                    if (_keyboardWrapperTemplate == null)
                    {
                        _keyboardWrapperTemplate = Resources.FindObjectsOfTypeAll<ImageView>().First(x =>
                            (x.gameObject != null ? x.gameObject.name : null) == "KeyboardWrapper" && (x.sprite != null ? x.sprite.name : null) == "RoundRect10" &&
                            (x.transform.parent != null ? x.transform.parent.name : null) == "Wrapper");
                    }
                    return _keyboardWrapperTemplate;
                }
            }

            private static ImageView _middleHorizontalCellTemplate;
            public static ImageView MiddleHorizontalCellTemplate
            {
                get
                {
                    if (_middleHorizontalCellTemplate == null)
                    {
                        var go = Resources.FindObjectsOfTypeAll<GameObject>()
                            .FirstOrDefault(x => x.name == "MiddleHorizontalTextSegmentedControlCell");
                        if (go != null)
                            _middleHorizontalCellTemplate = go.transform.Find("BG").GetComponent<ImageView>();
                    }
                    return _middleHorizontalCellTemplate;
                }
            }
        }

        private void AddBorder(GameObject go, bool squareSprite = false)
        {
            if (UITemplateCache.BorderTemplate == null)
            {
                return;
            }
            var borderGo = Object.Instantiate(UITemplateCache.BorderTemplate, go.transform).GetRect();
            borderGo.transform.SetParent(go.transform, false);
            if (go.GetComponent<HorizontalOrVerticalLayoutGroup>() != null)
            {
                var layout = borderGo.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
            }
            borderGo.anchorMin = Vector2.zero;
            borderGo.anchorMax = Vector2.one;
            borderGo.anchoredPosition = Vector2.zero;
            borderGo.sizeDelta = Vector2.zero;
            var image = borderGo.GetComponent<ImageView>();
            image.SetSkew(0);
            if (squareSprite)
            {
                borderGo.anchorMin = new Vector2(0.015f, -0.01f);
                borderGo.anchorMax = new Vector2(1.006f, 0.97f);
                InitSprite();
                image.sprite = _borderSprite;
                image.overrideSprite = _borderSprite;
                image.material = _bgMaterial;
                var color = image.color;
                color.a = 0.8f;
                image.color = color;
            }
        }

        private void InitSprite()
        {
            if (_bgSprite != null)
            {
                return;
            }
            var image = UITemplateCache.MiddleHorizontalCellTemplate;
            if (image == null)
            {
                Debug.LogError("Couldn't find background image prefab");
                return;
            }
            _bgSprite = image.sprite;
            _bgMaterial = image.material;
        }

        private ImageView GetOrAddImageView(Backgroundable backgroundable)
        {
            var imageView = backgroundable.GetComponent<ImageView>();
            if (imageView != null)
            {
                return imageView;
            }
            if (_imageViewPrefab == null)
            {
                _imageViewPrefab = Resources.FindObjectsOfTypeAll<ImageView>().First(x =>
                    (x.gameObject != null ? x.gameObject.name : null) == "KeyboardWrapper" && (x.sprite != null ? x.sprite.name : null) == "RoundRect10" &&
                    (x.transform.parent != null ? x.transform.parent.name : null) == "Wrapper");
                if (_imageViewPrefab == null)
                {
                    return null;
                }
            }
            return backgroundable.gameObject.AddComponent(_imageViewPrefab);
        }

        public static void TrySetBackgroundColor(Backgroundable background, string colorStr)
        {
            if (!ThemeManager.GetColor(colorStr, out var color))
            {
                return;
            }
            background.Background.color = color;
        }
    }

    public static class BSMLTools
    {
        public static string GetKebabCaseName(Type type)
        {
            var name = type.Name;
            var builder = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                if (char.IsLower(name[i]))
                {
                    builder.Append(name[i]);
                }
                else if (i == 0)
                {
                    builder.Append(char.ToLowerInvariant(name[i]));
                }
                else if (char.IsDigit(name[i]) && !char.IsDigit(name[i - 1]))
                {
                    builder.Append('-');
                    builder.Append(name[i]);
                }
                else if (char.IsDigit(name[i]))
                {
                    builder.Append(name[i]);
                }
                else if (char.IsLower(name[i - 1]))
                {
                    builder.Append('-');
                    builder.Append(char.ToLowerInvariant(name[i]));
                }
                else if (i + 1 == name.Length || char.IsUpper(name[i + 1]))
                {
                    builder.Append(char.ToLowerInvariant(name[i]));
                }
                else
                {
                    builder.Append('-');
                    builder.Append(char.ToLowerInvariant(name[i]));
                }
            }
            return builder.ToString();
        }
    }

    public class ButtonImageController : MonoBehaviour
    {
        public ImageView BackgroundImage;
        public ImageView ForegroundImage;
        public ImageView LineImage;
        public void SetIcon(string path)
        {
            if (ForegroundImage == null)
            {
                return;
            }
            ForegroundImage.sprite = TextureUtilities.LoadSpriteFromResource(path);
        }

        public void SetIconColor(string colorString)
        {
            if (ForegroundImage is null)
            {
                return;
            }
            if (ColorUtility.TryParseHtmlString(colorString, out var color))
            {
                ForegroundImage.color = color;
            }
        }

        public void SetBackgroundIcon(string path)
        {
            if (BackgroundImage == null)
            {
                return;
            }
            BackgroundImage.sprite = TextureUtilities.LoadSpriteFromResource(path);
        }

        public void ShowLine(bool show)
        {
            if (LineImage == null)
            {
                return;
            }
            LineImage.gameObject.SetActive(show);
        }

        public void SetLineColor(string colorString)
        {
            if (LineImage is null)
            {
                return;
            }
            if (!ColorUtility.TryParseHtmlString(colorString, out var color))
            {
                return;
            }
            LineImage.color = color;
        }

        public void SetIconPad(string sizeStr)
        {
            if (ForegroundImage is null)
            {
                return;
            }
            var size = int.Parse(sizeStr, CultureInfo.InvariantCulture);
            ForegroundImage.transform.parent.GetComponent<StackLayoutGroup>().padding = new RectOffset(size, size, size, size);
        }
    }

    [ComponentHandler(typeof(ButtonImageController))]
    public class ButtonImageControllerHandler : TypeHandler<ButtonImageController>
    {
        private static readonly Dictionary<string, string[]> _props = new Dictionary<string, string[]>
        {
            { "icon", new[] { "icon" } },
            { "iconColor", new[] { "icon-color" } },
            { "iconPad", new[] { "icon-pad" } },
            { "bgIcon", new[] { "bg-icon" } },
            { "showLine", new[] { "show-line" } },
            { "lineColor", new[] { "line-color" } },
            { "useGradient", new[] { "use-gradient" } },
            { "showFill", new[] { "show-fill" } },
            { "skew", new[] { "skew" } },
            { "color0", new[] { "color0" } },
            { "color1", new[] { "color1" } }
        };
        public override Dictionary<string, string[]> Props => _props;
        public override Dictionary<string, Action<ButtonImageController, string>> Setters =>
            new Dictionary<string, Action<ButtonImageController, string>>
            {
                { "icon", (images, iconPath) => images.SetIcon(iconPath) },
                { "iconColor", (images, color) => images.SetIconColor(color) },
                { "iconPad", (images, size) => images.SetIconPad(size) },
                { "bgIcon", (images, iconPath) => images.SetBackgroundIcon(iconPath) },
                { "showLine", (images, stringBool) => images.ShowLine(bool.Parse(stringBool)) },
                { "lineColor", (images, color) => images.SetLineColor(color) },
                { "useGradient", SetGradient },
                { "showFill", SetFill },
                { "skew", SetSkew },
                { "color0", SetColor0 },
                { "color1", SetColor1 }
            };
        public void SetGradient(ButtonImageController imageController, string usingGradient)
        {
            imageController.BackgroundImage.SetField("_gradient", bool.Parse(usingGradient));
        }

        public void SetFill(ButtonImageController imageController, string usingFill)
        {
            imageController.BackgroundImage.fillCenter = bool.Parse(usingFill);
        }

        private void SetColor0(ButtonImageController imageController, string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out var color);
            imageController.BackgroundImage.color0 = color;
        }

        private void SetColor1(ButtonImageController imageController, string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out var color);
            imageController.BackgroundImage.color1 = color;
        }

        private void SetSkew(ButtonImageController imageController, string skew)
        {
            imageController.BackgroundImage.SetField("_skew", float.Parse(skew, CultureInfo.InvariantCulture));
            imageController.LineImage.SetField("_skew", float.Parse(skew, CultureInfo.InvariantCulture));
            if (imageController.ForegroundImage != null)
            {
                imageController.ForegroundImage.SetField("_skew", float.Parse(skew, CultureInfo.InvariantCulture));
            }
            imageController.BackgroundImage.SetVerticesDirty();
            imageController.LineImage.SetVerticesDirty();
            if (imageController.ForegroundImage != null)
            {
                imageController.ForegroundImage.SetVerticesDirty();
            }
        }
    }

    public class ButtonStateColors : MonoBehaviour
    {
        public Color HoveredColor = new Color(0, 0, 0, 0.8f);
        public ImageView Image;
        public Color NormalColor = new Color(0, 0, 0, 0.5f);
        public Color SelectedColor;
        public ImageView UnderlineImage;
        private NoTransitionsButton.SelectionState _currentSelectionState = NoTransitionsButton.SelectionState.Normal;
        public void SetNormalColor(string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out NormalColor);
            SelectionDidChange(_currentSelectionState);
        }

        public void SetHoveredColor(string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out HoveredColor);
            SelectionDidChange(_currentSelectionState);
        }

        public void SetSelectedColor(string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out SelectedColor);
            SelectionDidChange(_currentSelectionState);
        }

        public void SelectionDidChange(NoTransitionsButton.SelectionState selectionState)
        {
            _currentSelectionState = selectionState;
            switch (selectionState)
            {
                case NoTransitionsButton.SelectionState.Normal:
                    Image.color = NormalColor;
                    if (UnderlineImage != null)
                    {
                        UnderlineImage.enabled = false;
                    }
                    break;
                case NoTransitionsButton.SelectionState.Highlighted:
                    Image.color = HoveredColor;
                    if (UnderlineImage != null)
                    {
                        UnderlineImage.enabled = true;
                    }
                    break;
                case NoTransitionsButton.SelectionState.Pressed:
                    Image.color = SelectedColor;
                    break;
                default:
                    Image.color = NormalColor;
                    break;
            }
        }

        public void UpdateSelectionState()
        {
            SelectionDidChange(_currentSelectionState);
        }
    }

    [ComponentHandler(typeof(ButtonStateColors))]
    public class ButtonStateColorsHandler : TypeHandler<ButtonStateColors>
    {
        private static readonly Dictionary<string, string[]> _props = new Dictionary<string, string[]>
        {
            { "normalColor", new[] { "normal-color" } },
            { "hoveredColor", new[] { "hovered-color" } },
            { "selectedColor", new[] { "selected-color" } }
        };
        public override Dictionary<string, string[]> Props => _props;
        public override Dictionary<string, Action<ButtonStateColors, string>> Setters =>
            new Dictionary<string, Action<ButtonStateColors, string>>
            {
                { "normalColor", (colors, val) => colors.SetNormalColor(val) },
                { "hoveredColor", (colors, val) => colors.SetHoveredColor(val) },
                { "selectedColor", (colors, val) => colors.SetSelectedColor(val) }
            };
    }

    [ComponentHandler(typeof(Backgroundable))]
    public class CustomBackgroundableHandler : TypeHandler<Backgroundable>
    {
        private static readonly Dictionary<string, string[]> _props = new Dictionary<string, string[]>
        {
            { "usingGradient", new[] { "gradient" } },
            { "usingFill", new[] { "fill" } },
            { "skew", new[] { "skew" } },
            { "color0", new[] { "color0" } },
            { "color1", new[] { "color1" } }
        };
        public override Dictionary<string, string[]> Props => _props;
        public override Dictionary<string, Action<Backgroundable, string>> Setters =>
            new Dictionary<string, Action<Backgroundable, string>>
            {
                { "usingGradient", SetGradient },
                { "usingFill", SetFill },
                { "skew", SetSkew },
                { "color0", SetColor0 },
                { "color1", SetColor1 }
            };
        public static void SetGradient(Backgroundable background, string usingGradient)
        {
            (background.Background as ImageView).SetField("_gradient", bool.Parse(usingGradient));
        }

        public static void SetFill(Backgroundable background, string usingFill)
        {
            background.Background.fillCenter = bool.Parse(usingFill);
        }

        private void SetColor0(Backgroundable background, string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out var color);
            var iv = background.Background as ImageView;
            iv.SetField("_color0", color);
            iv.SetVerticesDirty();
        }

        private void SetColor1(Backgroundable background, string colorStr)
        {
            ColorUtility.TryParseHtmlString(colorStr, out var color);
            var iv = background.Background as ImageView;
            iv.SetField("_color1", color);
            iv.SetVerticesDirty();
        }

        private void SetSkew(Backgroundable background, string skew)
        {
            var iv = background.Background as ImageView;
            iv.SetField("_skew", float.Parse(skew, CultureInfo.InvariantCulture));
            iv.SetVerticesDirty();
        }
    }

    [ComponentHandler(typeof(NoTransitionsButton))]
    public class CustomButtonHandler : TypeHandler<NoTransitionsButton>
    {
        private static readonly Dictionary<string, string[]> _props = new Dictionary<string, string[]>
        {
            { "run", new[] { "run" } }
        };
        public override Dictionary<string, string[]> Props => _props;
        public override Dictionary<string, Action<NoTransitionsButton, string>> Setters => new Dictionary<string, Action<NoTransitionsButton, string>>
        {
            { "run", SetRunAction}
        };
        private void SetRunAction(NoTransitionsButton button, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                try { Process.Start(value); } catch (Exception) { }
            });
        }
    }

    internal class CustomComponentHandler : IInitializable
    {
        public const string ComponentPrefix = "sui";
        public bool Registered { get; private set; }
        private readonly SiraLog _logger;
        private CustomComponentHandler(
            SiraLog logger,
            Popup.Factory popupFactory,
            CustomUiComponent.Factory customUiComponentFactory)
        {
            _logger = logger;
            _popupFactory = popupFactory;
            _customUiComponentFactory = customUiComponentFactory;
            _bsmlParser = BSMLParser.Instance;
            RegisterAll(BSMLParser.Instance);
        }

        public void Initialize()
        { }
        private void RegisterAll(BSMLParser parser)
        {
            if (Registered)
            {
                return;
            }
            foreach (var tag in InstantiateOfType<BSMLTag>())
            {
                parser.RegisterTag(tag);
            }
            foreach (var macro in InstantiateOfType<BSMLMacro>())
            {
                parser.RegisterMacro(macro);
            }
            foreach (var handler in InstantiateOfType<TypeHandler>())
            {
                parser.RegisterTypeHandler(handler);
            }
            RegisterCustomComponents(parser);
            _logger.Info("Registered Custom Components");
            Registered = true;
        }

        private void RegisterCustomComponents(BSMLParser parser)
        {
            foreach (var type in GetListOfType<CustomUiComponent>())
            {
                parser.RegisterTag(new CustomUiComponentTag(type, _customUiComponentFactory));
            }
            foreach (var type in GetListOfType<Popup>())
            {
                parser.RegisterTag(new PopupTag(type, _popupFactory));
            }
        }

        private Type[] GetTypesSafe()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).ToArray();
            }
        }

        private List<T> InstantiateOfType<T>(params object[] constructorArgs)
        {
            var objects = new List<T>();
            foreach (var type in GetTypesSafe().Where(myType =>
                myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T)) && myType != typeof(CustomUiComponentTag) &&
                myType != typeof(PopupTag)))
            {
                objects.Add((T)Activator.CreateInstance(type, constructorArgs));
            }
            return objects;
        }

        private List<Type> GetListOfType<T>()
        {
            var types = new List<Type>();
            foreach (var type in GetTypesSafe()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))))
            {
                types.Add(type);
            }
            return types;
        }
        #region Factories
        private readonly Popup.Factory _popupFactory;
        private readonly CustomUiComponent.Factory _customUiComponentFactory;
        private readonly BSMLParser _bsmlParser;
        #endregion
    }

    public class CustomListTableData : MonoBehaviour, TableView.IDataSource
    {
        public enum ListStyle
        {
            List,
            Box,
            Simple
        }

        private const string ReuseIdentifier = "SaberFactoryListTableCell";
        private static readonly Color HeartColor = new Color(0.921f, 0.360f, 0.321f);
        private static readonly Sprite FolderSprite =
            TextureUtilities.LoadSpriteFromResource("SaberFactory2.Resources.Icons.folder.png");
        public ListStyle Style
        {
            get => _listStyle;
            set
            {
                switch (value)
                {
                    case ListStyle.List:
                        cellSize = 8.5f;
                        break;
                    case ListStyle.Box:
                        cellSize = TableView.tableType == TableView.TableType.Horizontal ? 30f : 35f;
                        break;
                    case ListStyle.Simple:
                        cellSize = 8f;
                        break;
                }
                _listStyle = value;
            }
        }

        public float cellSize = 8.5f;
        public List<CustomCellInfo> Data = new List<CustomCellInfo>();
        public bool ExpandCell;
        public TableView TableView;
        private ListStyle _listStyle = ListStyle.List;
        private SimpleTextTableCell _simpleTextTableCellInstance;
        private LevelListTableCell _songListTableCellInstance;
        public virtual TableCell CellForIdx(TableView tableView, int idx)
        {
            switch (_listStyle)
            {
                case ListStyle.List:
                    var tableCell = GetTableCell();
                    var cellData = Data[idx];
                    var nameText = _songNameTextAccessor(ref tableCell);
                    var authorText = _songAuthorTextAccessor(ref tableCell);
                    var songDurationText = _songDurationTextAccessor(ref tableCell);
                    var songBpmText = _songBpmTextAccessor(ref tableCell);
                    UITemplateCache.AssignValidFont(nameText);
                    UITemplateCache.AssignValidFont(authorText);
                    UITemplateCache.AssignValidFont(songDurationText);
                    UITemplateCache.AssignValidFont(songBpmText);
                    var coverImage = _coverImageAccessor(ref tableCell);
                    var favoriteImage = _favoriteImageAccessor(ref tableCell);
                    var bg = _backgroundImageAccessor(ref tableCell).Cast<ImageView>();
                    (coverImage as ImageView).SetSkew(0);
                    nameText.color = cellData.IsCategory ? Color.red : Color.white;
                    if (cellData.IsCategory)
                    {
                        nameText.color = HeartColor;
                        nameText.rectTransform.anchoredPosition = nameText.rectTransform.anchoredPosition.With(null, 0);
                    }
                    else
                    {
                        nameText.color = Color.white;
                        nameText.rectTransform.anchoredPosition = nameText.rectTransform.anchoredPosition.With(null, 1.14f);
                    }
                    if (cellData.Icon is null)
                    {
                        if (cellData.IsCategory)
                        {
                            coverImage.gameObject.SetActive(true);
                            coverImage.sprite = FolderSprite;
                        }
                        else
                        {
                            coverImage.gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        coverImage.gameObject.SetActive(true);
                        coverImage.sprite = cellData.Icon;
                    }
                    if (string.IsNullOrEmpty(cellData.RightText))
                    {
                        songDurationText.gameObject.SetActive(false);
                    }
                    else
                    {
                        songDurationText.text = cellData.RightText;
                    }
                    if (string.IsNullOrEmpty(cellData.RightBottomText))
                    {
                        songBpmText.gameObject.SetActive(false);
                    }
                    else
                    {
                        songBpmText.text = cellData.RightBottomText;
                    }
                    favoriteImage.enabled = cellData.IsFavorite;
                    if (cellData.IsFavorite)
                    {
                        favoriteImage.color = HeartColor;
                    }
                    tableCell.transform.Find("BpmIcon").gameObject.SetActive(false);
                    if (ExpandCell)
                    {
                        nameText.rectTransform.anchorMax = new Vector3(2, 0.5f, 0);
                        authorText.rectTransform.anchorMax = new Vector3(2, 0.5f, 0);
                    }
                    nameText.text = Data[idx].Text;
                    authorText.text = Data[idx].Subtext;
                    bg.color0 = cellData.Color0;
                    bg.color1 = cellData.Color1;
                    bg.color0 = cellData.Color0;
                    bg.color1 = cellData.Color1;

                    var texts = tableCell.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var text in texts)
                    {
                        UITemplateCache.AssignValidFont(text);
                    }
                    return tableCell;
                case ListStyle.Simple:
                    var simpleCell = GetSimpleTextTableCell();
                    var textComp = simpleCell.GetField<TextMeshProUGUI, SimpleTextTableCell>("_text");
                    textComp.richText = true;
                    textComp.enableWordWrapping = true;
                    UITemplateCache.AssignValidFont(textComp);
                    simpleCell.text = Data[idx].Text;

                    var simpleTexts = simpleCell.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var text in simpleTexts)
                    {
                        UITemplateCache.AssignValidFont(text);
                    }
                    return simpleCell;
            }
            return null;
        }

        public float CellSize(int idx)
        {
            return cellSize;
        }

        public int NumberOfCells()
        {
            return Data.Count;
        }

        public LevelListTableCell GetTableCell()
        {
            var tableCell = (LevelListTableCell)TableView.DequeueReusableCellForIdentifier(ReuseIdentifier);
            if (!tableCell)
            {
                if (_songListTableCellInstance == null)
                {
                    _songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => x.name == "LevelListTableCell");
                }
                tableCell = Instantiate(_songListTableCellInstance);

                var texts = tableCell.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in texts)
                {
                    UITemplateCache.AssignValidFont(text);
                }
                var t = _favoriteImageAccessor(ref tableCell).gameObject.transform.AsRectTransform();
                t.sizeDelta = new Vector2(5, 5);
                t.anchoredPosition = new Vector2(-8.5f, 0);
                tableCell.SetField("_highlightBackgroundColor", Color.white.ColorWithAlpha(0.3f));
                tableCell.SetField("_selectedBackgroundColor", Color.white.ColorWithAlpha(0.7f));
                tableCell.SetField("_selectedAndHighlightedBackgroundColor", Color.white.ColorWithAlpha(0.7f));
                try
                {
                    tableCell.GetField<GameObject, LevelListTableCell>("_promoBadgeGo").SetActive(false);
                    tableCell.GetField<GameObject, LevelListTableCell>("_updatedBadgeGo").SetActive(false);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error constructing CustomListTableData for SF2");
                    Debug.LogError(e);
                }
                var bg = _backgroundImageAccessor(ref tableCell).Cast<ImageView>();
                bg.SetSkew(0);
                _songAuthorTextAccessor(ref tableCell).richText = true;
            }
            tableCell.SetField("_notOwned", false);
            tableCell.reuseIdentifier = ReuseIdentifier;
            return tableCell;
        }

        public SimpleTextTableCell GetSimpleTextTableCell()
        {
            var tableCell = (SimpleTextTableCell)TableView.DequeueReusableCellForIdentifier(ReuseIdentifier);
            if (!tableCell)
            {
                if (_simpleTextTableCellInstance == null)
                {
                    _simpleTextTableCellInstance = Resources.FindObjectsOfTypeAll<SimpleTextTableCell>().First(x => x.name == "SimpleTextTableCell");
                }
                tableCell = Instantiate(_simpleTextTableCellInstance);

                var texts = tableCell.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in texts)
                {
                    UITemplateCache.AssignValidFont(text);
                }
            }
            tableCell.reuseIdentifier = ReuseIdentifier;
            return tableCell;
        }

        public class CustomCellInfo
        {
            public Sprite Icon;
            public bool IsCategory;
            public bool IsFavorite;
            public string RightBottomText;
            public string RightText;
            public string Subtext;
            public string Text;
            public Color Color0;
            public Color Color1;

            public CustomCellInfo()
            {
            }

            public CustomCellInfo(string text, string subtext = null)
            {
                Text = text;
                Subtext = subtext;
            }
        }
        #region Accessors
        private static readonly FieldAccessor<LevelListTableCell, Image>.Accessor _favoriteImageAccessor =
            FieldAccessor<LevelListTableCell, Image>.GetAccessor("_favoritesBadgeImage");
        private static readonly FieldAccessor<LevelListTableCell, TextMeshProUGUI>.Accessor _songNameTextAccessor =
            FieldAccessor<LevelListTableCell, TextMeshProUGUI>.GetAccessor("_songNameText");
        private static readonly FieldAccessor<LevelListTableCell, TextMeshProUGUI>.Accessor _songAuthorTextAccessor =
            FieldAccessor<LevelListTableCell, TextMeshProUGUI>.GetAccessor("_songAuthorText");
        private static readonly FieldAccessor<LevelListTableCell, TextMeshProUGUI>.Accessor _songDurationTextAccessor =
            FieldAccessor<LevelListTableCell, TextMeshProUGUI>.GetAccessor("_songDurationText");
        private static readonly FieldAccessor<LevelListTableCell, TextMeshProUGUI>.Accessor _songBpmTextAccessor =
            FieldAccessor<LevelListTableCell, TextMeshProUGUI>.GetAccessor("_songBpmText");
        private static readonly FieldAccessor<LevelListTableCell, Image>.Accessor _coverImageAccessor =
            FieldAccessor<LevelListTableCell, Image>.GetAccessor("_coverImage");
        private static readonly FieldAccessor<LevelListTableCell, Image>.Accessor _backgroundImageAccessor =
            FieldAccessor<LevelListTableCell, Image>.GetAccessor("_backgroundImage");
        #endregion
    }

    public static class MainUIInstaller
    {
        public static void Install(DiContainer container)
        {
            container.BindInterfacesAndSelfTo<CustomComponentHandler>().AsSingle();
            container.Bind<BaseGameUiHandler>().AsSingle();
            container
                .BindFactory<Type, SubView.InitData, SubView, SubView.Factory>()
                .FromFactory<SubViewFactory>();
            container
                .BindFactory<Type, CustomViewController.InitData, CustomViewController, CustomViewController.Factory>()
                .FromFactory<ViewControllerFactory>();
            container
                .BindFactory<CustomScreen.InitData, CustomScreen, CustomScreen.Factory>()
                .FromFactory<ScreenFactory>();
            container.Bind<StyleSheetHandler>().AsSingle();
            container.BindInterfacesAndSelfTo<BsmlDecorator>().AsSingle();
            BindUiFactory<Popup, Popup.Factory>(container);
            BindUiFactory<CustomUiComponent, CustomUiComponent.Factory>(container);
        }

        private static FactoryToChoiceIdBinder<GameObject, Type, T> BindUiFactory<T, TFactory>(DiContainer container)
        {
            var bindStatement = container.StartBinding();
            var bindInfo = bindStatement.SpawnBindInfo();
            bindInfo.ContractTypes.Add(typeof(TFactory));
            var factoryBindInfo = new FactoryBindInfo(typeof(TFactory));
            bindStatement.SetFinalizer(new PlaceholderFactoryBindingFinalizer<T>(bindInfo, factoryBindInfo));
            return new FactoryToChoiceIdBinder<GameObject, Type, T>(container, bindInfo, factoryBindInfo);
        }
    }

    internal class ThemeManager
    {
        public static readonly Dictionary<string, Color> ColorTheme = new Dictionary<string, Color>
        {
            { "light-bg", GetColor("#FFF") },
            { "dark-bg", GetColor("#0000FF") },
            { "default-panel", GetColor("#668F8F") },
            { "saber-selector", GetColor("#668F8F") },
            { "saber-selector-sec", GetColor("#668F8F") },
            { "navbar", GetColor("#000000CC") },
            { "prop-cell", GetColor("#00000090") }
        };
        public static bool GetDefinedColor(string name, out Color color)
        {
            return ColorTheme.TryGetValue(name, out color);
        }

        public static bool GetColor(string colorString, out Color color)
        {
            if (colorString[0] == '$' && GetDefinedColor(colorString.Substring(1), out color))
            {
                return true;
            }
            if (ColorUtility.TryParseHtmlString(colorString, out color))
            {
                return true;
            }
            return false;
        }

        private static Color GetColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return color;
            }
            return Color.white;
        }

        public static string TryReplacingWithColor(string input, out bool replaced)
        {
            replaced = false;
            if (input[0] != '$')
            {
                return input;
            }
            if (GetDefinedColor(input.Substring(1), out var color))
            {
                replaced = true;
                return "#" + ColorUtility.ToHtmlStringRGBA(color);
            }
            return input;
        }
    }

    public abstract class BasePropCell : MonoBehaviour
    {
        public string ContentLocation => string.Join(".", GetType().Namespace, "PropCells", GetType().Name) + ".bsml";
        public Action<object> OnChangeCallback;
        public abstract void SetData(PropertyDescriptor data);
    }

    internal class BoolPropCell : BasePropCell
    {
        [UIComponent("bg")] private readonly Image _backgroundImage = null;
        [UIComponent("bool-val")] private readonly ToggleSetting _toggleSetting = null;
        public override void SetData(PropertyDescriptor data)
        {
            if (!(data.PropObject is bool val))
            {
                return;
            }
            OnChangeCallback = data.ChangedCallback;
            _toggleSetting.Value = val;
            _toggleSetting.ReceiveValue();
            _toggleSetting.Text = data.Text;
            if (ThemeManager.GetDefinedColor("prop-cell", out var bgColor))
            {
                _backgroundImage.type = Image.Type.Sliced;
                _backgroundImage.color = bgColor;
            }
        }

        [UIAction("bool-changed")]
        private void BoolChanged(bool val)
        {
            OnChangeCallback?.Invoke(val);
        }
    }

    internal class ColorPropCell : BasePropCell
    {
        [UIComponent("bg")] private readonly Image _backgroundImage = null;
        [UIComponent("color-setting")] private readonly ColorSetting _colorSetting = null;
        [UIComponent("color-setting")] private readonly TextMeshProUGUI _propName = null;
        public override void SetData(PropertyDescriptor data)
        {
            if (!(data.PropObject is Color color))
            {
                return;
            }
            OnChangeCallback = data.ChangedCallback;
            UITemplateCache.AssignValidFont(_propName);
            _propName.text = data.Text;
            _colorSetting.CurrentColor = color;
            if (ThemeManager.GetDefinedColor("prop-cell", out var bgColor))
            {
                _backgroundImage.type = Image.Type.Sliced;
                _backgroundImage.color = bgColor;
            }
            var positioner = _colorSetting.ModalColorPicker.gameObject.AddComponent<ModalPositioner>();
            positioner.SetPosition(new Vector2(11, 10));
        }

        [UIAction("color-changed")]
        private void ColorChanged(Color color)
        {
            OnChangeCallback?.Invoke(color);
        }

        internal class ModalPositioner : MonoBehaviour
        {
            private Vector2 _position;
            public async void OnEnable()
            {
                try
                {
                    await Task.Delay(10);
                    (transform as RectTransform).anchoredPosition = _position;
                }
                catch (Exception)
                {
                }
            }

            public void SetPosition(Vector2 pos)
            {
                _position = pos;
            }
        }
    }

    internal class FloatPropCell : BasePropCell
    {
        [UIComponent("bg")] private readonly Image _backgroundImage = null;
        [UIComponent("val-slider")] private readonly SliderSetting _sliderSetting = null;
        [UIComponent("val-slider")] private readonly TextMeshProUGUI _sliderSettingText = null;
        public override void SetData(PropertyDescriptor data)
        {
            if (!(data.PropObject is float val))
            {
                return;
            }
            OnChangeCallback = data.ChangedCallback;
            if (data.AddtionalData is Vector2 minMax && val > minMax.x && val < minMax.y)
            {
                _sliderSetting.Slider.minValue = minMax.x;
                _sliderSetting.Slider.maxValue = minMax.y;
            }
            _sliderSetting.Slider.value = val;
            _sliderSetting.ReceiveValue();
            UITemplateCache.AssignValidFont(_sliderSettingText);
            _sliderSettingText.text = data.Text;
            if (ThemeManager.GetDefinedColor("prop-cell", out var bgColor))
            {
                _backgroundImage.type = Image.Type.Sliced;
                _backgroundImage.color = bgColor;
            }
        }

        [UIAction("slider-changed")]
        private void SliderChanged(float val)
        {
            OnChangeCallback?.Invoke(val);
        }
    }

    internal class TexturePropCell : BasePropCell
    {
        [UIComponent("bg")] private readonly Image _backgroundImage = null;
        [UIComponent("prop-name")] private readonly TextMeshProUGUI _propName = null;
        [UIComponent("texture")] private readonly Image _propTexture = null;
        [UIComponent("texture-picker")] private readonly TexturePickerPopup _texturePicker = null;
        public override void SetData(PropertyDescriptor data)
        {
            if (!(data.PropObject is Texture2D tex))
            {
                return;
            }
            OnChangeCallback = data.ChangedCallback;
            UITemplateCache.AssignValidFont(_propName);
            _propName.text = ShortenText(data.Text, 14);
            if (data.AddtionalData is bool showPreview && showPreview)
            {
                _propTexture.sprite = Utilities.LoadSpriteFromTexture(tex);
            }
            if (ThemeManager.GetDefinedColor("prop-cell", out var bgColor))
            {
                _backgroundImage.type = Image.Type.Sliced;
                _backgroundImage.color = bgColor;
            }
        }

        private string ShortenText(string text, int length)
        {
            if (text.Length < length)
            {
                return text;
            }
            return text.Substring(0, length) + "...";
        }

        [UIAction("click-select")]
        private void ClickSelect()
        {
            _texturePicker.Show(tex =>
            {
                _propTexture.sprite = Utilities.LoadSpriteFromTexture(tex);
                OnChangeCallback?.Invoke(tex);
            });
        }
    }

    internal class BaseGameUiHandler
    {
        private readonly List<GameObject> _deactivatedScreens = new List<GameObject>();
        private readonly HierarchyManager _hierarchyManager;
        private readonly ScreenSystem _screenSystem;
        private BaseGameUiHandler(HierarchyManager hierarchyManager)
        {
            _hierarchyManager = hierarchyManager;
            _screenSystem = hierarchyManager.gameObject.GetComponent<ScreenSystem>();
        }

        public void DismissGameUI()
        {
            _deactivatedScreens.Clear();
            DeactivateScreen(_screenSystem.leftScreen);
            DeactivateScreen(_screenSystem.mainScreen);
            DeactivateScreen(_screenSystem.rightScreen);
            DeactivateScreen(_screenSystem.bottomScreen);
            DeactivateScreen(_screenSystem.topScreen);
        }

        public void PresentGameUI()
        {
            foreach (var screenObj in _deactivatedScreens)
            {
                screenObj.SetActive(true);
            }
        }

        public Transform GetUIParent()
        {
            return _hierarchyManager.transform;
        }

        private void DeactivateScreen(Screen screen)
        {
            var go = screen.gameObject;
            if (go.activeSelf)
            {
                _deactivatedScreens.Add(go);
                go.SetActive(false);
            }
        }

        private void HideViewControllers(IEnumerable<ViewController> vcs)
        {
            var cgs = vcs.NonNull().Select(x => x.GetComponent<CanvasGroup>());
            foreach (var cg in cgs)
            {
                cg.gameObject.SetActive(false);
            }
        }

        private void ShowViewControllers(IEnumerable<ViewController> vcs)
        {
            var cgs = vcs.NonNull().Select(x => x.GetComponent<CanvasGroup>());
            foreach (var cg in cgs)
            {
                cg.gameObject.SetActive(true);
            }
        }

        private void GetChildViewControllers(ViewController vc, List<ViewController> list)
        {
            if (vc.childViewController != null)
            {
                list.Add(vc.childViewController);
                GetChildViewControllers(vc.childViewController, list);
            }
        }

        private ViewController GetViewController(Screen screen)
        {
            return screen.GetField<ViewController, Screen>("_rootViewController");
        }
    }

    internal class BaseUiComposition
    {
        public GameObject GameObject { get; private set; }
        protected readonly List<CustomScreen> _screens = new List<CustomScreen>();
        protected readonly BsmlDecorator BsmlDecorator;
        protected CurvedCanvasSettings _curvedCanvasSettings;
        protected GameObject _curvedGO;
        private readonly BaseGameUiHandler _baseGameUiHandler;
        private readonly SiraLog _logger;
        private readonly PhysicsRaycasterWithCache _physicsRaycaster;
        private readonly CustomScreen.Factory _screenFactory;
        protected BaseUiComposition(
            SiraLog logger,
            CustomScreen.Factory screenFactory,
            BaseGameUiHandler baseGameUiHandler,
            PhysicsRaycasterWithCache physicsRaycaster,
            BsmlDecorator bsmlDecorator)
        {
            _logger = logger;
            _screenFactory = screenFactory;
            _baseGameUiHandler = baseGameUiHandler;
            _physicsRaycaster = physicsRaycaster;
            BsmlDecorator = bsmlDecorator;
        }

        public event Action OnClosePressed;
        public void Initialize()
        {
            SetupTemplates();
            GameObject = new GameObject(GetType().Namespace + " UI");
            GameObject.transform.SetParent(_baseGameUiHandler.GetUIParent(), false);
            GameObject.transform.localPosition = new Vector3(0, 1.1f, 2.6f);
            GameObject.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
            _curvedGO = GameObject.CreateGameObject("Curved UI");
            _curvedGO.AddComponent<Canvas>().sortingOrder = 3;
            var canvasScaler = _curvedGO.AddComponent<CanvasScaler>();
            canvasScaler.referencePixelsPerUnit = 10;
            canvasScaler.scaleFactor = 3.44f;
            var vrgr = _curvedGO.AddComponent<VRGraphicRaycaster>();
            vrgr.SetField("_physicsRaycaster", _physicsRaycaster);
            _curvedGO.AddComponent<CanvasRenderer>();
            _curvedCanvasSettings = _curvedGO.AddComponent<CurvedCanvasSettings>();
            SetupUI();
        }

        protected virtual void SetupUI()
        { }
        public void Open()
        {
            _baseGameUiHandler.DismissGameUI();
            if (GameObject != null)
            {
                foreach (var raycaster in GameObject.GetComponentsInChildren<VRGraphicRaycaster>(true))
                {
                    raycaster.enabled = true;
                }
            }
            foreach (var screen in _screens)
            {
                screen.Open();
            }
            DidOpen();
        }

        public void Close(bool instant = false)
        {
            if (GameObject != null)
            {
                foreach (var raycaster in GameObject.GetComponentsInChildren<VRGraphicRaycaster>(true))
                {
                    raycaster.enabled = false;
                }
            }
            foreach (var screen in _screens)
            {
                screen.Close(instant);
            }
            DidClose();
            _baseGameUiHandler.PresentGameUI();
        }

        protected void ClosePressed()
        {
            OnClosePressed?.Invoke();
        }

        protected virtual void DidOpen()
        { }
        protected virtual void DidClose()
        { }
        public void SetRadius(float radius)
        {
            _curvedCanvasSettings.SetRadius(radius);
        }

        protected virtual void SetupTemplates()
        {
            BsmlDecorator.StyleSheetHandler.LoadStyleSheet("SaberFactory2.UI.CustomSaber.BaseUiComposition.css");
            BsmlDecorator.AddTemplateHandler("ui-icon", (decorator, args) => "SaberFactory2.Resources.UI." + args[0] + ".png");
            BsmlDecorator.AddTemplateHandler("icon", (decorator, args) => "SaberFactory2.Resources.Icons." + args[0] + ".png");
        }

        protected CustomScreen AddScreen(CustomScreen.InitData initData)
        {
            initData.Parent = initData.IsCurved ? _curvedGO.transform : GameObject.transform;
            var screen = _screenFactory.Create(initData);
            _screens.Add(screen);
            return screen;
        }
    }

    internal class BsmlDecorator
    {
        [Inject] public readonly StyleSheetHandler StyleSheetHandler = null;
        private readonly Dictionary<string, Func<BsmlDecorator, string[], string>> _templateHandlers =
            new Dictionary<string, Func<BsmlDecorator, string[], string>>
            {
                { "put", (dec, args) => args[0] },
                { "color-template", (dec, args) => ThemeManager.TryReplacingWithColor(args[0], out _) },
                { "template", (dec, args) => dec._templates.TryGetValue(args[0], out var template) ? template : "" },
                {
                    "file", (dec, args) =>
                    {
                        var data = Readers.ReadResource(args[0]);
                        var content = Readers.BytesToString(data);
                        return dec.Process(content);
                    }
                }
            };
        private readonly Dictionary<string, string> _templates = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _bsmlCache = new Dictionary<string, string>();
        private readonly XmlReaderSettings _readerSettings = new XmlReaderSettings { IgnoreComments = true };
        public void AddTemplateHandler(string name, Func<BsmlDecorator, string[], string> action)
        {
            _templateHandlers[name] = action;
        }

        public void AddTemplate(string name, string template)
        {
            _templates[name] = template;
        }

        public async Task<BSMLParserParams> ParseFromResourceAsync(string resourceName, GameObject parent, object host)
        {
            if (!_bsmlCache.TryGetValue(resourceName, out var bsmlContent))
            {
                var data = await Readers.ReadResourceAsync(resourceName);
                if (data == null)
                {
                    Plugin.Logger?.Error($"Failed to load BSML resource: {resourceName}");
                    return null;
                }
                var content = Readers.BytesToString(data);
                content = Process(content);
                var doc = new XmlDocument();
                doc.Load(XmlReader.Create(new StringReader(content), _readerSettings));
                ProcessDoc(doc);
                bsmlContent = doc.OuterXml;
                _bsmlCache.Add(resourceName, bsmlContent);
            }
            return BSMLParser.Instance.Parse(bsmlContent, parent, host);
        }

        public BSMLParserParams ParseFromResource(string resourceName, GameObject parent, object host)
        {
            if (!_bsmlCache.TryGetValue(resourceName, out var bsmlContent))
            {
                var data = Readers.ReadResource(resourceName);
                var content = Readers.BytesToString(data);
                content = Process(content);
                var doc = new XmlDocument();
                doc.Load(XmlReader.Create(new StringReader(content), _readerSettings));
                ProcessDoc(doc);
                bsmlContent = doc.OuterXml;
                _bsmlCache.Add(resourceName, bsmlContent);
            }
            return BSMLParser.Instance.Parse(bsmlContent, parent, host);
        }

        public BSMLParserParams ParseFromString(string content, GameObject parent, object host)
        {
            content = Process(content);
            return BSMLParser.Instance.Parse(content, parent, host);
        }

        public void ProcessDoc(XmlDocument doc)
        {
            var vars = new Dictionary<string, string>();
            ProcessNode(doc, vars);
        }

        private void ProcessNode(XmlNode rootNode, Dictionary<string, string> vars)
        {
            foreach (XmlElement node in rootNode)
            {
                if (node.Name == "var")
                {
                    vars.Add(node.Attributes["name"].Value, ThemeManager.TryReplacingWithColor(node.Attributes["value"].Value, out _));
                }
                else if (node.Attributes["style"] is { } styleAttr)
                {
                    foreach (var rule in StyleSheetHandler.CollectRules(styleAttr.Value.Split(' ')))
                    {
                        node.SetAttribute(rule.Name, rule.Value);
                    }
                }
                ProcessNode(node, vars);
            }
        }

        public string Process(string content)
        {
            var varList = new Dictionary<string, string>();
            var pos = 0;
            while (pos < content.Length)
            {
                if (content[pos] == '{')
                {
                    var charBuffer = new StringBuilder();
                    for (var j = pos + 1; j < content.Length; j++)
                    {
                        if (content[j] == '}')
                        {
                            break;
                        }
                        charBuffer.Append(content[j]);
                    }
                    var charBufferStr = charBuffer.ToString();
                    content = content.Remove(pos, charBufferStr.Length + 2);
                    var template = ProcessTemplate(charBufferStr, varList);
                    content = content.Insert(pos, template);
                    pos += template.Length;
                    continue;
                }
                pos++;
            }
            return content;
        }

        private string ProcessTemplate(string template, Dictionary<string, string> varList)
        {
            var split = template.Split(';');
            if (split[0] == "var")
            {
                varList.Add(split[1], split[2]);
                return "";
            }
            if (split.Length > 1)
            {
                for (var i = 1; i < split.Length; i++)
                {
                    if (split[i].StartsWith("&"))
                    {
                        var varname = split[i].Substring(1);
                        if (!varList.TryGetValue(varname, out var varValue))
                        {
                            return "";
                        }
                        split[i] = varValue;
                    }
                }
            }
            if (_templateHandlers.TryGetValue(split[0], out var action))
            {
                var args = new string[split.Length - 1];
                Array.Copy(split, 1, args, 0, args.Length);
                return action(this, args);
            }
            return "";
        }
    }

    internal abstract class ComponentController
    {
        public ExternalComponents ExternalComponents;
        public abstract void RemoveEvent();
        public abstract string GetId();
        public abstract void SetValue(object val);
        public abstract object GetValue();
    }

    internal class ComponentPlaceholderFactory<TValue> : PlaceholderFactory<GameObject, Type, TValue> where TValue : UnityEngine.Component
    {
        [Inject] private readonly DiContainer _container = null;
        public override TValue Create(GameObject gameObject, Type type)
        {
            return (TValue)_container.InstantiateComponent(type, gameObject);
        }
    }

    internal class CustomParsable : MonoBehaviour, ICustomParsable
    {
        public BSMLParserParams ParserParams { get; private set; }
        protected virtual string ResourceName => string.Join(".", GetType().Namespace, GetType().Name) + ".bsml";
        [Inject] protected readonly BsmlDecorator BsmlDecorator = null;
        public virtual void Parse()
        {
            ParserParams = BsmlDecorator.ParseFromResource(ResourceName, gameObject, this);

            var texts = GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                UITemplateCache.AssignValidFont(text);
            }
        }

        public void Unparse()
        {
            foreach (Transform t in transform)
            {
                t.gameObject.TryDestroy();
            }
        }
    }

    internal class CustomScreen : Screen
    {
        public ViewController CurrentViewController { get; private set; }
        private SiraLog _logger;
        private CustomViewController.Factory _viewControllerFactory;
        [Inject]
        private void Construct(SiraLog logger, CustomViewController.Factory viewControllerFactory)
        {
            _logger = logger;
            _viewControllerFactory = viewControllerFactory;
        }

        public void Initialize(InitData initData)
        {
            var t = gameObject.transform;
            t.localPosition = initData.Position;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.transform.AsRectTransform().sizeDelta = initData.Size;
            gameObject.AddComponent<CanvasRenderer>();
            if (!initData.IsCurved)
            {
                var canvasScaler = gameObject.AddComponent<CanvasScaler>();
                canvasScaler.referencePixelsPerUnit = 10;
                canvasScaler.scaleFactor = 3.44f;
                var curvedCanvasSettings = gameObject.AddComponent<CurvedCanvasSettings>();
                curvedCanvasSettings.SetRadius(initData.CurveRadius);
            }
        }

        public T CreateViewController<T>() where T : CustomViewController
        {
            var initData = new CustomViewController.InitData
            {
                Parent = gameObject.transform,
                Screen = this
            };
            CurrentViewController = _viewControllerFactory.Create(typeof(T), initData);
            return (T)CurrentViewController;
        }

        public virtual async void Open()
        {
            SetRootViewController(CurrentViewController, ViewController.AnimationType.In);
            await CurrentViewController.Cast<CustomViewController>().AnimateIn(CancellationToken.None);
        }

        public virtual void Close(bool instant = false)
        {
            SetRootViewController(null, ViewController.AnimationType.Out);
            if (instant)
            {
                gameObject.SetActive(false);
            }
        }

        internal class Factory : PlaceholderFactory<InitData, CustomScreen>
        { }
        internal struct InitData
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector2 Size;
            public bool IsCurved;
            public float CurveRadius;
            public Transform Parent;
            public InitData(string name, Vector3 position, Quaternion rotation, Vector2 size, bool isCurved) : this()
            {
                Name = name;
                Position = position;
                Rotation = rotation;
                Size = size;
                IsCurved = isCurved;
            }
        }
    }

    internal class CustomUiComponent : CustomParsable
    {
        internal class Factory : ComponentPlaceholderFactory<CustomUiComponent>
        { }
    }

    internal class CustomViewController : ViewController, INotifyPropertyChanged, IAnimatableUi
    {
        protected virtual string _resourceName => string.Join(".", GetType().Namespace, GetType().Name) + ".bsml";
        public Action<bool, bool, bool> didActivate;
        protected SiraLog _logger;
        protected SubView.Factory _viewFactory;
        protected SubViewSwitcher SubViewSwitcher;
        private BsmlDecorator _bsmlDecorator;
        public new virtual IAnimatableUi.EAnimationType AnimationType => IAnimatableUi.EAnimationType.Horizontal;
        public event PropertyChangedEventHandler PropertyChanged;
        [Inject]
        private void Construct(SiraLog logger, SubView.Factory viewFactory, BsmlDecorator bsmlDecorator)
        {
            _logger = logger;
            _viewFactory = viewFactory;
            _bsmlDecorator = bsmlDecorator;
            SubViewSwitcher = new SubViewSwitcher();
        }

        public virtual async Task AnimateIn(CancellationToken cancellationToken)
        {
            await Task.Yield();
        }

        public override async void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                await _bsmlDecorator.ParseFromResourceAsync(_resourceName, gameObject, this);
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in texts)
                {
                    UITemplateCache.AssignValidFont(text);
                }
            }
            didActivate?.Invoke(firstActivation, addedToHierarchy, screenSystemEnabling);
            SubViewSwitcher.NotifyDidOpen();
        }

        public override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            SubViewSwitcher.NotifyDidClose();
        }

        protected T CreateSubView<T>(Transform parent, bool switchToView = false) where T : SubView
        {
            var initData = new SubView.InitData
            {
                Name = typeof(T).Name,
                Parent = parent
            };
            var view = (T)_viewFactory.Create(typeof(T), initData);
            view.SubViewSwitcher = SubViewSwitcher;
            if (switchToView)
            {
                SubViewSwitcher.SwitchView(view, false);
            }
            return view;
        }

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            catch (Exception)
            {
            }
        }

        internal class Factory : PlaceholderFactory<Type, InitData, CustomViewController>
        { }
        internal struct InitData
        {
            public Transform Parent;
            public CustomScreen Screen;
        }
    }

    internal class DynamicTypeFactory<TRet> : ComponentPlaceholderFactory<TRet> where TRet : UnityEngine.Component
    {
        public T Create<T>(GameObject gameObject) where T : TRet
        {
            return (T)Create(gameObject, typeof(T));
        }
    }

    public enum EPropertyType
    {
        Unhandled,
        Text,
        Float,
        Bool,
        Texture,
        Color
    }

    internal interface IAnimatableUi
    {
        EAnimationType AnimationType { get; }
        internal enum EAnimationType
        {
            Horizontal,
            Vertical,
            Z
        }
    }

    internal interface ICustomListItem
    {
        string ListName { get; }
        string ListAuthor { get; }
        Sprite ListCover { get; }
        bool IsFavorite { get; }
    }

    internal interface ICustomParsable
    {
        void Parse();
        void Unparse();
    }

    internal interface ISubViewHost
    {
        bool IsActive { get; }
        void Open();
        void Close();
    }

    internal class MenuButtonRegistrar : IInitializable, IDisposable
    {
        private readonly MenuButton _menuButton;
        protected MenuButtonRegistrar(string buttonText, string hoverText)
        {
            _menuButton = new MenuButton(buttonText, hoverText, OnClick);
        }

        public void Dispose()
        {
            if (MenuButtons.Instance != null && BSMLParser.Instance != null)
            {
                MenuButtons.Instance.UnregisterButton(_menuButton);
            }
        }

        public void Initialize()
        {
            MenuButtons.Instance.RegisterButton(_menuButton);
        }

        protected virtual void OnClick()
        { }
    }

    internal class Popup : CustomParsable
    {
        protected Transform _cachedTransform;
        private AnimationManager _animationManager;
        private CanvasGroup _canvasGroup;
        private Transform _originalParent;
        private CanvasGroup _parentCanvasGroup;
        public bool IsOpen;
        protected virtual void Awake()
        {
            _cachedTransform = transform;
            _canvasGroup = GetComponent<CanvasGroup>();
            _animationManager = new AnimationManager(0.3f, InAnimation, OutAnimation);
        }

        private void InAnimation(float t)
        {
            _cachedTransform.localScale = new Vector3(t, t, t);
            _canvasGroup.alpha = t;
        }

        private void OutAnimation(float t)
        {
            _canvasGroup.alpha = 1 - t;
            _cachedTransform.localScale = new Vector3(1 - t, 1 - t, 1 - t);
        }

        protected async Task AnimateIn()
        {
            await _animationManager.AnimateIn();
        }

        protected async Task AnimateOut()
        {
            await _animationManager.AnimateOut();
        }

        protected async Task Create(bool animated, bool fadeParents = true)
        {
            Parse();
            if (animated)
            {
                await AnimateIn();
            }
            else
            {
                _cachedTransform.localScale = Vector3.one;
                _canvasGroup.alpha = 1;
            }
            if (fadeParents)
            {
                FadeParentCanvases();
            }
            IsOpen = true;
        }

        protected async Task Hide(bool animated)
        {
            ShowParentCanvases();
            if (animated)
            {
                await AnimateOut();
            }
            Unparse();
            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, false);
            }
            IsOpen = false;
        }

        protected void ParentToViewController()
        {
            _originalParent = transform.parent;
            var parent = _originalParent;
            if (parent.TryGetComponent<CustomViewController>(out _))
            {
                return;
            }
            while (parent != null)
            {
                if (parent.TryGetComponent<CustomViewController>(out _))
                {
                    break;
                }
                parent = parent.parent;
            }
            transform.SetParent(parent, false);
        }

        protected void FadeParentCanvases()
        {
            var parent = transform.parent;
            while (parent != null)
            {
                var vc = parent.GetComponent<CanvasGroup>();
                if (vc != null)
                {
                    _parentCanvasGroup = vc;
                    vc.alpha = 0f;
                    break;
                }
                parent = parent.parent;
            }
        }

        protected void ShowParentCanvases()
        {
            if (_parentCanvasGroup == null)
            {
                return;
            }
            _parentCanvasGroup.alpha = 1;
            _parentCanvasGroup = null;
        }

        internal class Factory : ComponentPlaceholderFactory<Popup>
        { }
    }

    public class PropCell : TableCell
    {
        [UIComponent("bool-container")] private readonly GameObject _boolContainer = null;
        [UIComponent("color-container")] private readonly GameObject _colorContainer = null;
        [UIComponent("float-container")] private readonly GameObject _floatContainer = null;
        [UIComponent("texture-container")] private readonly GameObject _textureContainer = null;
        public void SetData(PropertyDescriptor data)
        {
            switch (data.Type)
            {
                case EPropertyType.Float:
                    FloatSetup(data);
                    break;
                case EPropertyType.Bool:
                    BoolSetup(data);
                    break;
                case EPropertyType.Color:
                    ColorSetup(data);
                    break;
                case EPropertyType.Texture:
                    TextureSetup(data);
                    break;
            }
        }

        private void FloatSetup(PropertyDescriptor data)
        {
            _floatContainer.SetActive(true);
        }

        private void BoolSetup(PropertyDescriptor data)
        {
            _boolContainer.SetActive(true);
        }

        private void ColorSetup(PropertyDescriptor data)
        {
            _colorContainer.SetActive(true);
        }

        private void TextureSetup(PropertyDescriptor data)
        {
            _textureContainer.SetActive(true);
        }
    }

    public class PropertyDescriptor
    {
        public object AddtionalData;
        public Action<object> ChangedCallback;
        public object PropObject;
        public string Text;
        public EPropertyType Type;
        public PropertyDescriptor(string text, EPropertyType type, object propObject, Action<object> changedCallback)
        {
            Text = text;
            Type = type;
            PropObject = propObject;
            ChangedCallback = changedCallback;
        }
    }

    internal class ScreenFactory : IFactory<CustomScreen.InitData, CustomScreen>
    {
        private readonly DiContainer _container;
        private ScreenFactory(DiContainer container)
        {
            _container = container;
        }

        public CustomScreen Create(CustomScreen.InitData initData)
        {
            var go = initData.Parent.CreateGameObject(initData.Name);
            var screen = _container.InstantiateComponent<CustomScreen>(go);
            screen.Initialize(initData);
            _container.InstantiateComponent<VRGraphicRaycaster>(go);
            return screen;
        }
    }

    internal class SliderController : ComponentController
    {
        public float Value
        {
            get => Slider.Slider.value;
            set
            {
                Slider.Slider.value = value;
                Slider.ReceiveValue();
            }
        }

        public int IntValue
        {
            get => (int)Value;
            set => Value = value;
        }

        public readonly SliderSetting Slider;
        private Action<RangeValuesTextSlider, float> _currentEvent;
        public SliderController(SliderSetting slider)
        {
            Slider = slider;
        }

        public void AddEvent(Action<RangeValuesTextSlider, float> action)
        {
            if (_currentEvent != null)
            {
                return;
            }
            _currentEvent = action;
            Slider.Slider.valueDidChangeEvent += _currentEvent;
        }

        public override void RemoveEvent()
        {
            if (_currentEvent is null)
            {
                return;
            }
            Slider.Slider.valueDidChangeEvent -= _currentEvent;
            _currentEvent = null;
        }

        public override string GetId()
        {
            return ExternalComponents.Components.OfType<TextMeshProUGUI>().FirstOrDefault()?.text ?? "UnknownID";
        }

        public override void SetValue(object val)
        {
            Value = (float)val;
        }

        public override object GetValue()
        {
            return Value;
        }
    }

    internal class StyleSheetHandler
    {
        private readonly Dictionary<string, StyleSelector> _selectors = new Dictionary<string, StyleSelector>();
        private readonly Regex SelectorRegex = new Regex(@"(\S+?)\s*{([\s\S]*?)}");
        private readonly Regex RuleRegex = new Regex(@"\s*(\S*?): *([\s\S]*)");
        public void LoadStyleSheet(string resourceName)
        {
            var content = Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), resourceName).Replace("\r\n", "\n");
            LoadStyle(content);
        }

        public void LoadStyle(string content)
        {
            var matches = SelectorRegex.Matches(content);
            foreach (Match match in matches)
            {
                var selector = new StyleSelector(match.Groups[1].Value);
                foreach (var rule in match.Groups[2].Value.Replace("\n", "").Split(';'))
                {
                    if (rule.Length < 1)
                    {
                        continue;
                    }
                    var ruleMatches = RuleRegex.Match(rule);
                    var ruleName = ruleMatches.Groups[1].Value;
                    var ruleValue = ruleMatches.Groups[2].Value;
                    selector.AddRule(new StyleRule(ruleName, ruleValue));
                }
                _selectors[selector.Name] = selector;
            }
        }

        public IEnumerable<StyleSelector> GetAllSelectors()
        {
            return _selectors.Values;
        }

        public bool GetSelector(string name, out StyleSelector selector)
        {
            return _selectors.TryGetValue(name, out selector);
        }

        public List<StyleRule> CollectRules(params string[] selectors)
        {
            var output = new Dictionary<string, StyleRule>();
            foreach (var selectorName in selectors)
            {
                if (_selectors.TryGetValue(selectorName, out var selector))
                {
                    foreach (var rule in selector.GetRules())
                    {
                        output[rule.Name] = rule;
                    }
                }
            }
            return output.Values.ToList();
        }

        internal class StyleRule
        {
            public readonly string Name;
            public readonly string Value;
            public StyleRule(string name, string value)
            {
                Name = name;
                Value = value = value.Replace("\"", "");
                if (value.Length > 1 && value[0] == '-' && value[1] == '-')
                {
                    ThemeManager.GetDefinedColor(value.Substring(2), out var color);
                    Value = "#" + ColorUtility.ToHtmlStringRGBA(color);
                }
            }
        }

        internal class StyleSelector
        {
            public readonly string Name;
            private readonly Dictionary<string, StyleRule> _rules = new Dictionary<string, StyleRule>();
            public StyleSelector(string name)
            {
                Name = name;
            }

            public void AddRule(StyleRule rule)
            {
                _rules[rule.Name] = rule;
            }

            public void RemoveRule(string rule)
            {
                _rules.Remove(rule);
            }

            public IEnumerable<StyleRule> GetRules()
            {
                return _rules.Values;
            }
        }
    }

    internal class SubView : MonoBehaviour, INotifyPropertyChanged
    {
        public virtual bool IsActive => gameObject.activeSelf;
        protected BSMLParserParams ParserParams { get; private set; }
        protected virtual string _resourceName => string.Join(".", GetType().Namespace, GetType().Name) + ".bsml";
        public SubViewSwitcher SubViewSwitcher;
        protected SiraLog Logger;
        private BsmlDecorator _bsmlDecorator;
        private bool _firstActivation = true;
        private PhysicsRaycasterWithCache _raycasterWithCache;
        public event PropertyChangedEventHandler PropertyChanged;
        [Inject]
        private void Construct(SiraLog logger, BsmlDecorator bsmlDecorator, PhysicsRaycasterWithCache raycasterWithCache)
        {
            _bsmlDecorator = bsmlDecorator;
            Logger = logger;
            _raycasterWithCache = raycasterWithCache;
        }

        public async Task Open(bool notify = true)
        {
            if (_firstActivation)
            {
                ParserParams = await _bsmlDecorator.ParseFromResourceAsync(_resourceName, gameObject, this);
                gameObject.SetActive(false);
                _firstActivation = false;
                foreach (var obj in ParserParams.GetObjectsWithTag("canvas"))
                {
                    var newParent = obj.transform.parent.CreateGameObject("CanvasContainer");
                    newParent.AddComponent<RectTransform>();
                    newParent.AddComponent<VerticalLayoutGroup>();
                    newParent.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    newParent.AddComponent<LayoutElement>();
                    newParent.AddComponent<Canvas>();
                    var canvasScaler = newParent.AddComponent<CanvasScaler>();
                    canvasScaler.referencePixelsPerUnit = 10;
                    canvasScaler.scaleFactor = 3.44f;
                    newParent.AddComponent<CurvedCanvasSettings>();
                    UIHelpers.AddVrRaycaster(newParent, _raycasterWithCache);
                    obj.transform.SetParent(newParent.transform, false);
                }

                var texts = gameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in texts)
                {
                    UITemplateCache.AssignValidFont(text);
                }

                await Init();
            }
            gameObject.SetActive(true);
            if (notify)
            {
                DidOpen();
            }
        }

        public void Close()
        {
            DidClose();
        }

        public virtual void DidOpen()
        { }
        public virtual void DidClose()
        { }
        public void GoBack()
        {
            SubViewSwitcher.GoBack();
        }

        public void UpdateProps()
        {
            ParserParams.EmitEvent("update-props");
        }

        protected virtual Task Init()
        {
            return Task.CompletedTask;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propName = "")
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propName);
            return true;
        }

        internal class Factory : PlaceholderFactory<Type, InitData, SubView>
        { }
        internal struct InitData
        {
            public string Name;
            public Transform Parent;
        }
    }

    internal class SubViewFactory : IFactory<Type, SubView.InitData, SubView>
    {
        private readonly DiContainer _container;
        private SubViewFactory(DiContainer container)
        {
            _container = container;
        }

        public SubView Create(Type subViewType, SubView.InitData initData)
        {
            var go = initData.Parent.CreateGameObject(initData.Name);
            var rt = go.AddComponent<RectTransform>();
            rt.localEulerAngles = Vector3.zero;
            rt.localScale = rt.anchorMax = Vector3.one;
            rt.anchorMin = rt.sizeDelta = Vector2.zero;
            go.AddComponent<CanvasGroup>();
            var subView = (SubView)_container.InstantiateComponent(subViewType, go);
            return subView;
        }
    }

    internal class SubViewSwitcher
    {
        private const float AnimationDuration = 0.2f;
        public SubView CurrentSubView { get; private set; }
        private CancellationTokenSource _currentTokenSource;
        private SubView _previousSubView;
        public async void SwitchView(SubView newSubView, bool notify = true)
        {
            if (CurrentSubView == newSubView)
            {
                return;
            }
            if (CurrentSubView != null)
            {
                CurrentSubView.Close();
            }
            _previousSubView = CurrentSubView;
            CurrentSubView = newSubView;
            _currentTokenSource?.Cancel();
            _currentTokenSource?.Dispose();
            _currentTokenSource = new CancellationTokenSource();
            await DoTransition(notify, _currentTokenSource.Token);
        }

        public void NotifyDidOpen()
        {
            if (CurrentSubView != null)
            {
                CurrentSubView.DidOpen();
            }
        }

        public void NotifyDidClose()
        {
            if (CurrentSubView != null)
            {
                CurrentSubView.DidClose();
            }
        }

        public void GoBack()
        {
            SwitchView(_previousSubView);
        }

        private async Task DoTransition(
            bool notify,
            CancellationToken cancellationToken)
        {
            if (CurrentSubView is null)
            {
                return;
            }
            var toPresentCG = CurrentSubView.GetComponent<CanvasGroup>();
            toPresentCG.alpha = 0;
            toPresentCG.blocksRaycasts = true;
            await CurrentSubView.Open(notify);
            float moveOffset = 20;
            if (_previousSubView != null)
            {
                var toDismissCG = _previousSubView.GetComponent<CanvasGroup>();
                toDismissCG.blocksRaycasts = false;
                var baseCanvasGroupAlpha = toDismissCG.alpha;
                await Animate(t =>
                {
                    toDismissCG.alpha = Mathf.Lerp(baseCanvasGroupAlpha, 0.0f, t);
                    toDismissCG.transform.localEulerAngles = new Vector3(0, -moveOffset * 4 * t, 0.0f);
                }, cancellationToken);
            }
            await Animate(t =>
            {
                toPresentCG.alpha = t;
                toPresentCG.transform.localEulerAngles = new Vector3(0, moveOffset * 4 * (1f - t), 0);
            }, cancellationToken);
            if (_previousSubView != null)
            {
                _previousSubView.gameObject.SetActive(false);
            }
        }

        private async Task Animate(Action<float> transitionAnimation, CancellationToken cancellationToken)
        {
            var elapsedTime = 0.0f;
            while (elapsedTime < AnimationDuration)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                var num = Easing.OutQuart(elapsedTime / AnimationDuration);
                transitionAnimation?.Invoke(num);
                elapsedTime += Time.deltaTime;
                await Task.Yield();
            }
            transitionAnimation?.Invoke(1f);
        }
    }

    internal class ToggleController : ComponentController
    {
        public bool Value
        {
            get => Toggle.Value;
            set => Toggle.Value = value;
        }

        public readonly ToggleSetting Toggle;
        private UnityAction<bool> _event;
        public ToggleController(ToggleSetting toggle)
        {
            Toggle = toggle;
        }

        public void SetEvent(Action<bool> action)
        {
            RemoveEvent();
            _event = new UnityAction<bool>(action);
            Toggle.Toggle.onValueChanged.AddListener(_event);
        }

        public override void RemoveEvent()
        {
            if (_event != null)
            {
                Toggle.Toggle.onValueChanged.RemoveListener(_event);
            }
        }

        public override string GetId()
        {
            return ExternalComponents.Components.OfType<TextMeshProUGUI>().FirstOrDefault()?.text ?? "UnknownID";
        }

        public override void SetValue(object val)
        {
            Value = (bool)val;
        }

        public override object GetValue()
        {
            return Value;
        }
    }

    internal class ViewControllerFactory : IFactory<Type, CustomViewController.InitData, CustomViewController>
    {
        private readonly DiContainer _container;
        public ViewControllerFactory(DiContainer container)
        {
            _container = container;
        }

        public CustomViewController Create(Type viewControllerType, CustomViewController.InitData initData)
        {
            var go = new GameObject(viewControllerType.Name);
            go.transform.SetParent(initData.Parent);
            go.SetActive(false);
            var canvas = go.AddComponent<Canvas>();
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.Normal;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.Tangent;
            _container.InstantiateComponent<VRGraphicRaycaster>(go);
            var vc = (CustomViewController)_container.InstantiateComponent(viewControllerType, go);
            var rt = vc.rectTransform;
            rt.localEulerAngles = Vector3.zero;
            rt.localScale = rt.anchorMax = Vector3.one;
            rt.anchorMin = rt.sizeDelta = Vector2.zero;
            return vc;
        }
    }

    internal static class UIHelpers
    {
        public static void AddVrRaycaster(GameObject go, PhysicsRaycasterWithCache physicsRaycaster)
        {
            var vrgr = go.AddComponent<VRGraphicRaycaster>();
            vrgr.SetField("_physicsRaycaster", physicsRaycaster);
        }
    }

    internal static class GameObjectExtensions
    {
        public static RectTransform GetRect(this GameObject go)
        {
            return go.GetComponent<RectTransform>();
        }
    }

    internal static class ImageViewExtensions
    {
        private static FieldInfo _skewField;
        public static void SetSkew(this ImageView imageView, float skew)
        {
            if (_skewField == null)
            {
                _skewField = typeof(ImageView).GetField("_skew", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (_skewField == null) _skewField = typeof(ImageView).GetField("<skew>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            _skewField?.SetValue(imageView, skew);
            imageView.SetVerticesDirty();
        }
    }
}