using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Lemon.Explore.Book.Chapters.Sound;

namespace Lemon.Explore.Book.Desktop.Sound;

/// <summary>
/// Streams a <see cref="SoundSynth"/> to the default output through an AudioToolbox audio queue: 32-bit float mono,
/// three buffers of 1024 samples, each refilled on the queue's own thread as it finishes playing.
/// When the queue cannot be made, <see cref="TryOpen"/> returns null and the chapter stays silent.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed unsafe partial class MacSynthOutput : IDisposable
{
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const int BufferCount = 3;
    private const int BufferSamples = 1024;

    // 'lpcm', and float | packed.
    private const uint LinearPcm = 0x6C70636D;
    private const uint FloatPacked = 0x1 | 0x8;

    private readonly SoundSynth _synth;
    private readonly GCHandle _self;
    private IntPtr _queue;
    private volatile bool _stopping;

    private MacSynthOutput(SoundSynth synth)
    {
        _synth = synth;
        _self = GCHandle.Alloc(this);
    }

    /// <summary>Starts a queue for <paramref name="synth"/>, or returns null when macOS has none to give.</summary>
    public static MacSynthOutput? TryOpen(SoundSynth synth)
    {
        var output = new MacSynthOutput(synth);
        var format = new StreamDescription
        {
            SampleRate = synth.SampleRate,
            FormatId = LinearPcm,
            FormatFlags = FloatPacked,
            BytesPerPacket = sizeof(float),
            FramesPerPacket = 1,
            BytesPerFrame = sizeof(float),
            ChannelsPerFrame = 1,
            BitsPerChannel = 32,
        };

        // A null run loop: the callbacks come on the queue's own thread.
        var status = AudioQueueNewOutput(ref format, &OnBufferDone, GCHandle.ToIntPtr(output._self), IntPtr.Zero, IntPtr.Zero, 0, out output._queue);
        if (status != 0)
        {
            Trace.TraceWarning($"Chapter sound: AudioQueueNewOutput failed with {status}.");
            output._self.Free();
            return null;
        }

        for (var i = 0; i < BufferCount; i++)
        {
            if (AudioQueueAllocateBuffer(output._queue, BufferSamples * sizeof(float), out var buffer) == 0)
            {
                output.Fill(buffer);
            }
        }

        status = AudioQueueStart(output._queue, IntPtr.Zero);
        if (status != 0)
        {
            Trace.TraceWarning($"Chapter sound: AudioQueueStart failed with {status}.");
            output.Dispose();
            return null;
        }

        return output;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnBufferDone(IntPtr user, IntPtr queue, IntPtr buffer)
    {
        if (GCHandle.FromIntPtr(user).Target is MacSynthOutput { _stopping: false } output)
        {
            output.Fill(buffer);
        }
    }

    /// <summary>Renders the next samples into a queue buffer and queues it again.</summary>
    private void Fill(IntPtr buffer)
    {
        var header = (QueueBuffer*)buffer;
        var samples = new Span<float>((void*)header->AudioData, BufferSamples);
        _synth.Render(samples);
        header->AudioDataByteSize = BufferSamples * sizeof(float);
        AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_queue == IntPtr.Zero)
        {
            return;
        }

        _stopping = true;
        AudioQueueStop(_queue, 1);
        AudioQueueDispose(_queue, 1);
        _queue = IntPtr.Zero;
        _self.Free();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamDescription
    {
        public double SampleRate;
        public uint FormatId;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    /// <summary>The start of an AudioQueueBuffer: how much it holds, its data, and how much of that is filled.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct QueueBuffer
    {
        public uint AudioDataBytesCapacity;
        public IntPtr AudioData;
        public uint AudioDataByteSize;
    }

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueNewOutput(ref StreamDescription format, delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> callback,
        IntPtr userData, IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, out IntPtr buffer);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint packetDescriptionCount, IntPtr packetDescriptions);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueStart(IntPtr queue, IntPtr startTime);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueStop(IntPtr queue, byte immediate);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueDispose(IntPtr queue, byte immediate);
}
