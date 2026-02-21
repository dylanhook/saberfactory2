using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CustomSaber;
using IPA.Utilities;
using SaberFactory2.DataStore;
using SaberFactory2.Instances;
using SaberFactory2.Models;
using SiraUtil.Logging;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace SaberFactory2.Helpers
{
    public static class AnimationHelper
    {
        public static async Task AsyncAnimation(float speedDivision, CancellationToken cancelToken, Action<float> transitionAnimation)
        {
            var elapsedTime = 0.0f;
            var cutoff = speedDivision - 0.1f;
            while (elapsedTime < cutoff)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }
                var num = Easing.OutQuart(elapsedTime / speedDivision);
                transitionAnimation?.Invoke(num);
                elapsedTime += Time.deltaTime;
                await Task.Yield();
            }
            transitionAnimation?.Invoke(1f);
        }
    }

    public class AnimationManager
    {
        private readonly Action<float> _inAnimation;
        private readonly Action<float> _outAnimation;
        private readonly float _speedDivision;
        private CancellationTokenSource _cancellationTokenSource;
        public AnimationManager(float speedDivision, Action<float> inAnimation, Action<float> outAnimation)
        {
            _speedDivision = speedDivision;
            _inAnimation = inAnimation;
            _outAnimation = outAnimation;
        }

        public async Task AnimateIn()
        {
            CancelCurrent();
            await AnimationHelper.AsyncAnimation(_speedDivision, _cancellationTokenSource.Token, _inAnimation);
        }

        public async Task AnimateOut()
        {
            CancelCurrent();
            await AnimationHelper.AsyncAnimation(_speedDivision, _cancellationTokenSource.Token, _outAnimation);
        }

        private void CancelCurrent()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            _cancellationTokenSource = new CancellationTokenSource();
        }
    }

    public static class BeatmapHelper
    {
        public static float GetLastNoteTime(this BeatmapData beatmapData)
        {
            var lastTime = 0f;
            foreach (var noteData in beatmapData.GetBeatmapDataItems<NoteData>(0))
            {
                if (noteData.colorType == ColorType.None)
                {
                    continue;
                }
                if (noteData.time > lastTime)
                {
                    lastTime = noteData.time;
                }
            }
            return lastTime;
        }
    }

    internal static class CommonHelpers
    {
        public static SaberType ToSaberType(this ESaberSlot saberSlot)
        {
            return saberSlot == ESaberSlot.Left ? SaberType.SaberA : SaberType.SaberB;
        }

        public static void SetLayer(this GameObject obj, int layer)
        {
            if (obj == null)
            {
                return;
            }
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                if (child == null)
                {
                    continue;
                }
                SetLayer(child.gameObject, layer);
            }
        }

        public static void SetLayer<T>(this GameObject obj, int layer) where T : Component
        {
            if (obj == null)
            {
                return;
            }
            foreach (var comp in obj.GetComponentsInChildren<T>())
            {
                comp.gameObject.layer = layer;
            }
        }

        public static T GetOrAdd<T>(this GameObject obj) where T : Component
        {
            if (obj.GetComponent<T>() is { } comp) return comp;
            return obj.AddComponent<T>();
        }

        public static T Cast<T>(this object obj)
        {
            return (T)obj;
        }

        public static T CastChecked<T>(this object obj)
        {
            if (obj is T ret)
            {
                return ret;
            }
            return default;
        }

        public static bool IsDate(int? day, int? month)
        {
            var time = Utils.CanUseDateTimeNowSafely ? DateTime.Now : DateTime.UtcNow;
            return (!day.HasValue || time.Day == day) && (!month.HasValue || time.Month == month);
        }

        public static async Task WaitForFinish(this ILoadingTask loadingTask)
        {
            if (loadingTask.CurrentTask == null)
            {
                return;
            }
            await loadingTask.CurrentTask;
        }

        public static Component Upgrade(Component monoBehaviour, Type upgradingType)
        {
            var originalType = monoBehaviour.GetType();
            var gameObject = monoBehaviour.gameObject;
            var upgradedDummyComponent = Activator.CreateInstance(upgradingType);
            foreach (FieldInfo info in originalType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(upgradedDummyComponent, info.GetValue(monoBehaviour));
            }
            UnityEngine.Object.DestroyImmediate(monoBehaviour);
            bool goState = gameObject.activeSelf;
            gameObject.SetActive(false);
            var upgradedMonoBehaviour = gameObject.AddComponent(upgradingType);
            foreach (FieldInfo info in upgradingType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(upgradedMonoBehaviour, info.GetValue(upgradedDummyComponent));
            }
            gameObject.SetActive(goState);
            return upgradedMonoBehaviour;
        }
    }

    internal class CoroutineRunner : MonoBehaviour
    {
    }

    public class DebugTimer
    {
        private readonly Stopwatch _stopwatch;
        private readonly string _taskName;
        public DebugTimer(string taskName = null)
        {
            _taskName = taskName ?? "Task";
            _stopwatch = new Stopwatch();
        }

        public static DebugTimer StartNew(string taskName = null)
        {
            var sw = new DebugTimer(taskName);
            sw.Start();
            return sw;
        }

        public void Start()
        {
            _stopwatch.Start();
        }

        public void Print()
        {
            Debug.LogError(GetString());
        }

        public void Print(SiraLog logger)
        {
            logger.Info(GetString());
        }

        private string GetString()
        {
            _stopwatch.Stop();
            return $"{_taskName} finished in {_stopwatch.Elapsed.Seconds}.{_stopwatch.Elapsed.Milliseconds}s";
        }
    }

    public static class PathTools
    {
        public static string RelativeExtension;
        public static string SaberFactoryUserPath => Path.Combine(UnityGame.UserDataPath, "Saber Factory 2");
        public static string ToFullPath(string relativePath)
        {
            return Path.Combine(UnityGame.InstallPath, relativePath);
        }

        public static string ToRelativePath(string path)
        {
            return path.Substring(UnityGame.InstallPath.Length + 1);
        }

        public static string GetSubDir(string relPath)
        {
            relPath = CorrectRelativePath(relPath);
            var split = relPath.Split(Path.DirectorySeparatorChar);
            if (split.Length < 3)
            {
                return "";
            }
            var output = "";
            for (var i = 1; i < split.Length - 1; i++)
            {
                output += split[i];
                if (i != split.Length - 2)
                {
                    output += "\\";
                }
            }
            return output;
        }

        public static string CorrectRelativePath(string path)
        {
            if (!string.IsNullOrEmpty(RelativeExtension) && path.StartsWith(RelativeExtension))
            {
                return path.Substring(RelativeExtension.Length);
            }
            return path;
        }

        public static FileInfo GetFile(this DirectoryInfo dir, string fileName)
        {
            return new FileInfo(Path.Combine(dir.FullName, fileName));
        }

        public static DirectoryInfo GetDirectory(this DirectoryInfo dir, string dirName, bool create = false)
        {
            if (create)
            {
                return dir.CreateSubdirectory(dirName);
            }
            return new DirectoryInfo(Path.Combine(dir.FullName, dirName));
        }

        public static void WriteText(this FileInfo file, string text)
        {
            File.WriteAllText(file.FullName, text);
        }

        public static async Task WriteTextAsync(this FileInfo file, string text)
        {
            using var writer = new StreamWriter(file.FullName);
            await writer.WriteAsync(text);
        }

        public static string ReadText(this FileInfo file)
        {
            return File.ReadAllText(file.FullName);
        }

        public static async Task<string> ReadTextAsync(this FileInfo file)
        {
            using var reader = new StreamReader(file.FullName);
            return await reader.ReadToEndAsync();
        }
    }

    internal class RandomUtil
    {
        private readonly List<int> _lastSelectedRandoms = new List<int> { 1 };
        private readonly System.Random RNG = new System.Random();
        public int RandomNumber(int count)
        {
            lock (RNG)
            {
                return RNG.Next(count);
            }
        }

        public T RandomizeFrom<T>(IList<T> meta)
        {
            if (_lastSelectedRandoms.Count == meta.Count)
            {
                _lastSelectedRandoms.Clear();
            }
            int idx;
            do
            {
                idx = RandomNumber(meta.Count);
            } while (_lastSelectedRandoms.Contains(idx));
            _lastSelectedRandoms.Add(idx);
            return meta[idx];
        }
    }

    public static class AsyncOperationExtensions
    {
        public static System.Runtime.CompilerServices.TaskAwaiter GetAwaiter(this AsyncOperation asyncOp)
        {
            var tcs = new TaskCompletionSource<object>();
            asyncOp.completed += _ => tcs.TrySetResult(null);
            return ((Task)tcs.Task).GetAwaiter();
        }
    }

    public static class Readers
    {
        public static async Task<byte[]> ReadFileAsync(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            return await stream.ReadStreamAsync();
        }

        public static async Task<byte[]> ReadStreamAsync(this Stream stream)
        {
            var result = new byte[stream.Length];
            await stream.ReadAsync(result, 0, result.Length);
            return result;
        }

        public static byte[] ReadStream(this Stream stream)
        {
            var result = new byte[stream.Length];
            stream.Read(result, 0, result.Length);
            return result;
        }

        public static async Task<byte[]> ReadResourceAsync(string path)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(path);
            if (stream == null)
            {
                var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith(path, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(resourceName))
                {
                    stream = assembly.GetManifestResourceStream(resourceName);
                }
            }
            if (stream == null)
            {
                return null;
            }
            using (stream)
            {
                return await ReadStreamAsync(stream);
            }
        }

        public static byte[] ReadResource(string path)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(path);
            if (stream == null)
            {
                var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith(path, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(resourceName))
                {
                    stream = assembly.GetManifestResourceStream(resourceName);
                }
            }
            if (stream == null)
            {
                return null;
            }
            using (stream)
            {
                return ReadStream(stream);
            }
        }

        public static string BytesToString(this byte[] data)
        {
            if (data == null) return string.Empty;
            return Encoding.UTF8.GetString(data, data[0] == 0xef ? 3 : 0, data.Length - (data[0] == 0xef ? 3 : 0));
        }

        public static async Task<Texture2D> ReadTexture(string path)
        {
            var data = await ReadFileAsync(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            return tex;
        }

        public static async Task<AssetBundle> LoadAssetBundleAsync(byte[] data)
        {
            var req = AssetBundle.LoadFromMemoryAsync(data);
            await req;
            return req.assetBundle;
        }

        public static async Task<T> LoadAssetFromAssetBundleAsync<T>(this AssetBundle assetBundle, string assetName) where T : Object
        {
            var req = assetBundle.LoadAssetAsync<T>(assetName);
            await req;
            return (T)req.asset;
        }

        public static async Task<Tuple<T, AssetBundle>> LoadAssetFromAssetBundleAsync<T>(byte[] bundleData, string assetName) where T : Object
        {
            var assetBundle = await LoadAssetBundleAsync(bundleData);
            if (assetBundle == null)
            {
                return null;
            }
            var asset = await assetBundle.LoadAssetFromAssetBundleAsync<T>(assetName);
            if (asset == null)
            {
                assetBundle.Unload(true);
            }
            return new Tuple<T, AssetBundle>(asset, assetBundle);
        }

        public static async Task<Tuple<T, AssetBundle>> LoadAssetFromAssetBundleAsync<T>(string path, string assetName) where T : Object
        {
            var createReq = AssetBundle.LoadFromFileAsync(path);
            await createReq;

            var bundle = createReq.assetBundle;
            if (bundle == null) return null;

            var assetReq = bundle.LoadAssetAsync<T>(assetName);
            await assetReq;

            return new Tuple<T, AssetBundle>((T)assetReq.asset, bundle);
        }
    }

    internal static class SaberHelpers
    {
        private static readonly List<CustomTrail> _trailCache = new List<CustomTrail>();
        public static List<CustomTrail> GetTrails(GameObject saberObject)
        {
            if (saberObject is null)
            {
                return null;
            }
            _trailCache.Clear();
            saberObject.GetComponentsInChildren(true, _trailCache);
            var result = new List<CustomTrail>(_trailCache.Count);
            foreach (var trail in _trailCache)
            {
                if (trail.PointEnd != null && trail.PointStart != null)
                {
                    result.Add(trail);
                }
            }
            for (int i = 0; i < result.Count - 1; i++)
            {
                for (int j = i + 1; j < result.Count; j++)
                {
                    if (result[j].PointEnd.position.z > result[i].PointEnd.position.z)
                    {
                        var temp = result[i];
                        result[i] = result[j];
                        result[j] = temp;
                    }
                }
            }
            return result;
        }

        public static SaberInstance.SaberMonoBehaviour GetSaberMonoBehaviour(GameObject go)
        {
            return go.GetComponentInParent<SaberInstance.SaberMonoBehaviour>();
        }
    }

    public static class TextureUtilities
    {
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        public static Sprite LoadSpriteFromTexture(Texture2D t, float pixelsPerUnit = 100.0f)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        public static Texture2D LoadTextureRaw(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                var te = new Texture2D(2, 2);
                if (te.LoadImage(data))
                {
                    return te;
                }
            }
            return null;
        }

        public static Sprite LoadSpriteFromResource(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (_spriteCache.TryGetValue(path, out var cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            if (path.StartsWith("#"))
            {
                var spriteName = path.Substring(1);
                var sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x.name == spriteName);
                if (sprite != null)
                {
                    _spriteCache[path] = sprite;
                }
                return sprite;
            }
            var data = Readers.ReadResource(path);
            var tex = LoadTextureRaw(data);
            if (tex != null)
            {
                var newSprite = LoadSpriteFromTexture(tex);
                _spriteCache[path] = newSprite;
                return newSprite;
            }
            return null;
        }
    }
#if DEBUG
    internal class ImmediateDrawer
    {
        public bool IsInitialized { get; private set; }
        public ImmediateDrawer()
        {
            var prim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ballmesh = prim.GetComponent<MeshFilter>().mesh;
            Object.Destroy(prim);
            var shader = FindShader();
            if (!shader)
            {
                return;
            }
            _mat = new Material(shader);
            IsInitialized = true;
        }

        public void DrawBall(Vector3 pos, float size, Color color)
        {
            _mat.color = color;
            _mat.SetPass(0);
            Graphics.DrawMesh(_ballmesh, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * size), _mat, 0);
        }

        public void DrawSmallBall(Vector3 pos, Color? color = null)
        {
            if (color == null)
                color = Color.red;
            DrawBall(pos, 0.05f, color.Value);
        }

        private Shader FindShader()
        {
            var possibleShaders = new string[]
            {
                "BeatSaber/Unlit Glow",
                "Standard",
            };
            return Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(x => possibleShaders.Contains(x.name));
        }

        private Mesh _ballmesh;
        private Material _mat;
    }
#endif
}