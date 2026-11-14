using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace Amp
{
    // A form owns its explicitly created fonts; controls may share them safely.
    internal sealed class UiResources : IDisposable
    {
        readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        bool disposed;
        public Font GetFont(string family, float size)
        {
            if (disposed)
                throw new ObjectDisposedException("UiResources");
            string key = family + "/" + size.ToString(CultureInfo.InvariantCulture);
            Font font;
            if (!fonts.TryGetValue(key, out font))
            {
                font = new Font(family, size);
                fonts.Add(key, font);
            }

            return font;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (Font font in fonts.Values)
                font.Dispose();
            fonts.Clear();
        }
    }
}
