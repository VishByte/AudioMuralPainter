using System;
using System.IO;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle(Amp.AppInfo.FullName)]
[assembly: System.Reflection.AssemblyProduct(Amp.AppInfo.FullName)]
[assembly: System.Reflection.AssemblyDescription("Customizable Windows playback visualizer")]
[assembly: System.Reflection.AssemblyVersion(Amp.AppInfo.Version)]
namespace Amp
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--self-test")
                    return Tests.Run(args.Length > 1 ? args[1] : Path.Combine("work", "self-test", "checks.txt"));
                if (args.Length > 0 && args[0] == "--probe-audio")
                    return Tests.Probe(args.Length > 1 ? args[1] : Path.Combine("work", "audio-probe", "report.txt"));
                if (args.Length > 0 && args[0] == "--benchmark")
                    return Benchmark.Run(args.Length > 1 ? args[1] : Path.Combine("work", "benchmark", "results.tsv"), args.Length > 2 ? args[2] : Preferences.FilePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Diagnostic failed: " + ex.Message);
                return 1;
            }

            if (args.Length > 0 && args[0] != "--input-test-target")
            {
                Console.Error.WriteLine("Unknown command: " + args[0]);
                return 2;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--input-test-target")
            {
                Application.Run(new InputTestTarget());
                return 0;
            }

            Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, AppInfo.FullName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            try
            {
                Application.Run(new Visualizer(Preferences.Load()));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppInfo.FullName + " could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
