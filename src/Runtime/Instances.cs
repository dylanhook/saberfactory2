using System;
using System.Collections.Generic;
using System.Linq;
using CustomSaber;
using HarmonyLib;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using SaberFactory2.Installers;
using SaberFactory2.Instances.Trail;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;
using SaberFactory2.Modifiers;
using SiraUtil.Logging;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.Instances
{
    internal static class ComponentBuffer
    {
        public static readonly List<Renderer> Renderers = new List<Renderer>();
        public static readonly List<Collider> Colliders = new List<Collider>();
        public static readonly List<AudioSource> AudioSources = new List<AudioSource>();
    }

    internal class BasePieceInstance : IDisposable
    {
        public PropertyBlockSetterHandler PropertyBlockSetterHandler { get; protected set; }
        public readonly Transform CachedTransform;
        public readonly GameObject GameObject;
        public readonly BasePieceModel Model;
        private List<Material> _colorableMaterials;
        protected readonly List<IPartPostProcessor> _postProcessors;
        protected BasePieceInstance(BasePieceModel model, List<IPartPostProcessor> postProcessors)
        {
            _postProcessors = postProcessors;
            Model = model;
            GameObject = Instantiate();
            CachedTransform = GameObject.transform;
            model.ModifyableComponentManager.SetInstance(GameObject);
        }

        public virtual void Dispose()
        {
            if (_colorableMaterials == null) return;
            foreach (var material in _colorableMaterials)
            {
                material.TryDestroy();
            }
        }

        public void SetParent(Transform parent)
        {
            CachedTransform.SetParent(parent, false);
        }

        protected virtual GameObject Instantiate()
        {
            return new GameObject("BasePiece");
        }

        public virtual PartEvents GetEvents()
        {
            return null;
        }

        public virtual void SetColor(Color color)
        {
            if (_colorableMaterials is null)
            {
                _colorableMaterials = new List<Material>();
                GetColorableMaterials(_colorableMaterials);
            }
            foreach (var material in _colorableMaterials)
            {
                material.SetColor(MaterialProperties.MainColor, color);
            }
        }

        protected virtual void GetColorableMaterials(List<Material> materials)
        { }
        internal class Factory : PlaceholderFactory<BasePieceModel, BasePieceInstance>
        { }
    }

    internal class InstanceFactory : IFactory<BasePieceModel, BasePieceInstance>
    {
        private readonly DiContainer _container;
        private readonly SiraLog _logger;
        public InstanceFactory(SiraLog logger, DiContainer container)
        {
            _logger = logger;
            _container = container;
        }

        public BasePieceInstance Create(BasePieceModel model)
        {
            if (model.InstanceType is null)
            {
                throw new ArgumentException($"InstanceType is null on {model.GetType().Name}", nameof(model));
            }
            return (BasePieceInstance)_container.Instantiate(model.InstanceType, new[] { model });
        }
    }

    public class MaterialDescriptor
    {
        public bool IsValid => Material != null;
        public Material Material;
        private Material _originalMaterial;
        public MaterialDescriptor(Material material)
        {
            Material = material;
            if (material != null)
            {
                _originalMaterial = new Material(material);
            }
        }

        public virtual void Revert()
        {
            if (_originalMaterial is null)
            {
                return;
            }
            DestroyCurrentMaterial();
            Material = new Material(_originalMaterial);
        }

        public void DestroyCurrentMaterial()
        {
            Material.TryDestroyImmediate();
        }

        public void DestroyBackupMaterial()
        {
            _originalMaterial.TryDestroyImmediate();
        }

        public void Destroy()
        {
            DestroyCurrentMaterial();
            DestroyBackupMaterial();
        }

        public void UpdateBackupMaterial(bool deleteOld)
        {
            if (deleteOld && _originalMaterial != null)
            {
                DestroyBackupMaterial();
            }
            _originalMaterial = new Material(Material);
        }

        public MaterialDescriptor CreateCopy()
        {
            return new MaterialDescriptor(new Material(Material));
        }
    }

    internal class RendererMaterialDescriptor : MaterialDescriptor, IDisposable
    {
        private readonly int _materialIndex;
        private readonly Renderer _renderer;
        public RendererMaterialDescriptor(Material material, Renderer renderer, int materialIndex) : base(material)
        {
            _renderer = renderer;
            _materialIndex = materialIndex;
        }

        public void Dispose()
        {
            Destroy();
        }

        public override void Revert()
        {
            base.Revert();
            _renderer.SetMaterial(_materialIndex, Material);
        }
    }

    public class SaberInstance
    {
        public const string SaberName = "SF2 Saber";
        internal ITrailHandler TrailHandler { get; private set; }
        internal List<PartEvents> Events { get; private set; }
        public readonly Transform CachedTransform;
        public readonly GameObject GameObject;
        internal readonly SaberModel Model;
        internal readonly PieceCollection<BasePieceInstance> PieceCollection;
        private readonly SiraLog _logger;
        private readonly TrailConfig _trailConfig;
        private InstanceTrailData _instanceTrailData;
        private List<CustomSaberTrailHandler> _secondaryTrails;
        private readonly PlayerDataModel _playerDataModel;
        private readonly SaberInstanceList _saberInstanceList;
        private readonly SaberSettableSettings _saberSettableSettings;
        public PlayerTransforms PlayerTransforms;
        private readonly Dictionary<Type, Component> _saberComponents = new Dictionary<Type, Component>();
        private SaberInstance(
            SaberModel model,
            BasePieceInstance.Factory pieceFactory,
            SiraLog logger,
            TrailConfig trailConfig,
            List<ISaberPostProcessor> saberMiddlewares,
            PlayerDataModel playerDataModel,
            SaberInstanceList saberInstanceList,
            [InjectOptional] SaberSettableSettings saberSettableSettings)
        {
            _logger = logger;
            _trailConfig = trailConfig;
            _playerDataModel = playerDataModel;
            _saberInstanceList = saberInstanceList;
            _saberSettableSettings = saberSettableSettings;
            Model = model;
            GameObject = new GameObject(SaberName);
            GameObject.AddComponent<SaberMonoBehaviour>().Init(this, _saberComponents, OnSaberGameObjectDestroyed);
            CachedTransform = GameObject.transform;
            PieceCollection = new PieceCollection<BasePieceInstance>();
            var sectionInstantiator = new SectionInstantiator(this, pieceFactory, PieceCollection);
            sectionInstantiator.InstantiateSections();
            GameObject.transform.localScale = new Vector3(model.SaberWidth, model.SaberWidth, model.SaberLength);
            saberMiddlewares.Do(x => x.ProcessSaber(this));
            SetupGlobalShaderVars();
            SetupTrailData();
            InitializeEvents();
            _saberInstanceList.Add(this);
        }

        internal event Action OnDestroyed;
        public void SetParent(Transform parent)
        {
            CachedTransform.SetParent(parent, false);
        }

        public void SetColor(Color color)
        {
            foreach (BasePieceInstance piece in PieceCollection)
            {
                piece.SetColor(color);
            }
            TrailHandler?.SetColor(color);
            if (_secondaryTrails != null)
            {
                foreach (var trail in _secondaryTrails)
                {
                    trail.SetColor(color);
                }
            }
        }

        private void InitializeEvents()
        {
            Events = new List<PartEvents>();
            foreach (BasePieceInstance piece in PieceCollection)
            {
                var events = piece.GetEvents();
                if (events != null)
                {
                    Events.Add(events);
                }
            }
        }

        private void SetupTrailData()
        {
            if (GetCustomSaber(out var customsaber))
            {
                return;
            }
            _instanceTrailData = null;
        }

        public void CreateTrail(bool editor, SaberTrail backupTrail = null)
        {
            var trailData = GetTrailData(out var secondaryTrails);
            if (trailData is null)
            {
                if (backupTrail is { })
                {
                    TrailHandler = new MainTrailHandler(GameObject, backupTrail, PlayerTransforms, _saberSettableSettings);
                    TrailHandler.CreateTrail(_trailConfig, editor);
                }
                return;
            }
            TrailHandler = new MainTrailHandler(GameObject, PlayerTransforms, _saberSettableSettings);
            TrailHandler.SetTrailData(trailData);
            TrailHandler.CreateTrail(_trailConfig, editor);
            if (secondaryTrails != null)
            {
                _secondaryTrails = new List<CustomSaberTrailHandler>();
                foreach (var customTrail in secondaryTrails)
                {
                    var handler = new CustomSaberTrailHandler(GameObject, customTrail, PlayerTransforms);
                    handler.CreateTrail(_trailConfig, editor);
                    _secondaryTrails.Add(handler);
                }
            }
        }

        public void DestroyTrail(bool immediate = false)
        {
            TrailHandler?.DestroyTrail(immediate);
            if (_secondaryTrails != null)
            {
                foreach (var trail in _secondaryTrails)
                {
                    trail.DestroyTrail();
                }
            }
        }

        public bool GetSaberComponent<T>(out T saberComp) where T : Component
        {
            saberComp = null;
            if (_saberComponents.TryGetValue(typeof(T), out var comp) && comp is T typedComp)
            {
                saberComp = typedComp;
                return true;
            }
            return false;
        }

        public void Destroy()
        {
            if (GameObject != null)
            {
                GameObject.TryDestroy();
            }
            OnDestroyed?.Invoke();
            _saberInstanceList.Remove(this);
        }

        private void OnSaberGameObjectDestroyed()
        {
            DestroyTrail();
            foreach (BasePieceInstance piece in PieceCollection)
            {
                piece.Dispose();
            }
            _saberComponents.Clear();
        }

        private bool GetCustomSaber(out CustomSaberInstance customSaberInstance)
        {
            if (PieceCollection.TryGetPiece(AssetTypeDefinition.CustomSaber, out var instance) && instance is CustomSaberInstance csInstance)
            {
                customSaberInstance = csInstance;
                return true;
            }
            customSaberInstance = null;
            return false;
        }

        internal InstanceTrailData GetTrailData(out List<CustomTrail> secondaryTrails)
        {
            secondaryTrails = null;
            if (GetCustomSaber(out var customsaber))
            {
                secondaryTrails = customsaber.InstanceTrailData?.SecondaryTrails?.Select(x => x.Trail).ToList();
                return customsaber.InstanceTrailData;
            }
            return _instanceTrailData;
        }

        public void SetSaberWidth(float width)
        {
            Model.SaberWidth = width;
            if (GameObject != null) GameObject.transform.localScale = new Vector3(width, width, Model.SaberLength);
        }

        public void SetSaberLength(float length)
        {
            Model.SaberLength = length;
            if (GameObject != null) GameObject.transform.localScale = new Vector3(Model.SaberWidth, Model.SaberWidth, length);
        }

        private void SetupGlobalShaderVars()
        {
            if (_playerDataModel?.playerData?.colorSchemesSettings?.GetSelectedColorScheme() is { } scheme)
            {
                Shader.SetGlobalColor(MaterialProperties.UserColorLeft, scheme.saberAColor);
                Shader.SetGlobalColor(MaterialProperties.UserColorRight, scheme.saberBColor);
            }
        }

        internal class Factory : PlaceholderFactory<SaberModel, SaberInstance>
        { }

        internal class SaberMonoBehaviour : MonoBehaviour
        {
            public SaberInstance SaberInstance { get; private set; }
            private Action _onDestroyed;
            private Dictionary<Type, Component> _saberComponentDict;

            private void OnDestroy()
            {
                _onDestroyed?.Invoke();
            }

            public void Init(SaberInstance saberInstance, Dictionary<Type, Component> saberComponentDict, Action onDestroyedCallback)
            {
                SaberInstance = saberInstance;
                _saberComponentDict = saberComponentDict;
                _onDestroyed = onDestroyedCallback;
            }

            public void RegisterComponent<T>(T comp) where T : Component
            {
                if (_saberComponentDict != null && comp != null)
                {
                    _saberComponentDict[typeof(T)] = comp;
                }
            }
        }
    }

    internal class SectionInstantiator
    {
        private readonly PieceCollection<BasePieceInstance> _pieceCollection;
        private readonly BasePieceInstance.Factory _pieceInstanceFactory;
        private readonly SaberInstance _saberInstance;
        public SectionInstantiator(SaberInstance saberInstance, BasePieceInstance.Factory pieceInstanceFactory,
            PieceCollection<BasePieceInstance> pieceCollection)
        {
            _saberInstance = saberInstance;
            _pieceInstanceFactory = pieceInstanceFactory;
            _pieceCollection = pieceCollection;
        }

        public void InstantiateSections()
        {
            if (_saberInstance.Model.PieceCollection.TryGetPiece(AssetTypeDefinition.CustomSaber, out var customSaberModel))
            {
                InstantiateSection(AssetTypeDefinition.CustomSaber, customSaberModel);
                return;
            }
            InstantiateSection(new AssetTypeDefinition(EAssetType.Model, EAssetSubType.Pommel));
            InstantiateSection(new AssetTypeDefinition(EAssetType.Model, EAssetSubType.Handle));
            InstantiateSection(new AssetTypeDefinition(EAssetType.Model, EAssetSubType.Emitter));
            InstantiateSection(new AssetTypeDefinition(EAssetType.Model, EAssetSubType.Blade));
        }

        private void InstantiateSection(AssetTypeDefinition definition, BasePieceModel explicitModel = null)
        {
            var modelPiece = explicitModel ?? _saberInstance.Model.PieceCollection[definition];
            if (modelPiece != null)
            {
                var instance = _pieceInstanceFactory.Create(modelPiece);
                instance.SetParent(_saberInstance.CachedTransform);
                _saberInstance.PieceCollection[definition] = instance;
            }
        }
    }

    internal class CustomSaberInstance : BasePieceInstance
    {
        public InstanceTrailData InstanceTrailData { get; private set; }
        private readonly SiraLog _logger;
        public CustomSaberInstance(
            CustomSaberModel model,
            SiraLog logger,
            List<IPartPostProcessor> postProcessors)
            : base(model, postProcessors)
        {
            _logger = logger;
            InitializeTrailData(GameObject, model.TrailModel);
        }

        public void InitializeTrailData(GameObject saberObject, TrailModel trailModel)
        {
            if (saberObject is null || trailModel is null)
            {
                return;
            }
            var trails = SaberHelpers.GetTrails(saberObject).ToArray();
            CustomTrail SetupTrail(int length, float startPos, float endPos, Material material)
            {
                var newTrail = saberObject.AddComponent<CustomTrail>();
                newTrail.Length = length;
                newTrail.PointStart = saberObject.CreateGameObject("PointStart").transform;
                newTrail.PointEnd = saberObject.CreateGameObject("PointEnd").transform;
                newTrail.PointEnd.localPosition = new Vector3(0, 0, endPos);
                newTrail.PointStart.localPosition = new Vector3(0, 0, startPos);
                newTrail.TrailMaterial = material;
                return newTrail;
            }
            if (trails is null || trails.Length < 1)
            {
                trails = new[] { SetupTrail(12, 0, 1, null) };
            }
            var saberTrail = trails[0];
            List<CustomTrail> secondaryTrails = null;
            if (trailModel.TrailOriginTrails is { } && trailModel.TrailOriginTrails.Count > 1)
            {
                secondaryTrails = new List<CustomTrail>();
                for (var i = 1; i < trails.Length; i++)
                {
                    Object.DestroyImmediate(trails[i]);
                }
                for (var i = 1; i < trailModel.TrailOriginTrails.Count; i++)
                {
                    var otherTrail = trailModel.TrailOriginTrails[i];
                    if (otherTrail.PointStart is null || otherTrail.PointEnd is null)
                    {
                        continue;
                    }
                    var newTrail = SetupTrail(
                        otherTrail.Length,
                        otherTrail.PointStart.localPosition.z,
                        otherTrail.PointEnd.localPosition.z,
                        otherTrail.TrailMaterial);
                    secondaryTrails.Add(newTrail);
                }
            }
            else if (trails.Length > 1)
            {
                secondaryTrails = new List<CustomTrail>();
                for (var i = 1; i < trails.Length; i++)
                {
                    secondaryTrails.Add(trails[i]);
                }
            }
            if (trailModel.Material is null)
            {
                trailModel.Material = new MaterialDescriptor(saberTrail.TrailMaterial);
                if (trailModel.Material != null && trailModel.Material.Material.TryGetMainTexture(out var tex))
                {
                    trailModel.OriginalTextureWrapMode = tex.wrapMode;
                }
            }
            var pointStart = saberTrail.PointStart;
            var pointEnd = saberTrail.PointEnd;
            var isTrailReversed = pointStart.localPosition.z > pointEnd.localPosition.z;
            if (isTrailReversed)
            {
                pointStart = saberTrail.PointEnd;
                pointEnd = saberTrail.PointStart;
            }
            InstanceTrailData = new InstanceTrailData(trailModel, pointStart, pointEnd, isTrailReversed, secondaryTrails);
        }

        public override PartEvents GetEvents()
        {
            return PartEvents.FromCustomSaber(GameObject);
        }

        protected override void GetColorableMaterials(List<Material> materials)
        {
            void AddMaterial(Renderer renderer, Material[] rendererMaterials, int index)
            {
                rendererMaterials[index] = new Material(rendererMaterials[index]);
                renderer.sharedMaterials = rendererMaterials;
                materials.Add(rendererMaterials[index]);
            }
            ComponentBuffer.Renderers.Clear();
            GameObject.GetComponentsInChildren(true, ComponentBuffer.Renderers);

            for (int r = 0; r < ComponentBuffer.Renderers.Count; r++)
            {
                var renderer = ComponentBuffer.Renderers[r];
                if (renderer is null) continue;

                var rendererMaterials = renderer.sharedMaterials;
                for (var i = 0; i < rendererMaterials.Length; i++)
                {
                    var material = rendererMaterials[i];
                    if (material == null || !material.HasProperty(MaterialProperties.MainColor)) continue;

                    if ((material.HasProperty(MaterialProperties.CustomColors) && material.GetFloat(MaterialProperties.CustomColors) > 0) ||
                        (material.HasProperty(MaterialProperties.Glow) && material.GetFloat(MaterialProperties.Glow) > 0) ||
                        (material.HasProperty(MaterialProperties.Bloom) && material.GetFloat(MaterialProperties.Bloom) > 0))
                    {
                        AddMaterial(renderer, rendererMaterials, i);
                    }
                }
            }
        }

        protected override GameObject Instantiate()
        {
            Model.Cast<CustomSaberModel>().FixTrailParents();
            var instance = Object.Instantiate(GetSaberPrefab(), Vector3.zero, Quaternion.identity);
            instance.SetActive(true);
            PropertyBlockSetterHandler = new CustomSaberPropertyBlockSetterHandler(instance, Model as CustomSaberModel);
            _postProcessors.Do(x => x.ProcessPart(instance));
            return instance;
        }

        private GameObject GetSaberPrefab()
        {
            return Model.AdditionalInstanceHandler.GetSaber(Model.SaberSlot);
        }
    }

    public interface IPartPostProcessor
    {
        void ProcessPart(GameObject partObject);
    }

    public interface ISaberPostProcessor
    {
        void ProcessSaber(SaberInstance saberObject);
    }

    internal class MainSaberPostProcessor : ISaberPostProcessor
    {
        private readonly PluginConfig _config;
        internal MainSaberPostProcessor(PluginConfig config)
        {
            _config = config;
        }

        public void ProcessSaber(SaberInstance saberObject)
        {
            var gameobject = saberObject.GameObject;
            gameobject.SetLayer<Renderer>(12);
            ComponentBuffer.Colliders.Clear();
            gameobject.GetComponentsInChildren(true, ComponentBuffer.Colliders);
            ComponentBuffer.Colliders.Do(x => x.enabled = false);
            ComponentBuffer.AudioSources.Clear();
            gameobject.GetComponentsInChildren(true, ComponentBuffer.AudioSources);
            ComponentBuffer.AudioSources.Do(x => x.volume *= _config.SaberAudioVolumeMultiplier);
            ComponentBuffer.Renderers.Clear();
            gameobject.GetComponentsInChildren(true, ComponentBuffer.Renderers);
            ComponentBuffer.Renderers.Do(x => { x.sortingOrder = 3; });
            if (gameobject.GetComponentInChildren<SFSaberSound>() is { } saberSound)
            {
                saberSound.ConfigVolume = _config.SwingSoundVolume;
            }
        }
    }

    internal class CustomSaberPropertyBlockSetterHandler : PropertyBlockSetterHandler
    {
        public TransformDataSetter TransformDataSetter;
        public CustomSaberPropertyBlockSetterHandler(GameObject gameObject, CustomSaberModel model)
        {
            var propBlock = (CustomSaberPropertyBlock)model.PropertyBlock;
            TransformDataSetter = new TransformDataSetter(gameObject, propBlock.TransformProperty);
        }
    }

    internal abstract class PropertyBlockSetterHandler
    { }
    internal class TransformDataSetter
    {
        public float Width
        {
            get => _transformPropertyBlock.Width;
            set
            {
                _gameObject.transform.localScale =
                    new Vector3(_baseWidth.x * value, _baseWidth.y * value, _baseWidth.z);
                _transformPropertyBlock.Width = value;
            }
        }

        public float Offset
        {
            get => _transformPropertyBlock.Offset;
            set
            {
                var pos = _transform.localPosition;
                pos.z = value;
                _transform.localPosition = pos;
                _transformPropertyBlock.Offset = value;
            }
        }

        public float Rotation
        {
            get => _transformPropertyBlock.Rotation;
            set
            {
                var rot = _transform.localEulerAngles;
                rot.z = value;
                _transform.localEulerAngles = rot;
                _transformPropertyBlock.Rotation = value;
            }
        }

        private readonly Vector3 _baseWidth;
        private readonly GameObject _gameObject;
        private readonly Transform _transform;
        private readonly TransformPropertyBlock _transformPropertyBlock;
        public TransformDataSetter(GameObject gameObject, TransformPropertyBlock transformPropertyBlock)
        {
            _gameObject = gameObject;
            _transform = gameObject.transform;
            _transformPropertyBlock = transformPropertyBlock;
            _baseWidth = gameObject.transform.localScale;
            Width = Width;
            Offset = Offset;
            Rotation = Rotation;
        }
    }
}