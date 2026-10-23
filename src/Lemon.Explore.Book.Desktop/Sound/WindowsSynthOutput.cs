using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Lemon.Explore.Book.Chapters.Sound;

namespace Lemon.Explore.Book.Desktop.Sound;

/// <summary>
/// Streams a <see cref="SoundSynth"/> to the default sound device through the Windows waveOut API (winmm): 16-bit mono,
/// four buffers of 1024 samples queued ahead (about 90 ms), refilled by a background thread as the device finishes each.
/// When there is no device, <see cref="TryOpen"/> returns null and the chapter stays silent.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsSynthOutput : IDisposable
{
    private const int BufferCount = 4;
    private const int BufferSamples = 1024;
    private const int WaveMapper = -1;
    private const int CallbackNull = 0;
    private const int WhdrDone = 0x1;

    private static readonly int HeaderSize = Marshal.SizeOf<WaveHeader>();
    private static readonly int FlagsOffset = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));

    private readonly SoundSynth _synth;
    private readonly IntPtr _device;
    private readonly IntPtr[] _headers = new IntPtr[BufferCount];
    private readonly IntPtr[] _data = new IntPtr[BufferCount];
    private readonly bool[] _queued = new bool[BufferCount];
    private readonly Thread _thread;
    private volatile bool _stopping;
    private bool _disposed;

    private WindowsSynthOutput(SoundSynth synth, IntPtr device)
    {
        _synth = synth;
        _device = device;
        for (var i = 0; i < BufferCount; i++)
        {
            _data[i] = Marshal.AllocHGlobal(BufferSamples * sizeof(short));
            _headers[i] = Marshal.AllocHGlobal(HeaderSize);
            Marshal.StructureToPtr(new WaveHeader { Data = _data[i], BufferLength = BufferSamples * sizeof(short) }, _headers[i], false);
            waveOutPrepareHeader(_device, _headers[i], HeaderSize);
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Chapter sound", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    /// <summary>Opens the default device for <paramref name="synth"/>, or null when Windows has none to give.</summary>
    public static WindowsSynthOutput? TryOpen(SoundSynth synth)
    {
        var format = new WaveFormat
        {
            FormatTag = 1,
            Channels = 1,
            SamplesPerSecond = synth.SampleRate,
            AverageBytesPerSecond = synth.SampleRate * sizeof(short),
            BlockAlign = sizeof(short),
            BitsPerSample = 16,
            Size = 0,
        };
        var result = waveOutOpen(out var device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, CallbackNull);
        if (result == 0)
        {
            return new WindowsSynthOutput(synth, device);
        }

        Trace.TraceWarning($"Chapter sound: waveOutOpen failed with {result}.");
        return null;
    }

    private void Run()
    {
        var samples = new float[BufferSamples];
        var pcm = new short[BufferSamples];
        while (!_stopping)
        {
            for (var i = 0; i < BufferCount && !_stopping; i++)
            {
                if (_queued[i] && (Marshal.ReadInt32(_headers[i], FlagsOffset) & WhdrDone) == 0)
                {
                    continue;
                }

                _synth.Render(samples);
                for (var s = 0; s < BufferSamples; s++)
                {
                    pcm[s] = (short)Math.Clamp(samples[s] * 32_000f, -32_767f, 32_767f);
                }

                Marshal.Copy(pcm, 0, _data[i], BufferSamples);
                waveOutWrite(_device, _headers[i], HeaderSize);
                _queued[i] = true;
            }

            Thread.Sleep(4);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;
        _thread.Join(500);
        waveOutReset(_device);
        for (var i = 0; i < BufferCount; i++)
        {
            waveOutUnprepareHeader(_device, _headers[i], HeaderSize);
            Marshal.FreeHGlobal(_headers[i]);
            Marshal.FreeHGlobal(_data[i]);
        }

        waveOutClose(_device);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public short FormatTag;
        public short Channels;
        public int SamplesPerSecond;
        public int AverageBytesPerSecond;
        public short BlockAlign;
        public short BitsPerSample;
        public short Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags;
        public int Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [LibraryImport("winmm.dll")]
    private static partial int waveOutOpen(out IntPtr device, int deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);

    [LibraryImport("winmm.dll")]
    private static partial int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);

    [LibraryImport("winmm.dll")]
    private static partial int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);

    [LibraryImport("winmm.dll")]
    private static partial int waveOutWrite(IntPtr device, IntPtr header, int size);

    [LibraryImport("winmm.dll")]
    private static partial int waveOutReset(IntPtr device);

    [LibraryImport("winmm.dll")]
    private static partial int waveOutClose(IntPtr device);
}
