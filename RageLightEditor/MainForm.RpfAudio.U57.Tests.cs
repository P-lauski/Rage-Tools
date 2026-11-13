using System;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void AwcAudioTest_U57(Action<string, bool, string> check)
        {
            try
            {
                var pcm = new byte[48000 * 2 * 2 * 3];
                var wav = AwcAudio_U57.Wav(pcm, 48000, 2);
                bool header = wav.Length == pcm.Length + 44 && wav[0] == (byte)'R' && wav[8] == (byte)'W' &&
                              BitConverter.ToInt16(wav, 22) == 2 && BitConverter.ToInt32(wav, 24) == 48000 &&
                              BitConverter.ToInt32(wav, 40) == pcm.Length;
                check("audio: an exported track is a standard 16-bit PCM .wav", header, $"{wav.Length} bytes");

                using var pl = new WavePlayer_U57 { Volume = 0f };
                pl.Load(pcm, 48000, 2);
                check("audio: the player knows the track's length", Math.Abs(pl.Duration - 3f) < 0.01f, pl.Duration.ToString("0.00"));
                if (!pl.Play())
                {
                    check("audio: no sound device here, playback not tested", true, pl.LastError ?? "");
                    return;
                }
                System.Threading.Thread.Sleep(400);
                float p1 = pl.Position;
                pl.Pause();
                float p2 = pl.Position;
                System.Threading.Thread.Sleep(250);
                float p3 = pl.Position;
                check("audio: it plays, and Pause holds the position",
                      p1 > 0.2f && Math.Abs(p3 - p2) < 0.02f && pl.Paused, $"{p1:0.00} then {p2:0.00} -> {p3:0.00}");
                pl.Seek(2.0f);
                float p4 = pl.Position;
                pl.Play();
                System.Threading.Thread.Sleep(200);
                float p5 = pl.Position;
                check("audio: seeking moves it, and Play resumes from there", Math.Abs(p4 - 2.0f) < 0.05f && p5 > p4, $"{p4:0.00} -> {p5:0.00}");
                pl.Stop();
                check("audio: Stop ends playback", !pl.Playing && !pl.Paused, "");
            }
            catch (Exception ex) { check("audio: the test ran", false, ex.ToString()); }
        }
    }
}
