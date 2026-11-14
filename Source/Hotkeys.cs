using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Amp
{
    internal enum AppCommand
    {
        None,
        Settings,
        Fullscreen,
        Quit
    }

    public sealed class HotkeyBindings
    {
        public string GlobalSettings = "Ctrl+Alt+F1";
        public string Settings = "F1";
        public string Fullscreen = "F11";
        public string FullscreenAlternate = "Alt+Enter";
        public string Quit = "Ctrl+Q";
        public HotkeyBindings Clone()
        {
            return (HotkeyBindings)MemberwiseClone();
        }

        internal static readonly string[] Fields =
        {
            "GlobalSettings",
            "Settings",
            "Fullscreen",
            "FullscreenAlternate",
            "Quit"
        };
        internal static readonly string[] Labels =
        {
            "Open settings / unlock (global)",
            "Show / hide settings (focused)",
            "Fullscreen (focused)",
            "Fullscreen alternative (focused)",
            "Quit (focused)"
        };
        internal string Get(string field)
        {
            return (string)typeof(HotkeyBindings).GetField(field).GetValue(this);
        }

        internal void Set(string field, string value)
        {
            typeof(HotkeyBindings).GetField(field).SetValue(this, value);
        }

        internal bool Validate(out string error)
        {
            var used = new HashSet<Keys>();
            foreach (string field in Fields)
            {
                Keys key;
                string value = Get(field);
                if (string.IsNullOrWhiteSpace(value) && field != "GlobalSettings")
                    continue;
                if (!Shortcut.TryParse(value, out key))
                {
                    error = "Choose a valid shortcut for " + Labels[Array.IndexOf(Fields, field)] + ".";
                    return false;
                }

                Keys code = key & Keys.KeyCode;
                if (code == Keys.Escape || code == Keys.Tab || key == (Keys.Alt | Keys.F4) || key == (Keys.Control | Keys.Alt | Keys.Delete))
                {
                    error = "Esc, Tab, Alt+F4 and Ctrl+Alt+Delete are reserved.";
                    return false;
                }

                if (field == "GlobalSettings" && ((key & (Keys.Control | Keys.Alt)) == 0 || code == Keys.F12))
                {
                    error = "The global recovery shortcut must include Ctrl or Alt and cannot use F12 (reserved by Windows).";
                    return false;
                }

                if (!used.Add(key))
                {
                    error = "Each action needs a different shortcut.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal void Normalize()
        {
            string error;
            if (!Validate(out error))
            {
                var defaults = new HotkeyBindings();
                foreach (string field in Fields)
                    Set(field, defaults.Get(field));
                return;
            }

            foreach (string field in Fields)
            {
                Keys key;
                Set(field, Shortcut.TryParse(Get(field), out key) ? Shortcut.Format(key) : "");
            }
        }

        internal AppCommand Resolve(Keys key)
        {
            if (Shortcut.Matches(Settings, key))
                return AppCommand.Settings;
            if (Shortcut.Matches(Fullscreen, key) || Shortcut.Matches(FullscreenAlternate, key))
                return AppCommand.Fullscreen;
            if (Shortcut.Matches(Quit, key))
                return AppCommand.Quit;
            return AppCommand.None;
        }
    }

    internal static class Shortcut
    {
        static readonly KeysConverter converter = new KeysConverter();
        public static bool TryParse(string text, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 64)
                return false;
            try
            {
                key = (Keys)converter.ConvertFromInvariantString(text);
            }
            catch
            {
                return false;
            }

            Keys code = key & Keys.KeyCode;
            if ((key & ~(Keys.KeyCode | Keys.Control | Keys.Alt | Keys.Shift)) != 0)
                return false;
            if ((int)code < 8 || (int)code > 254 || code == Keys.Packet || code == Keys.ProcessKey)
                return false;
            return Enum.IsDefined(typeof(Keys), code) && code != Keys.None && code != Keys.ControlKey && code != Keys.ShiftKey && code != Keys.Menu && code != Keys.LControlKey && code != Keys.RControlKey && code != Keys.LShiftKey && code != Keys.RShiftKey && code != Keys.LMenu && code != Keys.RMenu && code != Keys.LWin && code != Keys.RWin;
        }

        public static string Format(Keys key)
        {
            return key == Keys.None ? "" : converter.ConvertToInvariantString(key);
        }

        public static bool Matches(string text, Keys key)
        {
            Keys parsed;
            return TryParse(text, out parsed) && parsed == key;
        }

        public static uint Modifiers(Keys key)
        {
            return (uint)(((key & Keys.Alt) != 0 ? 1 : 0) | ((key & Keys.Control) != 0 ? 2 : 0) | ((key & Keys.Shift) != 0 ? 4 : 0));
        }

        public static string Display(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "None" : text;
        }
    }

    internal interface IHotkeyRegistrar
    {
        bool Register(IntPtr window, int id, Keys key, out string error);
        void Unregister(IntPtr window, int id);
    }

    internal sealed class WindowsHotkeyRegistrar : IHotkeyRegistrar
    {
        public bool Register(IntPtr window, int id, Keys key, out string error)
        {
            if (RegisterHotKey(window, id, Shortcut.Modifiers(key) | 0x4000, (uint)(key & Keys.KeyCode)))
            {
                error = null;
                return true;
            }

            int code = Marshal.GetLastWin32Error();
            error = new Win32Exception(code).Message + " (Win32 " + code + ")";
            return false;
        }

        public void Unregister(IntPtr window, int id)
        {
            UnregisterHotKey(window, id);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr window, int id);
    }

    // Registers a replacement before releasing the working shortcut, so conflicts cannot strand the user.
    internal sealed class RecoveryHotkey : IDisposable
    {
        readonly IHotkeyRegistrar registrar;
        readonly IntPtr window;
        int id;
        Keys active;
        public bool Registered
        {
            get
            {
                return id != 0;
            }
        }

        public RecoveryHotkey(IntPtr window, IHotkeyRegistrar registrar)
        {
            this.window = window;
            this.registrar = registrar;
        }

        public bool TrySet(string text, out string error)
        {
            Keys key;
            if (!Shortcut.TryParse(text, out key))
            {
                error = "Invalid global shortcut.";
                return false;
            }

            if (Registered && key == active)
            {
                error = null;
                return true;
            }

            int next = id == 0x4A11 ? 0x4A12 : 0x4A11;
            if (!registrar.Register(window, next, key, out error))
                return false;
            if (id != 0)
                registrar.Unregister(window, id);
            id = next;
            active = key;
            return true;
        }

        public bool Owns(int candidate)
        {
            return Registered && id == candidate;
        }

        public void Dispose()
        {
            if (id != 0)
            {
                registrar.Unregister(window, id);
                id = 0;
            }
        }
    }

    internal static class InteractionPolicy
    {
        public static bool ClickThrough(bool requested, bool recoveryAvailable, bool settingsOpen)
        {
            return requested && recoveryAvailable && !settingsOpen;
        }

        public static bool Layered(int opacity, bool clickThrough)
        {
            return opacity < 100 || clickThrough;
        }
    }
}
