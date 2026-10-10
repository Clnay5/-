using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OSGeo.GDAL;

namespace RSImage2026H
{
    internal static class RasterRenderer
    {
        private const int MaxPreviewSide = 1600;

        public static Bitmap Composite(Dataset dataset, int redBand, int greenBand, int blueBand)
        {
            int width, height;
            GetPreviewSize(dataset, out width, out height);
            float[] red = ReadBand(dataset, redBand, width, height);
            float[] green = greenBand == redBand ? red : ReadBand(dataset, greenBand, width, height);
            float[] blue = blueBand == redBand ? red :
                blueBand == greenBand ? green : ReadBand(dataset, blueBand, width, height);

            Range r = GetRange(red);
            Range g = green == red ? r : GetRange(green);
            Range b = blue == red ? r : blue == green ? g : GetRange(blue);
            byte[] pixels = new byte[checked(width * height * 4)];
            for (int i = 0; i < red.Length; i++)
            {
                int p = i * 4;
                pixels[p] = Scale(blue[i], b);
                pixels[p + 1] = Scale(green[i], g);
                pixels[p + 2] = Scale(red[i], r);
                pixels[p + 3] = 255;
            }
            return CreateBitmap(width, height, pixels);
        }

        public static Bitmap SingleBand(Dataset dataset, int band)
        {
            return Composite(dataset, band, band, band);
        }

        public static Bitmap NormalizedDifference(Dataset dataset, int firstBand,
            int secondBand, bool mask, double threshold)
        {
            int width, height;
            GetPreviewSize(dataset, out width, out height);
            float[] first = ReadBand(dataset, firstBand, width, height);
            float[] second = ReadBand(dataset, secondBand, width, height);
            byte[] pixels = new byte[checked(width * height * 4)];
            for (int i = 0; i < first.Length; i++)
            {
                float a = first[i];
                float b = second[i];
                double denominator = a + b;
                double value = denominator == 0 || float.IsNaN(a) || float.IsNaN(b)
                    ? -1 : (a - b) / denominator;
                int p = i * 4;
                if (mask)
                {
                    byte v = value > threshold ? (byte)255 : (byte)0;
                    pixels[p] = pixels[p + 1] = pixels[p + 2] = v;
                }
                else
                {
                    // 负值为褐色，正值逐渐变绿；指数值仍保留在原始波段计算中。
                    double t = Math.Max(-1, Math.Min(1, value));
                    pixels[p] = (byte)(t < 0 ? 40 : 25);
                    pixels[p + 1] = (byte)(t < 0 ? 80 * (t + 1) : 80 + 175 * t);
                    pixels[p + 2] = (byte)(t < 0 ? 160 * -t : 80 * (1 - t));
                }
                pixels[p + 3] = 255;
            }
            return CreateBitmap(width, height, pixels);
        }

        private static void GetPreviewSize(Dataset dataset, out int width, out int height)
        {
            if (dataset.RasterXSize <= 0 || dataset.RasterYSize <= 0)
                throw new InvalidOperationException("影像尺寸无效。");
            double scale = Math.Min(1.0, Math.Min(
                (double)MaxPreviewSide / dataset.RasterXSize,
                (double)MaxPreviewSide / dataset.RasterYSize));
            width = Math.Max(1, (int)Math.Round(dataset.RasterXSize * scale));
            height = Math.Max(1, (int)Math.Round(dataset.RasterYSize * scale));
        }

        private static float[] ReadBand(Dataset dataset, int number, int width, int height)
        {
            if (number < 1 || number > dataset.RasterCount)
                throw new ArgumentOutOfRangeException("number", "波段编号超出影像范围。");
            float[] values = new float[checked(width * height)];
            using (Band band = dataset.GetRasterBand(number))
                band.ReadRaster(0, 0, dataset.RasterXSize, dataset.RasterYSize,
                    values, width, height, 0, 0);
            return values;
        }

        private struct Range
        {
            public float Low;
            public float High;
        }

        private static Range GetRange(float[] values)
        {
            int step = Math.Max(1, values.Length / 100000);
            float[] sample = new float[(values.Length + step - 1) / step];
            int count = 0;
            for (int i = 0; i < values.Length; i += step)
            {
                float v = values[i];
                if (v != 0 && !float.IsNaN(v) && !float.IsInfinity(v)) sample[count++] = v;
            }
            if (count == 0) return new Range { Low = 0, High = 1 };
            Array.Sort(sample, 0, count);
            float low = sample[(int)((count - 1) * 0.02)];
            float high = sample[(int)((count - 1) * 0.98)];
            if (high <= low) high = low + 1;
            return new Range { Low = low, High = high };
        }

        private static byte Scale(float value, Range range)
        {
            if (float.IsNaN(value) || value <= range.Low) return 0;
            if (value >= range.High) return 255;
            return (byte)((value - range.Low) * 255f / (range.High - range.Low));
        }

        private static Bitmap CreateBitmap(int width, int height, byte[] pixels)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    if (data.Stride == width * 4)
                        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                    else
                        for (int y = 0; y < height; y++)
                            Marshal.Copy(pixels, y * width * 4,
                                IntPtr.Add(data.Scan0, y * data.Stride), width * 4);
                }
                finally { bitmap.UnlockBits(data); }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
    }
}
