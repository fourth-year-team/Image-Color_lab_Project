using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace ImageLabProject
{
    public class ImageProcessor
    {
        public static BitmapSource ConvertToBitmap(Image<Rgb24> img)
        {
            using var ms = new MemoryStream();
            img.SaveAsBmp(ms);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            return bmp;
        }

        public static Image<Rgb24>? ApplyColorQuantization(Image<Rgb24> originalImage, int levels)
        {
            if (originalImage == null || levels < 2) return null;

            Image<Rgb24> tempImage = originalImage.CloneAs<Rgb24>();
            
            float factor = 255f / (levels - 1);

            tempImage.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgb24> pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        pixelRow[x].R = (byte)(Math.Round(pixelRow[x].R / factor) * factor);
                        pixelRow[x].G = (byte)(Math.Round(pixelRow[x].G / factor) * factor);
                        pixelRow[x].B = (byte)(Math.Round(pixelRow[x].B / factor) * factor);
                    }
                }
            });

            return tempImage;
        }

        public static void ApplyColorSpaceToImage(Image<Rgb24> image, ColorSpace space)
        {
            image.Mutate(ctx =>
            {
                ctx.ProcessPixelRowsAsVector4(row =>
                {
                    for (int i = 0; i < row.Length; i++)
                    {
                        float r = row[i].X;
                        float g = row[i].Y;
                        float b = row[i].Z;

                        switch (space)
                        {
                            case ColorSpace.RGB:
                                break;

                            case ColorSpace.Grayscale:
                                float gray = 0.299f * r + 0.587f * g + 0.114f * b;
                                row[i].X = gray;
                                row[i].Y = gray;
                                row[i].Z = gray;
                                break;

                            case ColorSpace.YCbCr:
                                ColorConverter.RGBToYCbCr(r, g, b, out float yy, out float cb, out float cr);
                                row[i].X = yy;
                                row[i].Y = cb;
                                row[i].Z = cr;
                                break;

                            case ColorSpace.YUV:
                                ColorConverter.RGBToYUV(r, g, b, out float y1, out float u, out float v1);
                                row[i].X = y1;
                                row[i].Y = u;
                                row[i].Z = v1;
                                break;

                            case ColorSpace.CMYK:
                                ColorConverter.RGBToCMYK(r, g, b, out float c, out float m, out float y2, out float k);
                                row[i].X = c;
                                row[i].Y = m;
                                row[i].Z = y2;
                                break;

                            case ColorSpace.HSV:
                                ColorConverter.RGBToHSV(r, g, b, out float h, out float s, out float v);
                                row[i].X = h / 360f;
                                row[i].Y = s;
                                row[i].Z = v;
                                break;

                            case ColorSpace.Lab:
                                ColorConverter.RGBToLab(r, g, b, out float L, out float a, out float bb);
                                row[i].X = L;
                                row[i].Y = a;
                                row[i].Z = bb;
                                break;
                        }
                    }
                });
            });
        }
    }
}
