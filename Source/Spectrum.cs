using System;

namespace Amp
{
    internal sealed class Spectrum
    {
        double[] re, im, window;
        double windowSum;
        public double[] Magnitudes { get; private set; }

        public void Compute(float[] samples, int size)
        {
            if (samples == null)
                throw new ArgumentNullException("samples");
            if (size < 4 || size > samples.Length || (size & (size - 1)) != 0)
                throw new ArgumentOutOfRangeException("size");
            if (re == null || re.Length != size)
            {
                re = new double[size];
                im = new double[size];
                window = new double[size];
                Magnitudes = new double[size / 2 + 1];
                windowSum = 0;
                for (int i = 0; i < size; i++)
                {
                    window[i] = .5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1)));
                    windowSum += window[i];
                }
            }

            for (int i = 0; i < size; i++)
            {
                re[i] = samples[samples.Length - size + i] * window[i];
                im[i] = 0;
            }

            for (int i = 1, j = 0; i < size; i++)
            {
                int bit = size >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double tmp = re[i];
                    re[i] = re[j];
                    re[j] = tmp;
                }
            }

            for (int len = 2; len <= size; len <<= 1)
            {
                double ar = Math.Cos(-2 * Math.PI / len), ai = Math.Sin(-2 * Math.PI / len);
                for (int i = 0; i < size; i += len)
                {
                    double wr = 1, wi = 0;
                    for (int j = 0; j < len / 2; j++)
                    {
                        int a = i + j, b = a + len / 2;
                        double tr = wr * re[b] - wi * im[b], ti = wr * im[b] + wi * re[b];
                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                        double nr = wr * ar - wi * ai;
                        wi = wr * ai + wi * ar;
                        wr = nr;
                    }
                }
            }

            for (int i = 0; i < Magnitudes.Length; i++)
                Magnitudes[i] = Math.Sqrt(re[i] * re[i] + im[i] * im[i]) * (i == 0 || i == size / 2 ? 1 : 2) / windowSum;
        }

        public double Band(double low, double high, int rate, int size)
        {
            double first = Math.Max(1, low * size / rate);
            double last = Math.Min(Magnitudes.Length - 1, high * size / rate);
            if (last < first)
                return 0;
            double peak = Interpolate((first + last) * .5);
            for (int i = (int)Math.Ceiling(first); i <= (int)last; i++)
                peak = Math.Max(peak, Magnitudes[i]);
            return peak;
        }

        double Interpolate(double position)
        {
            int i = Math.Min(Magnitudes.Length - 2, (int)position);
            double part = position - i;
            return Magnitudes[i] * (1 - part) + Magnitudes[i + 1] * part;
        }
    }
}
