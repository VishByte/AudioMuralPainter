using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Amp
{
    // .NET Framework's array drawing wrappers marshal structures point by point.
    // Keep the same GDI+ renderer and state, but pass the blittable arrays directly.
    // Modern/changed runtimes use the public API if IntPtr handles are unavailable.
    internal static class NativeDrawing
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly PropertyInfo GraphicsProperty = typeof(Graphics).GetProperty("NativeGraphics", Flags);
        static readonly PropertyInfo PenProperty = typeof(Pen).GetProperty("NativePen", Flags);
        static readonly PropertyInfo BrushProperty = typeof(Brush).GetProperty("NativeBrush", Flags);
        static readonly FieldInfo GraphicsField = typeof(Graphics).GetField("nativeGraphics", Flags);
        static readonly FieldInfo PenField = typeof(Pen).GetField("nativePen", Flags);
        static readonly FieldInfo BrushField = typeof(Brush).GetField("nativeBrush", Flags);
        internal static bool Enabled = Available(GraphicsProperty, GraphicsField) && Available(PenProperty, PenField) && Available(BrushProperty, BrushField);
        static bool Available(PropertyInfo property, FieldInfo field)
        {
            return property != null && property.PropertyType == typeof(IntPtr) || field != null && field.FieldType == typeof(IntPtr);
        }

        static HandleRef Handle(object owner, PropertyInfo property, FieldInfo field)
        {
            IntPtr handle = property != null && property.PropertyType == typeof(IntPtr) ? (IntPtr)property.GetValue(owner, null) : (IntPtr)field.GetValue(owner);
            return new HandleRef(owner, handle);
        }

        public static void Lines(Graphics graphics, Pen pen, PointF[] points)
        {
            if (Enabled)
            {
                try
                {
                    if (GdipDrawLines(Handle(graphics, GraphicsProperty, GraphicsField), Handle(pen, PenProperty, PenField), points, points.Length) == 0)
                        return;
                    Enabled = false;
                }
                catch (Exception ex)
                {
                    if (!(ex is MemberAccessException || ex is TargetInvocationException || ex is System.Security.SecurityException || ex is InvalidCastException))
                        throw;
                    Enabled = false;
                }
            }

            graphics.DrawLines(pen, points);
        }

        public static void Rectangles(Graphics graphics, Brush brush, RectangleF[] rectangles, int count)
        {
            if (count == 0)
                return;
            if (Enabled)
            {
                try
                {
                    if (GdipFillRectangles(Handle(graphics, GraphicsProperty, GraphicsField), Handle(brush, BrushProperty, BrushField), rectangles, count) == 0)
                        return;
                    Enabled = false;
                }
                catch (Exception ex)
                {
                    if (!(ex is MemberAccessException || ex is TargetInvocationException || ex is System.Security.SecurityException || ex is InvalidCastException))
                        throw;
                    Enabled = false;
                }
            }

            if (count == rectangles.Length)
                graphics.FillRectangles(brush, rectangles);
            else
            {
                var exact = new RectangleF[count];
                Array.Copy(rectangles, exact, count);
                graphics.FillRectangles(brush, exact);
            }
        }

        [DllImport("gdiplus.dll", ExactSpelling = true)]
        static extern int GdipDrawLines(HandleRef graphics, HandleRef pen, [In] PointF[] points, int count);
        [DllImport("gdiplus.dll", ExactSpelling = true)]
        static extern int GdipFillRectangles(HandleRef graphics, HandleRef brush, [In] RectangleF[] rectangles, int count);
    }
}
