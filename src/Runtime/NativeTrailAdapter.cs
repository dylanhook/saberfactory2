using System;
using CustomSaber;
using SaberFactory2.Configuration;
using SaberFactory2.Helpers;
using UnityEngine;

namespace SaberFactory2.Instances.Trail
{
    internal class NativeTrailAdapter
    {
        public TrailRenderer TrailInstance { get; protected set; }
        private readonly CustomTrail _customTrail;
        private bool _canColorMaterial;

        public NativeTrailAdapter(GameObject gameObject, CustomTrail customTrail, PlayerTransforms playerTransforms)
        {
            TrailInstance = customTrail.PointEnd.gameObject.GetComponent<TrailRenderer>();
            if (TrailInstance == null)
            {
                TrailInstance = customTrail.PointEnd.gameObject.AddComponent<TrailRenderer>();
            }

            _customTrail = customTrail;
        }

        public void CreateTrail(TrailConfig trailConfig, bool editor)
        {
            if (_customTrail.PointStart == null || _customTrail.PointEnd == null) return;
            if (_customTrail.Length < 1) return;

            TrailInstance.time = _customTrail.Length / 60f;

            TrailInstance.minVertexDistance = 0.05f;

            TrailInstance.material = _customTrail.TrailMaterial;

            float width = Vector3.Distance(_customTrail.PointStart.position, _customTrail.PointEnd.position);
            TrailInstance.startWidth = width;
            TrailInstance.endWidth = width;

            _customTrail.PointEnd.LookAt(_customTrail.PointStart);

            if (!trailConfig.OnlyUseVertexColor)
            {
                _canColorMaterial = MaterialHelpers.IsMaterialColorable(_customTrail.TrailMaterial);
            }
        }

        public void SetRelativeMode(bool active)
        {
            TrailInstance.emitting = !active;
        }

        public void DestroyTrail()
        {
            if (TrailInstance != null)
            {
                UnityEngine.Object.Destroy(TrailInstance);
            }
        }

        public void SetColor(Color color)
        {
            if (TrailInstance != null)
            {
                TrailInstance.startColor = color;
                TrailInstance.endColor = new Color(color.r, color.g, color.b, 0f);
            }
            if (_canColorMaterial && TrailInstance != null && TrailInstance.material != null)
            {
                TrailInstance.material.SetColor("_Color", color);
            }
        }
    }
}