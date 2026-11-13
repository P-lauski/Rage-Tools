using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool awcProbeDone_U57;

        private void ServiceAwcProbe_U57()
        {
            var want = Environment.GetEnvironmentVariable("RLE_AWCPROBE");
            if (awcProbeDone_U57 || string.IsNullOrEmpty(want)) return;
            if (screenshotPath != null) screenshotFrames = -1;
            if (gameFiles == null || !gameFiles.Ready || gameFiles.Cache?.AllRpfs == null) return;
            awcProbeDone_U57 = true;
            var outDir = Environment.GetEnvironmentVariable("RLE_AWCPROBEOUT");
            var all = new List<RpfFileEntry>();
            foreach (var rpf in gameFiles.Cache.AllRpfs)
                if (rpf?.AllEntries != null)
                    foreach (var e in rpf.AllEntries)
                        if (e is RpfFileEntry fe && fe.NameLower.EndsWith(".awc", StringComparison.Ordinal)) all.Add(fe);
            Console.WriteLine($"AWCPROBE {all.Count} .awc files in the install");
            var picks = new List<RpfFileEntry>();
            if (want == "auto")
            {
                bool mono = false, stereo = false, multi = false; int tried = 0;
                foreach (var fe in all.Where((x, i) => i % 7 == 0))
                {
                    if (mono && stereo && multi || tried > 3000) break;
                    tried++;
                    try
                    {
                        var a = AwcAudio_U57.Load(ArchiveBrowser.Extract(fe), fe.Name, fe);
                        bool st = a.Streams?.Any(x => x.ChannelStreams?.Length == 2) == true;
                        bool mu = a.Streams?.Any(x => x.ChannelStreams?.Length > 2) == true;
                        bool mo = !st && !mu && a.Streams?.Length > 0;
                        if (st && !stereo) { stereo = true; picks.Add(fe); }
                        else if (mu && !multi) { multi = true; picks.Add(fe); }
                        else if (mo && !mono) { mono = true; picks.Add(fe); }
                    }
                    catch { }
                }
                Console.WriteLine($"AWCPROBE auto picked {picks.Count} after {tried} files (mono {mono}, stereo {stereo}, multichannel {multi})");
            }
            else foreach (var w in want.Split(','))
            {
                var hit = all.FirstOrDefault(e => e.NameLower.Contains(w.Trim().ToLowerInvariant()));
                if (hit != null) picks.Add(hit); else Console.WriteLine("AWCPROBE no file matching " + w);
            }
            foreach (var fe in picks)
            {
                try
                {
                    var awc = AwcAudio_U57.Load(ArchiveBrowser.Extract(fe), fe.Name, fe);
                    var tracks = AwcAudio_U57.TracksOf(awc);
                    int ok = 0, bad = 0;
                    foreach (var t in tracks)
                    {
                        if (!t.Playable) { bad++; continue; }
                        var pcm = AwcAudio_U57.Pcm16(t);
                        float secs = pcm.Length / (float)(t.Rate * t.Channels * 2);
                        if (Math.Abs(secs - t.Seconds) < 0.25f || t.Seconds == 0) ok++; else bad++;
                    }
                    Console.WriteLine($"AWCPROBE {fe.Path}: {tracks.Count} track(s), decoded {ok}, failed {bad}; " +
                                      string.Join(" | ", tracks.Take(4).Select(t => $"{t.Name} {t.Codec} {t.Rate}Hz ch{t.Channels} {t.LengthText}")));
                    if (!string.IsNullOrEmpty(outDir))
                    {
                        var (w, s) = AwcAudio_U57.ExportAll(tracks, Path.Combine(outDir, Path.GetFileNameWithoutExtension(fe.Name)));
                        Console.WriteLine($"AWCPROBE exported {w}, skipped {s}");
                    }
                    var first = tracks.FirstOrDefault(t => t.Playable);
                    if (first != null)
                    {
                        using var pl = new WavePlayer_U57 { Volume = 0f };
                        pl.Load(AwcAudio_U57.Pcm16(first), first.Rate, first.Channels);
                        bool started = pl.Play();
                        System.Threading.Thread.Sleep(600);
                        float p1 = pl.Position;
                        pl.Pause();
                        float p2 = pl.Position;
                        System.Threading.Thread.Sleep(300);
                        float p3 = pl.Position;
                        pl.Seek(Math.Min(5f, first.Seconds * 0.5f));
                        float p4 = pl.Position;
                        pl.Stop();
                        Console.WriteLine($"AWCPROBE play {first.Name}: started {started} {pl.LastError} pos after 0.6s {p1:0.00}, paused holds {p2:0.00}->{p3:0.00}, seek to {p4:0.00}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"AWCPROBE {fe.Path} FAILED {ex.Message}"); }
            }
            if (screenshotPath != null) screenshotFrames = 4;
        }
    }
}
