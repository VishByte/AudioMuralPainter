using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Amp;

internal static class DesktopSmoke
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr Style(IntPtr window, int index);
    [DllImport("user32.dll")]
    static extern uint GetGuiResources(IntPtr process, uint flags);
    static int passed;
    static string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop-checks.txt");
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        passed++;
        File.AppendAllText(report, "PASS " + message + Environment.NewLine);
    }

    static bool Through(Form form)
    {
        return (Style(form.Handle, -20).ToInt64() & 0x80020) == 0x80020;
    }

    [STAThread]
    static int Main()
    {
        File.WriteAllText(report, "");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var prefs = new Preferences
        {
            Width = 801,
            Height = 471,
            BackgroundOpacity = 0,
            ClickThrough = true
        };
        prefs.Hotkeys.GlobalSettings = "Ctrl+Alt+Shift+F9";
        Type type = typeof(Preferences).Assembly.GetType("Amp.Visualizer", true);
        Form form = (Form)Activator.CreateInstance(type, Private, null, new object[] { prefs, false }, null);
        Exception failure = null;
        form.Shown += delegate
        {
            form.BeginInvoke(new Action(delegate
            {
                try
                {
                    Check(Through(form), "Clear click-through uses actual Windows layered/transparent flags");
                    var apply = type.GetMethod("ApplyPreferences");
                    prefs.ClickThrough = false;
                    apply.Invoke(form, null);
                    Check(!Through(form), "Click-through turns off");
                    prefs.BackgroundOpacity = 100;
                    apply.Invoke(form, null);
                    Check((Style(form.Handle, -20).ToInt64() & 0x80020) == 0, "Opaque clickable mode removes layered and transparent flags");
                    prefs.ClickThrough = true;
                    apply.Invoke(form, null);
                    Check(Through(form), "Opaque click-through activates layering");
                    type.GetMethod("ShowSettings").Invoke(form, null);
                    Application.DoEvents();
                    Check(!Through(form) && prefs.ClickThrough, "Opening settings temporarily unlocks interaction");
                    var settings = (Form)type.GetField("settings", Private).GetValue(form);
                    settings.Close();
                    Application.DoEvents();
                    Check(Through(form), "Closing settings restores click-through");
                    typeof(Control).GetMethod("RecreateHandle", Private).Invoke(form, null);
                    Application.DoEvents();
                    File.AppendAllText(report, "INFO recreated style=" + Style(form.Handle, -20).ToInt64().ToString("X") + " requested=" + prefs.ClickThrough + " effective=" + type.GetProperty("ClickThroughActive").GetValue(form, null) + " status=" + type.GetProperty("AudioStatus").GetValue(form, null) + Environment.NewLine);
                    Check(Through(form), "Handle recreation restores recovery and layered interaction state");
                    FormClosingEventHandler cancel = delegate (object sender, FormClosingEventArgs args)
                    {
                        args.Cancel = true;
                    };
                    form.FormClosing += cancel;
                    form.Close();
                    form.FormClosing -= cancel;
                    Application.DoEvents();
                    Check(!form.IsDisposed && form.Visible && Through(form), "Cancelled close preserves the active window and click-through");
                    type.GetMethod("ShowSettings").Invoke(form, null);
                    Application.DoEvents();
                    settings = (Form)type.GetField("settings", Private).GetValue(form);
                    Check(settings != null && settings.Visible && !Through(form), "Settings remain usable after a cancelled close");
                    settings.Close();
                    Application.DoEvents();
                    Check(Through(form), "Settings recovery still restores click-through after a cancelled close");
                    prefs.ClickThrough = false;
                    apply.Invoke(form, null);
                    using (var process = Process.GetCurrentProcess())
                    {
                        for (int cycle = 0; cycle < 12; cycle++)
                        {
                            type.GetMethod("ShowSettings").Invoke(form, null);
                            Application.DoEvents();
                            settings = (Form)type.GetField("settings", Private).GetValue(form);
                            var page = settings.GetType().GetMethod("BuildPage", Private);
                            for (int section = 0; section < 7; section++)
                            {
                                page.Invoke(settings, new object[] { section });
                                settings.PerformLayout();
                            }

                            settings.Close();
                            Application.DoEvents();
                        }

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        uint gdi = GetGuiResources(process.Handle, 0), user = GetGuiResources(process.Handle, 1);
                        for (int cycle = 0; cycle < 12; cycle++)
                        {
                            type.GetMethod("ShowSettings").Invoke(form, null);
                            Application.DoEvents();
                            settings = (Form)type.GetField("settings", Private).GetValue(form);
                            var page = settings.GetType().GetMethod("BuildPage", Private);
                            for (int section = 0; section < 7; section++)
                            {
                                page.Invoke(settings, new object[] { section });
                                settings.PerformLayout();
                            }

                            settings.Close();
                            Application.DoEvents();
                        }

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        uint afterGdi = GetGuiResources(process.Handle, 0), afterUser = GetGuiResources(process.Handle, 1);
                        File.AppendAllText(report, "INFO GDI " + gdi + " -> " + afterGdi + "; USER " + user + " -> " + afterUser + Environment.NewLine);
                        Check(afterGdi <= gdi + 2 && afterUser <= user + 2, "Repeated settings/page rebuilds leave stable GUI resources after collection");
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    File.AppendAllText(report, "FAIL " + ex + Environment.NewLine);
                }
                finally
                {
                    form.Close();
                }
            }));
        };
        using (form)
            Application.Run(form);
        File.AppendAllText(report, "Completed " + passed + " desktop checks." + Environment.NewLine);
        return failure == null ? 0 : 1;
    }
}
