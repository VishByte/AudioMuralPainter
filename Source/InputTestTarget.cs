using System;
using System.Drawing;
using System.Windows.Forms;

namespace Amp
{
    // Ordinary temporary test window. Mouse/keyboard input is supplied only by the desktop test tool.
    internal sealed class InputTestTarget : Form
    {
        public InputTestTarget()
        {
            Text = "AMP input test — clicks: 0";
            ClientSize = new Size(720, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 14);
            var button = new Button
            {
                Text = "Test click",
                Size = new Size(220, 90),
                Location = new Point(250, 155)
            };
            int count = 0;
            button.Click += delegate
            {
                count++;
                Text = "AMP input test — clicks: " + count;
                button.Text = "Clicks received: " + count;
            };
            Controls.Add(button);
            Controls.Add(new Label { Text = "Temporary window for verifying overlay click-through", Location = new Point(25, 25), Size = new Size(670, 65) });
        }
    }
}
