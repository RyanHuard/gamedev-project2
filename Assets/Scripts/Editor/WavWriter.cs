using System.IO;

namespace HalloweenDerby.EditorTools
{
    /// Minimal 16-bit PCM mono WAV writer - no external dependency needed for the self-authored
    /// (fully CC0, no licensing risk) placeholder SFX/music this project generates procedurally.
    public static class WavWriter
    {
        public static void WriteMono16(string path, float[] samples, int sampleRate)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            int byteRate = sampleRate * 2;
            int dataSize = samples.Length * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)2); // block align
            writer.Write((short)16); // bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            foreach (float s in samples)
            {
                float clamped = UnityEngine.Mathf.Clamp(s, -1f, 1f);
                writer.Write((short)(clamped * short.MaxValue));
            }
        }
    }
}
