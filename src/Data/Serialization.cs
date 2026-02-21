using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using ModestTree;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberFactory2.DataStore;
using SaberFactory2.Helpers;
using SaberFactory2.Models;
using UnityEngine;
using Zenject;

namespace SaberFactory2.Serialization
{
    internal interface IFactorySerializable
    {
        public Task FromJson(JObject obj, Serializer serializer);
        public Task<JToken> ToJson(Serializer serializer);
    }

    internal interface IStringUiProvider
    {
        public string DrawUi();
    }

    internal static class JHelpers
    {
        public static void Populate<T>(this JToken value, T target)
        {
            using var sr = value.CreateReader();
            Serializer.JsonSerializer.Populate(sr, target);
        }

        public static async Task SaveToFile(this IFactorySerializable value, Serializer serializer, string filename)
        {
            var token = await value.ToJson(serializer);
            using var writer = new StreamWriter(filename);
            using var jsonWriter = new JsonTextWriter(writer);
            await token.WriteToAsync(jsonWriter);
        }

        public static async Task LoadFromFile(this IFactorySerializable value, Serializer serializer, string filename)
        {
            using var reader = new StreamReader(filename);
            using var jsonReader = new JsonTextReader(reader);
            var obj = (JObject)await JToken.LoadAsync(jsonReader);
            await value.FromJson(obj, serializer);
        }
    }

    public class MapSerializeAttribute : Attribute
    {
        public Type ConverterType;
    }

    public class PresetSaveManager
    {
        private readonly DirectoryInfo _presetDir;
        private readonly Serializer _serializer;
        private PresetSaveManager(PluginDirectories pluginDirs, Serializer serializer)
        {
            _serializer = serializer;
            _presetDir = pluginDirs.PresetDir;
        }

        public event Action OnSaberLoaded;
        public async Task SaveSaber(SaberSet saberSet, string fileName)
        {
            var file = _presetDir.GetFile(fileName);
            await saberSet.SaveToFile(_serializer, file.FullName);
        }

        public async Task LoadSaber(SaberSet saberSet, string fileName)
        {
            var file = _presetDir.GetFile(fileName);
            if (!file.Exists)
            {
                return;
            }
            await saberSet.LoadFromFile(_serializer, file.FullName);
            OnSaberLoaded?.Invoke();
        }
    }

    internal static class QuickSave
    {
        public static void SaveObject(object obj, string path, bool pretty = true)
        {
            using var writer = new StreamWriter(path);
            using var jsonWriter = new JsonTextWriter(writer);
            jsonWriter.Formatting = pretty ? Formatting.Indented : Formatting.None;
            Serializer.JsonSerializer.Serialize(jsonWriter, obj);
        }

        public static T LoadObject<T>(string path)
        {
            if (!File.Exists(path))
            {
                return default;
            }
            using var reader = new StreamReader(path);
            using var jsonReader = new JsonTextReader(reader);
            return Serializer.JsonSerializer.Deserialize<T>(jsonReader);
        }
    }

    public class Serializer
    {
        public static readonly JsonSerializer JsonSerializer = new JsonSerializer();
        [Inject] private readonly MainAssetStore _mainAssetStore = null;
        [Inject] private readonly ShaderPropertyCache _shaderPropertyCache = null;
        [Inject] private readonly TextureStore _textureStore = null;
        static Serializer()
        {
            JsonSerializer.Converters.Add(new Vec2Converter());
            JsonSerializer.Converters.Add(new Vec3Converter());
            JsonSerializer.Converters.Add(new Vec4Converter());
            JsonSerializer.Converters.Add(new ColorConverter());
        }

        public static void Install(DiContainer container)
        {
            container.Bind<Serializer>().AsSingle();
        }

        public static T Cast<T>(object obj)
        {
            return ((JObject)obj).ToObject<T>(JsonSerializer);
        }

        public async Task<ModelComposition> LoadPiece(JToken pathTkn)
        {
            await _mainAssetStore.WaitForFinish();
            return await _mainAssetStore[pathTkn.ToObject<string>()];
        }

        public JToken SerializeMaterial(Material material)
        {
            var result = new JObject();
            var shaderInfo = _shaderPropertyCache[material.shader];
            foreach (var prop in shaderInfo.GetAll())
            {
                result.Add(prop.Name, prop.ToJson(material));
            }
            return result;
        }

        public async Task LoadMaterial(JObject ser, Material material)
        {
            var shaderInfo = _shaderPropertyCache[material.shader];
            foreach (var prop in shaderInfo.GetAll())
            {
                var jProp = ser.Property(prop.Name);
                if (jProp is null)
                {
                    continue;
                }
                if (prop is ShaderPropertyInfo.ShaderTexture)
                {
                    prop.FromJson(jProp.Value, material, await ResolveTexture(jProp.Value.ToObject<string>()));
                    continue;
                }
                prop.FromJson(jProp.Value, material);
            }
        }

        private async Task<Texture2D> ResolveTexture(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            await _textureStore.WaitForFinish();
            return (await _textureStore[name])?.Texture;
        }
    }

    internal class ColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new[] { value.r, value.g, value.b, value.a });
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var fArr = serializer.Deserialize<float[]>(reader);
            return new Color(fArr[0], fArr[1], fArr[2], fArr[3]);
        }
    }

    internal class Vec2Converter : JsonConverter<Vector2>
    {
        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new[] { value.x, value.y });
        }

        public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var fArr = serializer.Deserialize<float[]>(reader);
            return new Vector2(fArr[0], fArr[1]);
        }
    }

    internal class Vec3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new[] { value.x, value.y, value.z });
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var fArr = serializer.Deserialize<float[]>(reader);
            return new Vector3(fArr[0], fArr[1], fArr[2]);
        }
    }

    internal class Vec4Converter : JsonConverter<Vector4>
    {
        public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new[] { value.x, value.y, value.z, value.w });
        }

        public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var fArr = serializer.Deserialize<float[]>(reader);
            return new Vector4(fArr[0], fArr[1], fArr[2], fArr[3]);
        }
    }
}