using System;
using System.Collections.Generic;
using System.Linq;
using CustomSaber;
using HarmonyLib;
using IPA.Utilities;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using SaberFactory2.Installers;
using SaberFactory2.Misc;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;
using UnityEngine;

namespace SaberFactory2.Instances.Trail
{
    internal class CustomSaberTrailHandler
    {
        public SaberFactory2.Misc.SaberTrail TrailInstance { get; protected set; }
        private readonly CustomTrail _customTrail;
        private bool _canColorMaterial;
        private PlayerTransforms _playerTransforms;
        public CustomSaberTrailHandler(GameObject gameobject, CustomTrail customTrail, PlayerTransforms playerTransforms)
        {
            TrailInstance = gameobject.AddComponent<SaberFactory2.Misc.SaberTrail>();
            _customTrail = customTrail;
            _playerTransforms = playerTransforms;
        }

        public void CreateTrail(TrailConfig trailConfig, bool editor)
        {
            if (_customTrail.PointStart == null || _customTrail.PointEnd == null)
            {
                return;
            }
            if (_customTrail.Length < 1)
            {
                return;
            }
            var trailInitData = new TrailInitData
            {
                TrailColor = Color.white,
                TrailLength = _customTrail.Length,
                Whitestep = 0,
                Granularity = 60,
                SamplingFrequency = 80,
                SamplingStepMultiplier = 1
            };
            TrailInstance.Setup(
                trailInitData,
                _customTrail.PointStart,
                _customTrail.PointEnd,
                _customTrail.TrailMaterial,
                editor
            );
            TrailInstance.PlayerTransforms = _playerTransforms;
            if (!trailConfig.OnlyUseVertexColor)
            {
                _canColorMaterial = MaterialHelpers.IsMaterialColorable(_customTrail.TrailMaterial);
            }
        }

        public void SetRelativeMode(bool active)
        {
            TrailInstance.RelativeMode = active;
        }

        public void DestroyTrail()
        {
            TrailInstance.TryDestroyImmediate();
        }

        public void SetColor(Color color)
        {
            if (TrailInstance != null)
            {
                TrailInstance.Color = color;
            }
            if (_canColorMaterial)
            {
                TrailInstance.SetMaterialBlock(MaterialHelpers.ColorBlock(color));
            }
        }
    }

    internal class InstanceTrailData
    {
        public TrailModel TrailModel { get; }
        public Transform PointStart { get; }
        public Transform PointEnd { get; }
        public MaterialDescriptor Material => TrailModel.Material;
        public int Length
        {
            get => TrailModel.Length;
            set => SetLength(value);
        }

        public float WhiteStep
        {
            get => TrailModel.Whitestep;
            set => SetWhitestep(value);
        }

        public float Offset
        {
            get => TrailModel.TrailPosOffset.z;
            set
            {
                var pos = TrailModel.TrailPosOffset;
                pos.z = value;
                TrailModel.TrailPosOffset = pos;
                PointEnd.localPosition = pos;
                Width = Width;
            }
        }

        public float Width
        {
            get => Mathf.Abs(PointEnd.parent.localPosition.z - PointStart.localPosition.z);
            set => SetWidth(value);
        }

        public bool ClampTexture
        {
            get => TrailModel.ClampTexture;
            set => SetClampTexture(value);
        }

        public bool Flip
        {
            get => TrailModel.Flip;
            set => TrailModel.Flip = value;
        }

        public bool HasMultipleTrails => SecondaryTrails.Count > 0;
        public List<SecondaryTrailHandler> SecondaryTrails { get; }
        private readonly bool _isTrailReversed;
        public InstanceTrailData(TrailModel trailModel, Transform pointStart, Transform pointEnd, bool isTrailReversed,
            List<CustomTrail> secondaryTrails = null)
        {
            TrailModel = trailModel;
            PointStart = pointStart;
            var newEnd = new GameObject("PointEnd").transform;
            newEnd.SetParent(pointEnd, false);
            PointEnd = newEnd;
            _isTrailReversed = isTrailReversed;
            SecondaryTrails = secondaryTrails?.Select(x => new SecondaryTrailHandler(x, trailModel.OriginalLength)).ToList() ??
                              new List<SecondaryTrailHandler>();
            SecondaryTrails.Do(x => x.UpdateLength(trailModel.Length));
            Init(trailModel);
        }

