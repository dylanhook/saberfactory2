using System;
using SaberFactory2.DataStore;
using SaberFactory2.Models;
using SaberFactory2.Models.CustomSaber;

namespace SaberFactory2.Core
{
    public static class EventBus
    {
        public static event Action<ModelComposition> OnSaberLoaded;
        public static void PublishSaberLoaded(ModelComposition composition) => OnSaberLoaded?.Invoke(composition);

        public static event Action<ModelComposition> OnSaberEquipped;
        public static void PublishSaberEquipped(ModelComposition composition) => OnSaberEquipped?.Invoke(composition);

        public static event Action<ModelComposition> OnPreviewSaberChanged;
        public static void PublishPreviewSaberChanged(ModelComposition composition) => OnPreviewSaberChanged?.Invoke(composition);

        public static event Action<SaberFactory2.Instances.SaberInstance> OnSaberPreviewInstantiated;
        public static void PublishSaberPreviewInstantiated(SaberFactory2.Instances.SaberInstance saber) => OnSaberPreviewInstantiated?.Invoke(saber);

        public static event Action<float> OnSaberWidthChanged;
        public static void PublishSaberWidthChanged(float width) => OnSaberWidthChanged?.Invoke(width);

        public static event Action<ModelComposition, bool> OnSaberFavoriteToggled;
        public static void PublishSaberFavoriteToggled(ModelComposition composition, bool isFavorite) => OnSaberFavoriteToggled?.Invoke(composition, isFavorite);

        public static event Action OnSettingsChanged;
        public static void PublishSettingsChanged() => OnSettingsChanged?.Invoke();

        public static event Action<float> OnSwingSoundVolumeChanged;
        public static void PublishSwingSoundVolumeChanged(float volume) => OnSwingSoundVolumeChanged?.Invoke(volume);
    }
}