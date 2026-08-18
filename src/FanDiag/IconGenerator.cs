using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace FanDiag
{
    public class IconGenerator
    {
        public static void GenerateAppIcon(string outputPath)
        {
            int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ICO Header
            bw.Write((short)0); // Reserved
            bw.Write((short)1); // Type: 1 = ICO
            bw.Write((short)sizes.Length); // Image count

            // Create bitmaps
            byte[][] pngBuffers = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    // Draw circular badge background
                    using (var brush = new LinearGradientBrush(
                        new Point(0, 0),
                        new Point(size, size),
                        Color.FromArgb(255, 20, 24, 32),
                        Color.FromArgb(255, 12, 74, 96)))
                    {
                        g.FillEllipse(brush, 1, 1, size - 2, size - 2);
                    }

                    // Outer cyan accent ring
                    using (var pen = new Pen(Color.FromArgb(255, 0, 180, 216), Math.Max(1.5f, size / 24f)))
                    {
                        g.DrawEllipse(pen, 2, 2, size - 4, size - 4);
                    }

                    // Center hub
                    float cx = size / 2f;
                    float cy = size / 2f;
                    float hubRadius = size * 0.16f;

                    // Fan Blades (4 curved aerodynamic blades)
                    int bladeCount = 4;
                    for (int b = 0; b < bladeCount; b++)
                    {
                        float angle = (float)(b * 2 * Math.PI / bladeCount);
                        using var bladePath = new GraphicsPath();
                        
                        PointF p0 = new PointF(cx + (float)Math.Cos(angle) * hubRadius, cy + (float)Math.Sin(angle) * hubRadius);
                        PointF p1 = new PointF(cx + (float)Math.Cos(angle + 0.4) * (size * 0.42f), cy + (float)Math.Sin(angle + 0.4) * (size * 0.42f));
                        PointF p2 = new PointF(cx + (float)Math.Cos(angle + 0.9) * (size * 0.38f), cy + (float)Math.Sin(angle + 0.9) * (size * 0.38f));
                        PointF p3 = new PointF(cx + (float)Math.Cos(angle + 0.5) * hubRadius, cy + (float)Math.Sin(angle + 0.5) * hubRadius);

                        bladePath.AddBezier(p0, p1, p2, p3);
                        bladePath.CloseFigure();

                        using var bladeBrush = new LinearGradientBrush(
                            p0, p2,
                            Color.FromArgb(255, 0, 210, 255),
                            Color.FromArgb(230, 0, 119, 182));
                        g.FillPath(bladeBrush, bladePath);
                    }

                    // Center Hub Fill
                    using (var hubBrush = new LinearGradientBrush(
                        new PointF(cx - hubRadius, cy - hubRadius),
                        new PointF(cx + hubRadius, cy + hubRadius),
                        Color.FromArgb(255, 40, 50, 65),
                        Color.FromArgb(255, 15, 20, 28)))
                    {
                        g.FillEllipse(hubBrush, cx - hubRadius, cy - hubRadius, hubRadius * 2, hubRadius * 2);
                    }

                    // Center Hub Inner Dot
                    using (var dotBrush = new SolidBrush(Color.FromArgb(255, 0, 230, 255)))
                    {
                        float dotR = hubRadius * 0.4f;
                        g.FillEllipse(dotBrush, cx - dotR, cy - dotR, dotR * 2, dotR * 2);
                    }
                }

                using var pngMs = new MemoryStream();
                bmp.Save(pngMs, ImageFormat.Png);
                pngBuffers[i] = pngMs.ToArray();
            }

            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                bw.Write((byte)(size >= 256 ? 0 : size)); // Width
                bw.Write((byte)(size >= 256 ? 0 : size)); // Height
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((short)1); // Color planes
                bw.Write((short)32); // Bits per pixel
                bw.Write(pngBuffers[i].Length); // Image size in bytes
                bw.Write(offset); // Image offset
                offset += pngBuffers[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngBuffers[i]);
            }

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outputPath, ms.ToArray());
            Console.WriteLine($"Icon saved to: {outputPath}");
        }
    }
}
