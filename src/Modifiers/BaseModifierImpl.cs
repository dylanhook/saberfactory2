using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Parser;
using Newtonsoft.Json.Linq;
using SaberFactory2.Serialization;

namespace SaberFactory2.Modifiers
{
    public abstract class BaseModifierImpl : IFactorySerializable, IStringUiProvider
    {
        public abstract string Name { get; }
        public abstract string TypeName { get; }
        public BSMLParserParams ParserParams { get; set; }
        public abstract string DrawUi();
        public int Id { get; }

        protected BaseModifierImpl(int id)
        {
            Id = id;
        }

        public abstract void SetInstance(object instance);
        public abstract void Reset();
        public virtual void WasSelected(params object[] args) { }
        public virtual void OnTick() { }

        public abstract Task FromJson(JObject obj, Serializer serializer);
        public abstract Task<JToken> ToJson(Serializer serializer);
        public abstract void Update();
        public abstract void Sync(object otherMod);
    }
}