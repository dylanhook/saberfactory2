using System;
using System.Collections.Generic;
using SaberFactory2.Models;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberFactory2.Misc
{
    internal struct TrailInitData
    {
        public int TrailLength;
        public float Whitestep;
        public Color TrailColor;
        public int Granularity;
        public int SamplingFrequency;
        public float SamplingStepMultiplier;
    }

    internal class SaberTrail : MonoBehaviour
    {
        public static bool CapFps;

        public Color Color = Color.white;
        public int Granularity = 60;
        public int SamplingFrequency = 20;
        public Material Material;
        public Transform PointEnd;
        public Transform PointStart;
        public string SortingLayerName;
        public int SortingOrder;
        public int TrailLength = 30;
        public float Whitestep;
        public bool RelativeMode { get; set; }
        public PlayerTransforms PlayerTransforms { get; set; }

        private bool _inited;
        private int _frameNum;
        private float _time;
        private const int SkipFirstFrames = 4;

        private Mesh _mesh;
        private GameObject _meshObj;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private Snapshot[] _snapshots;
        private float[] _snapshotDistances;
        private float _totalDistance;
        private int _headIdx;
        private int _snapshotCount;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _indices = new List<int>();

        private struct Snapshot
        {
            public Vector3 PointStart;
            public Vector3 PointEnd;
            public Vector3 Pos => (PointStart + PointEnd) * 0.5f;
            public Vector3 Normal => PointEnd - PointStart;
        }

        private Vector3 GetPlayerOffset() => PlayerTransforms ? PlayerTransforms.transform.position : Vector3.zero;

        private void OnEnable()
        {
            if (_meshObj) _meshObj.SetActive(true);
        }

        private void OnDisable()
        {
            if (_meshObj) _meshObj.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
            if (_meshObj) Destroy(_meshObj);
        }

        public void Setup(TrailInitData initData, Transform pointStart, Transform pointEnd, Material material, bool editor)
        {
            PointStart = pointStart;
            PointEnd = pointEnd;
            Material = material;
            Granularity = initData.Granularity;
            TrailLength = initData.TrailLength;
            Whitestep = initData.Whitestep;
            SamplingFrequency = initData.SamplingFrequency;

            gameObject.layer = 12;

            if (Material != null)
            {
                try
                {
                    AssetBundleLoadingTools.Utilities.ShaderRepair.FixShadersOnMaterials(new List<Material> { Material });
                }
                catch (Exception ex)
                {
                    Plugin.Logger.Warn($"Failed to repair trail material shader: {ex.Message}");
                }
            }

            _meshObj = new GameObject("SaberTrailMesh");
            _meshObj.layer = gameObject.layer;
            _meshObj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            _meshFilter = _meshObj.AddComponent<MeshFilter>();
            _meshRenderer = _meshObj.AddComponent<MeshRenderer>();

            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.sharedMaterial = Material;
            _meshRenderer.sortingLayerName = SortingLayerName;
            _meshRenderer.sortingOrder = editor ? 3 : SortingOrder;

            _meshFilter.sharedMesh = _mesh = new Mesh { bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f)) };
            _mesh.MarkDynamic();

            _snapshots = new Snapshot[TrailLength];
            _snapshotDistances = new float[TrailLength];
            _headIdx = 0;
            _snapshotCount = 0;

            InitArrays();

            _inited = true;
        }

        private void InitArrays()
        {
            int vertCount = Granularity * 3;
            int indexCount = (Granularity - 1) * 12;

            _vertices.Capacity = vertCount;
            _uvs.Capacity = vertCount;
            _colors.Capacity = vertCount;
            _indices.Capacity = indexCount;

            for (int i = 0; i < vertCount; i++)
            {
                _vertices.Add(Vector3.zero);
                _uvs.Add(Vector2.zero);
                _colors.Add(Color.white);
            }

            for (int i = 0; i < Granularity - 1; i++)
            {
                int baseIdx = i * 3;
                int nextBaseIdx = (i + 1) * 3;

                _indices.Add(nextBaseIdx);
                _indices.Add(nextBaseIdx + 1);
                _indices.Add(baseIdx);

                _indices.Add(nextBaseIdx + 1);
                _indices.Add(baseIdx + 1);
                _indices.Add(baseIdx);

                _indices.Add(nextBaseIdx + 1);
                _indices.Add(nextBaseIdx + 2);
                _indices.Add(baseIdx + 1);

                _indices.Add(nextBaseIdx + 2);
                _indices.Add(baseIdx + 2);
                _indices.Add(baseIdx + 1);
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_indices, 0);
        }

        private void InitBuffer()
        {
            var snap = new Snapshot { PointStart = PointStart.position, PointEnd = PointEnd.position };
            if (RelativeMode)
            {
                var offset = GetPlayerOffset();
                snap.PointStart -= offset;
                snap.PointEnd -= offset;
            }
            for (int i = 0; i < TrailLength; i++)
                _snapshots[i] = snap;

            _snapshotCount = TrailLength;
            _headIdx = 0;
        }

        private void LateUpdate()
        {
            if (!_inited) return;

            if (CapFps)
            {
                _time += Time.deltaTime;
                if (_time < 1f / 90f) return;
                _time = 0;
            }

            _frameNum++;
            if (_frameNum == SkipFirstFrames + 1)
            {
                if (_meshObj) _meshObj.SetActive(true);
                InitBuffer();
            }
            else if (_frameNum < SkipFirstFrames + 1)
            {
                return;
            }

            _snapshots[_headIdx] = new Snapshot
            {
                PointStart = PointStart.position,
                PointEnd = PointEnd.position
            };

            if (RelativeMode)
            {
                var offset = GetPlayerOffset();
                _snapshots[_headIdx].PointStart -= offset;
                _snapshots[_headIdx].PointEnd -= offset;
            }

            RecalculateDistances();
            UpdateMesh();

            _headIdx = (_headIdx - 1 + TrailLength) % TrailLength;
            _snapshots[_headIdx] = _snapshots[(_headIdx + 1) % TrailLength];
            if (_snapshotCount < TrailLength) _snapshotCount++;
        }

        private void RecalculateDistances()
        {
            _snapshotDistances[0] = 0;
            _totalDistance = 0;

            for (int i = 1; i < _snapshotCount; i++)
            {
                var prev = GetSnapshot(i - 1);
                var cur = GetSnapshot(i);
                float dist = (cur.Pos - prev.Pos).magnitude;
                _totalDistance += dist;
                _snapshotDistances[i] = _totalDistance;
            }
        }

        private Snapshot GetSnapshot(int index)
        {
            index = Mathf.Clamp(index, 0, _snapshotCount - 1);
            return _snapshots[(_headIdx + index) % TrailLength];
        }

        private int GetIndexFromDistance(float t, out float localT)
        {
            float targetDist = t * _totalDistance;
            for (int i = 0; i < _snapshotCount; i++)
            {
                if (_snapshotDistances[i] >= targetDist)
                {
                    if (i == 0)
                    {
                        localT = 0;
                        return 0;
                    }
                    float prevDist = _snapshotDistances[i - 1];
                    float segLen = _snapshotDistances[i] - prevDist;
                    localT = segLen > 0 ? (targetDist - prevDist) / segLen : 0;
                    return i - 1;
                }
            }
            localT = 0;
            return _snapshotCount - 1;
        }

        private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private Vector3 Interpolate(float t, bool isNormal)
        {
            if (_snapshotCount < 2) return isNormal ? GetSnapshot(0).Normal : GetSnapshot(0).Pos;

            t = Mathf.Clamp01(t);
            int idx = GetIndexFromDistance(t, out float localT);

            var s0 = GetSnapshot(idx - 1);
            var s1 = GetSnapshot(idx);
            var s2 = GetSnapshot(idx + 1);
            var s3 = GetSnapshot(idx + 2);

            if (isNormal)
                return CatmullRom(s0.Normal, s1.Normal, s2.Normal, s3.Normal, localT);
            else
                return CatmullRom(s0.Pos, s1.Pos, s2.Pos, s3.Pos, localT);
        }

        private void UpdateMesh()
        {
            var offset = GetPlayerOffset();
            float trailWidth = (PointStart.position - PointEnd.position).magnitude;
            float halfWidth = trailWidth * 0.5f;

            for (int i = 0; i < Granularity; i++)
            {
                float t = (float)i / (Granularity > 1 ? Granularity - 1 : 1);
                var pos = Interpolate(t, false);
                if (RelativeMode) pos += offset;

                var up = Interpolate(t, true).normalized;

                var pos0 = pos + up * halfWidth;
                var pos1 = pos - up * halfWidth;

                var c = Color;
                if (Whitestep > 0 && t < Whitestep)
                {
                    c = Color.LerpUnclamped(Color.white, Color, t / Whitestep);
                }

                int baseIdx = i * 3;

                _vertices[baseIdx] = pos0;
                _colors[baseIdx] = c;
                _uvs[baseIdx] = new Vector2(0f, t);

                _vertices[baseIdx + 1] = pos;
                _colors[baseIdx + 1] = c;
                _uvs[baseIdx + 1] = new Vector2(0.5f, t);

                _vertices[baseIdx + 2] = pos1;
                _colors[baseIdx + 2] = c;
                _uvs[baseIdx + 2] = new Vector2(1f, t);
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.RecalculateBounds();
        }

        public void SetMaterialBlock(MaterialPropertyBlock block)
        {
            if (_meshRenderer) _meshRenderer.SetPropertyBlock(block);
        }
    }
}