using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Amp {
    // Windows shared-mode endpoint loopback; no microphone, driver, or external library.
    internal sealed class Loopback : IDisposable {
        readonly object sync = new object();
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
        public int SampleRate { get { return rate; } }
        public string Status { get { return status; } }
        public long Frames { get { return Interlocked.Read(ref frames); } }

        public Loopback() : this(true) { }
        internal Loopback(bool start) {
            if(!start)return;
            thread = new Thread(Run) { IsBackground = true, Name = "AMP playback capture" };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }
        public void Snapshot(float[] target) {
            bool hasSignal; Snapshot(target,target.Length,out hasSignal);
        }
        public bool HasRecentSignal(int required) {
            lock(sync) return count>0 && lastNonzeroFrame>=0 && frames-lastNonzeroFrame<required && Stopwatch.GetTimestamp()-lastPacket<=Stopwatch.Frequency/5;
        }
        public long Snapshot(float[] target,int required,out bool hasSignal) {
            required=Math.Max(1,Math.Min(target.Length,required));
            lock (sync) {
                hasSignal=false;
                if (Stopwatch.GetTimestamp() - lastPacket > Stopwatch.Frequency / 5) {Array.Clear(target,target.Length-required,required);return -revision-1;}
                int n = Math.Min(count, required);
                int start = (write - n + ring.Length) % ring.Length;
                int first=Math.Min(n,ring.Length-start);
                if(n<required) Array.Clear(target,target.Length-required,required-n);
                Array.Copy(ring,start,target,target.Length-n,first);
                if(first<n) Array.Copy(ring,0,target,target.Length-n+first,n-first);
                hasSignal=lastNonzeroFrame>=0 && frames-lastNonzeroFrame<required;
                return revision;
            }
        }
        void Append(float[] samples, int n) {
            lock (sync) {
                int keep=Math.Min(n,ring.Length),source=n-keep;
                int first=Math.Min(keep,ring.Length-write);
                Array.Copy(samples,source,ring,write,first);
                if(first<keep) Array.Copy(samples,source+first,ring,0,keep-first);
                write=(write+keep)%ring.Length;
                count = Math.Min(ring.Length, count + n);
                for(int i=n-1;i>=0;i--) if(samples[i]!=0) {lastNonzeroFrame=frames+i+1;break;}
                Interlocked.Add(ref frames,n);revision++;
                lastPacket = Stopwatch.GetTimestamp();
            }
        }
        void Clear() { lock(sync) { count = write = 0; lastPacket = 0;lastNonzeroFrame=-1;revision++; } }
        static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
        static void Release(object obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj); }
        static string EndpointId(IMMDevice device) {
            string id; Check(device.GetId(out id)); return id;
        }
        static IAudioClient ActivateClient(IMMDevice device) {
            object obj;Guid id=typeof(IAudioClient).GUID;Check(device.Activate(ref id,23,IntPtr.Zero,out obj));return (IAudioClient)obj;
        }
        void Run() {
            while (!stop.WaitOne(0)) {
                IMMDeviceEnumerator enumerator = null;
                IMMDevice device = null;
                IAudioClient client = null;
                IAudioCaptureClient capture = null;
                IntPtr format = IntPtr.Zero;
                bool started = false;
                AutoResetEvent ready=null;
                try {
                    Clear();
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
                    string id = EndpointId(device);
                    client=ActivateClient(device);
                    Check(client.GetMixFormat(out format));
                    var wave = (WaveFormat)Marshal.PtrToStructure(format, typeof(WaveFormat));
                    bool floating = wave.Tag == 3;
                    if (wave.Tag == 0xFFFE && wave.Extra >= 22) {
                        Guid subtype = (Guid)Marshal.PtrToStructure(IntPtr.Add(format, 24), typeof(Guid));
                        floating = subtype == new Guid("00000003-0000-0010-8000-00aa00389b71");
                        if (!floating && subtype != new Guid("00000001-0000-0010-8000-00aa00389b71"))
                            throw new NotSupportedException("Unsupported Windows audio mix format.");
                    } else if (wave.Tag != 1 && wave.Tag != 3) throw new NotSupportedException("Unsupported Windows audio mix format.");
                    if (wave.Channels < 1 || wave.Align < wave.Channels * (wave.Bits / 8) ||
                        (floating ? wave.Bits != 32 && wave.Bits != 64 : wave.Bits != 8 && wave.Bits != 16 && wave.Bits != 24 && wave.Bits != 32))
                        throw new NotSupportedException("Unsupported Windows audio sample size.");
                    rate = (int)wave.Rate;
                    // Windows 11 signals newly available loopback buffers instead of polling every 5 ms.
                    int initialized=client.Initialize(0,0x00060000,0,0,format,IntPtr.Zero);
                    if(initialized>=0) {
                        ready=new AutoResetEvent(false);
                        Check(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()));
                    } else {
                        // Retain a polling fallback for endpoints that reject event-driven capture.
                        Release(client);client=ActivateClient(device);
                        Check(client.Initialize(0,0x00020000,1000000,0,format,IntPtr.Zero));
                    }
                    Guid captureId = typeof(IAudioCaptureClient).GUID;
                    object service; Check(client.GetService(ref captureId, out service));
                    capture = (IAudioCaptureClient)service;
                    Check(client.Start()); started = true;
                    status = "Listening to default Windows playback  /  " + rate + " Hz";
                    byte[] bytes = new byte[0];
                    float[] mono = new float[0];
                    long nextEndpointCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    WaitHandle[] waits=ready==null?null:new WaitHandle[]{stop,ready};
                    while (ready==null ? !stop.WaitOne(10) : WaitHandle.WaitAny(waits,250)!=0) {
                        uint packet; Check(capture.GetNextPacketSize(out packet));
                        while (packet > 0) {
                            IntPtr data; uint n, flags; ulong pos, clock;
                            Check(capture.GetBuffer(out data, out n, out flags, out pos, out clock));
                            try {
                                int length = checked((int)n * wave.Align);
                                if (mono.Length < n) mono = new float[n];
                                if ((flags & 2) != 0) Array.Clear(mono, 0, (int)n);
                                else {
                                    if (bytes.Length < length) bytes = new byte[length];
                                    Marshal.Copy(data, bytes, 0, length);
                                    Decode(bytes, mono, (int)n, wave.Channels, wave.Align, wave.Bits, floating);
                                }
                                Append(mono, (int)n);
                            } finally { Check(capture.ReleaseBuffer(n)); }
                            Check(capture.GetNextPacketSize(out packet));
                        }
                        if (Stopwatch.GetTimestamp() >= nextEndpointCheck) {
                            IMMDevice current = null;
                            try {
                                Check(enumerator.GetDefaultAudioEndpoint(0, 1, out current));
                                if (EndpointId(current) != id) break;
                            } finally { Release(current); }
                            nextEndpointCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                        }
                    }
                } catch (Exception ex) {
                    status = "Playback unavailable; retrying. " + ex.Message;
                } finally {
                    if (started && client != null) { try { client.Stop(); } catch { } }
                    Release(capture); Release(client); Release(device); Release(enumerator);
                    if(ready!=null)ready.Dispose();
                    if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                    Clear();
                }
                stop.WaitOne(600);
            }
        }
        internal static void Decode(byte[] bytes, float[] mono, int n, int channels, int align, int bits, bool floating) {
            int size = bits / 8;
            for (int frame = 0; frame < n; frame++) {
                double sum = 0;
                for (int ch = 0; ch < channels; ch++) {
                    int p = frame * align + ch * size;
                    double value;
                    if (floating) value = bits == 64 ? BitConverter.ToDouble(bytes, p) : BitConverter.ToSingle(bytes, p);
                    else if (bits == 8) value = (bytes[p] - 128) / 128.0;
                    else if (bits == 16) value = BitConverter.ToInt16(bytes, p) / 32768.0;
                    else if (bits == 24) value = ((bytes[p] | (bytes[p+1] << 8) | (bytes[p+2] << 16)) << 8 >> 8) / 8388608.0;
                    else value = BitConverter.ToInt32(bytes, p) / 2147483648.0;
                    if (!double.IsNaN(value) && !double.IsInfinity(value)) sum += value;
                }
                mono[frame] = (float)Math.Max(-1, Math.Min(1, sum / channels));
            }
        }
        public void Dispose() {if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Set();if(thread==null || thread.Join(2500))stop.Dispose();}
    }

    internal sealed class Spectrum {
        double[] re, im, window;
        double windowSum;
        public double[] Magnitudes { get; private set; }
        public void Compute(float[] samples, int size) {
            if (re == null || re.Length != size) {
                re = new double[size]; im = new double[size]; window = new double[size];
                Magnitudes = new double[size / 2 + 1]; windowSum = 0;
                for (int i = 0; i < size; i++) { window[i] = .5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1))); windowSum += window[i]; }
            }
            for (int i = 0; i < size; i++) { re[i] = samples[samples.Length - size + i] * window[i]; im[i] = 0; }
            for (int i = 1, j = 0; i < size; i++) {
                int bit = size >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { double tmp = re[i]; re[i] = re[j]; re[j] = tmp; }
            }
            for (int len = 2; len <= size; len <<= 1) {
                double ar = Math.Cos(-2 * Math.PI / len), ai = Math.Sin(-2 * Math.PI / len);
                for (int i = 0; i < size; i += len) {
                    double wr = 1, wi = 0;
                    for (int j = 0; j < len / 2; j++) {
                        int a = i + j, b = a + len / 2;
                        double tr = wr * re[b] - wi * im[b], ti = wr * im[b] + wi * re[b];
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        double nr = wr * ar - wi * ai; wi = wr * ai + wi * ar; wr = nr;
                    }
                }
            }
            for (int i = 0; i < Magnitudes.Length; i++) Magnitudes[i] = Math.Sqrt(re[i]*re[i] + im[i]*im[i]) * 2 / windowSum;
        }
        public double Band(double low, double high, int rate, int size) {
            double first = Math.Max(1, low * size / rate);
            double last = Math.Min(Magnitudes.Length - 1, high * size / rate);
            if (last < first) return 0;
            double peak = Interpolate((first + last) * .5);
            for (int i = (int)Math.Ceiling(first); i <= (int)last; i++) peak = Math.Max(peak, Magnitudes[i]);
            return peak;
        }
        double Interpolate(double position) {
            int i = Math.Min(Magnitudes.Length - 2, (int)position);
            double part = position - i;
            return Magnitudes[i] * (1 - part) + Magnitudes[i + 1] * part;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack=2)]
    internal struct WaveFormat { public ushort Tag, Channels; public uint Rate, BytesPerSecond; public ushort Align, Bits, Extra; }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out IntPtr collection);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient {
        [PreserveSig] int Initialize(int mode, uint flags, long duration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }
    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong time);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
