using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalloweenDerby.Audio
{
    [Serializable]
    public struct SfxEntry
    {
        public SfxId Id;
        public AudioClip[] Clips; // multiple = randomly varied (e.g. bat-crack variants)
        [Range(0f, 1f)] public float Volume;
    }

    [Serializable]
    public struct MusicEntry
    {
        public MusicId Id;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume;
    }

    /// Singleton SFX/music player. Every playback call is null-safe: a missing clip just logs
    /// a warning instead of throwing, so the game still runs if an asset wasn't sourced yet.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private SfxEntry[] sfxEntries;
        [SerializeField] private MusicEntry[] musicEntries;
        [SerializeField] private int sfxPoolSize = 12;
        [SerializeField] private AudioSource musicSource;

        private readonly Dictionary<SfxId, SfxEntry> sfxLookup = new();
        private readonly Dictionary<MusicId, MusicEntry> musicLookup = new();
        private readonly List<AudioSource> sfxPool = new();
        private int nextPoolIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (var entry in sfxEntries) sfxLookup[entry.Id] = entry;
            foreach (var entry in musicEntries) musicLookup[entry.Id] = entry;

            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                sfxPool.Add(src);
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }
        }

        public void PlayOneShot(SfxId id, float volumeScale = 1f)
        {
            if (!sfxLookup.TryGetValue(id, out var entry) || entry.Clips == null || entry.Clips.Length == 0)
            {
                Debug.LogWarning($"AudioManager: no clip configured for {id}.");
                return;
            }

            var clip = entry.Clips[UnityEngine.Random.Range(0, entry.Clips.Length)];
            if (clip == null)
            {
                Debug.LogWarning($"AudioManager: {id} has a null clip slot.");
                return;
            }

            var source = sfxPool[nextPoolIndex];
            nextPoolIndex = (nextPoolIndex + 1) % sfxPool.Count;
            source.PlayOneShot(clip, entry.Volume * volumeScale);
        }

        public void PlayMusic(MusicId id)
        {
            if (!musicLookup.TryGetValue(id, out var entry) || entry.Clip == null)
            {
                Debug.LogWarning($"AudioManager: no music clip configured for {id}.");
                return;
            }

            if (musicSource.clip == entry.Clip && musicSource.isPlaying) return;
            musicSource.clip = entry.Clip;
            musicSource.volume = entry.Volume;
            musicSource.Play();
        }

        public void StopMusic() => musicSource.Stop();
    }
}
