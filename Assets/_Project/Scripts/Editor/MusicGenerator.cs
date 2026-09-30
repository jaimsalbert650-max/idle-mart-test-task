using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Synthesises the background music loop (C–Am–F–G arpeggios with a soft bass) into a WAV file,
    /// so the project ships its own music without third-party audio.
    /// </summary>
    public static class MusicGenerator
    {
        public const string OutputPath = ContentBuilder.Root + "/Audio/music_loop.wav";

        private const int SampleRate = 22050;
        private const float Bpm = 96f;

        [MenuItem("Idle Mart/Generate Music Loop", priority = 20)]
        public static void Generate()
        {
            var beat = 60f / Bpm;
            int[][] chords =
            {
                new[] { 60, 64, 67 }, // C
                new[] { 57, 60, 64 }, // Am
                new[] { 53, 57, 60 }, // F
                new[] { 55, 59, 62 }  // G
            };
            const int barsPerChord = 2;
            var totalBeats = chords.Length * barsPerChord * 4;
            var samples = new float[(int)(totalBeats * beat * SampleRate)];

            // Arpeggio pattern in eighth notes (indexes into the chord, 3 = octave of the root).
            int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 1 };

            for (var c = 0; c < chords.Length; c++)
            for (var bar = 0; bar < barsPerChord; bar++)
            {
                var barStart = (c * barsPerChord + bar) * 4 * beat;

                for (var i = 0; i < 8; i++)
                {
                    var note = pattern[i] == 3 ? chords[c][0] + 12 : chords[c][pattern[i]];
                    AddNote(samples, barStart + i * beat * 0.5f, beat * 0.9f, Midi(note + 12), 0.16f, Pluck);
                }

                for (var q = 0; q < 4; q++)
                    AddNote(samples, barStart + q * beat, beat * 0.95f, Midi(chords[c][0] - 12), 0.22f, Soft);

                // Sustained pad under every bar.
                foreach (var n in chords[c])
                    AddNote(samples, barStart, 4 * beat, Midi(n), 0.035f, Pad);
            }

            Normalize(samples, 0.8f);
            WriteWav(OutputPath, samples);
            AssetDatabase.ImportAsset(OutputPath);

            var importer = (AudioImporter)AssetImporter.GetAtPath(OutputPath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            Debug.Log($"Idle Mart: music written to {OutputPath}");
        }

        private static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        private delegate float Voice(float phase, float t, float duration);

        private static float Pluck(float phase, float t, float duration)
        {
            var tri = 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f);
            return tri * Mathf.Exp(-t * 7f);
        }

        private static float Soft(float phase, float t, float duration) =>
            Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 3f) * Mathf.Clamp01((duration - t) * 20f);

        private static float Pad(float phase, float t, float duration)
        {
            var envelope = Mathf.Clamp01(t * 2f) * Mathf.Clamp01((duration - t) * 2f);
            return Mathf.Sin(phase * 2f * Mathf.PI) * envelope;
        }

        private static void AddNote(float[] buffer, float start, float duration, float frequency, float volume, Voice voice)
        {
            var first = (int)(start * SampleRate);
            var count = (int)(duration * SampleRate);
            for (var i = 0; i < count; i++)
            {
                // Wrap around so notes near the end blend into the loop start.
                var index = (first + i) % buffer.Length;
                var t = (float)i / SampleRate;
                buffer[index] += voice(frequency * t % 1f, t, duration) * volume;
            }
        }

        private static void Normalize(float[] buffer, float peak)
        {
            var max = 0f;
            foreach (var s in buffer) max = Mathf.Max(max, Mathf.Abs(s));
            if (max <= 0f) return;
            for (var i = 0; i < buffer.Length; i++) buffer[i] = buffer[i] / max * peak;
        }

        private static void WriteWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            var dataSize = samples.Length * 2;

            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(dataSize);
            foreach (var s in samples) writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
