using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace Amp
{
    internal static class Benchmark
    {
        public static int Run(string report, string settingsPath)
        {
            try
            {
                var samples = new float[65536];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = (float)(.14 * Math.Sin(2 * Math.PI * 110 * i / 48000) + .07 * Math.Sin(2 * Math.PI * 1500 * i / 48000) + .025 * Math.Sin(2 * Math.PI * 3300 * i / 48000));
                var text = new StringBuilder("Case\tFrames\tWallMsPerFrame\tCpuMsPerFrame\tGen0Collections\r\n");
                string folder = Path.GetDirectoryName(Path.GetFullPath(report));
                Directory.CreateDirectory(folder);
                foreach (string view in new[]
                {
                    "Both",
                    "Bars",
                    "Oscilloscope"
                }

                )
                {
                    var p = Preferences.Read(settingsPath);
                    p.View = view;
                    var renderer = new SceneRenderer();
                    using (var bitmap = new Bitmap(p.Width, p.Height, PixelFormat.Format32bppPArgb))
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        for (int i = 0; i < 60; i++)
                            renderer.Draw(g, bitmap.Size, p, samples, 48000, 1.0 / 60, 1);
                        g.Flush(FlushIntention.Sync);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        using (var process = Process.GetCurrentProcess())
                        {
                            process.Refresh();
                            double start = process.TotalProcessorTime.TotalMilliseconds;
                            int gen0 = GC.CollectionCount(0);
                            var clock = Stopwatch.StartNew();
                            for (int i = 0; i < 360; i++)
                                renderer.Draw(g, bitmap.Size, p, samples, 48000, 1.0 / 60, 1);
                            g.Flush(FlushIntention.Sync);
                            clock.Stop();
                            process.Refresh();
                            text.AppendLine(view + "\t360\t" + (clock.Elapsed.TotalMilliseconds / 360).ToString("0.000", CultureInfo.InvariantCulture) + "\t" + ((process.TotalProcessorTime.TotalMilliseconds - start) / 360).ToString("0.000", CultureInfo.InvariantCulture) + "\t" + (GC.CollectionCount(0) - gen0));
                        }

                        bitmap.Save(Path.Combine(folder, Path.GetFileNameWithoutExtension(report) + "-" + view + ".png"));
                    }

                    var disposable = (object)renderer as IDisposable;
                    if (disposable != null)
                        disposable.Dispose();
                }

                File.WriteAllText(report, text.ToString());
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(report, "FAIL: " + ex);
                return 1;
            }
        }
    }
}