        private void Init(TrailModel trailModel)
        {
            SetClampTexture(trailModel.ClampTexture);
            SetWidth(trailModel.Width);
            Offset = Offset;
        }

        public void SetWidth(float width)
        {
            TrailModel.Width = width;
            var pos = PointStart.localPosition;
            pos.z = PointEnd.parent.localPosition.z - width;
            PointStart.localPosition = pos;
        }

        public void SetLength(int length)
        {
            TrailModel.Length = length;
            SecondaryTrails.Do(x => x.UpdateLength(length));
        }

        public void SetWhitestep(float whitestep)
        {
            TrailModel.Whitestep = whitestep;
        }

        public void SetClampTexture(bool shouldClampTexture)
        {
            TrailModel.ClampTexture = shouldClampTexture;
            if (TrailModel.OriginalTextureWrapMode.HasValue &&
                TrailModel.Material.IsValid &&
                TrailModel.Material.Material.TryGetMainTexture(out var tex))
            {
                tex.wrapMode = shouldClampTexture ? TextureWrapMode.Clamp : TrailModel.OriginalTextureWrapMode.GetValueOrDefault();
            }
        }

        public void RevertMaterialForCustomSaber(CustomSaberModel saber)
        {
            TrailModel.Material.Revert();
            var saberTrail = saber.StoreAsset.Prefab.GetComponent<CustomTrail>();
            if (saberTrail == null)
            {
                return;
            }
            saberTrail.TrailMaterial = TrailModel.Material.Material;
        }

        public void RevertMaterial()
        {
            TrailModel.Material.Revert();
        }

        public (Transform start, Transform end) GetPoints()
        {
            var pointStart = _isTrailReversed ? PointEnd : PointStart;
            var pointEnd = _isTrailReversed ? PointStart : PointEnd;
            return (Flip ? pointEnd : pointStart, Flip ? pointStart : pointEnd);
        }

        internal class SecondaryTrailHandler
        {
            public CustomTrail Trail { get; }
            private readonly int _lengthOffset;
            public SecondaryTrailHandler(CustomTrail trail, int mainTrailLength)
            {
                Trail = trail;
                _lengthOffset = mainTrailLength - trail.Length;
            }

            public void UpdateLength(int newMainTrailLength)
            {
                Trail.Length = Mathf.Max(0, newMainTrailLength - _lengthOffset);
            }
        }
    }

    internal interface ITrailHandler
    {
        public void CreateTrail(TrailConfig config, bool editor);
        public void DestroyTrail(bool immediate = false);
        public void SetTrailData(InstanceTrailData instanceTrailData);
        public void SetColor(Color color);
    }

    internal class MainTrailHandler : ITrailHandler
    {
        public SaberFactory2.Misc.SaberTrail TrailInstance { get; protected set; }
        protected InstanceTrailData InstanceTrailData;
        private readonly global::SaberTrail _backupTrail;
        private readonly PlayerTransforms _playerTransforms;
        private readonly SaberSettableSettings _saberSettableSettings;
        private bool _canColorMaterial;
        public MainTrailHandler(GameObject gameobject, PlayerTransforms playerTransforms, SaberSettableSettings saberSettableSettings)
        {
            TrailInstance = gameobject.AddComponent<SaberFactory2.Misc.SaberTrail>();
            _playerTransforms = playerTransforms;
            _saberSettableSettings = saberSettableSettings;
        }

        public MainTrailHandler(GameObject gameobject, global::SaberTrail backupTrail, PlayerTransforms playerTransforms, SaberSettableSettings saberSettableSettings)
            : this(gameobject, playerTransforms, saberSettableSettings)
        {
            _backupTrail = backupTrail;
        }

