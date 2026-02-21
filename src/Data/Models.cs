using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using CustomSaber;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberFactory2.DataStore;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Loaders;
using SaberFactory2.Models.CustomSaber;
using SaberFactory2.Modifiers;
using SaberFactory2.Serialization;
using SaberFactory2.UI.Lib;
using UnityEngine;
using UnityEngine.Events;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.Models
{
    public class AdditionalInstanceHandler
    {
        public bool IsInstantiated => _instance != null;
        private readonly GameObject _prefab;
        private readonly GameObject _fallbackRightSaber;
        private GameObject _customSaberLeftSaber;
        private GameObject _customSaberRightSaber;
        private GameObject _instance;
        public AdditionalInstanceHandler(GameObject prefab, GameObject fallbackRightSaber)
        {
            _prefab = prefab;
            _fallbackRightSaber = fallbackRightSaber;
        }

        public GameObject GetInstance()
        {
            if (!_instance)
            {
                Instantiate();
            }
            return _instance;
        }

        public void Destroy()
        {
            _instance.TryDestroyImmediate();
        }

        public GameObject GetSaber(ESaberSlot saberSlot)
        {
            if (saberSlot == ESaberSlot.Left && !_customSaberLeftSaber ||
                saberSlot == ESaberSlot.Right && !_customSaberRightSaber)
            {
                var saber = FindInInstance(saberSlot == ESaberSlot.Left ? "LeftSaber" : "RightSaber");
                if (saber)
                {
                    return saber.gameObject;
                }
                return null;
            }
            return saberSlot == ESaberSlot.Left ? _customSaberLeftSaber : _customSaberRightSaber;
        }

        public T GetComponent<T>() where T : Component
        {
            return GetInstance().GetComponent<T>();
        }

        public Transform FindInInstance(string name)
        {
            return GetInstance().transform.Find(name);
        }

        private void Instantiate()
        {
            _instance = Object.Instantiate(_prefab);
            _instance.name = "Additional Instances";
            _customSaberLeftSaber = GetSaber(ESaberSlot.Left);
            _customSaberRightSaber = GetSaber(ESaberSlot.Right);
            if (_customSaberRightSaber == null)
            {
                _customSaberRightSaber = Object.Instantiate(_fallbackRightSaber);
            }
            if (_customSaberLeftSaber != null)
            {
                _customSaberLeftSaber.SetActive(false);
            }
            if (_customSaberRightSaber != null)
            {
                _customSaberRightSaber.SetActive(false);
            }
        }
    }

    [Serializable]
    public readonly struct AssetTypeDefinition
    {
        public static readonly AssetTypeDefinition CustomSaber = new AssetTypeDefinition(EAssetType.Model, EAssetSubType.CustomSaber);
        public AssetTypeDefinition(EAssetType assetType, EAssetSubType assetSubType)
        {
            AssetType = assetType;
            AssetSubType = assetSubType;
        }

        public EAssetType AssetType { get; }
        public EAssetSubType AssetSubType { get; }
    }

    public class BasePieceModel : IDisposable, IFactorySerializable
    {
        public virtual Type InstanceType { get; protected set; }
        public ModelComposition ModelComposition { get; set; }
        public GameObject Prefab => StoreAsset.Prefab;
        public readonly ModifyableComponentManager ModifyableComponentManager;
        public readonly StoreAsset StoreAsset;
        public AdditionalInstanceHandler AdditionalInstanceHandler;
        public PiecePropertyBlock PropertyBlock;
        public ESaberSlot SaberSlot;
        protected BasePieceModel(StoreAsset storeAsset)
        {
            StoreAsset = storeAsset;
            ModifyableComponentManager = new ModifyableComponentManager(storeAsset.Prefab);
            ModifyableComponentManager.Map();
        }

        public virtual void Dispose()
        { }
        public virtual async Task FromJson(JObject obj, Serializer serializer)
        {
            await PropertyBlock.FromJson((JObject)obj[nameof(PropertyBlock)], serializer);
            await ModifyableComponentManager.FromJson((JObject)obj[nameof(ModifyableComponentManager)], serializer);
        }

        public virtual async Task<JToken> ToJson(Serializer serializer)
        {
            var obj = new JObject
            {
                { "Path", StoreAsset.RelativePath },
                { nameof(PropertyBlock), await PropertyBlock.ToJson(serializer) },
                { nameof(ModifyableComponentManager), await ModifyableComponentManager.ToJson(serializer) }
            };
            return obj;
        }

        public virtual void Init()
        { }
        public virtual void OnLazyInit()
        { }
        public virtual void SaveAdditionalData()
        { }
        public virtual ModelMetaData GetMetaData()
        {
            return default;
        }

        public virtual void SyncFrom(BasePieceModel otherModel)
        {
            PropertyBlock.SyncFrom(otherModel.PropertyBlock);
            ModifyableComponentManager.Sync(otherModel.ModifyableComponentManager);
        }
    }

    public enum EAssetSubType
    {
        Blade,
        Emitter,
        Handle,
        Pommel,
        CustomSaber
    }

    public enum EAssetType
    {
        Model,
        Halo
    }

    public enum ESaberSlot
    {
        Left,
        Right
    }

    internal enum ESaberType
    {
        Saberfactory,
        CustomSaber
    }

    internal interface IStoreAssetParser
    {
        ModelComposition GetComposition(StoreAsset storeAsset);
    }

    public class ModelComposition : IDisposable, ICustomListItem
    {
        public readonly AdditionalInstanceHandler AdditionalInstanceHandler;
        public readonly AssetTypeDefinition AssetTypeDefinition;
        private readonly BasePieceModel _modelLeft;
        private readonly BasePieceModel _modelRight;
        private bool _didLazyInit;
        private ModelMetaData _metaData;
        public ModelComposition(AssetTypeDefinition definition, BasePieceModel modelLeft, BasePieceModel modelRight, GameObject additionalData)
        {
            AssetTypeDefinition = definition;
            _modelLeft = modelLeft;
            _modelRight = modelRight;
            AdditionalInstanceHandler = new AdditionalInstanceHandler(additionalData, modelRight.StoreAsset.Prefab);
            if (_modelLeft == null && _modelRight == null)
            {
                return;
            }
            if (_modelLeft != null)
            {
                _modelLeft.ModelComposition = this;
                _modelLeft.AdditionalInstanceHandler = AdditionalInstanceHandler;
                _modelLeft.Init();
                _metaData = modelLeft.GetMetaData();
            }
            if (_modelRight != null)
            {
                _modelRight.ModelComposition = this;
                _modelRight.AdditionalInstanceHandler = AdditionalInstanceHandler;
                _modelRight.Init();
            }
        }

        public string ListName => _metaData.Name;
        public string ListAuthor => _metaData.Author;
        public Sprite ListCover => _metaData.Cover;
        public bool IsFavorite => _metaData.IsFavorite;
        public void Dispose()
        {
            if (_modelLeft != null)
            {
                _modelLeft.StoreAsset.Unload();
                _modelLeft.Dispose();
            }
            _modelRight?.Dispose();
        }

        public void LazyInit()
        {
            if (_didLazyInit || _modelLeft == null)
            {
                return;
            }
            _didLazyInit = true;
            _modelLeft.OnLazyInit();
        }

        public void SaveAdditionalData()
        {
            if (_modelLeft == null)
            {
                return;
            }
            _modelLeft.SaveAdditionalData();
        }

        public void Sync(BasePieceModel syncModel)
        {
            var otherModel = _modelLeft == syncModel ? _modelRight : _modelLeft;
            otherModel?.SyncFrom(syncModel);
        }

        public BasePieceModel GetLeft()
        {
            return _modelLeft;
        }

        public BasePieceModel GetRight()
        {
            if (_modelRight == null)
            {
                return _modelLeft;
            }
            return _modelRight;
        }

        public BasePieceModel GetPiece(ESaberSlot saberSlot)
        {
            return saberSlot == ESaberSlot.Left ? GetLeft() : GetRight();
        }

        public void DestroyAdditionalInstances()
        {
            AdditionalInstanceHandler.Destroy();
        }

        public void SetFavorite(bool isFavorite)
        {
            _metaData.IsFavorite = isFavorite;
        }
    }

    public struct ModelMetaData
    {
        public string Name;
        public string Author;
        public Sprite Cover;
        public bool IsFavorite;
        public ModelMetaData(string name, string author, Sprite cover, bool isFavorite)
        {
            Name = name;
            Author = author;
            Cover = cover;
            IsFavorite = isFavorite;
        }
    }

    public class PartEvents
    {
        public UnityEvent MultiplierUp;
        public UnityEvent<float> OnAccuracyChanged;
        public UnityEvent OnBlueLightOn;
        public UnityEvent OnComboBreak;
        public UnityEvent<int> OnComboChanged;
        public UnityEvent OnLevelEnded;
        public UnityEvent OnLevelFail;
        public UnityEvent OnLevelStart;
        public UnityEvent OnRedLightOn;
        public UnityEvent OnSlice;
        public UnityEvent SaberStartColliding;
        public UnityEvent SaberStopColliding;
        public static PartEvents FromCustomSaber(GameObject saberObject)
        {
            var eventManager = saberObject.GetComponent<EventManager>();
            if (!eventManager)
            {
                return null;
            }
            var partEvents = new PartEvents
            {
                OnSlice = eventManager.OnSlice,
                OnComboBreak = eventManager.OnComboBreak,
                MultiplierUp = eventManager.MultiplierUp,
                SaberStartColliding = eventManager.SaberStartColliding,
                SaberStopColliding = eventManager.SaberStopColliding,
                OnLevelStart = eventManager.OnLevelStart,
                OnLevelFail = eventManager.OnLevelFail,
                OnLevelEnded = eventManager.OnLevelEnded,
                OnBlueLightOn = eventManager.OnBlueLightOn,
                OnRedLightOn = eventManager.OnRedLightOn,
                OnComboChanged = eventManager.OnComboChanged,
                OnAccuracyChanged = eventManager.OnAccuracyChanged
            };
            return partEvents;
        }
    }

    public class PieceCollection<T> : IEnumerable
    {
        public T this[AssetTypeDefinition definition]
        {
            get => GetPiece(definition);
            set => AddPiece(definition, value);
        }

        public int PieceCount => _pieceModels.Count;
        private readonly Dictionary<AssetTypeDefinition, T> _pieceModels;
        public PieceCollection()
        {
            _pieceModels = new Dictionary<AssetTypeDefinition, T>();
        }

        public IEnumerator GetEnumerator()
        {
            return _pieceModels.Values.GetEnumerator();
        }

        public bool HasPiece(AssetTypeDefinition definition)
        {
            return _pieceModels.ContainsKey(definition);
        }

        public void AddPiece(AssetTypeDefinition definition, T model)
        {
            if (!HasPiece(definition))
            {
                _pieceModels.Add(definition, model);
            }
            else
            {
                _pieceModels[definition] = model;
            }
        }

        public bool TryGetPiece(AssetTypeDefinition definition, out T model)
        {
            return _pieceModels.TryGetValue(definition, out model);
        }

        public T GetPiece(AssetTypeDefinition definition)
        {
            return _pieceModels.TryGetValue(definition, out var model) ? model : default;
        }
    }

    public class PreloadMetaData : ICustomListItem
    {
        public AssetTypeDefinition AssetTypeDefinition { get; private set; }
        public Texture2D CoverTex
        {
            get
            {
                if (_coverTex == null)
                {
                    _coverTex = LoadTexture();
                }
                return _coverTex;
            }
        }

        public Sprite CoverSprite
        {
            get
            {
                if (_coverSprite == null)
                {
                    _coverSprite = LoadSprite();
                }
                return _coverSprite;
            }
        }

        internal readonly AssetMetaPath AssetMetaPath;
        private byte[] _coverData;
        private Sprite _coverSprite;
        private Texture2D _coverTex;
        internal PreloadMetaData(AssetMetaPath assetMetaPath)
        {
            AssetMetaPath = assetMetaPath;
        }

        internal PreloadMetaData(AssetMetaPath assetMetaPath, ICustomListItem customListItem, AssetTypeDefinition assetTypeDefinition)
        {
            AssetMetaPath = assetMetaPath;
            AssetTypeDefinition = assetTypeDefinition;
            ListName = customListItem.ListName;
            ListAuthor = customListItem.ListAuthor;
            _coverSprite = customListItem.ListCover;
        }

        public string ListName { get; private set; }
        public string ListAuthor { get; private set; }
        public Sprite ListCover => CoverSprite;
        public bool IsFavorite { get; set; }
        public void SaveToFile()
        {
            if (AssetMetaPath.HasMetaData)
            {
                File.Delete(AssetMetaPath.MetaDataPath);
            }
            var ser = new SerializableMeta();
            ser.Name = ListName;
            ser.Author = ListAuthor;
            ser.AssetTypeDefinition = AssetTypeDefinition;
            if (_coverSprite != null)
            {
                var tex = _coverSprite.texture;
                ser.CoverData = GetTextureData(tex);
            }
            File.WriteAllText(AssetMetaPath.MetaDataPath, JsonConvert.SerializeObject(ser));
        }

        public void LoadFromFile()
        {
            LoadFromFile(AssetMetaPath.MetaDataPath);
        }

        public void LoadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            var ser = JsonConvert.DeserializeObject<SerializableMeta>(json);
            ListName = ser.Name;
            ListAuthor = ser.Author;
            _coverData = ser.CoverData;
            AssetTypeDefinition = ser.AssetTypeDefinition;
            LoadSprite();
        }

        public void SetFavorite(bool isFavorite)
        {
            IsFavorite = isFavorite;
        }

        private byte[] GetTextureData(Texture2D tex)
        {
            var tmp = RenderTexture.GetTemporary(
                tex.width,
                tex.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Default);
            Graphics.Blit(tex, tmp);
            var previous = RenderTexture.active;
            RenderTexture.active = tmp;
            var myTexture2D = new Texture2D(tex.width, tex.height);
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            return myTexture2D.EncodeToPNG();
        }

        private Texture2D LoadTexture()
        {
            return _coverData == null ? null : TextureUtilities.LoadTextureRaw(_coverData);
        }

        private Sprite LoadSprite()
        {
            return CoverTex == null ? null : Utilities.LoadSpriteFromTexture(CoverTex);
        }

        [Serializable]
        internal class SerializableMeta
        {
            public AssetTypeDefinition AssetTypeDefinition;
            public string Author;
            public byte[] CoverData;
            public string Name;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class SaberModel : IFactorySerializable
    {
        public bool IsEmpty => PieceCollection.PieceCount == 0;
        public readonly PieceCollection<BasePieceModel> PieceCollection;
        public readonly ESaberSlot SaberSlot;
        [JsonProperty][MapSerialize] public float SaberLength = 1;
        [JsonProperty][MapSerialize] public float SaberWidth = 1;
        public TrailModel TrailModel;
        public SaberModel(ESaberSlot saberSlot)
        {
            SaberSlot = saberSlot;
            PieceCollection = new PieceCollection<BasePieceModel>();
        }

        public async Task FromJson(JObject obj, Serializer serializer)
        {
            obj.Populate(this);
            var piecesTkn = obj.Property(nameof(PieceCollection));
            if (piecesTkn != null)
            {
                var pieceList = (JArray)piecesTkn.Value;
                foreach (var pieceTkn in pieceList)
                {
                    var piece = await serializer.LoadPiece(pieceTkn["Path"]);
                    if (piece == null)
                    {
                        continue;
                    }
                    PieceCollection.AddPiece(piece.AssetTypeDefinition, piece.GetPiece(SaberSlot));
                    await piece.GetPiece(SaberSlot)?.FromJson((JObject)pieceTkn, serializer);
                }
            }
        }

        public async Task<JToken> ToJson(Serializer serializer)
        {
            var obj = JObject.FromObject(this);
            var pieceList = new JArray();
            foreach (BasePieceModel pieceModel in PieceCollection)
            {
                pieceList.Add(await pieceModel.ToJson(serializer));
            }
            obj.Add(nameof(PieceCollection), pieceList);
            return obj;
        }

        public void SetModelComposition(ModelComposition composition)
        {
            PieceCollection[composition.AssetTypeDefinition] = SaberSlot == ESaberSlot.Left
                ? composition.GetLeft()
                : composition.GetRight();
        }

        public TrailModel GetTrailModel()
        {
            if (GetCustomSaber(out var customsaber))
            {
                return customsaber.TrailModel;
            }
            return TrailModel;
        }

        public void Sync()
        {
            foreach (BasePieceModel piece in PieceCollection)
            {
                piece.ModelComposition.Sync(piece);
            }
        }

        public bool GetCustomSaber(out CustomSaberModel customsaber)
        {
            if (PieceCollection.TryGetPiece(AssetTypeDefinition.CustomSaber, out var model))
            {
                customsaber = model as CustomSaberModel;
                return true;
            }
            customsaber = null;
            return false;
        }
    }

    public class SaberSet : IFactorySerializable, ILoadingTask
    {
        public SaberModel LeftSaber { get; set; }
        public SaberModel RightSaber { get; set; }
        public bool IsEmpty => LeftSaber.IsEmpty && RightSaber.IsEmpty;
        private readonly MainAssetStore _mainAssetStore;
        private readonly PresetSaveManager _presetSaveManager;
        private SaberSet(
            [Inject(Id = ESaberSlot.Left)] SaberModel leftSaber,
            [Inject(Id = ESaberSlot.Right)] SaberModel rightSaber,
            PresetSaveManager presetSaveManager,
            MainAssetStore mainAssetStore)
        {
            _presetSaveManager = presetSaveManager;
            _mainAssetStore = mainAssetStore;
            LeftSaber = leftSaber;
            RightSaber = rightSaber;
            _ = Load();
        }

        public async Task FromJson(JObject obj, Serializer serializer)
        {
            try
            {
                if (obj.TryGetValue(nameof(LeftSaber), out var leftToken))
                {
                    await LeftSaber.FromJson((JObject)leftToken, serializer);
                }
                if (obj.TryGetValue(nameof(RightSaber), out var rightToken))
                {
                    await RightSaber.FromJson((JObject)rightToken, serializer);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.Error("[SaberSet] Saber loading error:\n" + e);
                throw;
            }
        }

        public async Task<JToken> ToJson(Serializer serializer)
        {
            var obj = new JObject
            {
                { nameof(LeftSaber), await LeftSaber.ToJson(serializer) },
                { nameof(RightSaber), await RightSaber.ToJson(serializer) }
            };
            return obj;
        }

        public Task CurrentTask { get; private set; }
        public void SetModelComposition(ModelComposition modelComposition)
        {
            LeftSaber.SetModelComposition(modelComposition);
            RightSaber.SetModelComposition(modelComposition);
        }

        public async Task SetSaber(string saberName)
        {
            var metaData = _mainAssetStore.GetAllMetaData(AssetTypeDefinition.CustomSaber);
            var saber = metaData.FirstOrDefault(x => x.ListName == saberName);
            await SetSaber(saber);
        }

        public async Task SetSaber(PreloadMetaData preloadData)
        {
            if (preloadData == null)
            {
                return;
            }
            SetModelComposition(await _mainAssetStore.GetCompositionByMeta(preloadData));
        }

        public async Task Save(string fileName = "default")
        {
            await _presetSaveManager.SaveSaber(this, fileName);
        }

        public async Task Load(string fileName = "default")
        {
            CurrentTask = _presetSaveManager.LoadSaber(this, fileName);
            await CurrentTask;
            CurrentTask = null;
        }

        public void Sync(SaberModel fromModel)
        {
            fromModel.Sync();
            var otherSaber = fromModel == LeftSaber ? RightSaber : LeftSaber;
            otherSaber.SaberWidth = fromModel.SaberWidth;
            otherSaber.SaberLength = fromModel.SaberLength;
        }
    }

    public class TrailModel : IFactorySerializable
    {
        public int OriginalLength { get; private set; }
        [MapSerialize] public bool ClampTexture;
        [MapSerialize] public bool Flip;
        [MapSerialize] public int Length;
        [JsonIgnore] public MaterialDescriptor Material;
        public TextureWrapMode? OriginalTextureWrapMode;
        [MapSerialize] public string TrailOrigin;
        [JsonIgnore] public List<CustomTrail> TrailOriginTrails;
        [MapSerialize] public Vector3 TrailPosOffset;
        [MapSerialize] public float Whitestep;
        [MapSerialize] public float Width;
        public TrailModel(
            Vector3 trailPosOffset,
            float width,
            int length,
            MaterialDescriptor material,
            float whitestep,
            TextureWrapMode? originalTextureWrapMode,
            string trailOrigin = "")
        {
            TrailPosOffset = trailPosOffset;
            Width = width;
            Length = length;
            OriginalLength = length;
            Material = material;
            Whitestep = whitestep;
            OriginalTextureWrapMode = originalTextureWrapMode;
            TrailOrigin = trailOrigin;
        }

        public TrailModel()
        { }
        public async Task FromJson(JObject obj, Serializer serializer)
        {
            obj.Populate(this);
            if (!string.IsNullOrEmpty(TrailOrigin))
            {
                await LoadFromTrailOrigin(serializer, TrailOrigin);
            }
            if (obj.SelectToken("Material") is { } materialToken)
            {
                if (Material is null)
                {
                    Material = new MaterialDescriptor(null);
                }
                await serializer.LoadMaterial((JObject)materialToken, Material.Material);
            }
        }

        public Task<JToken> ToJson(Serializer serializer)
        {
            var obj = JObject.FromObject(this, Serializer.JsonSerializer);
            if (Material.IsValid)
            {
                obj.Add("Material", serializer.SerializeMaterial(Material.Material));
            }
            return Task.FromResult<JToken>(obj);
        }

        public void CopyFrom(TrailModel other)
        {
            TrailPosOffset = other.TrailPosOffset;
            Width = other.Width;
            Length = other.Length;
            Material ??= new MaterialDescriptor(null);
            Material.Material = new Material(other.Material.Material);
            Whitestep = other.Whitestep;
            TrailOrigin = other.TrailOrigin;
            ClampTexture = other.ClampTexture;
            Flip = other.Flip;
            OriginalLength = other.OriginalLength;
        }

        private async Task LoadFromTrailOrigin(Serializer serializer, JToken trailOrigin)
        {
            var comp = await serializer.LoadPiece(trailOrigin);
            if (!(comp?.GetLeft() is CustomSaberModel cs))
            {
                return;
            }
            var originTrailModel = cs.GrabTrail(true);
            if (originTrailModel == null)
            {
                return;
            }
            Material ??= new MaterialDescriptor(null);
            Material.Material = originTrailModel.Material.Material;
            TrailOriginTrails = SaberHelpers.GetTrails(cs.Prefab);
        }
    }

    public abstract class PiecePropertyBlock : IFactorySerializable
    {
        public TransformPropertyBlock TransformProperty;
        protected PiecePropertyBlock()
        {
            TransformProperty = new TransformPropertyBlock();
        }

        public virtual async Task FromJson(JObject obj, Serializer serializer)
        {
            await TransformProperty.FromJson((JObject)obj[nameof(TransformProperty)], serializer);
        }

        public virtual async Task<JToken> ToJson(Serializer serializer)
        {
            var obj = new JObject
            {
                { nameof(TransformProperty), await TransformProperty.ToJson(serializer) }
            };
            return obj;
        }

        public abstract void SyncFrom(PiecePropertyBlock otherBlock);
    }

    public class TransformPropertyBlock : IFactorySerializable
    {
        public float Width { get; set; } = 1;
        public float Offset { get; set; }
        public float Rotation { get; set; }
        public Task FromJson(JObject obj, Serializer serializer)
        {
            obj.Populate(this);
            return Task.CompletedTask;
        }

        public Task<JToken> ToJson(Serializer serializer)
        {
            return Task.FromResult<JToken>(JObject.FromObject(this, Serializer.JsonSerializer));
        }
    }

    internal class CustomSaberPropertyBlock : PiecePropertyBlock
    {
        public override void SyncFrom(PiecePropertyBlock otherBlock)
        {
            var block = (CustomSaberPropertyBlock)otherBlock;
            TransformProperty.Width = block.TransformProperty.Width;
            TransformProperty.Rotation = -block.TransformProperty.Rotation;
            TransformProperty.Offset = block.TransformProperty.Offset;
        }
    }
}