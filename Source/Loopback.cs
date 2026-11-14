using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Amp
{
    // Windows shared-mode endpoint loopback; no microphone, driver, or external library.
    internal sealed class Loopback : IDisposable
    {
        readonly object sync = new object ();
        readonly float[] ring = new float[65536];
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Thread thread;
        int write, count;
        long lastPacket;
        volatile int rate = 48000;
        volatile string status = "Connecting to Windows playback...";
        long frames;
        long lastNonzeroFrame = -1, revision;
        int disposed;
        public int SampleRate
        {
            get
            {
                return rate;
            }
        }

        public string Status
        {
            get
            {
                return status;
            }
        }

        public long Frames
        {
            get
            {
                return Interlocked.Read(ref frames);
            }
        }

        public Loopback() : this(true)
        {
        }

        internal Loopback(bool start)
        {
            if (!start)
                return;
            thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "AMP playback capture"
            };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        public void Snapshot(float[] target)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            bool hasSignal;
            Snapshot(target, target.Length, out hasSignal);
        }

        public bool HasRecentSignal(int required)
        {
            lock (sync)
                return count > 0 && lastNonzeroFrame >= 0 && frames - lastNonzeroFrame < required && Stopwatch.GetTimestamp() - lastPacket <= Stopwatch.Frequency / 5;
        }

        public long Snapshot(float[] target, int required, out bool hasSignal)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (target.Length == 0 || required < 1)
                throw new ArgumentOutOfRangeException("required");
            required = Math.Max(1, Math.Min(target.Length, required));
            lock (sync)
            {
                hasSignal = false;
                if (Stopwatch.GetTimestamp() - lastPacket > Stopwatch.Frequency / 5)
                {
                    Array.Clear(target, target.Length - required, required);
                    return -revision - 1;
                }

                int n = Math.Min(count, required);
                int start = (write - n + ring.Length) % ring.Length;
                int first = Math.Min(n, ring.Length - start);
                if (n < required)
                    Array.Clear(target, target.Length - required, required - n);
                Array.Copy(ring, start, target, target.Length - n, first);
                if (first < n)
                    Array.Copy(ring, 0, target, target.Length - n + first, n - first);
                hasSignal = lastNonzeroFrame >= 0 && frames - lastNonzeroFrame < required;
                return revision;
            }
        }

        void Append(float[] samples, int n)
        {
            lock (sync)
            {
                int keep = Math.Min(n, ring.Length), source = n - keep;
                int first = Math.Min(keep, ring.Length - write);
                Array.Copy(samples, source, ring, write, first);
                if (first < keep)
                    Array.Copy(samples, source + first, ring, 0, keep - first);
                write = (write + keep) % ring.Length;
                count = Math.Min(ring.Length, count + n);
                for (int i = n - 1; i >= 0; i--)
                    if (samples[i] != 0)
                    {
                        lastNonzeroFrame = frames + i + 1;
                        break;
                    }

                Interlocked.Add(ref frames, n);
                revision++;
                lastPacket = Stopwatch.GetTimestamp();
            }
        }

        void Clear()
        {
            lock (sync)
            {
                count = write = 0;
                lastPacket = 0;
                lastNonzeroFrame = -1;
                revision++;
            }
        }

        static void Check(int result)
        {
            if (result < 0)
                Marshal.ThrowExceptionForHR(result);
        }

        static void Release(object obj)
        {
            if (obj != null && Marshal.IsComObject(obj))
                Marshal.ReleaseComObject(obj);
        }

        static string EndpointId(IMMDevice device)
        {
            string id;
            Check(device.GetId(out id));
            return id;
        }

        static IAudioClient ActivateClient(IMMDevice device)
        {
            object obj;
            Guid id = typeof(IAudioClient).GUID;
            Check(device.Activate(ref id, 23, IntPtr.Zero, out obj));
            return (IAudioClient)obj;
        }

        void Run()
        {
            while (!stop.WaitOne(0))
            {
                IMMDeviceEnumerator enumerator = null;
                IMMDevice device = null;
                IAudioClient client = null;
                IAudioCaptureClient capture = null;
                IntPtr format = IntPtr.Zero;
                bool started = false;
                AutoResetEvent ready = null;
                try
                {
                    Clear();
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
                    string id = EndpointId(device);
                    client = ActivateClient(device);
                    Check(client.GetMixFormat(out format));
                    var wave = (WaveFormat)Marshal.PtrToStructure(format, typeof(WaveFormat));
                    bool floating = wave.Tag == 3;
                    if (wave.Tag == 0xFFFE && wave.Extra >= 22)
                    {
                        Guid subtype = (Guid)Marshal.PtrToStructure(IntPtr.Add(format, 24), typeof(Guid));
                        floating = subtype == new Guid("00000003-0000-0010-8000-00aa00389b71");
                        if (!floating && subtype != new Guid("00000001-0000-0010-8000-00aa00389b71"))
                            throw new NotSupportedException("Unsupported Windows audio mix format.");
                    }
                    else if (wave.Tag != 1 && wave.Tag != 3)
                        throw new NotSupportedException("Unsupported Windows audio mix format.");
                    if (wave.Rate < 8000 || wave.Rate > 384000 || wave.Channels < 1 || wave.Align < wave.Channels * (wave.Bits / 8) || (floating ? wave.Bits != 32 && wave.Bits != 64 : wave.Bits != 8 && wave.Bits != 16 && wave.Bits != 24 && wave.Bits != 32))
                        throw new NotSupportedException("Unsupported Windows audio sample size.");
                    rate = (int)wave.Rate;
                    // Windows 11 signals newly available loopback buffers instead of polling every 5 ms.
                    int initialized = client.Initialize(0, 0x00060000, 0, 0, format, IntPtr.Zero);
                    if (initialized >= 0)
                    {
                        ready = new AutoResetEvent(false);
                        Check(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()));
                    }
                    else
                    {
                        // Retain a polling fallback for endpoints that reject event-driven capture.
                        Release(client);
                        client = ActivateClient(device);
                        Check(client.Initialize(0, 0x00020000, 1000000, 0, format, IntPtr.Zero));
                    }

                    Guid captureId = typeof(IAudioCaptureClient).GUID;
                    object service;
                    Check(client.GetService(ref captureId, out service));
                    capture = (IAudioCaptureClient)service;
                    Check(client.Start());
                    started = true;
                    status = "Listening to default Windows playback  /  " + rate + " Hz";
                    byte[] bytes = new byte[0];
                    float[] mono = new float[0];
                    long nextEndpointCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    WaitHandle[] waits = ready == null ? null : new WaitHandle[]
                    {
                        stop,
                        ready
                    };
                    while (ready == null ? !stop.WaitOne(10) : WaitHandle.WaitAny(waits, 250) != 0)
                    {
                        uint packet;
                        Check(capture.GetNextPacketSize(out packet));
                        while (packet > 0)
                        {
                            if (stop.WaitOne(0))
                                break;
                            IntPtr data;
                            uint n, flags;
                            ulong pos, clock;
                            Check(capture.GetBuffer(out data, out n, out flags, out pos, out clock));
                            try
                            {
                                if ((flags & 1) != 0)
                                    Clear(); // Do not join samples across a capture discontinuity.
                                int length = checked((int)n * wave.Align);
                                if (mono.Length < n)
                                    mono = new float[n];
                                if ((flags & 2) != 0)
                                    Array.Clear(mono, 0, (int)n);
                                else
                                {
                                    if (bytes.Length < length)
                                        bytes = new byte[length];
                                    Marshal.Copy(data, bytes, 0, length);
                                    PcmDecoder.Decode(bytes, mono, (int)n, wave.Channels, wave.Align, wave.Bits, floating);
                                }

                                Append(mono, (int)n);
                            }
                            finally
                            {
                                Check(capture.ReleaseBuffer(n));
                            }

                            Check(capture.GetNextPacketSize(out packet));
                        }

                        if (Stopwatch.GetTimestamp() >= nextEndpointCheck)
                        {
                            IMMDevice current = null;
                            try
                            {
                                Check(enumerator.GetDefaultAudioEndpoint(0, 1, out current));
                                if (EndpointId(current) != id)
                                    break;
                            }
                            finally
                            {
                                Release(current);
                            }

                            nextEndpointCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                        }
                    }
                }
                catch (Exception ex)
                {
                    status = "Playback unavailable; retrying. " + ex.Message;
                }
                finally
                {
                    if (started && client != null)
                    {
                        try
                        {
                            client.Stop();
                        }
                        catch
                        {
                        }
                    }

                    Release(capture);
                    Release(client);
                    Release(device);
                    Release(enumerator);
                    if (ready != null)
                        ready.Dispose();
                    if (format != IntPtr.Zero)
                        Marshal.FreeCoTaskMem(format);
                    Clear();
                }

                stop.WaitOne(600);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            stop.Set();
            if (thread == null || thread.Join(2500))
                stop.Dispose();
        }
    }
}