        public void CreateTrail(TrailConfig trailConfig, bool editor)
        {
            if (InstanceTrailData is null)
            {
                if (_backupTrail is null)
                {
                    return;
                }
                var trailStart = TrailInstance.gameObject.CreateGameObject("Trail StartNew");
                var trailEnd = TrailInstance.gameObject.CreateGameObject("TrailEnd");
                trailEnd.transform.localPosition = new Vector3(0, 0, 1);
                var trailRenderer = _backupTrail.GetField<SaberTrailRenderer, global::SaberTrail>("_trailRendererPrefab");
                var material = trailRenderer.GetField<MeshRenderer, SaberTrailRenderer>("_meshRenderer").material;
                var trailInitDataVanilla = new TrailInitData
                {
                    TrailColor = Color.white,
                    TrailLength = 15,
                    Whitestep = 0.02f,
                    Granularity = trailConfig.Granularity
                };
                TrailInstance.Setup(trailInitDataVanilla, trailStart.transform, trailEnd.transform, material, editor);
                TrailInstance.PlayerTransforms = _playerTransforms;
                InitSettableSettings();
                return;
            }
            if (InstanceTrailData.Length < 1)
            {
                return;
            }
            var trailInitData = new TrailInitData
            {
                TrailColor = Color.white,
                TrailLength = InstanceTrailData.Length,
                Whitestep = InstanceTrailData.WhiteStep,
                Granularity = trailConfig.Granularity,
                SamplingFrequency = trailConfig.SamplingFrequency
            };
            var (pointStart, pointEnd) = InstanceTrailData.GetPoints();
            if (pointStart == null || pointEnd == null)
            {
                return;
            }
            TrailInstance.Setup(
                trailInitData,
                pointStart,
                pointEnd,
                InstanceTrailData.Material.Material,
                editor
            );
            TrailInstance.PlayerTransforms = _playerTransforms;
            InitSettableSettings();
            if (!trailConfig.OnlyUseVertexColor)
            {
                _canColorMaterial = MaterialHelpers.IsMaterialColorable(InstanceTrailData.Material.Material);
            }
        }

        private void UpdateRelativeMode()
        {
            TrailInstance.RelativeMode = _saberSettableSettings.RelativeTrailMode.Value;
        }

        private void InitSettableSettings()
        {
            if (_saberSettableSettings == null) return;
            UpdateRelativeMode();
            _saberSettableSettings.RelativeTrailMode.ValueChanged += UpdateRelativeMode;
        }

        private void UnInitSettableSettings()
        {
            if (_saberSettableSettings == null) return;
            _saberSettableSettings.RelativeTrailMode.ValueChanged -= UpdateRelativeMode;
        }

        public void DestroyTrail(bool immediate = false)
        {
            UnInitSettableSettings();
            if (immediate)
            {
                TrailInstance.TryDestroyImmediate();
            }
            else
            {
                TrailInstance.TryDestroy();
            }
        }

        public void SetTrailData(InstanceTrailData instanceTrailData)
        {
            InstanceTrailData = instanceTrailData;
        }

        public void SetColor(Color color)
        {
            if (TrailInstance != null)
            {
                TrailInstance.Color = color;
            }
            if (_canColorMaterial)
            {
                TrailInstance.SetMaterialBlock(MaterialHelpers.ColorBlock(color));
            }
        }
    }

    public class SaberInstanceList
    {
        private readonly List<WeakReference<SaberInstance>> _list = new List<WeakReference<SaberInstance>>();
        public PlayerTransforms PlayerTransforms { get; set; }
        public int Count => _list.Count;
        public void Add(SaberInstance saberInstance)
        {
            _list.Add(new WeakReference<SaberInstance>(saberInstance));
            saberInstance.PlayerTransforms = PlayerTransforms;
        }

        public void Remove(SaberInstance saberInstance)
        {
            _list.Remove(_list.Find(wr => wr.TryGetTarget(out var si) && si == saberInstance));
        }

        public void Clear()
        {
            _list.Clear();
        }

        public List<SaberInstance> GetAll()
        {
            var newList = new List<SaberInstance>();
            for (var i = _list.Count - 1; i >= 0; i--)
            {
                if (_list[i].TryGetTarget(out var saberInstance))
                {
                    newList.Add(saberInstance);
                }
                else
                {
                    _list.RemoveAt(i);
                }
            }
            return newList;
        }
    }
}