using System.ComponentModel;
using System.Runtime.CompilerServices;
using SaberFactory2.Core;
using SaberFactory2.DataStore;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;

namespace SaberFactory2.UI.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propName = "")
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyPropertyChanged(propName);
            return true;
        }
    }

    public class MainViewModel : ViewModelBase
    {
        private ModelComposition _equippedSaber;
        public ModelComposition EquippedSaber { get => _equippedSaber; set { if (SetProperty(ref _equippedSaber, value)) EventBus.PublishSaberEquipped(value); } }

        private ModelComposition _previewSaber;
        public ModelComposition PreviewSaber { get => _previewSaber; set { if (SetProperty(ref _previewSaber, value)) EventBus.PublishPreviewSaberChanged(value); } }

        private float _saberWidth = 1f;
        public float SaberWidth { get => _saberWidth; set { if (SetProperty(ref _saberWidth, value)) EventBus.PublishSaberWidthChanged(value); } }
    }
}