using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Amp {
    internal sealed class HotkeyDialog : Form {
        readonly Visualizer owner;
        readonly Dictionary<string,ShortcutBox> inputs=new Dictionary<string,ShortcutBox>();
        readonly Label error;
        public HotkeyDialog(Visualizer owner,HotkeyBindings current) {
            this.owner=owner;Text=AppInfo.FullName+" — Hotkeys";Icon=owner.Icon;
            BackColor=Color.FromArgb(14,19,16);ForeColor=Color.FromArgb(232,239,235);Font=new Font("Segoe UI",10);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(700,450);
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            var content=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=3,RowCount=9};
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,51));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,14));
            var intro=new Label {Text="Click a shortcut field, then press the keys you want. The global shortcut works while another app has focus.",Dock=DockStyle.Fill,AutoSize=false};content.Controls.Add(intro,0,0);content.SetColumnSpan(intro,3);content.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
            var defaults=new HotkeyBindings();
            for(int i=0;i<HotkeyBindings.Fields.Length;i++) {
                string field=HotkeyBindings.Fields[i];
                content.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
                var label=new Label {Text=HotkeyBindings.Labels[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9)};
                var box=new ShortcutBox {Text=current.Get(field),Dock=DockStyle.Fill,AccessibleName=HotkeyBindings.Labels[i],Margin=new Padding(2,8,8,8)};inputs.Add(field,box);
                var reset=new LinkLabel {Text="(default)",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=Color.FromArgb(147,164,155),Font=new Font("Segoe UI",8)};
                reset.LinkClicked+=delegate {box.Text=defaults.Get(field);};content.Controls.Add(label,0,i+1);content.Controls.Add(box,1,i+1);content.Controls.Add(reset,2,i+1);
            }
            var note=new Label {Text="Esc closes dialogs or leaves fullscreen. Alt+F4 closes the active window. These shortcuts stay fixed. Backspace clears an optional shortcut.",Dock=DockStyle.Fill,Font=new Font("Segoe UI",8.5f),ForeColor=Color.FromArgb(147,164,155)};content.Controls.Add(note,0,6);content.SetColumnSpan(note,3);content.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            error=new Label {Dock=DockStyle.Fill,ForeColor=Color.FromArgb(255,125,125),Font=new Font("Segoe UI",8.5f)};content.Controls.Add(error,0,7);content.SetColumnSpan(error,3);content.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var buttons=new FlowLayoutPanel {FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill,Height=38};
            var cancel=new Button {Text="Cancel",DialogResult=DialogResult.Cancel,Size=new Size(100,32)};
            var apply=new Button {Text="Apply",Size=new Size(100,32)};apply.Click+=delegate {Apply();};buttons.Controls.Add(cancel);buttons.Controls.Add(apply);content.Controls.Add(buttons,0,8);content.SetColumnSpan(buttons,3);content.RowStyles.Add(new RowStyle(SizeType.Absolute,38));CancelButton=cancel;Controls.Add(content);
        }
        void Apply() {
            var candidate=new HotkeyBindings();foreach(var entry in inputs)candidate.Set(entry.Key,entry.Value.Text);
            string message;if(!owner.TryApplyHotkeys(candidate,out message)){error.Text=message;return;}DialogResult=DialogResult.OK;Close();
        }
        internal sealed class ShortcutBox : TextBox {
            public ShortcutBox(){ReadOnly=true;BackColor=Color.FromArgb(28,35,31);ForeColor=Color.FromArgb(232,239,235);BorderStyle=BorderStyle.FixedSingle;ShortcutsEnabled=false;}
            protected override bool ProcessCmdKey(ref Message msg,Keys key) {
                if(key==Keys.Tab || key==(Keys.Shift|Keys.Tab) || key==Keys.Escape || key==(Keys.Alt|Keys.F4))return base.ProcessCmdKey(ref msg,key);
                if(key==Keys.Back || key==Keys.Delete){Text="";return true;}
                Keys code=key&Keys.KeyCode;
                if(code==Keys.ControlKey || code==Keys.ShiftKey || code==Keys.Menu || code==Keys.LWin || code==Keys.RWin)return true;
                string formatted=Shortcut.Format(key);Keys parsed;if(Shortcut.TryParse(formatted,out parsed))Text=formatted;return true;
            }
        }
    }
}
