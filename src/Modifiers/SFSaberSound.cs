using SaberFactory2.Helpers;
using UnityEngine;

namespace SaberFactory2.Modifiers
{
    public class SFSaberSound : MonoBehaviour
    {
        public Transform SaberTop;
        public AudioSource AudioSource;
        public AnimationCurve PitchBySpeedCurve;
        public AnimationCurve GainBySpeedCurve;
        public float SpeedMultiplier = 0.05f;
        public float UpSmooth = 4f;
        public float DownSmooth = 4f;

        [Tooltip("No sound is produced if saber point moves more than this distance in one frame.")]
        public float NoSoundTopThresholdSqr = 1f;

        [Range(0, 1)]
        public float Volume = 1;

#if !UNITY
        public float ConfigVolume = 1;
        private Vector3 _prevPos;
        private float _speed;

        public virtual void Start()
        {
            _prevPos = SaberTop.position;
            var saberMb = SaberHelpers.GetSaberMonoBehaviour(gameObject);
            if (saberMb)
            {
                saberMb.RegisterComponent(this);
            }
            SaberFactory2.Core.EventBus.OnSwingSoundVolumeChanged += UpdateVolume;
        }

        private void OnDestroy()
        {
            SaberFactory2.Core.EventBus.OnSwingSoundVolumeChanged -= UpdateVolume;
        }

        private void UpdateVolume(float volume)
        {
            ConfigVolume = volume;
        }

        public virtual void Update()
        {
            var position = SaberTop.position;
            float sqrDistance = (_prevPos - position).sqrMagnitude;

            if (sqrDistance > NoSoundTopThresholdSqr)
            {
                _prevPos = position;
                return;
            }

            float targetSpeed = Time.deltaTime == 0f ? 0f : SpeedMultiplier * Mathf.Sqrt(sqrDistance) / Time.deltaTime;

            if (targetSpeed < _speed)
            {
                _speed = Mathf.Clamp01(Mathf.Lerp(_speed, targetSpeed, Time.deltaTime * DownSmooth));
            }
            else
            {
                _speed = Mathf.Clamp01(Mathf.Lerp(_speed, targetSpeed, Time.deltaTime * UpSmooth));
            }

            AudioSource.pitch = PitchBySpeedCurve.Evaluate(_speed);
            AudioSource.volume = GainBySpeedCurve.Evaluate(_speed) * Volume * ConfigVolume;

            _prevPos = position;
        }
#endif
    }
}