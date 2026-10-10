using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RSImage2026H
{
    internal static class ImageAlgorithms
    {
        private sealed class Pixels
        {
            public readonly int Width;
            public readonly int Height;
            public readonly byte[] Data;

            public Pixels(int width, int height)
            {
                Width = width;
                Height = height;
                Data = new byte[checked(width * height * 4)];
            }

            public static Pixels Read(Bitmap bitmap)
            {
                Pixels result = new Pixels(bitmap.Width, bitmap.Height);
                using (Bitmap copy = new Bitmap(bitmap.Width, bitmap.Height,
                    PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(copy))
                        g.DrawImageUnscaled(bitmap, 0, 0);
                    BitmapData bits = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height),
                        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        for (int y = 0; y < copy.Height; y++)
                            Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride),
                                result.Data, y * copy.Width * 4, copy.Width * 4);
                    }
                    finally { copy.UnlockBits(bits); }
                }
                return result;
            }

            public Bitmap ToBitmap()
            {
                Bitmap result = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                try
                {
                    BitmapData bits = result.LockBits(new Rectangle(0, 0, Width, Height),
                        ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        for (int y = 0; y < Height; y++)
                            Marshal.Copy(Data, y * Width * 4,
                                IntPtr.Add(bits.Scan0, y * bits.Stride), Width * 4);
                    }
                    finally { result.UnlockBits(bits); }
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
        }

        private static byte Clip(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }

        private static int GrayAt(byte[] data, int p)
        {
            return (data[p + 2] * 299 + data[p + 1] * 587 + data[p] * 114) / 1000;
        }

        public static Bitmap Grayscale(Bitmap source)
        {
            Pixels p = Pixels.Read(source);
            for (int i = 0; i < p.Data.Length; i += 4)
                p.Data[i] = p.Data[i + 1] = p.Data[i + 2] = (byte)GrayAt(p.Data, i);
            return p.ToBitmap();
        }

        public static Bitmap Invert(Bitmap source)
        {
            Pixels p = Pixels.Read(source);
            for (int i = 0; i < p.Data.Length; i += 4)
                for (int c = 0; c < 3; c++) p.Data[i + c] = (byte)(255 - p.Data[i + c]);
            return p.ToBitmap();
        }

        public static Bitmap PseudoColor(Bitmap source)
        {
            Pixels p = Pixels.Read(source);
            for (int i = 0; i < p.Data.Length; i += 4)
            {
                int v = GrayAt(p.Data, i);
                p.Data[i] = Clip(255 - Math.Abs(v - 64) * 4);
                p.Data[i + 1] = Clip(255 - Math.Abs(v - 128) * 4);
                p.Data[i + 2] = Clip(255 - Math.Abs(v - 192) * 4);
            }
            return p.ToBitmap();
        }

        public static Bitmap Brightness(Bitmap source, int delta)
        {
            Pixels p = Pixels.Read(source);
            for (int i = 0; i < p.Data.Length; i += 4)
                for (int c = 0; c < 3; c++) p.Data[i + c] = Clip(p.Data[i + c] + delta);
            return p.ToBitmap();
        }

        public static Bitmap Gamma(Bitmap source, double gamma)
        {
            if (gamma <= 0) throw new ArgumentOutOfRangeException("gamma");
            Pixels p = Pixels.Read(source);
            byte[] map = new byte[256];
            for (int i = 0; i < 256; i++) map[i] = Clip(255 * Math.Pow(i / 255.0, gamma));
            for (int i = 0; i < p.Data.Length; i += 4)
                for (int c = 0; c < 3; c++) p.Data[i + c] = map[p.Data[i + c]];
            return p.ToBitmap();
        }

        public static Bitmap Equalize(Bitmap source)
        {
            Pixels p = Pixels.Read(source);
            int[,] hist = new int[3, 256];
            for (int i = 0; i < p.Data.Length; i += 4)
                for (int c = 0; c < 3; c++) hist[c, p.Data[i + c]]++;
            byte[,] map = new byte[3, 256];
            int count = p.Width * p.Height;
            for (int c = 0; c < 3; c++)
            {
                int cumulative = 0;
                for (int v = 0; v < 256; v++)
                {
                    cumulative += hist[c, v];
                    map[c, v] = Clip(255.0 * cumulative / count);
                }
            }
            for (int i = 0; i < p.Data.Length; i += 4)
                for (int c = 0; c < 3; c++) p.Data[i + c] = map[c, p.Data[i + c]];
            return p.ToBitmap();
        }

        public static Bitmap Blur(Bitmap source)
        {
            return Convolve(source, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }, 9);
        }

        public static Bitmap Sharpen(Bitmap source)
        {
            return Convolve(source, new[] { 0, -1, 0, -1, 5, -1, 0, -1, 0 }, 1);
        }

        private static Bitmap Convolve(Bitmap source, int[] kernel, int divisor)
        {
            Pixels input = Pixels.Read(source);
            Pixels output = new Pixels(input.Width, input.Height);
            for (int y = 0; y < input.Height; y++)
            for (int x = 0; x < input.Width; x++)
            {
                int target = (y * input.Width + x) * 4;
                for (int c = 0; c < 3; c++)
                {
                    int sum = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int sx = Math.Max(0, Math.Min(input.Width - 1, x + dx));
                        int sy = Math.Max(0, Math.Min(input.Height - 1, y + dy));
                        sum += input.Data[(sy * input.Width + sx) * 4 + c] *
                            kernel[(dy + 1) * 3 + dx + 1];
                    }
                    output.Data[target + c] = Clip((double)sum / divisor);
                }
                output.Data[target + 3] = 255;
            }
            return output.ToBitmap();
        }

        public static Bitmap Edges(Bitmap source)
        {
            Pixels input = Pixels.Read(source);
            Pixels output = new Pixels(input.Width, input.Height);
            int[] gx = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] gy = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };
            for (int y = 0; y < input.Height; y++)
            for (int x = 0; x < input.Width; x++)
            {
                int sx = 0, sy = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = Math.Max(0, Math.Min(input.Width - 1, x + dx));
                    int yy = Math.Max(0, Math.Min(input.Height - 1, y + dy));
                    int v = GrayAt(input.Data, (yy * input.Width + xx) * 4);
                    int k = (dy + 1) * 3 + dx + 1;
                    sx += v * gx[k]; sy += v * gy[k];
                }
                int p = (y * input.Width + x) * 4;
                output.Data[p] = output.Data[p + 1] = output.Data[p + 2] =
                    Clip(Math.Sqrt((double)sx * sx + (double)sy * sy));
                output.Data[p + 3] = 255;
            }
            return output.ToBitmap();
        }

        public static Bitmap Threshold(Bitmap source, int threshold)
        {
            Pixels p = Pixels.Read(source);
            for (int i = 0; i < p.Data.Length; i += 4)
                p.Data[i] = p.Data[i + 1] = p.Data[i + 2] =
                    GrayAt(p.Data, i) >= threshold ? (byte)255 : (byte)0;
            return p.ToBitmap();
        }

        public static Bitmap Morphology(Bitmap source, bool dilate)
        {
            Pixels input = Pixels.Read(source);
            Pixels output = new Pixels(input.Width, input.Height);
            for (int y = 0; y < input.Height; y++)
            for (int x = 0; x < input.Width; x++)
            {
                bool result = !dilate;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    bool foreground = xx >= 0 && yy >= 0 &&
                        xx < input.Width && yy < input.Height &&
                        GrayAt(input.Data, (yy * input.Width + xx) * 4) >= 128;
                    if (dilate) result |= foreground;
                    else result &= foreground;
                }
                int p = (y * input.Width + x) * 4;
                output.Data[p] = output.Data[p + 1] = output.Data[p + 2] =
                    result ? (byte)255 : (byte)0;
                output.Data[p + 3] = 255;
            }
            return output.ToBitmap();
        }

        public static Bitmap KMeans(Bitmap source, int clusters)
        {
            if (clusters < 2 || clusters > 8) throw new ArgumentOutOfRangeException("clusters");
            Pixels input = Pixels.Read(source);
            Pixels output = new Pixels(input.Width, input.Height);
            int pixelCount = input.Width * input.Height;
            int step = Math.Max(1, pixelCount / 30000);
            double[,] centers = new double[clusters, 3];
            for (int k = 0; k < clusters; k++)
            {
                int i = ((k * pixelCount / clusters) * 4);
                for (int c = 0; c < 3; c++) centers[k, c] = input.Data[i + c];
            }
            for (int iteration = 0; iteration < 10; iteration++)
            {
                double[,] sum = new double[clusters, 3];
                int[] count = new int[clusters];
                for (int i = 0; i < pixelCount; i += step)
                {
                    int k = Nearest(input.Data, i * 4, centers, clusters);
                    for (int c = 0; c < 3; c++) sum[k, c] += input.Data[i * 4 + c];
                    count[k]++;
                }
                for (int k = 0; k < clusters; k++)
                    if (count[k] > 0)
                        for (int c = 0; c < 3; c++) centers[k, c] = sum[k, c] / count[k];
            }
            for (int i = 0; i < pixelCount; i++)
            {
                int k = Nearest(input.Data, i * 4, centers, clusters);
                for (int c = 0; c < 3; c++) output.Data[i * 4 + c] = Clip(centers[k, c]);
                output.Data[i * 4 + 3] = 255;
            }
            return output.ToBitmap();
        }

        private static int Nearest(byte[] values, int offset, double[,] centers, int clusters)
        {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int k = 0; k < clusters; k++)
            {
                double distance = 0;
                for (int c = 0; c < 3; c++)
                {
                    double difference = values[offset + c] - centers[k, c];
                    distance += difference * difference;
                }
                if (distance < bestDistance) { bestDistance = distance; best = k; }
            }
            return best;
        }

        public static Bitmap Rotate(Bitmap source, RotateFlipType transform)
        {
            Bitmap result = (Bitmap)source.Clone();
            result.RotateFlip(transform);
            return result;
        }

        public static Bitmap Translate(Bitmap source, int dx, int dy)
        {
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Black);
                g.DrawImageUnscaled(source, dx, dy);
            }
            return result;
        }

        public static string Quality(Bitmap reference, Bitmap current)
        {
            if (reference.Width != current.Width || reference.Height != current.Height)
                throw new InvalidOperationException("质量评价要求两幅图像的尺寸相同。");
            Pixels a = Pixels.Read(reference);
            Pixels b = Pixels.Read(current);
            double squaredError = 0;
            int[] histogram = new int[256];
            for (int i = 0; i < a.Data.Length; i += 4)
            {
                for (int c = 0; c < 3; c++)
                {
                    int difference = a.Data[i + c] - b.Data[i + c];
                    squaredError += difference * difference;
                }
                histogram[GrayAt(b.Data, i)]++;
            }
            double mse = squaredError / (a.Width * (double)a.Height * 3);
            double psnr = mse == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255 / mse);
            double entropy = 0;
            double total = a.Width * (double)a.Height;
            foreach (int n in histogram)
            {
                if (n == 0) continue;
                double probability = n / total;
                entropy -= probability * Math.Log(probability, 2);
            }
            return string.Format("与原始合成图比较\nMSE：{0:F3}\nPSNR：{1:F2} dB\n当前灰度熵：{2:F3} bit",
                mse, psnr, entropy);
        }
    }
}
