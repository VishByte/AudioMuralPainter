using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Amp {
    internal sealed class SettingsForm : Form {
        readonly Preferences prefs;
        readonly Visualizer owner;
        readonly Color ink=Color.FromArgb(232,239,235),muted=Color.FromArgb(147,164,155),surface=Color.FromArgb(28,35,31),accent=Color.FromArgb(85,255,170);
        readonly FlowLayoutPanel body;
        readonly Label title,status;
        readonly Label shortcutHelp;
        readonly PreviewControl preview;
        readonly Timer statusTimer;
        readonly Button[] navigation=new Button[7];
        readonly List<Action> bindings=new List<Action>();
        readonly ToolTip tips=new ToolTip {AutoPopDelay=12000};
        readonly LinkLabel sectionReset;
        readonly string[] sections={"Display","Bars","Oscilloscope","Response","Colors","Labels","Presets"};
        bool updating;
        int selected;
        CheckBox clickThroughCheck;
        HotkeyDialog hotkeyDialog;
        public SettingsForm(Visualizer owner,Preferences prefs) {
            SuspendLayout();this.owner=owner;this.prefs=prefs;
            Text=AppInfo.FullName+" Settings";Icon=owner.Icon;BackColor=Color.FromArgb(14,19,16);ForeColor=ink;
            Font=new Font("Segoe UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(880,740);MinimumSize=new Size(760,500);StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false;KeyPreview=true;MinimizeBox=false;
            var header=new Panel {Dock=DockStyle.Top,Height=78,Padding=new Padding(22,12,22,8)};
            header.Controls.Add(new Label {Text="AMP  /  SETTINGS",ForeColor=accent,Dock=DockStyle.Top,Height=24,Font=new Font("Segoe UI Semibold",11)});
            header.Controls.Add(new Label {Text="Changes are live. Click (default) beside any control to reset it.",ForeColor=muted,Dock=DockStyle.Bottom,Height=24,Font=new Font("Segoe UI",9)});
            var footer=new Panel {Dock=DockStyle.Bottom,Height=96,Padding=new Padding(22,8,22,10)};
            status=new Label {Dock=DockStyle.Top,Height=28,ForeColor=muted,Font=new Font("Segoe UI",8)};
            footer.Controls.Add(status);
            shortcutHelp=new Label {Dock=DockStyle.Bottom,Height=42,ForeColor=muted,Font=new Font("Segoe UI",8)};footer.Controls.Add(shortcutHelp);
            var main=new Panel {Dock=DockStyle.Fill,Padding=new Padding(0,0,18,0)};
            var nav=new FlowLayoutPanel {Dock=DockStyle.Left,Width=156,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14,0,8,0)};
            for(int i=0;i<sections.Length;i++) {int index=i;var button=Button(sections[i]);button.Size=new Size(130,38);button.Margin=new Padding(0,0,0,5);button.Click+=delegate {BuildPage(index);};nav.Controls.Add(button);navigation[i]=button;}
            var resetAll=new LinkLabel {Text="Reset visual settings",Size=new Size(130,30),LinkColor=muted,ActiveLinkColor=accent,Margin=new Padding(0,22,0,0),Font=new Font("Segoe UI",9)};
            resetAll.LinkClicked+=delegate {prefs.CopyFrom(new Preferences(),true);Changed();BuildPage(selected);};nav.Controls.Add(resetAll);
            var content=new Panel {Dock=DockStyle.Fill,Padding=new Padding(14,0,0,0)};
            var sectionHeading=new Panel {Dock=DockStyle.Top,Height=42};
            title=new Label {Dock=DockStyle.Fill,Font=new Font("Segoe UI",18),ForeColor=ink};
            sectionReset=new LinkLabel {Text="Reset section",Dock=DockStyle.Right,Width=110,LinkColor=muted,ActiveLinkColor=accent,TextAlign=ContentAlignment.MiddleRight,Font=new Font("Segoe UI",9)};
            sectionReset.LinkClicked+=delegate {prefs.Reset(SectionFields(selected));Changed();BuildPage(selected);};
            sectionHeading.Controls.Add(title);sectionHeading.Controls.Add(sectionReset);
            var previewHost=new Panel {Dock=DockStyle.Top,Height=140,Padding=new Padding(0,0,0,12)};
            preview=new PreviewControl(prefs) {Dock=DockStyle.Fill};previewHost.Controls.Add(preview);
            body=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(0,0,2,8)};
            body.SizeChanged+=delegate {FitRows();};
            content.Controls.Add(body);content.Controls.Add(previewHost);content.Controls.Add(sectionHeading);
            main.Controls.Add(content);main.Controls.Add(nav);
            Controls.Add(main);Controls.Add(footer);Controls.Add(header);
            statusTimer=new Timer {Interval=1000};statusTimer.Tick+=delegate {status.Text=owner.SaveError??owner.AudioStatus;};statusTimer.Start();status.Text=owner.AudioStatus;
            RefreshShortcutHelp();
            BuildPage(0);ResumeLayout(true);
        }
        protected override void OnShown(EventArgs e) {
            base.OnShown(e);Rectangle work=Screen.FromControl(owner).WorkingArea;
            Size=new Size(Math.Min(Width,work.Width-24),Math.Min(Height,work.Height-24));
            Location=new Point(Math.Max(work.Left,Math.Min(owner.Left+(owner.Width-Width)/2,work.Right-Width)),Math.Max(work.Top,Math.Min(owner.Top+(owner.Height-Height)/2,work.Bottom-Height)));
        }
        protected override void Dispose(bool disposing) {
            if(disposing) {if(statusTimer!=null)statusTimer.Dispose();if(tips!=null)tips.Dispose();}
            base.Dispose(disposing);
        }
        static string[] SectionFields(int page) {return new[]{Preferences.DisplayFields,Preferences.BarsFields,Preferences.ScopeFields,Preferences.ResponseFields,Preferences.ColorFields,Preferences.LabelFields,new string[0]}[page];}
        void FitRows() {int width=Math.Max(240,body.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-5);foreach(Control child in body.Controls)child.Width=width;}
        void Changed() {
            if(updating)return;prefs.Validate();owner.ApplyPreferences();
            updating=true;try {foreach(var binding in bindings)binding();} finally {updating=false;}
            preview.RefreshSettings();RefreshShortcutHelp();
        }
        void RefreshShortcutHelp() {shortcutHelp.Text="Settings: "+Shortcut.Display(prefs.Hotkeys.Settings)+" / Esc     Fullscreen: "+Shortcut.Display(prefs.Hotkeys.Fullscreen)+"     Quit: "+Shortcut.Display(prefs.Hotkeys.Quit)+Environment.NewLine+"Global settings / unlock: "+prefs.Hotkeys.GlobalSettings+"     •     Preferences save when settings close";}
        public void ShowHotkeys() {
            if(hotkeyDialog!=null && !hotkeyDialog.IsDisposed){hotkeyDialog.BringToFront();hotkeyDialog.Activate();return;}
            try {using(hotkeyDialog=new HotkeyDialog(owner,prefs.Hotkeys))if(hotkeyDialog.ShowDialog(this)==DialogResult.OK){RefreshShortcutHelp();if(selected==0)BuildPage(0);}}finally {hotkeyDialog=null;}
        }
        public void ActivateSettings() {
            Form active=hotkeyDialog!=null && !hotkeyDialog.IsDisposed?(Form)hotkeyDialog:this;
            active.BringToFront();active.Activate();
        }
        public void RefreshInteractionState() {
            if(clickThroughCheck==null || clickThroughCheck.IsDisposed)return;
            bool prior=updating;updating=true;try {clickThroughCheck.Checked=prefs.ClickThrough;clickThroughCheck.Enabled=owner.CanEnableClickThrough;}finally {updating=prior;}
        }
        object Get(string field) {return typeof(Preferences).GetField(field).GetValue(prefs);}
        void Set(string field,object value) {var f=typeof(Preferences).GetField(field);f.SetValue(prefs,Convert.ChangeType(value,f.FieldType));}
        Button Button(string text) {
            var b=new Button {Text=text,FlatStyle=FlatStyle.Flat,BackColor=surface,ForeColor=ink,UseVisualStyleBackColor=false,Cursor=Cursors.Hand,Height=34};b.FlatAppearance.BorderSize=0;return b;
        }
        void Row(string label,Control editor,string[] fields,int height) {
            var row=new TableLayoutPanel {Height=height,Margin=new Padding(0,0,0,3),ColumnCount=3,RowCount=1};
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,41));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,14));
            var caption=new Label {Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=ink,Font=new Font("Segoe UI",9),Margin=new Padding(0,0,6,0)};
            editor.Dock=DockStyle.Fill;editor.Margin=new Padding(0,6,5,5);
            var reset=new LinkLabel {Text="(default)",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=muted,ActiveLinkColor=accent,Font=new Font("Segoe UI",8),AccessibleName="Reset "+label+" to default",Margin=new Padding(0)};
            reset.LinkClicked+=delegate {prefs.Reset(fields);Changed();};
            row.Controls.Add(caption,0,0);row.Controls.Add(editor,1,0);row.Controls.Add(reset,2,0);body.Controls.Add(row);
        }
        void Heading(string text) {body.Controls.Add(new Label {Text=text,Height=31,ForeColor=accent,Font=new Font("Segoe UI Semibold",10),TextAlign=ContentAlignment.BottomLeft,Margin=new Padding(0,4,0,5)});}
        void Info(string text,int height) {body.Controls.Add(new Label {Text=text,Height=height,ForeColor=muted,Font=new Font("Segoe UI",8.5f),Margin=new Padding(0,0,0,8)});}
        void Choice(string label,string field,string[] options,Action after) {
            var combo=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=surface,ForeColor=ink,AccessibleName=label};combo.Items.AddRange(options);combo.SelectedItem=Get(field).ToString();
            bindings.Add(delegate {combo.SelectedItem=Get(field).ToString();});
            combo.SelectedIndexChanged+=delegate {if(updating)return;Set(field,combo.SelectedItem);if(after!=null)after();Changed();};
            Row(label,combo,new[]{field},40);
        }
        void Choice(string label,string field,params string[] options) {Choice(label,field,options,null);}
        CheckBox Check(string label,string field,Func<bool> enabled,Action after) {
            var box=new CheckBox {Text="Enabled",Checked=(bool)Get(field),ForeColor=ink,AccessibleName=label};
            bindings.Add(delegate {box.Checked=(bool)Get(field);box.Enabled=enabled==null || enabled();});
            box.CheckedChanged+=delegate {if(updating)return;Set(field,box.Checked);if(after!=null)after();Changed();};Row(label,box,new[]{field},39);return box;
        }
        void Check(string label,string field) {Check(label,field,null,null);}
        NumericUpDown Number(string label,string field,decimal min,decimal max,int decimals,decimal increment,Func<bool> enabled) {
            return Numeric(label,()=>Convert.ToDecimal(Get(field)),v=>Set(field,v),()=>min,()=>max,decimals,increment,new[]{field},enabled);
        }
        NumericUpDown Number(string label,string field,decimal min,decimal max) {return Number(label,field,min,max,0,1,null);}
        NumericUpDown Numeric(string label,Func<decimal> get,Action<decimal> set,Func<decimal> min,Func<decimal> max,int decimals,decimal increment,string[] fields,Func<bool> enabled) {
            var number=new NumericUpDown {Minimum=min(),Maximum=max(),DecimalPlaces=decimals,Increment=increment,BackColor=surface,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,TextAlign=HorizontalAlignment.Right,AccessibleName=label};
            Action refresh=delegate {number.Minimum=min();number.Maximum=max();number.Value=Math.Max(number.Minimum,Math.Min(number.Maximum,get()));number.Enabled=enabled==null || enabled();};refresh();bindings.Add(refresh);
            number.ValueChanged+=delegate {if(updating)return;set(number.Value);Changed();};Row(label,number,fields,40);return number;
        }
        void Slider(string label,string field,int min,int max) {
            var group=new TableLayoutPanel {ColumnCount=2,RowCount=1};group.RowStyles.Add(new RowStyle(SizeType.Percent,100));group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            var slider=new TrackBar {Minimum=min,Maximum=max,TickStyle=TickStyle.None,Dock=DockStyle.Fill,AutoSize=false,AccessibleName=label,Margin=new Padding(0)};
            var number=new NumericUpDown {Minimum=min,Maximum=max,Dock=DockStyle.Fill,BackColor=surface,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,TextAlign=HorizontalAlignment.Right,Margin=new Padding(6,1,0,0),AccessibleName=label+" value"};
            Action refresh=delegate {slider.Value=Convert.ToInt32(Get(field));number.Value=slider.Value;};refresh();bindings.Add(refresh);
            slider.ValueChanged+=delegate {if(updating)return;Set(field,slider.Value);Changed();};number.ValueChanged+=delegate {if(updating)return;Set(field,number.Value);Changed();};
            group.Controls.Add(slider,0,0);group.Controls.Add(number,1,0);Row(label,group,new[]{field},46);
        }
        void ColorRow(string label,string field) {
            var group=new TableLayoutPanel {ColumnCount=2,RowCount=1};group.RowStyles.Add(new RowStyle(SizeType.Percent,100));group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42));group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var swatch=Button("");swatch.Dock=DockStyle.Fill;swatch.Margin=new Padding(0,0,8,0);swatch.AccessibleName=label+" color picker";
            var input=new TextBox {Text="#"+Get(field),BackColor=surface,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,MaxLength=7,Dock=DockStyle.Fill,AccessibleName=label+" hex color",Margin=new Padding(0,1,0,0)};
            Action refresh=delegate {swatch.BackColor=Preferences.ColorOf((string)Get(field));if(!input.Focused)input.Text="#"+Get(field);};refresh();bindings.Add(refresh);
            input.TextChanged+=delegate {if(updating)return;Color c;if(Preferences.TryColor(input.Text,out c)) {Set(field,Preferences.Hex(c));input.ForeColor=ink;Changed();} else input.ForeColor=Color.FromArgb(255,115,115);};
            input.Leave+=delegate {input.Text="#"+Get(field);input.ForeColor=ink;};
            swatch.Click+=delegate {using(var dialog=new ColorDialog {Color=Preferences.ColorOf((string)Get(field)),FullOpen=true}) if(dialog.ShowDialog(this)==DialogResult.OK) {Set(field,Preferences.Hex(dialog.Color));input.Text="#"+Get(field);Changed();}};
            group.Controls.Add(swatch,0,0);group.Controls.Add(input,1,0);Row(label,group,new[]{field},43);
        }
        void BuildPage(int page) {
            selected=page;body.SuspendLayout();bindings.Clear();while(body.Controls.Count>0) {var control=body.Controls[0];body.Controls.RemoveAt(0);control.Dispose();}
            title.Text=sections[page];sectionReset.Visible=page<6;
            for(int i=0;i<navigation.Length;i++) {navigation[i].BackColor=i==page?Color.FromArgb(38,67,50):surface;navigation[i].ForeColor=i==page?accent:ink;}
            if(page==0)DisplayPage();if(page==1)BarsPage();if(page==2)ScopePage();if(page==3)ResponsePage();if(page==4)ColorsPage();if(page==5)LabelsPage();if(page==6)PresetsPage();
            updating=true;try {foreach(var binding in bindings)binding();}finally {updating=false;}
            FitRows();body.ResumeLayout(true);body.AutoScrollPosition=Point.Empty;
        }
        void DisplayPage() {
            Choice("Visualizer","View","Both","Oscilloscope","Bars");Slider("Background opacity (%)","BackgroundOpacity",0,100);
            Info("0 = clear, 100 = black. Visuals keep their brightness. Mouse interaction is controlled separately below.",36);
            clickThroughCheck=Check("Mouse click-through","ClickThrough",()=>owner.CanEnableClickThrough,null);
            Info("Global settings / unlock: "+prefs.Hotkeys.GlobalSettings+". Opening settings pauses click-through. Right-click the AMP tray icon for Open settings or Make AMP clickable.",48);
            var hotkeys=Button("Customize hotkeys…");hotkeys.Margin=new Padding(0,2,0,10);hotkeys.Click+=delegate {ShowHotkeys();};body.Controls.Add(hotkeys);
            if(!owner.CanEnableClickThrough)Info("Click-through becomes available when Windows reserves the global recovery shortcut. Use Customize hotkeys if the shortcut is in use.",44);
            var dimensions=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,BackColor=surface,ForeColor=ink,FlatStyle=FlatStyle.Flat,AccessibleName="Window size"};
            dimensions.Items.AddRange(new object[]{"Custom / current","800 × 480","1000 × 600","1280 × 720","1920 × 1080"});dimensions.SelectedIndex=0;
            dimensions.SelectedIndexChanged+=delegate {if(dimensions.SelectedIndex==0)return;string[] values=dimensions.SelectedItem.ToString().Split('×');owner.SetWindowSize(int.Parse(values[0].Trim()),int.Parse(values[1].Trim()));};
            // Window geometry is deliberately excluded from appearance presets.
            var sizeRow=new TableLayoutPanel {ColumnCount=2,Height=40,Margin=new Padding(0,0,0,3)};sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
            sizeRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            sizeRow.Controls.Add(new Label {Text="Window size",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9)},0,0);
            var sizeGroup=new TableLayoutPanel {ColumnCount=2,RowCount=1,Dock=DockStyle.Fill};sizeGroup.RowStyles.Add(new RowStyle(SizeType.Percent,100));sizeGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,75));sizeGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));dimensions.Dock=DockStyle.Fill;
            var sizeReset=new LinkLabel {Text="(default)",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=muted,Font=new Font("Segoe UI",8)};sizeReset.LinkClicked+=delegate {owner.SetWindowSize(1000,600);dimensions.SelectedIndex=0;};sizeGroup.Controls.Add(dimensions,0,0);sizeGroup.Controls.Add(sizeReset,1,0);sizeRow.Controls.Add(sizeGroup,1,0);body.Controls.Add(sizeRow);
            Choice("Maximum refresh rate (fps)","Fps",new[]{"30","60","120"},null);
            Number("Outer margin (logical px)","Padding",0,64);Number("Gap between views (px)","ViewGap",0,64);Number("Scope share of both views (%)","ScopeShare",15,85);Check("Always on top","OnTop");
            Info("Margins and view gap stay the same size as the window grows. Windows display scaling is respected.",36);
        }
        void BarsPage() {
            Heading("Frequency range");Number("Minimum frequency (Hz)","MinFrequency",1,95999,1,10,null);Number("Maximum frequency (Hz)","MaxFrequency",2,96000,1,100,null);
            Choice("Frequency spacing","FrequencyScale","Logarithmic","Linear");Number("Number of bars","Bars",4,256);
            Choice("Frequency resolution (FFT)","FftSize",new[]{"1024","2048","4096","8192","16384","32768"},null);
            Info("Labels follow the effective range. Frequencies are limited to what the playback device can represent. Larger FFT sizes improve bass detail with more latency.",48);
            Heading("Level range and cutoff");Number("Meter floor (dBFS)","MeterFloorDb",-120,-12);Check("Minimum response cutoff","GateEnabled");
            Number("Cutoff level (dBFS)","GateDb",-120,0,0,1,()=>prefs.GateEnabled);Number("Soft cutoff width (dB)","GateSoftnessDb",0,24,0,1,()=>prefs.GateEnabled);
            Info("Cutoff uses the incoming level before sensitivity gain. 0 dB softness gives a hard cutoff; a wider edge fades quiet responses. Existing bars settle using release.",48);
            Heading("Shape");Check("Segmented bars","Segmented");Number("Space between bars (px)","BarGap",0,32,1,.5m,null);
            Number("Segment height (px)","SegmentHeight",1,32,1,.5m,()=>prefs.Segmented);Number("Segment gap (px)","SegmentGap",0,16,1,.5m,()=>prefs.Segmented);
            Check("Dim baseline segments","IdleBars");Number("Baseline opacity (%)","IdleOpacity",0,100,0,1,()=>prefs.IdleBars);Check("Peak markers","Peaks");
        }
        void ScopePage() {
            Number("Waveform detail (points)","WavePoints",128,8192);Number("Time window (ms)","TimeMs",1,200);
            Number("Line thickness (px)","WaveThickness",.5m,8,1,.1m,null);Slider("Glow strength (%)","WaveGlow",0,100);
            Info("A shorter time window zooms in on the wave. Detail controls drawing points. Time labels and the amplitude scale have separate toggles on Labels.",48);
        }
        void ResponsePage() {
            Check("Link both sensitivities","LinkSensitivity",null,delegate {if(!prefs.LinkSensitivity) {prefs.ScopeGain=prefs.Gain;prefs.BarGain=prefs.Gain;}});
            Number("Linked sensitivity (dB)","Gain",-18,36,0,1,()=>prefs.LinkSensitivity);
            Number("Scope sensitivity (dB)","ScopeGain",-18,36,0,1,()=>!prefs.LinkSensitivity);
            Number("Bars sensitivity (dB)","BarGain",-18,36,0,1,()=>!prefs.LinkSensitivity);
            Heading("Bar movement");Number("Attack (ms)","AttackMs",1,500);Number("Release / smoothing (ms)","ReleaseMs",10,3000);
            Info("Short attack catches transients. Long release produces a smoother fall. These timings stay consistent at different refresh rates.",38);
            Number("Peak hold (ms)","PeakHoldMs",0,3000,0,10,()=>prefs.Peaks);Number("Peak full-scale fall time (ms)","PeakFallMs",100,10000,0,100,()=>prefs.Peaks);
        }
        void ColorsPage() {
            Heading("Level colors and thresholds");ColorRow("Bars / low","LowColor");ColorRow("Bars / middle","MidColor");ColorRow("Bars / peak","HighColor");
            Choice("Threshold units","ThresholdUnit",new[]{"Percent","dBFS"},delegate {BeginInvoke(new Action(delegate {if(!IsDisposed && selected==4)BuildPage(4);}));});
            Func<double,decimal> value=pct=>(decimal)(prefs.ThresholdUnit=="Percent"?pct:prefs.ThresholdDb(pct));
            Action<bool,decimal> set=delegate(bool middle,decimal input) {
                double pct=prefs.ThresholdUnit=="Percent"?(double)input:prefs.ThresholdPercent((double)input);
                if(middle) {prefs.MidThreshold=pct;if(prefs.HighThreshold<=pct)prefs.HighThreshold=Math.Min(99,pct+1);}
                else {prefs.HighThreshold=pct;if(prefs.MidThreshold>=pct)prefs.MidThreshold=Math.Max(1,pct-1);}
            };
            Numeric("Middle color begins",()=>value(prefs.MidThreshold),v=>set(true,v),()=>value(1),()=>value(98),1,1,new[]{"MidThreshold"},null);
            Numeric("Peak color begins",()=>value(prefs.HighThreshold),v=>set(false,v),()=>value(2),()=>value(99),1,1,new[]{"HighThreshold"},null);
            var thresholdPreview=new ThresholdPreview(prefs) {Height=57,Margin=new Padding(0,5,0,8)};thresholdPreview.Changed+=delegate {Changed();};body.Controls.Add(thresholdPreview);bindings.Add(delegate {thresholdPreview.Invalidate();});
            Info("Drag the two markers or enter values. Percent and dBFS are two views of the same thresholds. dBFS here is the displayed level after sensitivity gain.",45);
            Heading("Gradients");Choice("Bar color style","BarStyle","Level colors","Gradient");Choice("Scope color style","ScopeStyle","Solid","Gradient");
            Choice("Gradient direction","GradientDirection","Vertical","Horizontal");Check("Reverse gradient direction","GradientReverse");Choice("Gradient coverage","GradientCoverage","Each bar","Bars view","Both views");
            Info("Vertical runs bottom → top; reverse runs top → bottom. Horizontal runs left → right. Both views uses the bar palette continuously across scope, gap, and bars; enable Gradient for both styles.",58);
            Heading("Oscilloscope palette");ColorRow("Scope / solid or gradient start","ScopeColor");ColorRow("Scope / gradient middle","ScopeGradientMid");ColorRow("Scope / gradient end","ScopeGradientEnd");
        }
        void LabelsPage() {
            Heading("Independent toggles");Check("Bars frequency labels","BarsLabels");Check("Scope time labels","ScopeTimeLabels");Check("Scope amplitude scale","ScopeAmplitudeLabels");Check("Bars guide grid","BarsGrid");Check("Scope guide grid","ScopeGrid");
            Heading("Appearance");ColorRow("Label / grid color","LabelColor");Number("Label size (px)","LabelSize",6,24);Slider("Label opacity (%)","LabelOpacity",0,100);Number("Maximum horizontal labels","LabelDensity",2,16);Slider("Grid opacity (%)","GridOpacity",0,100);
            Info("Labels adapt to the available width. Scope time runs from the start of the displayed window to 0 ms; amplitude is normalized from −1 to +1 after gain.",46);
        }
        void PresetsPage() {
            Info("Presets include appearance and response settings. Applying a preset keeps your current window size and position.",40);
            var picker=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,BackColor=surface,ForeColor=ink,FlatStyle=FlatStyle.Flat,Height=32,Margin=new Padding(0,2,0,8),AccessibleName="Preset"};
            picker.Items.AddRange(new object[]{"Default","Minimal scope","Classic meters","Continuous gradient","Glass"});foreach(string name in Presets.Names())picker.Items.Add("Saved: "+name);picker.SelectedIndex=0;body.Controls.Add(picker);
            var apply=Button("Apply selected preset");apply.Margin=new Padding(0,0,0,16);apply.Click+=delegate {try {string name=picker.SelectedItem.ToString();prefs.CopyFrom(name.StartsWith("Saved: ")?Preferences.Read(Presets.PathFor(name.Substring(7))):Presets.BuiltIn(name),true);Changed();status.Text="Applied "+name;}catch(Exception ex){Error(ex);}};body.Controls.Add(apply);
            Heading("Save your current setup");var nameInput=new TextBox {BackColor=surface,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,MaxLength=48,Text="My preset",Height=28,Margin=new Padding(0,3,0,8),AccessibleName="New preset name"};body.Controls.Add(nameInput);
            var save=Button("Save named preset");save.Click+=delegate {try {string name=nameInput.Text.Trim();string path=Presets.PathFor(name);if(File.Exists(path) && MessageBox.Show(this,"Replace the saved preset “"+name+"”?","AMP",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;Presets.Save(name,prefs);BuildPage(6);status.Text="Saved "+name;}catch(Exception ex){Error(ex);}};body.Controls.Add(save);
            Heading("Share or back up");var import=Button("Import preset…");import.Margin=new Padding(0,4,0,8);import.Click+=delegate {using(var dialog=new OpenFileDialog {Filter="AMP presets (*.xml)|*.xml",Title="Import AMP preset"}) if(dialog.ShowDialog(this)==DialogResult.OK)try {prefs.CopyFrom(Preferences.Read(dialog.FileName),true);Changed();status.Text="Imported preset. Use Save named preset to keep it in your library.";}catch(Exception ex){Error(ex);}};body.Controls.Add(import);
            var export=Button("Export current preset…");export.Click+=delegate {using(var dialog=new SaveFileDialog {Filter="AMP presets (*.xml)|*.xml",DefaultExt="xml",FileName="AMP preset.xml",Title="Export AMP preset"})if(dialog.ShowDialog(this)==DialogResult.OK)try {prefs.Write(dialog.FileName);status.Text="Preset exported.";}catch(Exception ex){Error(ex);}};body.Controls.Add(export);
        }
        void Error(Exception ex) {MessageBox.Show(this,ex.Message,"AMP",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        protected override bool ProcessCmdKey(ref Message msg,Keys key) {
            if(key==Keys.Escape) {Close();return true;}
            if(owner.DispatchShortcut(key))return true;
            return base.ProcessCmdKey(ref msg,key);
        }
    }

    internal sealed class ThresholdPreview : Control {
        readonly Preferences prefs;int dragging;
        public event EventHandler Changed;
        public ThresholdPreview(Preferences prefs) {this.prefs=prefs;DoubleBuffered=true;Cursor=Cursors.SizeWE;AccessibleName="Drag bar color threshold markers";}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;float width=Math.Max(1,Width-12),x=6,y=8,h=18;
            using(var low=new SolidBrush(Preferences.ColorOf(prefs.LowColor)))using(var mid=new SolidBrush(Preferences.ColorOf(prefs.MidColor)))using(var high=new SolidBrush(Preferences.ColorOf(prefs.HighColor))) {
                float middle=x+width*(float)prefs.MidThreshold/100,peak=x+width*(float)prefs.HighThreshold/100;
                g.FillRectangle(low,x,y,middle-x,h);g.FillRectangle(mid,middle,y,peak-middle,h);g.FillRectangle(high,peak,y,x+width-peak,h);
                using(var pen=new Pen(Color.White,2)) {g.DrawLine(pen,middle,y-4,middle,y+h+5);g.DrawLine(pen,peak,y-4,peak,y+h+5);}
                using(var brush=new SolidBrush(Color.FromArgb(147,164,155))) {
                    string unit=prefs.ThresholdUnit=="Percent"?"%":" dBFS";
                    double a=prefs.ThresholdUnit=="Percent"?prefs.MidThreshold:prefs.ThresholdDb(prefs.MidThreshold),b=prefs.ThresholdUnit=="Percent"?prefs.HighThreshold:prefs.ThresholdDb(prefs.HighThreshold);
                    g.DrawString("Middle "+a.ToString("0.#")+unit+"     Peak "+b.ToString("0.#")+unit,Font,brush,6,32);
                }
            }
        }
        protected override void OnMouseDown(MouseEventArgs e) {if(e.Button!=MouseButtons.Left)return;double pct=100.0*(e.X-6)/Math.Max(1,Width-12);dragging=Math.Abs(pct-prefs.MidThreshold)<Math.Abs(pct-prefs.HighThreshold)?1:2;Capture=true;UpdateValue(e.X);}
        protected override void OnMouseMove(MouseEventArgs e) {if(dragging!=0)UpdateValue(e.X);}
        protected override void OnMouseUp(MouseEventArgs e) {dragging=0;Capture=false;}
        void UpdateValue(int x) {
            double pct=Math.Round(1000.0*(x-6)/Math.Max(1,Width-12))/10;
            if(dragging==1)prefs.MidThreshold=Math.Max(1,Math.Min(prefs.HighThreshold-1,pct));else prefs.HighThreshold=Math.Max(prefs.MidThreshold+1,Math.Min(99,pct));
            Invalidate();if(Changed!=null)Changed(this,EventArgs.Empty);
        }
    }
    internal sealed class PreviewControl : Control {
        readonly Preferences prefs;readonly SceneRenderer renderer=new SceneRenderer();readonly Timer timer=new Timer {Interval=50};
        readonly float[] samples=new float[65536];double time;
        Bitmap frame;Graphics frameGraphics;
        readonly SolidBrush checkerA=new SolidBrush(Color.FromArgb(37,45,40)),checkerB=new SolidBrush(Color.FromArgb(50,59,53));
        public PreviewControl(Preferences prefs) {
            this.prefs=prefs;DoubleBuffered=true;timer.Tick+=delegate {time+=.05;Invalidate();};timer.Start();
            Disposed+=delegate {timer.Dispose();renderer.Dispose();if(frameGraphics!=null)frameGraphics.Dispose();if(frame!=null)frame.Dispose();checkerA.Dispose();checkerB.Dispose();};AccessibleName="Visualizer preview with example signal";
        }
        public void RefreshSettings() {renderer.InvalidateResources();Invalidate();}
        protected override void OnPaint(PaintEventArgs e) {
            if(Width<2 || Height<2)return;
            int used=Math.Min(samples.Length,Math.Max(prefs.FftSize,48000*prefs.TimeMs/1000+600));
            double level=.1+.065*Math.Sin(time*2);
            for(int i=samples.Length-used;i<samples.Length;i++) {
                double phase=(i+time*48000)/48000;
                samples[i]=(float)(level*Math.Sin(phase*2*Math.PI*110)+.065*Math.Sin(phase*2*Math.PI*440)+.025*Math.Sin(phase*2*Math.PI*1800)+.01*Math.Sin(phase*2*Math.PI*6500));
            }
            // Composite a real transparent render onto a checkerboard for a useful opacity preview.
            if(frame==null || frame.Size!=Size){if(frameGraphics!=null)frameGraphics.Dispose();if(frame!=null)frame.Dispose();frame=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);frameGraphics=Graphics.FromImage(frame);}
            renderer.Draw(frameGraphics,Size,prefs,samples,48000,.05f,1);
            for(int y=0;y<Height;y+=12)for(int x=0;x<Width;x+=12)e.Graphics.FillRectangle(((x/12+y/12)%2==0)?checkerA:checkerB,x,y,12,12);
            e.Graphics.DrawImageUnscaled(frame,0,0);
        }
    }
}
