using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalloweenDerby.EditorTools
{
    /// Procedurally synthesizes every SFX clip the game needs as self-authored (fully CC0, zero
    /// licensing risk) .wav files, rather than sourcing third-party audio - the project brief
    /// explicitly allows "source CC0 sounds yourself or generate them." No music: feedback was
    /// that the game should have none right now.
    ///
    /// Second pass after feedback that the first version was "too synthetic/beepy" and "weak,
    /// no punch." The first version leaned on raw sine tones for body and unfiltered white
    /// noise for texture - sine tones read as "beeps," and unfiltered noise reads as thin static
    /// rather than a solid impact. This version uses the standard game-audio percussion
    /// technique instead: a fast pitch-dropping tone (the "thump"/body, like kick-drum synthesis)
    /// layered with LOW-PASS FILTERED noise (the "crack"/texture, so it's a dull thwack instead
    /// of hiss) plus soft saturation on the loudest hits for perceived punch.
    public static class AudioGenerator
    {
        private const int SampleRate = 44100;
        private const string SfxDir = "Assets/Audio/sfx";
        private const string MusicDir = "Assets/Audio/music";

        [MenuItem("Halloween Derby/Generate Placeholder Audio")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(SfxDir);

            // No music is generated or wired - PlayMusic is not called anywhere in the game by
            // design. If Assets/Audio/music has leftover files from an earlier pass, remove them
            // so there's nothing stale lying around for AudioManager to accidentally pick up.
            if (Directory.Exists(MusicDir))
            {
                foreach (var f in Directory.GetFiles(MusicDir, "*.wav"))
                    AssetDatabase.DeleteAsset(f.Replace(Application.dataPath, "Assets"));
            }

            WriteSfx("BatCrack_Perfect", BuildImpact(0.16f, thumpFreqStart: 180f, thumpFreqEnd: 60f, noiseCutoff: 2600f, punch: 1f));
            WriteSfx("BatCrack_Good", BuildImpact(0.14f, thumpFreqStart: 150f, thumpFreqEnd: 55f, noiseCutoff: 2000f, punch: 0.82f));
            WriteSfx("BatCrack_Weak", BuildImpact(0.11f, thumpFreqStart: 120f, thumpFreqEnd: 50f, noiseCutoff: 1400f, punch: 0.6f));

            WriteSfx("SwingWhoosh", BuildWhoosh(0.22f, startCutoff: 3500f, endCutoff: 500f, amp: 0.55f));
            WriteSfx("SwingMiss", BuildWhoosh(0.28f, startCutoff: 4200f, endCutoff: 350f, amp: 0.6f));
            WriteSfx("PitcherWindup", BuildWhoosh(0.4f, startCutoff: 900f, endCutoff: 250f, amp: 0.4f));

            WriteSfx("PumpkinBurst", BuildImpact(0.28f, thumpFreqStart: 140f, thumpFreqEnd: 45f, noiseCutoff: 1200f, punch: 0.85f, noiseTail: 0.18f));
            WriteSfx("TombstoneBreak", BuildRockCrumble(0.45f, amp: 0.8f));
            WriteSfx("GlassBreak", BuildGlassShatter(0.4f, amp: 0.75f));
            WriteSfx("TargetThud", BuildImpact(0.16f, thumpFreqStart: 100f, thumpFreqEnd: 40f, noiseCutoff: 900f, punch: 0.55f));

            WriteSfx("GhostOooo", BuildGhostOooo(1.2f, amp: 0.5f));

            WriteSfx("RoundStart", BuildChime(new[] { 440f, 554f, 659f }, 0.5f, amp: 0.45f));
            WriteSfx("RoundClear", BuildChime(new[] { 523f, 659f, 784f, 1047f }, 0.7f, amp: 0.5f));
            WriteSfx("RoundFail", BuildChime(new[] { 392f, 349f, 294f }, 0.6f, amp: 0.45f));
            WriteSfx("StarAward", BuildChime(new[] { 784f, 988f }, 0.3f, amp: 0.4f));
            WriteSfx("OutCalled", BuildImpact(0.18f, thumpFreqStart: 220f, thumpFreqEnd: 90f, noiseCutoff: 1800f, punch: 0.6f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AudioGenerator: all placeholder SFX regenerated (no music, per feedback).");
        }

        private static void WriteSfx(string name, float[] samples) => WriteWav($"{SfxDir}/{name}.wav", samples);

        private static void WriteWav(string path, float[] samples)
        {
            WavWriter.WriteMono16(path, samples, SampleRate);
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        // ---------- DSP building blocks ----------

        private static float Envelope(float t, float duration, float attack, float decay)
        {
            if (t < attack) return t / Mathf.Max(attack, 0.0001f);
            float release = duration - attack;
            float u = (t - attack) / Mathf.Max(release, 0.0001f);
            return Mathf.Pow(1f - Mathf.Clamp01(u), decay);
        }

        private static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        /// One-pole low-pass filter applied to a full noise buffer. Unfiltered white noise is
        /// full-spectrum hiss/static - every impact/crack/burst sound here runs noise through
        /// this first so it reads as a dull "thwack"/rumble instead of harsh static. Lower
        /// cutoff = darker/duller; higher cutoff = brighter/sharper.
        private static float[] LowPassNoise(int n, float cutoffHz, System.Random rng)
        {
            var raw = new float[n];
            for (int i = 0; i < n; i++) raw[i] = Noise(rng);

            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float dt = 1f / SampleRate;
            float alpha = dt / (rc + dt);
            var filtered = new float[n];
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                prev += alpha * (raw[i] - prev);
                filtered[i] = prev;
            }
            return filtered;
        }

        /// Soft saturation (tanh) - pushes the loudest transients toward a rounded ceiling
        /// instead of hard-clipping, which reads as "punchy"/"thick" rather than "distorted."
        private static float SoftClip(float x) => (float)System.Math.Tanh(x * 1.6) / 1.2f;

        // ---------- Per-sound composition ----------

        /// The core "impact" sound: a fast pitch-dropping sine (the felt "thump"/body - the same
        /// technique used to synthesize kick drums) layered with low-pass-filtered noise (the
        /// "crack" transient), with soft saturation for punch. Used for bat cracks, bursts,
        /// thuds, and the out-called buzz - varying thumpFreq/noiseCutoff/punch gives each a
        /// distinct character without each being a bespoke one-off.
        private static float[] BuildImpact(float duration, float thumpFreqStart, float thumpFreqEnd, float noiseCutoff, float punch, float noiseTail = 0f)
        {
            int n = (int)(duration * SampleRate);
            var rng = new System.Random((int)(thumpFreqStart * 7 + noiseCutoff));
            var noise = LowPassNoise(n, noiseCutoff, rng);
            var samples = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = Mathf.Clamp01(t / duration);
                float thumpFreq = Mathf.Lerp(thumpFreqStart, thumpFreqEnd, u * u); // fast initial drop
                phase += thumpFreq / SampleRate;
                float thumpEnv = Envelope(t, duration, attack: 0.002f, decay: 3.5f);
                float noiseEnv = Envelope(t, duration, attack: 0.001f, decay: 7f) + noiseTail * (1f - u);
                float thump = Mathf.Sin(2f * Mathf.PI * phase) * thumpEnv;
                float crack = noise[i] * noiseEnv;
                samples[i] = SoftClip((thump * 0.65f + crack * 0.7f) * punch);
            }
            return samples;
        }

        private static float[] BuildWhoosh(float duration, float startCutoff, float endCutoff, float amp)
        {
            int n = (int)(duration * SampleRate);
            var rng = new System.Random((int)(startCutoff + endCutoff));
            var samples = new float[n];
            // A swept-cutoff filtered noise reads as actual moving air; the earlier version
            // mixed in a raw sine tone here too, which is exactly what made it sound "beepy."
            float rc, alpha, prev = 0f;
            float dt = 1f / SampleRate;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / duration;
                float cutoff = Mathf.Lerp(startCutoff, endCutoff, u);
                rc = 1f / (2f * Mathf.PI * cutoff);
                alpha = dt / (rc + dt);
                prev += alpha * (Noise(rng) - prev);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)); // rises then falls
                samples[i] = SoftClip(prev * env * amp * 2.2f);
            }
            return samples;
        }

        private static float[] BuildRockCrumble(float duration, float amp)
        {
            int n = (int)(duration * SampleRate);
            var rng = new System.Random(7);
            var noise = LowPassNoise(n, 1100f, rng);
            var samples = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, duration, attack: 0.01f, decay: 2.2f);
                // Several short clustered impacts to read as rubble/crumbling rock rather than a
                // single smooth decay.
                float cluster = Mathf.PerlinNoise(t * 22f, 0f) > 0.5f ? 1f : 0.35f;
                samples[i] = SoftClip(noise[i] * env * cluster * amp * 1.8f);
            }
            return samples;
        }

        private static float[] BuildGlassShatter(float duration, float amp)
        {
            int n = (int)(duration * SampleRate);
            var rng = new System.Random(99);
            var highNoise = LowPassNoise(n, 5200f, rng);
            var samples = new float[n];
            // A handful of overlapping high-pitched decaying sine "shards" over bright filtered
            // noise - shards alone sounded too tonal/beepy; noise alone sounded like plain hiss.
            float[] shardFreqs = { 2200f, 3100f, 4200f, 2700f, 3600f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, duration, attack: 0.002f, decay: 4.5f);
                float shards = 0f;
                foreach (var f in shardFreqs)
                    shards += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 16f);
                shards /= shardFreqs.Length;
                samples[i] = SoftClip((shards * 0.5f + highNoise[i] * 0.9f) * env * amp * 1.4f);
            }
            return samples;
        }

        private static float[] BuildGhostOooo(float duration, float amp)
        {
            int n = (int)(duration * SampleRate);
            var samples = new float[n];
            // Three slightly-detuned oscillators (chorus) instead of one sine + its harmonic -
            // a single clean sine glide reads as a "whistle"/beep, not an otherworldly moan.
            float[] detunes = { -3f, 0f, 4f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / duration;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
                float vibrato = Mathf.Sin(2f * Mathf.PI * 4.5f * t) * 5f;
                float baseFreq = 180f + vibrato - u * 35f; // gentle downward glide, spooky "oooo"
                float wave = 0f;
                foreach (var d in detunes)
                    wave += Mathf.Sin(2f * Mathf.PI * (baseFreq + d) * t);
                wave /= detunes.Length;
                samples[i] = wave * env * amp;
            }
            return samples;
        }

        private static float[] BuildChime(float[] notesHz, float duration, float amp)
        {
            int n = (int)(duration * SampleRate);
            var samples = new float[n];
            float perNote = duration / notesHz.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                int noteIndex = Mathf.Min((int)(t / perNote), notesHz.Length - 1);
                float noteT = t - noteIndex * perNote;
                float env = Envelope(noteT, perNote, attack: 0.01f, decay: 3f);
                float freq = notesHz[noteIndex];
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                samples[i] = wave * env * amp;
            }
            return samples;
        }
    }
}
