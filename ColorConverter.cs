using System;

namespace ImageLabProject
{
    public enum ColorSpace
    {
        RGB,
        HSV,
        CMYK,
        YCbCr,
        Lab,
        YUV,
        Grayscale
    }

    public static class ColorConverter
    {
        public static void RGBToYUV(float r, float g, float b, out float y, out float u, out float v)
        {
            y = 0.299f * r + 0.587f * g + 0.114f * b;
            u = -0.14713f * r - 0.28886f * g + 0.436f * b + 0.5f;
            v = 0.615f * r - 0.51499f * g - 0.10001f * b + 0.5f;
        }

        public static void RGBToYCbCr(float r, float g, float b, out float y, out float cb, out float cr)
        {
            y = 0.299f * r + 0.587f * g + 0.114f * b;
            cb = -0.1687f * r - 0.3313f * g + 0.5f * b + 0.5f;
            cr = 0.5f * r - 0.4187f * g - 0.0813f * b + 0.5f;
        }

        public static void RGBToHSV(float r, float g, float b, out float h, out float s, out float v)
        {
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            v = max;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0)
            {
                h = 0;
                return;
            }

            if (max == r)
                h = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                h = 60 * (((b - r) / delta) + 2);
            else
                h = 60 * (((r - g) / delta) + 4);

            if (h < 0) h += 360;
        }

        public static void RGBToCMYK(float r, float g, float b,
            out float c, out float m, out float y, out float k)
        {
            k = 1 - Math.Max(r, Math.Max(g, b));
            if (k >= 1f)
            {
                c = m = y = 0;
                return;
            }

            c = (1 - r - k) / (1 - k);
            m = (1 - g - k) / (1 - k);
            y = (1 - b - k) / (1 - k);
        }

        public static void RGBToLab(float r, float g, float b,
            out float L, out float a, out float bb)
        {
            r = r > 0.04045f ? MathF.Pow((r + 0.055f) / 1.055f, 2.4f) : r / 12.92f;
            g = g > 0.04045f ? MathF.Pow((g + 0.055f) / 1.055f, 2.4f) : g / 12.92f;
            b = b > 0.04045f ? MathF.Pow((b + 0.055f) / 1.055f, 2.4f) : b / 12.92f;

            float x = r * 0.4124f + g * 0.3576f + b * 0.1805f;
            float y = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            float z = r * 0.0193f + g * 0.1192f + b * 0.9505f;

            x /= 0.95047f;
            y /= 1.0f;
            z /= 1.08883f;

            Func<float, float> f =
                t => t > 0.008856f ? MathF.Cbrt(t) : (7.787f * t + 16f / 116f);

            float fx = f(x);
            float fy = f(y);
            float fz = f(z);

            L = (116f * fy - 16f);
            a = 500f * (fx - fy);
            bb = 200f * (fy - fz);
        }

        public static void HSVToRGB(float h, float s, float v, out float r, out float g, out float b)
        {
            if (s == 0)
            {
                r = g = b = v * 255f;
                return;
            }

            float sector = h / 60f;
            int i = (int)MathF.Floor(sector);
            float f = sector - i;
            float p = v * (1f - s);
            float q = v * (1f - s * f);
            float t = v * (1f - s * (1f - f));

            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }

            r *= 255f;
            g *= 255f;
            b *= 255f;
        }

        public static void YCbCrToRGB(float y, float cb, float cr, out float r, out float g, out float b)
        {
            cb -= 0.5f;
            cr -= 0.5f;

            r = (y + 1.402f * cr) * 255f;
            g = (y - 0.344136f * cb - 0.714136f * cr) * 255f;
            b = (y + 1.772f * cb) * 255f;

            r = Math.Clamp(r, 0f, 255f);
            g = Math.Clamp(g, 0f, 255f);
            b = Math.Clamp(b, 0f, 255f);
        }

        public static void CMYKToRGB(float c, float m, float y, float k, out float r, out float g, out float b)
        {
            r = 255f * (1f - c) * (1f - k);
            g = 255f * (1f - m) * (1f - k);
            b = 255f * (1f - y) * (1f - k);
        }

        public static void LabToRGB(float L, float a, float bb, out float r, out float g, out float b)
        {
            float fy = (L + 16f) / 116f;
            float fx = a / 500f + fy;
            float fz = fy - bb / 200f;

            float x = fx > 0.206897f ? MathF.Pow(fx, 3f) : (fx - 16f / 116f) / 7.787f;
            float y = fy > 0.206897f ? MathF.Pow(fy, 3f) : (fy - 16f / 116f) / 7.787f;
            float z = fz > 0.206897f ? MathF.Pow(fz, 3f) : (fz - 16f / 116f) / 7.787f;

            x *= 0.95047f;
            y *= 1.0f;
            z *= 1.08883f;

            r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
            g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
            b = x * 0.0557f + y * -0.2040f + z * 1.0570f;

            r = r > 0.0031308f ? 1.055f * MathF.Pow(r, 1f / 2.4f) - 0.055f : 12.92f * r;
            g = g > 0.0031308f ? 1.055f * MathF.Pow(g, 1f / 2.4f) - 0.055f : 12.92f * g;
            b = b > 0.0031308f ? 1.055f * MathF.Pow(b, 1f / 2.4f) - 0.055f : 12.92f * b;

            r = Math.Clamp(r * 255f, 0, 255);
            g = Math.Clamp(g * 255f, 0, 255);
            b = Math.Clamp(b * 255f, 0, 255);
        }

        public static void YUVToRGB(float y, float u, float v, out float r, out float g, out float b)
        {
            u -= 0.5f;
            v -= 0.5f;

            r = (y + 1.13983f * v) * 255f;
            g = (y - 0.39465f * u - 0.58060f * v) * 255f;
            b = (y + 2.03211f * u) * 255f;

            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);
        }
    }
}
