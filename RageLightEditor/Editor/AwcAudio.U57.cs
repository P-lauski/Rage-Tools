using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public sealed class AwcTrack_U57
    {
        public string Name = "";
        public string Codec = "";
        public int Rate;
        public int Channels = 1;
        public float Seconds;
        public AwcStream[] Parts = Array.Empty<AwcStream>();
        public byte[] MidiData;
        public bool Playable;
        public bool IsMidi => MidiData != null;
        public string LengthText => TimeSpan.FromSeconds(Math.Max(0, Seconds)).ToString(Seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    }

    public static class AwcAudio_U57
    {
        public static AwcFile Load(byte[] data, string name, RpfFileEntry entry = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var copy = (byte[])data.Clone();
            entry ??= new RpfBinaryFileEntry { Name = name ?? "audio.awc", NameLower = (name ?? "audio.awc").ToLowerInvariant() };
            var awc = new AwcFile();
            awc.Load(copy, entry);
            if (!string.IsNullOrEmpty(awc.ErrorMessage) && (awc.Streams == null || awc.Streams.Length == 0))
                throw new InvalidDataException(awc.ErrorMessage);
            return awc;
        }

        private static AwcCodecType CodecOf(AwcStream s) =>
            s.StreamFormat?.Codec ?? s.FormatChunk?.Codec ?? AwcCodecType.PCM;

        private static bool Decodable(AwcStream s) =>
            (s.FormatChunk != null || s.StreamFormat != null) &&
            (CodecOf(s) == AwcCodecType.PCM || CodecOf(s) == AwcCodecType.ADPCM) &&
            s.SamplesPerSecond > 0;

        public static List<AwcTrack_U57> TracksOf(AwcFile awc)
        {
            var list = new List<AwcTrack_U57>();
            if (awc?.Streams == null) return list;
            foreach (var s in awc.Streams.Where(x => x != null).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                bool stereo = s.ChannelStreams?.Length == 2;
                if (s.StreamBlocks != null && !stereo) continue;
                if (stereo)
                {
                    var a = s.ChannelStreams[0]; var b = s.ChannelStreams[1];
                    bool aLeft = IsLeft(a.Name) || IsRight(b.Name);
                    var l = aLeft ? a : b; var r = aLeft ? b : a;
                    list.Add(new AwcTrack_U57
                    {
                        Name = TrimSide(l.Name) + " (stereo)",
                        Codec = CodecOf(l).ToString(),
                        Rate = l.SamplesPerSecond,
                        Channels = 2,
                        Seconds = Math.Max(l.Length, r.Length),
                        Parts = new[] { l, r },
                        Playable = Decodable(l) && Decodable(r) && l.SamplesPerSecond == r.SamplesPerSecond,
                    });
                    continue;
                }
                if (s.MidiChunk != null)
                {
                    list.Add(new AwcTrack_U57 { Name = s.Name, Codec = "MIDI", MidiData = s.MidiChunk.Data, Parts = new[] { s } });
                    continue;
                }
                if (s.FormatChunk == null && s.StreamFormat == null) continue;
                list.Add(new AwcTrack_U57
                {
                    Name = s.Name,
                    Codec = Decodable(s) ? CodecOf(s).ToString() : "unknown codec",
                    Rate = s.SamplesPerSecond,
                    Channels = 1,
                    Seconds = s.Length,
                    Parts = new[] { s },
                    Playable = Decodable(s),
                });
            }
            return list;
        }

        private static bool IsLeft(string n) => n != null && (n.EndsWith("left", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".l", StringComparison.OrdinalIgnoreCase) || n.EndsWith("_l", StringComparison.OrdinalIgnoreCase));
        private static bool IsRight(string n) => n != null && (n.EndsWith("right", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".r", StringComparison.OrdinalIgnoreCase) || n.EndsWith("_r", StringComparison.OrdinalIgnoreCase));

        private static string TrimSide(string n)
        {
            if (string.IsNullOrEmpty(n)) return "track";
            foreach (var suf in new[] { "_left", "left", ".l", "_l" })
                if (n.EndsWith(suf, StringComparison.OrdinalIgnoreCase)) return n.Substring(0, n.Length - suf.Length);
            return n;
        }

        public static byte[] Pcm16(AwcTrack_U57 t)
        {
            if (t == null || !t.Playable) throw new InvalidOperationException("this track cannot be decoded");
            if (t.Channels == 1) return t.Parts[0].GetPcmData();
            var l = t.Parts[0].GetPcmData();
            var r = t.Parts[1].GetPcmData();
            int frames = Math.Min(l.Length, r.Length) / 2;
            var outp = new byte[frames * 4];
            for (int i = 0; i < frames; i++)
            {
                outp[i * 4] = l[i * 2]; outp[i * 4 + 1] = l[i * 2 + 1];
                outp[i * 4 + 2] = r[i * 2]; outp[i * 4 + 3] = r[i * 2 + 1];
            }
            return outp;
        }

        public static byte[] Wav(byte[] pcm16, int rate, int channels)
        {
            using var ms = new MemoryStream(44 + pcm16.Length);
            using var w = new BinaryWriter(ms);
            int blockAlign = channels * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + pcm16.Length);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * blockAlign);
            w.Write((short)blockAlign);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(pcm16.Length);
            w.Write(pcm16);
            w.Flush();
            return ms.ToArray();
        }

        public static string FileNameFor(AwcTrack_U57 t)
        {
            var n = (t?.Name ?? "track").Replace(" (stereo)", "_stereo");
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n + (t?.IsMidi == true ? ".mid" : ".wav");
        }

        public static byte[] ExportBytes(AwcTrack_U57 t) =>
            t.IsMidi ? t.MidiData : Wav(Pcm16(t), t.Rate, t.Channels);

        public static (int written, int skipped) ExportAll(IEnumerable<AwcTrack_U57> tracks, string folder)
        {
            Directory.CreateDirectory(folder);
            int written = 0, skipped = 0;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tracks)
            {
                if (!t.Playable && !t.IsMidi) { skipped++; continue; }
                var name = FileNameFor(t);
                var stem = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name);
                for (int k = 2; !used.Add(name); k++) name = $"{stem}_{k}{ext}";
                try { File.WriteAllBytes(Path.Combine(folder, name), ExportBytes(t)); written++; }
                catch { skipped++; }
            }
            return (written, skipped);
        }
    }
}
