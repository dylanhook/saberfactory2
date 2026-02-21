using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomSaber;
using HMUI;
using Newtonsoft.Json.Linq;
using SaberFactory2.Gizmo;
using SaberFactory2.Serialization;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberFactory2.Helpers
{
    public static class BaseGameTypeExtension
    {
        public static bool IsLeft(this SaberType saberType)
        {
            return saberType == SaberType.SaberA;
        }

        public static GameObject CreateGameObject(this Transform parent, string name, bool worldPositionStays = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays);
            return go;
        }

        public static GameObject CreateGameObject(this GameObject parent, string name, bool worldPositionStays = false)
        {
            return parent.transform.CreateGameObject(name, worldPositionStays);
        }

        public static void TryDestroy(this Object obj)
        {
            if (!obj)
            {
                return;
            }
            Object.Destroy(obj);
        }

        public static void TryDestroyImmediate(this Object obj)
        {
            if (!obj)
            {
                return;
            }
            Object.DestroyImmediate(obj);
        }

        public static RectTransform AsRectTransform(this Transform transform)
        {
            return transform as RectTransform;
        }

        public static void SetCurve(GameObject root, float radius)
        {
            foreach (var curvedCanvasSettingse in root.GetComponentsInChildren<CurvedCanvasSettings>())
            {
                curvedCanvasSettingse.SetRadius(radius);
            }
        }

        public static float GetTransfomWidth(Transform t1, Transform t2)
        {
            if (t1 == null || t2 == null)
            {
                return 0;
            }
            return Mathf.Abs(t1.localPosition.z - t2.localPosition.z);
        }

        public static float GetWidth(this CustomTrail trail)
        {
            if (trail == null)
            {
                return 0;
            }
            return GetTransfomWidth(trail.PointEnd, trail.PointStart);
        }

        public static void SetMaterial(this Renderer renderer, int index, Material material)
        {
            var mats = renderer.sharedMaterials;
            mats[index] = material;
            renderer.materials = mats;
        }

        public static Vector2 With(this Vector2 vec, float? x, float? y)
        {
            return new Vector2(x ?? vec.x, y ?? vec.y);
        }

        public static Vector3 With(this Vector3 vec, float? x, float? y, float? z)
        {
            return new Vector3(x ?? vec.x, y ?? vec.y, z ?? vec.z);
        }

        public static Color ColorFromArray(float[] arr)
        {
            return new Color(arr[0], arr[1], arr[2], arr[3]);
        }

        public static float[] ToArray(this Color clr)
        {
            return new[] { clr.r, clr.g, clr.b, clr.a };
        }

        public static float[] ToArray(this Vector2 vec)
        {
            return new[] { vec.x, vec.y };
        }

        public static float[] ToArray(this Vector3 vec)
        {
            return new[] { vec.x, vec.y, vec.z };
        }

        public static float[] ToArray(this Vector4 vec)
        {
            return new[] { vec.x, vec.y, vec.z, vec.w };
        }

        public static Vector2 ToVec2(this float[] fl)
        {
            return new Vector2(fl[0], fl[1]);
        }

        public static Vector3 ToVec3(this float[] fl)
        {
            return new Vector3(fl[0], fl[1], fl[2]);
        }

        public static Vector4 ToVec4(this float[] fl)
        {
            return new Vector4(fl[0], fl[1], fl[2], fl[3]);
        }
    }

    internal class GizmoAssets : IInitializable
    {
        private readonly EmbeddedAssetLoader _assetLoader;
        private Material _gizmoMaterial;
        public Mesh PositionMesh { get; private set; }
        public Mesh RotationMesh { get; private set; }
        public Mesh ScalingMesh { get; private set; }
        public GizmoAssets(EmbeddedAssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        public void Activate()
        {
            GizmoDrawer.Activate(_gizmoMaterial);
        }

        public void Deactivate()
        {
            GizmoDrawer.Deactivate();
        }

        public async void Initialize()
        {
            GizmoDrawer.Init();
            var shader = await _assetLoader.LoadAsset<Shader>("sh_sfglow_doublesided.shader");
            _gizmoMaterial = new Material(shader);
            PositionMesh = await LoadMesh("PositionGizmo");
            RotationMesh = await LoadMesh("RotationGizmo");
            ScalingMesh = await LoadMesh("ScalingGizmo");
            PositionGizmo.PositionMesh = PositionMesh;
            RotationGizmo.RotationMesh = RotationMesh;
            ScaleGizmo.ScalingMesh = ScalingMesh;
            GizmoDrawer.Init();
        }

        private async Task<Mesh> LoadMesh(string name)
        {
            return (await _assetLoader.LoadAsset<GameObject>(name)).GetComponentInChildren<MeshFilter>().sharedMesh;
        }
    }

    public class GizmoDrawer : MonoBehaviour
    {
        public readonly struct DrawCommand
        {
            private readonly Mesh _mesh;
            private readonly Matrix4x4 _matrix;
            private readonly Color _color;
            public DrawCommand(Mesh mesh, Color color, Matrix4x4 matrix)
            {
                _mesh = mesh;
                _matrix = matrix;
                _color = color;
            }

            public void Draw(Material m)
            {
                m.color = _color;
                m.SetFloat(GlowId, _color.a);
                m.SetPass(0);
                Graphics.DrawMeshNow(_mesh, _matrix);
            }
        }

        private static readonly List<GizmoDrawer> _drawers = new List<GizmoDrawer>();
        private static Dictionary<PrimitiveType, Mesh> _meshes;
        private static bool _initd;
        private static bool _active;
        public static void Init()
        {
            if (_initd)
            {
                return;
            }
            _meshes = new Dictionary<PrimitiveType, Mesh>();
            foreach (var pt in (PrimitiveType[])Enum.GetValues(typeof(PrimitiveType)))
            {
                var go = GameObject.CreatePrimitive(pt);
                var m = go.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(go);
                _meshes.Add(pt, m);
            }
            _initd = true;
        }

        public static void Activate(Material material)
        {
            if (_active || !_initd)
            {
                return;
            }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                AddDrawer(cam, material);
            }
            _active = true;
        }

        public static void Deactivate()
        {
            if (!_active)
            {
                return;
            }
            foreach (var drawer in _drawers)
            {
                Destroy(drawer);
            }
            _active = false;
        }

        private static GizmoDrawer AddDrawer(Camera cam, Material material)
        {
            if (cam == null)
            {
                return null;
            }
            var drawer = cam.gameObject.AddComponent<GizmoDrawer>();
            drawer.InitDrawer(material);
            _drawers.Add(drawer);
            return drawer;
        }

        public static void Draw(DrawCommand command)
        {
            if (!active || !Application.isPlaying)
            {
                return;
            }
            if (_drawers.Count == 0)
            {
                return;
            }
            foreach (var d in _drawers)
            {
                d._cmds.Add(command);
            }
        }

        public static void Draw(Mesh mesh, Color color, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Draw(new DrawCommand(mesh, color, Matrix4x4.TRS(position, rotation, scale)));
        }

        private static void Draw(PrimitiveType primitiveType, Color color, Matrix4x4 matrix)
        {
            Draw(new DrawCommand(_meshes[primitiveType], color, matrix));
        }

        public static bool active = true;
        public static void DrawSphere(Vector3 position, float radius, Color color)
        {
            Draw(
                PrimitiveType.Sphere, color,
                Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * radius)
            );
        }

        public static void DrawBox(Vector3 position, Quaternion rotation, Vector3 size, Color color)
        {
            Draw(PrimitiveType.Cube, color, Matrix4x4.TRS(position, rotation, size));
        }

        private Material _mat;
        private readonly List<DrawCommand> _cmds = new List<DrawCommand>();
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        public void InitDrawer(Material material)
        {
            _mat = material;
        }

        private void OnDestroy()
        {
            _drawers.Remove(this);
        }

        private void OnPostRender()
        {
            if (_mat == null)
            {
                _cmds.Clear();
                return;
            }
            foreach (var c in _cmds)
            {
                c.Draw(_mat);
            }
            _cmds.Clear();
        }
    }

    internal static class MaterialAttributes
    {
        public static readonly string SfNoPreview = "SFNoPreview";
        public static readonly string HideInSf = "HideInSF";
    }

    internal static class MaterialHelpers
    {
        public static bool TryGetTexture(this Material material, string propName, out Texture tex)
        {
            tex = null;
            if (!material.HasProperty(propName))
            {
                return false;
            }
            tex = material.GetTexture(propName);
            return tex != null;
        }

        public static bool TryGetTexture(this Material material, int propId, out Texture tex)
        {
            tex = null;
            if (!material.HasProperty(propId))
            {
                return false;
            }
            tex = material.GetTexture(propId);
            return tex != null;
        }

        public static bool TryGetMainTexture(this Material material, out Texture tex)
        {
            return TryGetTexture(material, MaterialProperties.MainTexture, out tex);
        }

        public static bool TryGetFloat(this Material material, string propName, out float val)
        {
            val = 0;
            if (!material.HasProperty(propName))
            {
                return false;
            }
            val = material.GetFloat(propName);
            return true;
        }

        public static bool TryGetFloat(this Material material, int propId, out float val)
        {
            val = 0;
            if (!material.HasProperty(propId))
            {
                return false;
            }
            val = material.GetFloat(propId);
            return true;
        }

        public static void SetMainColor(this Material material, Color color)
        {
            if (material.HasProperty(MaterialProperties.MainColor))
            {
                material.SetColor(MaterialProperties.MainColor, color);
            }
        }

        public static bool IsMaterialColorable(Material material)
        {
            if (material is null || !material.HasProperty(MaterialProperties.MainColor))
            {
                return false;
            }
            if (material.TryGetFloat(MaterialProperties.CustomColors, out var val))
            {
                if (val > 0)
                {
                    return true;
                }
            }
            else if (material.TryGetFloat(MaterialProperties.Glow, out val) && val > 0)
            {
                return true;
            }
            else if (material.TryGetFloat(MaterialProperties.Bloom, out val) && val > 0)
            {
                return true;
            }
            return false;
        }

        public static MaterialPropertyBlock ColorBlock(Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(MaterialProperties.MainColor, color);
            return block;
        }

        public static bool HasCustomColorsEnabled(this Material material)
        {
            return material.TryGetFloat(MaterialProperties.CustomColors, out var customColors) && customColors > 0.5f;
        }

        public static IEnumerable<(object, int, ShaderPropertyType)> GetProperties(this Material material, string ignoredAttribute = null)
        {
            var shader = material.shader;
            var propCount = shader.GetPropertyCount();
            for (var i = 0; i < propCount; i++)
            {
                if (!string.IsNullOrEmpty(ignoredAttribute) &&
                    shader.GetPropertyAttributes(i).Contains(ignoredAttribute))
                {
                    continue;
                }
                var nameId = shader.GetPropertyNameId(i);
                var type = shader.GetPropertyType(i);
                yield return (material.GetProperty(nameId, type), nameId, type);
            }
        }

        public static object GetProperty(this Material material, int nameId, ShaderPropertyType type)
        {
            switch (type)
            {
                case ShaderPropertyType.Color:
                    return material.GetColor(nameId);
                case ShaderPropertyType.Vector:
                    return material.GetVector(nameId);
                case ShaderPropertyType.Float:
                    return material.GetFloat(nameId);
                case ShaderPropertyType.Range:
                    return material.GetFloat(nameId);
                case ShaderPropertyType.Texture:
                    return material.GetTexture(nameId);
            }
            return null;
        }

        public static void SetProperty(this Material material, int nameId, object obj)
        {
            var type = obj.GetType();
            if (type == typeof(Color))
            {
                material.SetColor(nameId, (Color)obj);
                return;
            }
            if (type == typeof(Vector2))
            {
                material.SetVector(nameId, (Vector2)obj);
                return;
            }
            if (type == typeof(Vector3))
            {
                material.SetVector(nameId, (Vector3)obj);
                return;
            }
            if (type == typeof(Vector4))
            {
                material.SetColor(nameId, (Vector4)obj);
                return;
            }
            if (type == typeof(float))
            {
                material.SetFloat(nameId, (float)obj);
                return;
            }
            if (type == typeof(Texture))
            {
                material.SetTexture(nameId, (Texture)obj);
            }
        }

        public static void SetProperty(this Material material, int nameId, object obj, ShaderPropertyType type)
        {
            switch (type)
            {
                case ShaderPropertyType.Color:
                    material.SetColor(nameId, (Color)obj);
                    return;
                case ShaderPropertyType.Vector:
                    var objType = obj.GetType();
                    if (objType == typeof(Vector2))
                    {
                        material.SetVector(nameId, (Vector2)obj);
                        return;
                    }
                    if (objType == typeof(Vector3))
                    {
                        material.SetVector(nameId, (Vector3)obj);
                        return;
                    }
                    if (objType == typeof(Vector4))
                    {
                        material.SetColor(nameId, (Vector4)obj);
                        return;
                    }
                    return;
                case ShaderPropertyType.Float:
                    material.SetFloat(nameId, (float)obj);
                    return;
                case ShaderPropertyType.Range:
                    material.SetFloat(nameId, (float)obj);
                    return;
                case ShaderPropertyType.Texture:
                    material.SetTexture(nameId, (Texture)obj);
                    return;
            }
        }
    }

    internal static class MaterialProperties
    {
        public static readonly int MainColor = Shader.PropertyToID("_Color");
        public static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        public static readonly int CustomColors = Shader.PropertyToID("_CustomColors");
        public static readonly int Glow = Shader.PropertyToID("_Glow");
        public static readonly int Bloom = Shader.PropertyToID("_Bloom");
        public static readonly int UserColorLeft = Shader.PropertyToID("_UserColorLeft");
        public static readonly int UserColorRight = Shader.PropertyToID("_UserColorRight");
    }

    internal class ShaderPropertyCache
    {
        public ShaderPropertyInfo this[Shader shader] => Get(shader);
        private readonly Dictionary<string, ShaderPropertyInfo> _shaderPropertyInfos =
            new Dictionary<string, ShaderPropertyInfo>();
        public ShaderPropertyInfo Get(Shader shader)
        {
            if (_shaderPropertyInfos.TryGetValue(shader.name, out var info))
            {
                return info;
            }
            info = new ShaderPropertyInfo(shader);
            _shaderPropertyInfos.Add(shader.name, info);
            return info;
        }
    }

    internal class ShaderPropertyInfo
    {
        public readonly List<ShaderColor> Colors = new List<ShaderColor>();
        public readonly List<ShaderFloat> Floats = new List<ShaderFloat>();
        public readonly List<ShaderRange> Ranges = new List<ShaderRange>();
        public readonly List<ShaderTexture> Textures = new List<ShaderTexture>();
        public readonly List<ShaderVector> Vectors = new List<ShaderVector>();
        public ShaderPropertyInfo(Shader shader)
        {
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Range:
                        Ranges.Add(new ShaderRange(shader, i));
                        break;
                    case ShaderPropertyType.Float:
                        Floats.Add(new ShaderFloat(shader, i));
                        break;
                    case ShaderPropertyType.Vector:
                        Vectors.Add(new ShaderVector(shader, i));
                        break;
                    case ShaderPropertyType.Texture:
                        Textures.Add(new ShaderTexture(shader, i));
                        break;
                    case ShaderPropertyType.Color:
                        Colors.Add(new ShaderColor(shader, i));
                        break;
                }
            }
        }

        public List<BaseProperty> GetAll()
        {
            var result = new List<BaseProperty>();
            result.AddRange(Ranges);
            result.AddRange(Floats);
            result.AddRange(Vectors);
            result.AddRange(Textures);
            result.AddRange(Colors);
            return result;
        }

        public BaseProperty FindFromAll(string name)
        {
            if (Find(Ranges, name, out var prop))
            {
                return prop;
            }
            if (Find(Floats, name, out prop))
            {
                return prop;
            }
            if (Find(Vectors, name, out prop))
            {
                return prop;
            }
            if (Find(Textures, name, out prop))
            {
                return prop;
            }
            if (Find(Colors, name, out prop))
            {
                return prop;
            }
            return null;
        }

        public bool Find<T>(List<T> list, string name, out BaseProperty prop) where T : BaseProperty
        {
            foreach (var p in list)
            {
                if (p.Name == name)
                {
                    prop = p;
                    return true;
                }
            }
            prop = null;
            return false;
        }

        internal class ShaderRange : ShaderFloat
        {
            public float Min { get; }
            public float Max { get; }
            public ShaderRange(Shader shader, int idx) : base(shader, idx)
            {
                var range = shader.GetPropertyRangeLimits(idx);
                Min = range.x;
                Max = range.y;
            }
        }

        internal class ShaderFloat : BaseProperty
        {
            public ShaderFloat(Shader shader, int idx) : base(shader, idx)
            { }
            public override object GetValue(Material mat)
            {
                return mat.GetFloat(PropId);
            }

            public override void SetValue(Material mat, object value)
            {
                mat.SetFloat(PropId, (float)value);
            }

            public override void FromJson(JToken token, Material mat, params object[] args)
            {
                SetValue(mat, token.ToObject<float>());
            }
        }

        internal class ShaderVector : BaseProperty
        {
            public ShaderVector(Shader shader, int idx) : base(shader, idx)
            { }
            public override object GetValue(Material mat)
            {
                return mat.GetVector(PropId);
            }

            public override void SetValue(Material mat, object value)
            {
                mat.SetVector(PropId, (Vector4)value);
            }

            public override void FromJson(JToken token, Material mat, params object[] args)
            {
                SetValue(mat, token.ToObject<Vector4>(Serializer.JsonSerializer));
            }

            public override JToken ToJson(Material mat)
            {
                return JArray.FromObject(GetValue(mat), Serializer.JsonSerializer);
            }
        }

        internal class ShaderTexture : BaseProperty
        {
            public ShaderTexture(Shader shader, int idx) : base(shader, idx)
            { }
            public override object GetValue(Material mat)
            {
                return mat.GetTexture(PropId);
            }

            public override void SetValue(Material mat, object value)
            {
                mat.SetTexture(PropId, (Texture)value);
            }

            public override void FromJson(JToken token, Material mat, params object[] args)
            {
            }

            public override JToken ToJson(Material mat)
            {
                return null;
            }
        }

        internal class ShaderColor : BaseProperty
        {
            public ShaderColor(Shader shader, int idx) : base(shader, idx)
            { }
            public override object GetValue(Material mat)
            {
                return mat.GetColor(PropId);
            }

            public override void SetValue(Material mat, object value)
            {
                mat.SetColor(PropId, (Color)value);
            }

            public override void FromJson(JToken token, Material mat, params object[] args)
            {
                SetValue(mat, token.ToObject<Color>(Serializer.JsonSerializer));
            }

            public override JToken ToJson(Material mat)
            {
                return JArray.FromObject(GetValue(mat), Serializer.JsonSerializer);
            }
        }

        internal abstract class BaseProperty
        {
            public string Name { get; }
            public string Description { get; }
            public int PropId { get; }
            public ShaderPropertyType Type { get; }
            public string[] Attributes { get; }
            protected BaseProperty(Shader shader, int idx)
            {
                Name = shader.GetPropertyName(idx);
                Description = shader.GetPropertyDescription(idx);
                PropId = shader.GetPropertyNameId(idx);
                Type = shader.GetPropertyType(idx);
                Attributes = shader.GetPropertyAttributes(idx);
            }

            public bool HasAttribute(string name)
            {
                return Attributes != null && Attributes.Contains(name);
            }

            public abstract object GetValue(Material mat);
            public abstract void SetValue(Material mat, object value);
            public abstract void FromJson(JToken token, Material mat, params object[] args);
            public virtual JToken ToJson(Material mat)
            {
                return new JValue(GetValue(mat));
            }
        }
    }
}