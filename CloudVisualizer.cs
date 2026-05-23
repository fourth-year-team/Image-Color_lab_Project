using HelixToolkit.Wpf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ImageLabProject
{
    public class CloudVisualizer
    {
        private readonly HelixViewport3D _viewport;

        public CloudVisualizer(HelixViewport3D viewport)
        {
            _viewport = viewport;
        }

        public void Clear()
        {
            _viewport.Children.Clear();
            _viewport.Children.Add(new SunLight());
        }

        public void GenerateImageColorCloud(Image<Rgb24>? image, ColorSpace currentSpace)
        {
            if (image == null) return;

            var pointsList = new List<Point3D>();
            var colorsList = new List<System.Windows.Media.Color>();

            // Higher sampling for smoother cloud now that we have performance optimization
            int step = Math.Max(2, image.Width / 120); 

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y += step)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x += step)
                    {
                        var p = row[x];
                        float rF = p.R / 255f;
                        float gF = p.G / 255f;
                        float bF = p.B / 255f;

                        double posX = 0, posY = 0, posZ = 0;

                        switch (currentSpace)
                        {
                            case ColorSpace.RGB:
                                posX = p.R * (200.0 / 255.0);
                                posY = p.G * (200.0 / 255.0);
                                posZ = p.B * (200.0 / 255.0);
                                break;

                            case ColorSpace.HSV:
                                ColorConverter.RGBToHSV(rF, gF, bF, out float h, out float s, out float v);
                                double angleRad = h * Math.PI / 180.0;
                                double radius = s * 100.0; 
                                posX = radius * Math.Cos(angleRad);
                                posY = radius * Math.Sin(angleRad);
                                posZ = v * 200.0; 
                                break;

                            case ColorSpace.YCbCr:
                                ColorConverter.RGBToYCbCr(rF, gF, bF, out float yL, out float cb, out float cr);
                                posX = (cb - 0.5f) * 200.0;
                                posY = (cr - 0.5f) * 200.0;
                                posZ = yL * 200.0;
                                break;

                            case ColorSpace.CMYK:
                                ColorConverter.RGBToCMYK(rF, gF, bF, out float c, out float m, out float yC, out float k);
                                posX = c * 200.0;
                                posY = m * 200.0;
                                posZ = yC * 200.0;
                                break;

                            case ColorSpace.Grayscale:
                                float gray = 0.299f * p.R + 0.587f * p.G + 0.114f * p.B;
                                double gScaled = gray * (200.0 / 255.0);
                                posX = gScaled;
                                posY = gScaled;
                                posZ = gScaled;
                                break;

                            case ColorSpace.Lab:
                                ColorConverter.RGBToLab(rF, gF, bF, out float L, out float a, out float bL);
                                posX = a; 
                                posY = bL; 
                                posZ = L * 2.0; 
                                break;

                            case ColorSpace.YUV:
                                ColorConverter.RGBToYUV(rF, gF, bF, out float yV, out float uV, out float vV);
                                posX = (uV - 0.5f) * 200.0;
                                posY = (vV - 0.5f) * 200.0;
                                posZ = yV * 200.0;
                                break;
                        }

                        pointsList.Add(new Point3D(posX, posY, posZ));
                        colorsList.Add(System.Windows.Media.Color.FromRgb(p.R, p.G, p.B));
                    }
                }
            });

            _viewport.Dispatcher.Invoke(() => RenderColorCloud(pointsList, colorsList));
        }

        private void RenderColorCloud(List<Point3D> points, List<System.Windows.Media.Color> colors)
        {
            Clear();

            if (points.Count == 0) return;

            // PERFORMANCE OPTIMIZATION: 
            // Instead of thousands of individual models, we use ONE single mesh.
            // Since WPF doesn't support per-vertex colors, we use Texture Mapping.
            // We create a 1 x N pixel texture where each pixel is one point's color.
            
            int n = points.Count;
            var bitmap = new WriteableBitmap(n, 1, 96, 96, PixelFormats.Bgra32, null);
            uint[] pixels = new uint[n];

            for (int i = 0; i < n; i++)
            {
                var c = colors[i];
                pixels[i] = (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B);
            }
            bitmap.WritePixels(new Int32Rect(0, 0, n, 1), pixels, n * 4, 0);

            var mesh = new MeshGeometry3D();
            double size = 1.2;

            for (int i = 0; i < n; i++)
            {
                var p = points[i];
                int baseIdx = mesh.Positions.Count;

                // Create a small billboard-like quad (facing Z for simplicity, 
                // but at this scale it looks like a point from most angles)
                mesh.Positions.Add(new Point3D(p.X - size, p.Y - size, p.Z));
                mesh.Positions.Add(new Point3D(p.X + size, p.Y - size, p.Z));
                mesh.Positions.Add(new Point3D(p.X + size, p.Y + size, p.Z));
                mesh.Positions.Add(new Point3D(p.X - size, p.Y + size, p.Z));

                // Map all 4 vertices of this quad to the same pixel in our color texture
                double u = (i + 0.5) / n;
                mesh.TextureCoordinates.Add(new System.Windows.Point(u, 0.5));
                mesh.TextureCoordinates.Add(new System.Windows.Point(u, 0.5));
                mesh.TextureCoordinates.Add(new System.Windows.Point(u, 0.5));
                mesh.TextureCoordinates.Add(new System.Windows.Point(u, 0.5));

                mesh.TriangleIndices.Add(baseIdx);
                mesh.TriangleIndices.Add(baseIdx + 1);
                mesh.TriangleIndices.Add(baseIdx + 2);
                mesh.TriangleIndices.Add(baseIdx);
                mesh.TriangleIndices.Add(baseIdx + 2);
                mesh.TriangleIndices.Add(baseIdx + 3);
            }

            mesh.Freeze();

            var material = new DiffuseMaterial(new ImageBrush(bitmap) 
            { 
                ViewportUnits = BrushMappingMode.Absolute,
                TileMode = TileMode.None 
            });
            
            var geometryModel = new GeometryModel3D(mesh, material);
            // Also add back material to see points from behind
            geometryModel.BackMaterial = material; 

            _viewport.Children.Add(new ModelVisual3D { Content = geometryModel });
        }
    }
}
