using System;
using System.Linq;
using System.Threading.Tasks;
using CustomSaber;
using Newtonsoft.Json.Linq;
using SaberFactory2.Configuration;
using SaberFactory2.DataStore;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Serialization;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.Models.CustomSaber
{
    public class CustomSaberModel : BasePieceModel
    {
        public override Type InstanceType { get; protected set; } = typeof(CustomSaberInstance);
        public TrailModel TrailModel
        {
            get
            {
                if (_trailModel == null)
                {
                    var trailModel = GrabTrail(false);
                    if (trailModel == null)
                    {
                        _hasTrail = false;
                        return null;
                    }
                    _trailModel = trailModel;
                }
                return _trailModel;
            }
            set => _trailModel = value;
        }

        public bool HasTrail
        {
            get
            {
                _hasTrail ??= CheckTrail();
                return _hasTrail.Value;
            }
        }

        public SaberDescriptor SaberDescriptor;
        private bool _didReparentTrail;
        private bool? _hasTrail;
        private TrailModel _trailModel;
        [Inject] private readonly PluginDirectories _pluginDirectories = null;
        public CustomSaberModel(StoreAsset storeAsset) : base(storeAsset)
        {
            PropertyBlock = new CustomSaberPropertyBlock();
        }

        public override void OnLazyInit()
        {
            if (!HasTrail)
            {
                return;
            }
            var trailModel = TrailModel;
            var path = _pluginDirectories.Cache.GetFile(StoreAsset.NameWithoutExtension + ".trail").FullName;
            var trail = QuickSave.LoadObject<TrailProportions>(path);
            if (trail == null)
            {
                return;
            }
            trailModel.Length = trail.Length;
            trailModel.Width = trail.Width;
        }

        public override void SaveAdditionalData()
        {
            if (!HasTrail)
            {
                return;
            }
            var trailModel = TrailModel;
            var path = _pluginDirectories.Cache.GetFile(StoreAsset.NameWithoutExtension + ".trail").FullName;
            var trail = new TrailProportions
            {
                Length = trailModel.Length,
                Width = trailModel.Width
            };
            QuickSave.SaveObject(trail, path);
        }

        public override ModelMetaData GetMetaData()
        {
            return new ModelMetaData(SaberDescriptor.SaberName, SaberDescriptor.AuthorName,
                SaberDescriptor.CoverImage, false);
        }

        public override void SyncFrom(BasePieceModel otherModel)
        {
            base.SyncFrom(otherModel);
            var otherCs = (CustomSaberModel)otherModel;
            if (otherCs.HasTrail || otherCs.TrailModel is { })
            {
                TrailModel ??= new TrailModel();
                TrailModel.TrailOriginTrails = otherCs.TrailModel.TrailOriginTrails;
                var originalMaterial = TrailModel.Material?.Material;
                TrailModel.CopyFrom(otherCs.TrailModel);
                var otherMat = TrailModel.Material.Material;
                if (originalMaterial != null && (string.IsNullOrWhiteSpace(TrailModel.TrailOrigin) ||
                                    originalMaterial.shader.name == otherMat.shader.name))
                {
                    foreach (var prop in otherMat.GetProperties(MaterialAttributes.HideInSf))
                    {
                        originalMaterial.SetProperty(prop.Item2, prop.Item1, prop.Item3);
                    }
                    TrailModel.Material.Material = originalMaterial;
                }
                else
                {
                    originalMaterial.TryDestroyImmediate();
                }
            }
        }

        public TrailModel GrabTrail(bool addTrailOrigin)
        {
            var trail = SaberHelpers.GetTrails(Prefab).FirstOrDefault();
            if (trail == null)
            {
                return null;
            }
            if (!trail.TrailMaterial)
            {
                return null;
            }
            TextureWrapMode wrapMode = default;
            if (trail.TrailMaterial != null && trail.TrailMaterial.TryGetMainTexture(out var tex))
            {
                wrapMode = tex.wrapMode;
            }
            FixTrailParents();
            return new TrailModel(
                Vector3.zero,
                trail.GetWidth(),
                trail.Length,
                new MaterialDescriptor(trail.TrailMaterial),
                0, wrapMode,
                addTrailOrigin ? StoreAsset.RelativePath : null);
        }

        private bool CheckTrail()
        {
            if (!Prefab)
            {
                return false;
            }
            if (Prefab.GetComponent<CustomTrail>() is { } trail && trail.TrailMaterial)
            {
                return true;
            }
            return false;
        }

        public void ResetTrail()
        {
            TrailModel = GrabTrail(false);
        }

        public void FixTrailParents()
        {
            if (_didReparentTrail)
            {
                return;
            }
            _didReparentTrail = true;
            var trail = Prefab.GetComponent<CustomTrail>();
            if (trail is null)
            {
                return;
            }
            trail.PointStart.SetParent(Prefab.transform, true);
            trail.PointEnd.SetParent(Prefab.transform, true);
        }

        public override async Task FromJson(JObject obj, Serializer serializer)
        {
            await base.FromJson(obj, serializer);
            var trailModelToken = obj[nameof(TrailModel)];
            if (trailModelToken != null)
            {
                if (TrailModel == null)
                {
                    TrailModel = new TrailModel();
                }
                await TrailModel.FromJson((JObject)trailModelToken, serializer);
            }
        }

        public override async Task<JToken> ToJson(Serializer serializer)
        {
            var obj = (JObject)await base.ToJson(serializer);
            if (TrailModel != null)
            {
                obj.Add(nameof(TrailModel), await TrailModel.ToJson(serializer));
            }
            return obj;
        }

        internal class Factory : PlaceholderFactory<StoreAsset, CustomSaberModel>
        { }
        internal class TrailProportions
        {
            public int Length;
            public float Width;
        }
    }

    internal class CustomSaberModelLoader : IStoreAssetParser
    {
        private readonly PluginConfig _config;
        private readonly CustomSaberModel.Factory _factory;
        public CustomSaberModelLoader(CustomSaberModel.Factory factory, PluginConfig config)
        {
            _factory = factory;
            _config = config;
        }

        public ModelComposition GetComposition(StoreAsset storeAsset)
        {
            var (leftSaber, rightSaber) = GetSabers(storeAsset.Prefab.transform);
            if (rightSaber == null)
            {
                var newParent = new GameObject("RightSaber").transform;
                newParent.parent = storeAsset.Prefab.transform;
                rightSaber = Object.Instantiate(leftSaber, newParent, false);
                rightSaber.transform.position = Vector3.zero;
                rightSaber.transform.localScale = new Vector3(-1, 1, 1);
                rightSaber.name = "RightSaberMirror";
                rightSaber = newParent.gameObject;
                rightSaber.SetActive(false);
            }
            var storeAssetLeft = new StoreAsset(storeAsset.RelativePath, leftSaber, storeAsset.AssetBundle);
            var storeAssetRight = new StoreAsset(storeAsset.RelativePath, rightSaber, storeAsset.AssetBundle);
            var modelLeft = _factory.Create(storeAssetLeft);
            var modelRight = _factory.Create(storeAssetRight);
            modelLeft.SaberDescriptor = modelRight.SaberDescriptor = storeAsset.Prefab.GetComponent<SaberDescriptor>();
            modelLeft.SaberSlot = ESaberSlot.Left;
            modelRight.SaberSlot = ESaberSlot.Right;
            var composition = new ModelComposition(AssetTypeDefinition.CustomSaber, modelLeft, modelRight, storeAsset.Prefab);
            composition.SetFavorite(_config.IsFavorite(storeAsset.RelativePath));
            return composition;
        }

        private (GameObject leftSaber, GameObject rightSaber) GetSabers(Transform root)
        {
            GameObject left = null;
            GameObject right = null;

            FindRecursive(root);

            void FindRecursive(Transform current)
            {
                if (left != null && right != null) return;

                if (current.name.Equals("LeftSaber", StringComparison.OrdinalIgnoreCase))
                    left = current.gameObject;
                else if (current.name.Equals("RightSaber", StringComparison.OrdinalIgnoreCase))
                    right = current.gameObject;

                for (int i = 0; i < current.childCount; i++)
                {
                    FindRecursive(current.GetChild(i));
                }
            }

            return (left, right);
        }
    }
}