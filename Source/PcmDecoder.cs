using System;

namespace Amp
{
    internal static class PcmDecoder
    {
        internal static void Decode(byte[] bytes, float[] mono, int n, int channels, int align, int bits, bool floating)
        {
            if (bytes == null || mono == null)
                throw new ArgumentNullException(bytes == null ? "bytes" : "mono");
            if (n < 0 || n > mono.Length || channels < 1 || (floating ? bits != 32 && bits != 64 : bits != 8 && bits != 16 && bits != 24 && bits != 32) || (long)align < (long)channels * (bits / 8) || (long)n * align > bytes.Length)
                throw new ArgumentException("Invalid audio buffer dimensions or sample format.");
            int size = bits / 8;
            for (int frame = 0; frame < n; frame++)
            {
                double sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int p = frame * align + ch * size;
                    double value;
                    if (floating)
                        value = bits == 64 ? BitConverter.ToDouble(bytes, p) : BitConverter.ToSingle(bytes, p);
                    else if (bits == 8)
                        value = (bytes[p] - 128) / 128.0;
                    else if (bits == 16)
                        value = BitConverter.ToInt16(bytes, p) / 32768.0;
                    else if (bits == 24)
                        value = ((bytes[p] | (bytes[p + 1] << 8) | (bytes[p + 2] << 16)) << 8 >> 8) / 8388608.0;
                    else
                        value = BitConverter.ToInt32(bytes, p) / 2147483648.0;
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                        sum += value;
                }

                mono[frame] = (float)Math.Max(-1, Math.Min(1, sum / channels));
            }
        }
    }
}
