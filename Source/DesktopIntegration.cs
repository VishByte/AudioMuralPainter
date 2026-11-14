using System;
using System.Drawing;
using System.Windows.Forms;

namespace Amp {
    // Owns desktop input and tray lifetime; rendering/audio do not depend on these details.
    internal sealed class DesktopIntegration : IDisposable {
        readonly Preferences prefs;
        readonly RecoveryHotkey recovery;
        readonly Action openSettings,customize,quit,changed,save;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem state,settingsItem,clickableItem,throughItem;
        bool settingsOpen,disposed;
        public string RecoveryError {get;private set;}
        public bool RecoveryAvailable {get {return recovery.Registered;}}
        public bool EffectiveClickThrough {get {return InteractionPolicy.ClickThrough(prefs.ClickThrough,RecoveryAvailable,settingsOpen);}}
        public DesktopIntegration(Form owner,Preferences prefs,Action openSettings,Action customize,Action quit,Action changed,Action save) {
            this.prefs=prefs;this.openSettings=openSettings;this.customize=customize;this.quit=quit;this.changed=changed;this.save=save;
            recovery=new RecoveryHotkey(owner.Handle,new WindowsHotkeyRegistrar());
            menu=new ContextMenuStrip();state=new ToolStripMenuItem {Enabled=false};menu.Items.Add(state);menu.Items.Add(new ToolStripSeparator());
            settingsItem=new ToolStripMenuItem("Open settings");settingsItem.Click+=delegate {this.openSettings();};menu.Items.Add(settingsItem);
            clickableItem=new ToolStripMenuItem("Make AMP clickable");clickableItem.Click+=delegate {MakeClickable();};menu.Items.Add(clickableItem);
            throughItem=new ToolStripMenuItem("Mouse click-through");throughItem.Click+=delegate {SetClickThrough(!prefs.ClickThrough);};menu.Items.Add(throughItem);
            var keys=new ToolStripMenuItem("Customize hotkeys…");keys.Click+=delegate {this.customize();};menu.Items.Add(keys);
            menu.Items.Add(new ToolStripSeparator());var exit=new ToolStripMenuItem("Quit AMP");exit.Click+=delegate {this.quit();};menu.Items.Add(exit);
            menu.Opening+=delegate {Refresh();};
            tray=new NotifyIcon {Icon=owner.Icon??SystemIcons.Application,Text=AppInfo.ShortName+" — "+AppInfo.FullName,ContextMenuStrip=menu,Visible=true};
            tray.MouseDoubleClick+=delegate(object sender,MouseEventArgs e) {if(e.Button==MouseButtons.Left)this.openSettings();};
            string error;
            if(!recovery.TrySet(prefs.Hotkeys.GlobalSettings,out error)) {
                prefs.ClickThrough=false;RecoveryError="Cannot reserve "+prefs.Hotkeys.GlobalSettings+": "+error;
                tray.ShowBalloonTip(6000,AppInfo.FullName,"The recovery shortcut is unavailable. AMP stays clickable. Use the tray menu to customize hotkeys.",ToolTipIcon.Info);
            }
            Refresh();
        }
        public bool HandleMessage(int message,IntPtr id) {
            if(message!=0x312 || !recovery.Owns(id.ToInt32()))return false;openSettings();return true;
        }
        public void SetSettingsOpen(bool value) {settingsOpen=value;changed();Refresh();}
        public void Reconcile() {if(!RecoveryAvailable)prefs.ClickThrough=false;Refresh();}
        public void MakeClickable() {SetClickThrough(false);}
        public void SetClickThrough(bool value) {
            prefs.ClickThrough=value && RecoveryAvailable;changed();Refresh();save();
        }
        public bool TryApplyHotkeys(HotkeyBindings candidate,out string error) {
            if(!candidate.Validate(out error))return false;
            if(!recovery.TrySet(candidate.GlobalSettings,out error)) {error="Windows could not reserve "+candidate.GlobalSettings+". "+error+(recovery.Registered?" The previous shortcut remains active.":" AMP stays clickable.");return false;}
            candidate=candidate.Clone();candidate.Normalize();prefs.Hotkeys=candidate;RecoveryError=null;Refresh();save();return true;
        }
        void Refresh() {
            state.Text=EffectiveClickThrough?"AMP • click-through":settingsOpen && prefs.ClickThrough?"AMP • interaction unlocked for settings":"AMP • clickable";
            settingsItem.ShortcutKeyDisplayString=prefs.Hotkeys.GlobalSettings;
            throughItem.Checked=prefs.ClickThrough;throughItem.Enabled=RecoveryAvailable;clickableItem.Enabled=prefs.ClickThrough;
            string tooltip=EffectiveClickThrough?"AMP — click-through ("+prefs.Hotkeys.GlobalSettings+")":AppInfo.ShortName+" — "+AppInfo.FullName;
            tray.Text=tooltip.Length>63?tooltip.Substring(0,63):tooltip;
        }
        public void Dispose() {if(disposed)return;disposed=true;recovery.Dispose();tray.Visible=false;tray.Dispose();menu.Dispose();}
    }
}
