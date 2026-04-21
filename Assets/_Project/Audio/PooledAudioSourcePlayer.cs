#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionWeight.Audio
{
    [DisallowMultipleComponent]
    public sealed class PooledAudioSourcePlayer : MonoBehaviour
    {
        private const int DefaultPoolSize = 8;

        private static PooledAudioSourcePlayer? s_instance;

        [SerializeField]
        private int _initialPoolSize = DefaultPoolSize;

        private readonly Queue<AudioSource> _availableSources = new();
        private readonly List<ActivePlayback> _activePlaybacks = new();

        public static PooledAudioSourcePlayer Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var root = new GameObject(nameof(PooledAudioSourcePlayer));
                    s_instance = root.AddComponent<PooledAudioSourcePlayer>();
                }

                return s_instance;
            }
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            EnsurePoolSize(Mathf.Max(1, _initialPoolSize));
        }

        private void Update()
        {
            for (var i = _activePlaybacks.Count - 1; i >= 0; i--)
            {
                var playback = _activePlaybacks[i];
                playback.RemainingSeconds -= Time.deltaTime;
                if (playback.RemainingSeconds > 0f && playback.Source.isPlaying)
                {
                    _activePlaybacks[i] = playback;
                    continue;
                }

                playback.Source.Stop();
                playback.Source.clip = null;
                _availableSources.Enqueue(playback.Source);
                _activePlaybacks.RemoveAt(i);
            }
        }

        public void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null)
            {
                return;
            }

            var source = _availableSources.Count > 0 ? _availableSources.Dequeue() : CreateSource();
            source.transform.position = position;
            source.volume = volume;
            source.pitch = pitch;
            source.clip = clip;
            source.Play();

            _activePlaybacks.Add(new ActivePlayback
            {
                Source = source,
                RemainingSeconds = Mathf.Max(clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)), 0.01f),
            });
        }

        private void EnsurePoolSize(int poolSize)
        {
            while ((_availableSources.Count + _activePlaybacks.Count) < poolSize)
            {
                _availableSources.Enqueue(CreateSource());
            }
        }

        private AudioSource CreateSource()
        {
            var sourceObject = new GameObject("OneShotAudioSource");
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private struct ActivePlayback
        {
            public AudioSource Source;
            public float RemainingSeconds;
        }
    }
}
