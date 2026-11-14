using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Amp
{
    // Reuses a top-down premultiplied DIB, rather than allocating GDI handles each frame.
    internal sealed class LayeredSurface : IDisposable
    {
        const int ExtendedStyleIndex = -20;
        const long LayeredStyle = 0x00080000;
        const long TransparentStyle = 0x00000020;
        const uint RefreshWindowFrame = 0x0020 | 0x0010 | 0x0004 | 0x0002 | 0x0001;
        IntPtr dc, bitmap, previous;
        Bitmap image;
        Graphics graphics;
        Size size;
        public Graphics Graphics
        {
            get
            {
                return graphics;
            }
        }

        internal static long WindowStyle(long current, bool layered, bool clickThrough)
        {
            long style = current & ~(LayeredStyle | TransparentStyle);
            if (layered || clickThrough)
                style |= LayeredStyle;
            if (clickThrough)
                style |= TransparentStyle;
            return style;
        }

        public static void SetWindowMode(IntPtr window, bool enabled, bool clickThrough)
        {
            long style = IntPtr.Size == 8 ? GetWindowLongPtr64(window, ExtendedStyleIndex).ToInt64() : GetWindowLong32(window, ExtendedStyleIndex);
            long next = WindowStyle(style, enabled, clickThrough);
            if (next == style)
                return;
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(window, ExtendedStyleIndex, new IntPtr(next));
            else
                SetWindowLong32(window, ExtendedStyleIndex, (int)next);
            long applied = IntPtr.Size == 8 ? GetWindowLongPtr64(window, ExtendedStyleIndex).ToInt64() : GetWindowLong32(window, ExtendedStyleIndex);
            const long interactionStyles = LayeredStyle | TransparentStyle;
            if ((applied & interactionStyles) != (next & interactionStyles))
                throw new Win32Exception("Windows could not apply the requested overlay interaction mode.");
            // FRAMECHANGED, NOACTIVATE, NOZORDER, NOMOVE, NOSIZE.
            if (!SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, RefreshWindowFrame))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Ensure(Size requested)
        {
            if (size == requested && image != null)
                return;
            Dispose();
            size = requested;
            dc = CreateCompatibleDC(IntPtr.Zero);
            var info = new BitmapInfo
            {
                Size = 40,
                Width = size.Width,
                Height = -size.Height,
                Planes = 1,
                BitCount = 32,
                Compression = 0
            };
            IntPtr bits;
            bitmap = CreateDIBSection(dc, ref info, 0, out bits, IntPtr.Zero, 0);
            if (dc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            previous = SelectObject(dc, bitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
            {
                previous = IntPtr.Zero;
                Dispose();
                throw new Win32Exception("Windows could not select the overlay surface.");
            }

            image = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppPArgb, bits);
            graphics = Graphics.FromImage(image);
        }

        public void Present(IntPtr window, Point location)
        {
            graphics.Flush();
            var destination = new NativePoint
            {
                X = location.X,
                Y = location.Y
            };
            var origin = new NativePoint();
            var extent = new NativeSize
            {
                Width = size.Width,
                Height = size.Height
            };
            var blend = new BlendFunction
            {
                Operation = 0,
                Flags = 0,
                ConstantAlpha = 255,
                AlphaFormat = 1
            };
            if (!UpdateLayeredWindow(window, IntPtr.Zero, ref destination, ref extent, dc, ref origin, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Dispose()
        {
            if (graphics != null)
            {
                graphics.Dispose();
                graphics = null;
            }

            if (image != null)
            {
                image.Dispose();
                image = null;
            }

            if (dc != IntPtr.Zero && previous != IntPtr.Zero)
            {
                SelectObject(dc, previous);
                previous = IntPtr.Zero;
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
                bitmap = IntPtr.Zero;
            }

            if (dc != IntPtr.Zero)
            {
                DeleteDC(dc);
                dc = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativePoint
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeSize
        {
            public int Width, Height;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BlendFunction
        {
            public byte Operation, Flags, ConstantAlpha, AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfo
        {
            public uint Size;
            public int Width, Height;
            public ushort Planes, BitCount;
            public uint Compression, SizeImage;
            public int XPels, YPels;
            public uint Used, Important;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong32(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        static extern int SetWindowLong32(IntPtr window, int index, int value);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr item);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref NativePoint location, ref NativeSize size, IntPtr source, ref NativePoint origin, uint key, ref BlendFunction blend, uint flags);
    }
}
