using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using SaberFactory2.Configuration;
using SaberFactory2.DataStore;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Misc;
using SaberFactory2.Models;
using SiraUtil.Interfaces;
using SiraUtil.Logging;
using SiraUtil.Sabers;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.Game
{
    internal static class CJDHandler
    {
        private static bool _initialized;
        private static ICJDHandler _cjdHandler;
        public static bool GetField(this BeatmapData beatmap, string field, out object obj)
        {
            if (!_initialized)
            {
                Init();
            }
            if (_cjdHandler == null)
            {
                obj = null;
                return false;
            }
            return _cjdHandler.GetField(beatmap, field, out obj);
        }

        private static void Init()
        {
            _initialized = true;
            var pluginMetadata = IPA.Loader.PluginManager.GetPluginFromId("CustomJSONData");
            if (pluginMetadata == null)
            {
                return;
            }
            if (pluginMetadata.HVersion.Major > 1)
            {
                _cjdHandler = new CJD2Handler();
            }
            else
            {
                _cjdHandler = new CJD1Handler();
            }
            _cjdHandler.Setup(pluginMetadata.Assembly);
        }

        internal interface ICJDHandler
        {
            public bool GetField(BeatmapData beatmap, string field, out object obj);
            public void Setup(Assembly assembly);
        }

        internal class CJD1Handler : ICJDHandler
        {
            private MethodInfo _atMethod;
            private PropertyInfo _levelCustomDataProp;
            private bool _isValid;
            bool ICJDHandler.GetField(BeatmapData beatmap, string field, out object obj)
            {
                try
                {
                    if (!_isValid)
                    {
                        obj = null;
                        return false;
                    }
                    var customData = _levelCustomDataProp.GetValue(beatmap);
                    obj = _atMethod.Invoke(null, new object[] { customData, field });
                    return obj != null;
                }
                catch
                {
                    obj = null;
                    return false;
                }
            }

            public void Setup(Assembly assembly)
            {
                var customBeatmapDataType = assembly.GetType("CustomJSONData.CustomBeatmap.CustomBeatmapData");
                var treesType = assembly.GetType("CustomJSONData.Trees");
                if (customBeatmapDataType == null || treesType == null) return;
                _atMethod = treesType.GetMethod("at", BindingFlags.Static | BindingFlags.Public);
                _levelCustomDataProp = customBeatmapDataType.GetProperty("levelCustomData", BindingFlags.Public | BindingFlags.Instance);

                if (_atMethod != null && _levelCustomDataProp != null)
                {
                    _isValid = true;
                }
            }
        }

        internal class CJD2Handler : ICJDHandler
        {
            private PropertyInfo _levelCustomDataProp;
            private bool _isValid;
            bool ICJDHandler.GetField(BeatmapData beatmap, string field, out object obj)
            {
                try
                {
                    if (!_isValid)
                    {
                        obj = null;
                        return false;
                    }
                    var dict = _levelCustomDataProp.GetValue(beatmap) as Dictionary<string, object>;
                    if (dict == null || !dict.TryGetValue(field, out obj))
                    {
                        obj = null;
                        return false;
                    }
                    return true;
                }
                catch
                {
                    obj = null;
                    return false;
                }
            }

            public void Setup(Assembly assembly)
            {
                var customBeatmapDataType = assembly.GetType("CustomJSONData.CustomBeatmap.CustomBeatmapData");
                if (customBeatmapDataType == null) return;
                _levelCustomDataProp = customBeatmapDataType.GetProperty("levelCustomData", BindingFlags.Public | BindingFlags.Instance);

                if (_levelCustomDataProp != null)
                {
                    _isValid = true;
                }
            }
        }
    }

    internal class EventPlayer : IDisposable
    {
        [Inject] private readonly BeatmapObjectManager _beatmapObjectManager = null;
        [Inject] private readonly GameEnergyCounter _energyCounter = null;
        [InjectOptional] private readonly ObstacleSaberSparkleEffectManager _obstacleSaberSparkleEffectManager = null;
        [Inject] private readonly PluginConfig _pluginConfig = null;
        [Inject] private readonly IScoreController _scoreController = null;
        [Inject] private readonly IComboController _comboController = null;
        [Inject] private readonly RelativeScoreAndImmediateRankCounter _scoreCounter = null;
        [Inject] private readonly IReadonlyBeatmapData _beatmapData = null;
        [InjectOptional] private readonly GameCoreSceneSetupData _gameCoreSceneSetupData = null;
        [Inject] private readonly SiraLog _logger = null;
        public bool IsActive;
        private float? _lastNoteTime;
        private SaberType _saberType;
        private float _prevScore;

        private Action _onLevelEnded;
        private Action _onComboBreak;
        private Action _onLevelFail;
        private Action _multiplierUp;
        private Action _onSlice;
        private Action _saberStartColliding;
        private Action _saberStopColliding;
        private Action _onLevelStart;
        private Action<int> _onComboChanged;
        private Action<float> _onAccuracyChanged;

        public void SetPartEventList(List<PartEvents> partEventsList, SaberType saberType)
        {
            _saberType = saberType;

            _onLevelEnded = null;
            _onComboBreak = null;
            _onLevelFail = null;
            _multiplierUp = null;
            _onSlice = null;
            _saberStartColliding = null;
            _saberStopColliding = null;
            _onLevelStart = null;
            _onComboChanged = null;
            _onAccuracyChanged = null;

            foreach (var partEvent in partEventsList)
            {
                if (partEvent.OnLevelEnded != null) _onLevelEnded += partEvent.OnLevelEnded.Invoke;
                if (partEvent.OnComboBreak != null) _onComboBreak += partEvent.OnComboBreak.Invoke;
                if (partEvent.OnLevelFail != null) _onLevelFail += partEvent.OnLevelFail.Invoke;
                if (partEvent.MultiplierUp != null) _multiplierUp += partEvent.MultiplierUp.Invoke;
                if (partEvent.OnSlice != null) _onSlice += partEvent.OnSlice.Invoke;
                if (partEvent.SaberStartColliding != null) _saberStartColliding += partEvent.SaberStartColliding.Invoke;
                if (partEvent.SaberStopColliding != null) _saberStopColliding += partEvent.SaberStopColliding.Invoke;
                if (partEvent.OnLevelStart != null) _onLevelStart += partEvent.OnLevelStart.Invoke;
                if (partEvent.OnComboChanged != null) _onComboChanged += partEvent.OnComboChanged.Invoke;
                if (partEvent.OnAccuracyChanged != null) _onAccuracyChanged += partEvent.OnAccuracyChanged.Invoke;
            }
            if (!_pluginConfig.EnableEvents)
            {
                return;
            }
            if (_gameCoreSceneSetupData == null)
            {
                return;
            }
            IsActive = true;
            _lastNoteTime = _beatmapData.CastChecked<BeatmapData>()?.GetLastNoteTime();
            if (!_lastNoteTime.HasValue)
            {
                _logger.Warn("Couldn't get last note time. Certain level end events won't work");
            }
            _beatmapObjectManager.noteWasCutEvent += OnNoteCut;
            _beatmapObjectManager.noteWasMissedEvent += OnNoteMiss;
            if (_obstacleSaberSparkleEffectManager)
            {
                _obstacleSaberSparkleEffectManager.sparkleEffectDidStartEvent += SaberStartCollide;
                _obstacleSaberSparkleEffectManager.sparkleEffectDidEndEvent += SaberEndCollide;
            }
            _energyCounter.gameEnergyDidReach0Event += OnLevelFail;
            _scoreController.multiplierDidChangeEvent += MultiplayerDidChange;
            _scoreCounter.relativeScoreOrImmediateRankDidChangeEvent += ScoreChanged;
            _comboController.comboDidChangeEvent += OnComboDidChangeEvent;
            _onLevelStart?.Invoke();
        }

        public void Dispose()
        {
            _beatmapObjectManager.noteWasCutEvent -= OnNoteCut;
            _beatmapObjectManager.noteWasMissedEvent -= OnNoteMiss;
            if (_obstacleSaberSparkleEffectManager)
            {
                _obstacleSaberSparkleEffectManager.sparkleEffectDidStartEvent -= SaberStartCollide;
                _obstacleSaberSparkleEffectManager.sparkleEffectDidEndEvent -= SaberEndCollide;
            }
            _energyCounter.gameEnergyDidReach0Event -= OnLevelFail;
            _scoreController.multiplierDidChangeEvent -= MultiplayerDidChange;
            _scoreCounter.relativeScoreOrImmediateRankDidChangeEvent -= ScoreChanged;
            _comboController.comboDidChangeEvent -= OnComboDidChangeEvent;
        }

        private void OnLevelFail()
        {
            _onLevelFail?.Invoke();
        }
        #region Events
        private void ScoreChanged()
        {
            var score = _scoreCounter.relativeScore;
            if (Math.Abs(_prevScore - score) >= 0.001f)
            {
                _onAccuracyChanged?.Invoke(score);
                _prevScore = score;
            }
        }

        private void OnComboDidChangeEvent(int combo)
        {
            _onComboChanged?.Invoke(combo);
        }

        private void OnNoteCut(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            if (!_lastNoteTime.HasValue)
            {
                return;
            }
            if (!noteCutInfo.allIsOK)
            {
                _onComboBreak?.Invoke();
            }
            else
            {
                if (_saberType == noteCutInfo.saberType)
                {
                    _onSlice?.Invoke();
                }
            }
            if (Mathf.Approximately(noteController.noteData.time, _lastNoteTime.Value))
            {
                _lastNoteTime = 0;
                _onLevelEnded?.Invoke();
            }
        }

        private void OnNoteMiss(NoteController noteController)
        {
            if (!_lastNoteTime.HasValue)
            {
                return;
            }
            if (noteController.noteData.colorType != ColorType.None)
            {
                _onComboBreak?.Invoke();
            }
            if (Mathf.Approximately(noteController.noteData.time, _lastNoteTime.Value))
            {
                _lastNoteTime = 0;
                _onLevelEnded?.Invoke();
            }
        }

        private void SaberEndCollide(SaberType saberType)
        {
            if (saberType == _saberType)
            {
                _saberStopColliding?.Invoke();
            }
        }

        private void SaberStartCollide(SaberType saberType)
        {
            if (saberType == _saberType)
            {
                _saberStartColliding?.Invoke();
            }
        }

        private void MultiplayerDidChange(int multiplier, float progress)
        {
            if (multiplier > 1 && progress < 0.1f)
            {
                _multiplierUp?.Invoke();
            }
        }
        #endregion
    }

    internal class GameSaberSetup : IInitializable, IDisposable
    {
        public Task SetupTask { get; private set; }
        private readonly BeatmapData _beatmapData;
        private readonly PluginConfig _config;
        private readonly MainAssetStore _mainAssetStore;
        private readonly SaberModel _oldLeftSaberModel;
        private readonly SaberModel _oldRightSaberModel;
        private readonly RandomUtil _randomUtil;
        private readonly SaberSet _saberSet;
        private GameSaberSetup(PluginConfig config, SaberSet saberSet, MainAssetStore mainAssetStore,
            IReadonlyBeatmapData beatmap, RandomUtil randomUtil)
        {
            _config = config;
            _saberSet = saberSet;
            _mainAssetStore = mainAssetStore;
            _beatmapData = beatmap.CastChecked<BeatmapData>();
            _randomUtil = randomUtil;
            _oldLeftSaberModel = _saberSet.LeftSaber;
            _oldRightSaberModel = _saberSet.RightSaber;
            Setup();
        }

        public void Dispose()
        {
            _saberSet.LeftSaber = _oldLeftSaberModel;
            _saberSet.RightSaber = _oldRightSaberModel;
        }

        public void Initialize()
        { }
        public async void Setup()
        {
            SetupTask = SetupInternal();
            await SetupTask;
        }

        private async Task SetupInternal()
        {
            if (_config.RandomSaber)
            {
                await RandomSaber();
                return;
            }
            if (!_config.OverrideSongSaber)
            {
                await SetupSongSaber();
            }
        }

        private async Task RandomSaber()
        {
            if (
                _config.AssetType == EAssetTypeConfiguration.CustomSaber ||
                _config.AssetType == EAssetTypeConfiguration.None)
            {
                var randomComp = _randomUtil.RandomizeFrom(_mainAssetStore.GetAllMetaData(AssetTypeDefinition.CustomSaber).ToList());
                _saberSet.SetModelComposition(await _mainAssetStore.GetCompositionByMeta(randomComp));
            }
        }

        private async Task SetupSongSaber()
        {
            try
            {
                if (_beatmapData == null)
                {
                    return;
                }
                if (!_beatmapData.GetField("_customSaber", out var songSaber))
                {
                    return;
                }
                var metaData = _mainAssetStore.GetAllMetaData(AssetTypeDefinition.CustomSaber);
                var saber = metaData.FirstOrDefault(x => x.ListName == songSaber.ToString());
                if (saber == null)
                {
                    return;
                }
                _saberSet.LeftSaber = new SaberModel(ESaberSlot.Left);
                _saberSet.RightSaber = new SaberModel(ESaberSlot.Right);
                await _saberSet.SetSaber(saber);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }
    }

    internal class SaberMovementTester : IInitializable
    {
        private readonly AudioTimeSyncController _audioController;
        private readonly InitData _initData;
        private readonly SiraSaberFactory _saberFactory;
        private SiraSaber _saber;
        private SaberMovementTester(InitData initData, SiraSaberFactory saberFactory, AudioTimeSyncController audioController)
        {
            _initData = initData;
            _saberFactory = saberFactory;
            _audioController = audioController;
        }

        public async void Initialize()
        {
            await Task.Delay(1000);
            _audioController.Pause();
            if (!_initData.CreateTestingSaber)
            {
                return;
            }
            var saberA = CreateSaber(SaberType.SaberA, new Vector3(0, 0.6f, 0), Quaternion.Euler(90, 0, 0));
            var saberB = CreateSaber(SaberType.SaberB, new Vector3(0, 0.6f, 0), Quaternion.Euler(90, 0, 0));
            var runner = new GameObject("SF_CoroutineRunner").AddComponent<CoroutineRunner>();
            runner.StartCoroutine(GroundRoundAnimationCoroutine(-0.2f, saberA));
            runner.StartCoroutine(GroundRoundAnimationCoroutine(0.2f, saberB));

        }

        public Transform CreateSaber(SaberType saberType, Vector3 pos, Quaternion rot)
        {
            var parent = new GameObject("SaberTester_" + saberType).transform;
            parent.localPosition = new Vector3(0, 0.6f, 0);
            parent.localRotation = Quaternion.Euler(90, 0, 0);
            _saber = _saberFactory.Spawn(saberType);
            _saber.transform.SetParent(parent, false);
            return parent;
        }

        private IEnumerator GroundRoundAnimationCoroutine(float xPos, Transform t)
        {
            var currentPos = t.localPosition.z;
            while (true)
            {
                while (currentPos < 1)
                {
                    currentPos += 0.02f;
                    t.localPosition = new Vector3(xPos, 0.6f, currentPos);
                    yield return null;
                }
                while (currentPos > 0)
                {
                    currentPos -= 0.02f;
                    t.localPosition = new Vector3(xPos, 0.6f, currentPos);
                    yield return null;
                }
            }
        }

        internal class InitData
        {
            public bool CreateTestingSaber;
        }
    }

    internal class SfSaberModelController : SaberModelController, IColorable, IPreSaberModelInit
    {
        [InjectOptional] private readonly EventPlayer _eventPlayer = null;
        [Inject] private readonly GameSaberSetup _gameSaberSetup = null;
        [Inject] private readonly SiraLog _logger = null;
        [Inject] private readonly SaberInstance.Factory _saberInstanceFactory = null;
        [Inject] private readonly SaberSet _saberSet = null;
        [Inject] private readonly List<ICustomizer> _customizers = null;
        private Color? _saberColor;
        private SaberInstance _saberInstance;
        public void SetColor(Color color)
        {
            _saberColor = color;
            _saberInstance?.SetColor(color);
        }

        public Color Color
        {
            get => _saberColor.GetValueOrDefault();
            set => SetColor(value);
        }

        public bool PreInit(Transform parent, Saber saber)
        {
            InitAsync(parent, saber);
            return false;
        }

        private async void InitAsync(Transform parent, Saber saber)
        {
            await _gameSaberSetup.SetupTask;
            transform.SetParent(parent, false);
            var saberModel = saber.saberType == SaberType.SaberA ? _saberSet.LeftSaber : _saberSet.RightSaber;
            _saberInstance = _saberInstanceFactory.Create(saberModel);
            if (saber.saberType == SaberType.SaberA)
            {
                _customizers.Do(x => x.SetSaber(_saberInstance));
            }
            _saberInstance.SetParent(transform);
            _saberInstance.CreateTrail(false, _saberTrail);
            SetColor(_saberColor ?? _colorManager.ColorForSaberType(_saberInstance.Model.SaberSlot.ToSaberType()));
            _eventPlayer?.SetPartEventList(_saberInstance.Events, saber.saberType);
            _logger.Info("Instantiated Saber");
        }
    }
}