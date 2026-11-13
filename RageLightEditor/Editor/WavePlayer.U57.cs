using System;
using System.Runtime.InteropServices;

namespace RageLightEditor.Editor
{
    public sealed class WavePlayer_U57 : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHdr
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MmTime
        {
            public uint wType;
            public uint value;
            public uint pad;
        }

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr hwo, int device, ref WaveFormatEx fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr hwo);
        [DllImport("winmm.dll")] private static extern int waveOutPause(IntPtr hwo);
        [DllImport("winmm.dll")] private static extern int waveOutRestart(IntPtr hwo);
        [DllImport("winmm.dll")] private static extern int waveOutGetPosition(IntPtr hwo, ref MmTime t, int size);
        [DllImport("winmm.dll")] private static extern int waveOutSetVolume(IntPtr hwo, uint vol);

        private const uint WhdrDone = 1;
        private const uint TimeBytes = 4;

        private IntPtr dev, hdr, buf;
        private byte[] pcm;
        private int rate, channels, startByte;
        private float volume = 0.8f;

        public bool Loaded => pcm != null;
        public bool Paused { get; private set; }
        public int ByteRate => rate * channels * 2;
        public float Duration => pcm == null || ByteRate == 0 ? 0f : pcm.Length / (float)ByteRate;
        public string LastError { get; private set; }

        public bool Playing
        {
            get
            {
                if (dev == IntPtr.Zero || hdr == IntPtr.Zero) return false;
                var h = Marshal.PtrToStructure<WaveHdr>(hdr);
                return (h.dwFlags & WhdrDone) == 0;
            }
        }

        public float Position
        {
            get
            {
                if (pcm == null || ByteRate == 0) return 0f;
                if (dev == IntPtr.Zero) return startByte / (float)ByteRate;
                var t = new MmTime { wType = TimeBytes };
                waveOutGetPosition(dev, ref t, Marshal.SizeOf<MmTime>());
                long b = startByte + (t.wType == TimeBytes ? t.value : 0);
                return Math.Min(b, pcm.Length) / (float)ByteRate;
            }
        }

        public float Volume
        {
            get => volume;
            set
            {
                volume = Math.Clamp(value, 0f, 1f);
                if (dev != IntPtr.Zero)
                {
                    uint v = (uint)(volume * 0xFFFF);
                    waveOutSetVolume(dev, v | (v << 16));
                }
            }
        }

        public void Load(byte[] pcm16, int sampleRate, int channelCount)
        {
            Stop();
            pcm = pcm16;
            rate = sampleRate;
            channels = Math.Max(1, channelCount);
            startByte = 0;
        }

        public bool Play(float fromSeconds = -1f)
        {
            if (pcm == null) return false;
            float from = fromSeconds >= 0 ? fromSeconds : (Paused ? Position : 0f);
            if (Paused && fromSeconds < 0 && dev != IntPtr.Zero)
            {
                waveOutRestart(dev);
                Paused = false;
                return true;
            }
            Close();
            int align = channels * 2;
            int offset = (int)Math.Clamp((long)(from * ByteRate) / align * align, 0, Math.Max(0, pcm.Length - align));
            int len = pcm.Length - offset;
            if (len <= 0) return false;
            var fmt = new WaveFormatEx
            {
                wFormatTag = 1, nChannels = (ushort)channels, nSamplesPerSec = (uint)rate,
                nAvgBytesPerSec = (uint)ByteRate, nBlockAlign = (ushort)align, wBitsPerSample = 16, cbSize = 0,
            };
            int r = waveOutOpen(out dev, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
            if (r != 0) { dev = IntPtr.Zero; LastError = "the sound device would not open (error " + r + ")"; return false; }
            buf = Marshal.AllocHGlobal(len);
            Marshal.Copy(pcm, offset, buf, len);
            hdr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>());
            Marshal.StructureToPtr(new WaveHdr { lpData = buf, dwBufferLength = (uint)len }, hdr, false);
            waveOutPrepareHeader(dev, hdr, Marshal.SizeOf<WaveHdr>());
            Volume = volume;
            r = waveOutWrite(dev, hdr, Marshal.SizeOf<WaveHdr>());
            if (r != 0) { LastError = "the sound device refused the audio (error " + r + ")"; Close(); return false; }
            startByte = offset;
            Paused = false;
            LastError = null;
            return true;
        }

        public void Pause()
        {
            if (dev == IntPtr.Zero || Paused) return;
            waveOutPause(dev);
            Paused = true;
        }

        public void Seek(float seconds)
        {
            bool wasPaused = Paused;
            bool active = dev != IntPtr.Zero && (Playing || Paused);
            if (!active) { startByte = (int)Math.Clamp((long)(seconds * ByteRate), 0, pcm?.Length ?? 0); return; }
            Play(seconds);
            if (wasPaused) Pause();
        }

        public void Stop()
        {
            Close();
            startByte = 0;
            Paused = false;
        }

        private void Close()
        {
            if (dev != IntPtr.Zero)
            {
                waveOutReset(dev);
                if (hdr != IntPtr.Zero) waveOutUnprepareHeader(dev, hdr, Marshal.SizeOf<WaveHdr>());
                waveOutClose(dev);
                dev = IntPtr.Zero;
            }
            if (hdr != IntPtr.Zero) { Marshal.FreeHGlobal(hdr); hdr = IntPtr.Zero; }
            if (buf != IntPtr.Zero) { Marshal.FreeHGlobal(buf); buf = IntPtr.Zero; }
        }

        public void Dispose() => Close();
    }
}
