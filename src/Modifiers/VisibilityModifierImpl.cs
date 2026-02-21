using System.Text;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using Newtonsoft.Json.Linq;
using SaberFactory2.Serialization;

namespace SaberFactory2.Modifiers
{
    internal class VisibilityModifierImpl : BaseModifierImpl
    {
        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                SetVisibility(value);
            }
        }

        public override string Name { get; }
        public override string TypeName => "Visibility Modifier";

        [UIValue("DefaultValueText")]
        private string DefaultValueText => "<color=#ffffff80>Visible by default:</color> " + _visibilityModifier.DefaultValue;

        private bool _visible;
        private VisibilityModifier _visibilityModifier;

        public VisibilityModifierImpl(VisibilityModifier visibilityModifier) : base(visibilityModifier.Id)
        {
            _visible = visibilityModifier.DefaultValue;
            Name = visibilityModifier.Name;
        }

        public override void SetInstance(object instance)
        {
            _visibilityModifier = (VisibilityModifier)instance;

            Visible = Visible;
        }

        public override void Reset()
        {
            Visible = _visibilityModifier.DefaultValue;
        }

        public override Task FromJson(JObject obj, Serializer serializer)
        {
            if (obj != null && obj.TryGetValue(nameof(Visible), out var visibleTkn))
            {
                Visible = visibleTkn.ToObject<bool>();
            }
            return Task.CompletedTask;
        }

        public override Task<JToken> ToJson(Serializer serializer)
        {
            return Task.FromResult<JToken>(new JObject { { nameof(Visible), Visible } });
        }

        public override void Update()
        {
        }

        public override void Sync(object otherMod)
        {
            if (otherMod is VisibilityModifierImpl other)
            {
                Visible = other.Visible;
            }
        }

        public override string DrawUi()
        {
            var str = new StringBuilder();
            str.AppendLine("<vertical>");
            str.AppendLine("<vertical bg='round-rect-panel' custom-color='#777' vertical-fit='PreferredSize' horizontal-fit='Unconstrained' pad='2'>");
            str.AppendLine("<text text='" + _visibilityModifier.Name + "' align='Center'/>");
            str.AppendLine("</vertical>");
            str.AppendLine("<text text='~DefaultValueText' align='Center'/>");
            str.AppendLine("<checkbox text='Visible' value='Visible' apply-on-change='true' pref-width='50'/>");
            str.AppendLine("</vertical>");
            return str.ToString();
        }

        private void SetVisibility(bool visible)
        {
            if (_visibilityModifier?.Objects == null)
            {
                return;
            }
            foreach (var obj in _visibilityModifier.Objects)
            {
                if (obj == null)
                {
                    continue;
                }
                obj.SetActive(visible);
            }
        }
    }
}