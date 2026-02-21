using System;
using System.Collections.Generic;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using SaberFactory2.Instances;
using SaberFactory2.Instances.Trail;
using SaberFactory2.Models;
using SaberFactory2.Modifiers;
using SiraUtil.Affinity;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

namespace SaberFactory2.Misc
{
    internal class PSManager
    {
        public readonly ParticleSystem ParticleSystem;
        public readonly ParticleSystemRenderer Renderer;
        private ParticleSystem.MainModule _main;
        public Material Material
        {
            get => Renderer.sharedMaterial;
            set => Renderer.sharedMaterial = value;
        }

        public Color Color
        {
            get => _main.startColor.color;
            set => _main.startColor = value;
        }

        public ParticleSystem.MainModule Main => _main;
        public PSManager(ParticleSystem particleSystem)
        {
            ParticleSystem = particleSystem;
            _main = particleSystem.main;
            Renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        }
    }

    internal class SaberClashCustomizer : IInitializable, IAffinity, ICustomizer
    {
        public bool ClashEnabled { get; set; }
        private SaberClashEffect _currentClashEffect;
        private PSManager _sparkle;
        private PSManager _glow;
        internal SaberClashCustomizer(EmbeddedAssetLoader assetLoader)
        {
        }

        public void SetSaber(SaberInstance saber)
        {
            var info = saber.Model.PieceCollection[AssetTypeDefinition.CustomSaber].ModelComposition
                .AdditionalInstanceHandler.GetComponent<SFClashEffect>();
            if (!info)
            {
                return;
            }
            if (info.Material)
            {
                _glow.Material = info.Material;
            }
        }

        [AffinityPostfix]
        [AffinityPatch(typeof(SaberClashEffect), nameof(SaberClashEffect.Start))]
        protected void Setup(SaberClashEffect __instance, ParticleSystem ____sparkleParticleSystem, ParticleSystem ____glowParticleSystem)
        {
            _currentClashEffect = __instance;
            _sparkle = new PSManager(____sparkleParticleSystem);
            _glow = new PSManager(____glowParticleSystem);
        }

        public void Initialize()
        {
        }
    }

    internal interface ICustomizer
    {
        public void SetSaber(SaberInstance saber);
    }
}