using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace ImageLabProject
{
    public partial class MainWindow : Window
    {
        private Image<Rgb24>? _originalImage;
        private Image<Rgb24>? _workingImage;

        private CancellationTokenSource? _cts;
private Image<Rgb24>? _baseImageForChannels;
        private ColorSpace _currentSpace = ColorSpace.RGB;

        public MainWindow()
        {
            InitializeComponent();


        }

        // ================= IMPORT =================

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dlg.ShowDialog() == true)
                LoadAndDisplayImage(dlg.FileName);
        }

        private void LoadAndDisplayImage(string path)
        {
            try
            {
                _originalImage?.Dispose();
                _workingImage?.Dispose();

                _originalImage = Image.Load<Rgb24>(path);
                _workingImage = _originalImage.Clone();

                UpdateView(_workingImage);
                        Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        GenerateImageColorCloud(_workingImage);
                    });
                });
                UpdateImageInfo(path);
                ResetSliders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ================= IMAGE INFO =================

        private void UpdateImageInfo(string path)
        {
            var info = new FileInfo(path);

            TxtFileName.Text = info.Name;
            TxtFileSize.Text = $"{info.Length / 1024.0:N1} KB";

            if (_originalImage != null)
                TxtDimensions.Text = $"{_originalImage.Width} x {_originalImage.Height}";
        }

        // ================= VIEW =================

        private void UpdateView(Image<Rgb24> image)
        {
            DisplayedImage.Source = ConvertToBitmap(image);
            PlaceholderText.Visibility = Visibility.Collapsed;
        }

        private BitmapSource ConvertToBitmap(Image<Rgb24> img)
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

        // ================= REAL TIME SLIDERS =================

    private DateTime _lastUpdate = DateTime.MinValue;

private async void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (_workingImage == null) return;

    if ((DateTime.Now - _lastUpdate).TotalMilliseconds < 50)
        return;

    _lastUpdate = DateTime.Now;

    _cts?.Cancel();
    _cts = new CancellationTokenSource();

    var token = _cts.Token;

    float brightness = (float)BrightnessSlider.Value;
    float contrast = (float)ContrastSlider.Value;

    try
    {
        var bmp = await Task.Run(() =>
        {
            using var temp = _workingImage.Clone(ctx =>
            {
                ctx.Brightness(brightness + 1f);
                ctx.Contrast(contrast + 1f);
            });

            return ConvertToBitmap(temp);

        }, token);

        if (!token.IsCancellationRequested)
            DisplayedImage.Source = bmp;
    }
    catch { }
}

        // ================= COLOR SPACE SYSTEM =================

        private enum ColorSpace
        {
            RGB,
            HSV,
            CMYK,
            YCbCr,
            Lab,
            YUV,
            Grayscale
        }

     private void ApplyColorSpace(ColorSpace space)
{
    if (_originalImage == null) return;

    _currentSpace = space;

    _workingImage?.Dispose();

    _workingImage = _originalImage.Clone(ctx =>
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

                        float gray = 0.299f * r +
                                     0.587f * g +
                                     0.114f * b;

                        row[i].X = gray;
                        row[i].Y = gray;
                        row[i].Z = gray;

                        break;

                    case ColorSpace.YCbCr:

                        RGBToYCbCr(r, g, b,
                            out float yy,
                            out float cb,
                            out float cr);

                        row[i].X = yy;
                        row[i].Y = cb;
                        row[i].Z = cr;

                        break;

                    case ColorSpace.YUV:

                        RGBToYUV(r, g, b,
                            out float y1,
                            out float u,
                            out float v1);

                        row[i].X = y1;
                        row[i].Y = u;
                        row[i].Z = v1;

                        break;

                    case ColorSpace.CMYK:

                        RGBToCMYK(r, g, b,
                            out float c,
                            out float m,
                            out float y2,
                            out float k);

                        row[i].X = c;
                        row[i].Y = m;
                        row[i].Z = y2;

                        break;

                    case ColorSpace.HSV:

                        RGBToHSV(r, g, b,
                            out float h,
                            out float s,
                            out float v);

                        row[i].X = h / 360f;
                        row[i].Y = s;
                        row[i].Z = v;

                        break;

                    case ColorSpace.Lab:

                        RGBToLab(r, g, b,
                            out float L,
                            out float a,
                            out float bb);

                        row[i].X = L;
                        row[i].Y = a;
                        row[i].Z = bb;

                        break;
                }
            }
        });Task.Run(() => GenerateImageColorCloud(_workingImage));
    });

    _baseImageForChannels?.Dispose();
    _baseImageForChannels = _workingImage.Clone();

    UpdateView(_workingImage);

    ResetSliders();
}

        // ================= CONVERSIONS =================

        private void RGBToYUV(float r, float g, float b, out float y, out float u, out float v)
        {
            y = 0.299f * r + 0.587f * g + 0.114f * b;
            u = -0.14713f * r - 0.28886f * g + 0.436f * b + 0.5f;
            v = 0.615f * r - 0.51499f * g - 0.10001f * b + 0.5f;
        }

        private void RGBToYCbCr(float r, float g, float b, out float y, out float cb, out float cr)
        {
            y = 0.299f * r + 0.587f * g + 0.114f * b;
            cb = -0.1687f * r - 0.3313f * g + 0.5f * b + 0.5f;
            cr = 0.5f * r - 0.4187f * g - 0.0813f * b + 0.5f;
        }

        private void RGBToHSV(float r, float g, float b, out float h, out float s, out float v)
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

        private void RGBToCMYK(float r, float g, float b,
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

        private void RGBToLab(float r, float g, float b,
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

            L = (116f * fy - 16f) / 100f;
            a = (500f * (fx - fy) + 128f) / 255f;
            bb = (200f * (fy - fz) + 128f) / 255f;
        }
        private void HSVToRGB(float h, float s, float v, out float r, out float g, out float b)
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

private void YCbCrToRGB(float y, float cb, float cr, out float r, out float g, out float b)
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
        // ================= FILTER ENGINE =================

        private void ApplyFilter(Action<IImageProcessingContext> operation)
        {
            if (_originalImage == null) return;

            _workingImage?.Dispose();
            _workingImage = _originalImage.Clone(operation);

            UpdateView(_workingImage);
            ResetSliders();
        }

        // ================= BUTTONS =================

       

        private void BtnToYUV_Click(object sender, RoutedEventArgs e)
            => ApplyColorSpace(ColorSpace.YUV);

        private void BtnToCMYK_Click(object sender, RoutedEventArgs e)
            => ApplyColorSpace(ColorSpace.CMYK);

        
        private void BtnToLab_Click(object sender, RoutedEventArgs e)
            => ApplyColorSpace(ColorSpace.Lab);

        private void BtnGrayscale_Click(object sender, RoutedEventArgs e)
            => ApplyColorSpace(ColorSpace.Grayscale);

        private void BtnExtractRed_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(ctx =>
            {
                ctx.ProcessPixelRowsAsVector4(row =>
                {
                    for (int i = 0; i < row.Length; i++)
                    {
                        row[i].Y = 0;
                        row[i].Z = 0;
                    }
                });
            });
        }

        // ================= RESET =================
private void BtnReset_Click(object sender, RoutedEventArgs e)
{
    if (_originalImage == null) return;

    _workingImage?.Dispose();
    _workingImage = _originalImage.Clone();

    _baseImageForChannels?.Dispose();
    _baseImageForChannels = _originalImage.Clone();

    _currentSpace = ColorSpace.RGB;

    UpdateSliderLabels(
        "RGB CHANNELS",
        "Red",
        "Green",
        "Blue");

    UpdateView(_workingImage);

    ResetSliders();
     RgbViewport.Children.Clear();
    RgbViewport.Children.Add(new HelixToolkit.Wpf.SunLight());


    Task.Run(() =>
    {
        GenerateImageColorCloud(_workingImage);
    });
}

        // ================= SAVE =================

 private void BtnSave_Click(object sender, RoutedEventArgs e)
{
    if (_workingImage == null) return;

    var dlg = new SaveFileDialog
    {
        Filter = "PNG|*.png|JPEG|*.jpg"
    };

    if (dlg.ShowDialog() == true)
    {
        using var final = _workingImage.Clone(ctx =>
        {
            ctx.Brightness((float)BrightnessSlider.Value + 1f);
            ctx.Contrast((float)ContrastSlider.Value + 1f);
        });

        final.Save(dlg.FileName);

        MessageBox.Show("Saved successfully");
    }
}

        // ================= RESET SLIDERS =================

  private void ResetSliders()
{
    BrightnessSlider.ValueChanged -= Slider_ValueChanged;
    ContrastSlider.ValueChanged -= Slider_ValueChanged;

    BrightnessSlider.Value = 0;
    ContrastSlider.Value = 0;

    BrightnessSlider.ValueChanged += Slider_ValueChanged;
    ContrastSlider.ValueChanged += Slider_ValueChanged;
}

        // ================= DRAG DROP =================

        private void ImageControl_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }


        private void ImageControl_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 0) return;

            LoadAndDisplayImage(files[0]);
        }

private void UpdateSliderLabels(string title, string ch1, string ch2, string ch3)
{
    
    SliderCh2.ValueChanged -= ChannelSlider_ValueChanged;
    SliderCh3.ValueChanged -= ChannelSlider_ValueChanged;

    TxtChannelsTitle.Text = title;
    LblCh1.Text = ch1;
    LblCh2.Text = ch2;
    LblCh3.Text = ch3;

    SliderCh1.Value = 1;
    SliderCh2.Value = 1;
    SliderCh3.Value = 1;


    SliderCh1.ValueChanged += ChannelSlider_ValueChanged;
    SliderCh2.ValueChanged += ChannelSlider_ValueChanged;
    SliderCh3.ValueChanged += ChannelSlider_ValueChanged;
}



private void BtnToHSV_Click(object sender, RoutedEventArgs e) {
    UpdateSliderLabels("HSV CHANNELS", "Hue (H)", "Saturation (S)", "Value (V)");
    ApplyColorSpace(ColorSpace.HSV);
}

private void BtnToYCbCr_Click(object sender, RoutedEventArgs e) {
    UpdateSliderLabels("YCbCr CHANNELS", "Luma (Y)", "Chroma (Cb)", "Chroma (Cr)");
    ApplyColorSpace(ColorSpace.YCbCr);
}



private async void ChannelSlider_ValueChanged(
    object sender,
    RoutedPropertyChangedEventArgs<double> e)
{
    if (_baseImageForChannels == null)
        return;

    _cts?.Cancel();

    _cts = new CancellationTokenSource();

    var token = _cts.Token;

    float v1 = (float)SliderCh1.Value;
    float v2 = (float)SliderCh2.Value;
    float v3 = (float)SliderCh3.Value;

    try
    {
        var bmp = await Task.Run(() =>
        {
            Image<Rgb24> temp =
                _baseImageForChannels.CloneAs<Rgb24>();

            temp.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgb24> pixelRow =
                        accessor.GetRowSpan(y);

                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        float r = pixelRow[x].R;
                        float g = pixelRow[x].G;
                        float b = pixelRow[x].B;

                        switch (_currentSpace)
                        {
                            case ColorSpace.RGB:

                                r = Math.Clamp(r * v1, 0, 255);
                                g = Math.Clamp(g * v2, 0, 255);
                                b = Math.Clamp(b * v3, 0, 255);

                                break;

                            case ColorSpace.HSV:

                                RGBToHSV(
                                    r / 255f,
                                    g / 255f,
                                    b / 255f,
                                    out float h,
                                    out float s,
                                    out float v);

                                h = (h * v1) % 360f;

                                s = Math.Clamp(
                                    s * v2,
                                    0f,
                                    1f);

                                v = Math.Clamp(
                                    v * v3,
                                    0f,
                                    1f);

                                HSVToRGB(
                                    h,
                                    s,
                                    v,
                                    out r,
                                    out g,
                                    out b);

                                break;

                            case ColorSpace.YCbCr:

                                RGBToYCbCr(
                                    r / 255f,
                                    g / 255f,
                                    b / 255f,
                                    out float yl,
                                    out float cb,
                                    out float cr);

                                yl = Math.Clamp(
                                    yl * v1,
                                    0f,
                                    1f);

                                cb = Math.Clamp(
                                    (cb - 0.5f) * v2 + 0.5f,
                                    0f,
                                    1f);

                                cr = Math.Clamp(
                                    (cr - 0.5f) * v3 + 0.5f,
                                    0f,
                                    1f);

                                YCbCrToRGB(
                                    yl,
                                    cb,
                                    cr,
                                    out r,
                                    out g,
                                    out b);

                                break;

                            case ColorSpace.Grayscale:

                                float gray =
                                    0.299f * r +
                                    0.587f * g +
                                    0.114f * b;

                                r = g = b =
                                    Math.Clamp(gray * v1,
                                    0,
                                    255);

                                break;

                            case ColorSpace.YUV:

                                RGBToYUV(
                                    r / 255f,
                                    g / 255f,
                                    b / 255f,
                                    out float yuvY,
                                    out float yuvU,
                                    out float yuvV);

                                yuvY = Math.Clamp(
                                    yuvY * v1,
                                    0f,
                                    1f);

                                r = (yuvY +
                                     1.13983f *
                                     (yuvV - 0.5f)) * 255f;

                                g = (yuvY -
                                     0.39465f *
                                     (yuvU - 0.5f) -
                                     0.58060f *
                                     (yuvV - 0.5f)) * 255f;

                                b = (yuvY +
                                     2.03211f *
                                     (yuvU - 0.5f)) * 255f;

                                break;
                        }

                        pixelRow[x].R =
                            (byte)Math.Clamp(r, 0, 255);

                        pixelRow[x].G =
                            (byte)Math.Clamp(g, 0, 255);

                        pixelRow[x].B =
                            (byte)Math.Clamp(b, 0, 255);
                    }
                }
            });

            return ConvertToBitmap(temp);

        }, token);

        if (!token.IsCancellationRequested)
        {
            DisplayedImage.Source = bmp;
        }
    }
    catch
    {
    }
}


private void SliderLevels_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    // Use the correct name for your label (ensure it matches XAML Name="lblLevels")
    if (lblLevels != null)
    {
        lblLevels.Text = ((int)e.NewValue).ToString();
        ApplyColorQuantization((int)e.NewValue);
    }
}

private void ApplyColorQuantization(int levels)

{

    if (_originalImage == null || levels < 2) return;



    _workingImage?.Dispose();



    Image<Rgb24> tempImage = _originalImage.CloneAs<Rgb24>();




    tempImage.ProcessPixelRows(accessor =>

    {

        for (int y = 0; y < accessor.Height; y++)

        {

            Span<Rgb24> pixelRow = accessor.GetRowSpan(y);

            

            for (int x = 0; x < pixelRow.Length; x++)

            {

               
                pixelRow[x].R = (byte)(Math.Round(pixelRow[x].R / 255.0 * (levels - 1)) * 255.0 / (levels - 1));

                pixelRow[x].G = (byte)(Math.Round(pixelRow[x].G / 255.0 * (levels - 1)) * 255.0 / (levels - 1));

                pixelRow[x].B = (byte)(Math.Round(pixelRow[x].B / 255.0 * (levels - 1)) * 255.0 / (levels - 1));

            }

        }

    });




    _workingImage = tempImage;

    UpdateView(_workingImage);

}
private void DisplayedImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (_originalImage == null || DisplayedImage.Source == null) return;

    System.Windows.Point clickPoint = e.GetPosition(DisplayedImage);

    double imageWidth = DisplayedImage.ActualWidth;
    double imageHeight = DisplayedImage.ActualHeight;

    int pixelX = (int)((clickPoint.X / imageWidth) * _originalImage.Width);
    int pixelY = (int)((clickPoint.Y / imageHeight) * _originalImage.Height);

    if (pixelX < 0 || pixelX >= _originalImage.Width || pixelY < 0 || pixelY >= _originalImage.Height) return;

    Rgb24 targetPixel = _originalImage[pixelX, pixelY];
    byte r = targetPixel.R;
    byte g = targetPixel.G;
    byte b = targetPixel.B;

    SelectedColorPreview.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
    HexColorText.Text = $"#{r:X2}{g:X2}{b:X2}";

    string rgbText = $"RGB: ({r}, {g}, {b})";

    RGBToHSV(r / 255f, g / 255f, b / 255f, out float h, out float s, out float v);
    string hsvText = $"HSV: ({h:F1}°, {s * 100:F0}%, {v * 100:F0}%)";

    RGBToYCbCr(r / 255f, g / 255f, b / 255f, out float y, out float cb, out float cr);
    string ycbcrText = $"YCbCr: (Y: {y:F2}, Cb: {cb:F2}, Cr: {cr:F2})";

    float k = 1f - MathF.Max(r / 255f, MathF.Max(g / 255f, b / 255f));
    float c = (k == 1f) ? 0 : (1f - (r / 255f) - k) / (1f - k);
    float m = (k == 1f) ? 0 : (1f - (g / 255f) - k) / (1f - k);
    float yL = (k == 1f) ? 0 : (1f - (b / 255f) - k) / (1f - k);
    string cmykText = $"CMYK: ({c * 100:F0}%, {m * 100:F0}%, {yL * 100:F0}%, {k * 100:F0}%)";

    RgbLabel.Text = rgbText;
HsvLabel.Text = hsvText;
YcbcrLabel.Text = ycbcrText;
CmykLabel.Text = cmykText;
}



private void GenerateRgbCube()
{
    RgbViewport.Children.Clear();

    RgbViewport.Children.Add(new SunLight());

    for (int r = 0; r <= 255; r += 32)
    {
        for (int g = 0; g <= 255; g += 32)
        {
            for (int b = 0; b <= 255; b += 32)
            {
                var point = new SphereVisual3D
                {
                    Center = new Point3D(r, g, b),
                    Radius = 4,

                    Fill = new SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            (byte)r,
                            (byte)g,
                            (byte)b))
                };

                RgbViewport.Children.Add(point);
            }
        }
    }
}
private void RenderColorCloud(Point3DCollection points, List<System.Windows.Media.Color> colors)
{

    RgbViewport.Children.Clear();
    

    RgbViewport.Children.Add(new SunLight());

    var modelGroup = new System.Windows.Media.Media3D.Model3DGroup();
    double size = 2.0; 

    for (int i = 0; i < points.Count; i++)
    {
        var p = points[i];
        var color = colors[i];

        var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        
        mesh.Positions.Add(new Point3D(p.X - size, p.Y - size, p.Z));
        mesh.Positions.Add(new Point3D(p.X + size, p.Y - size, p.Z));
        mesh.Positions.Add(new Point3D(p.X + size, p.Y + size, p.Z));
        mesh.Positions.Add(new Point3D(p.X - size, p.Y + size, p.Z));

        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);

        var brush = new System.Windows.Media.SolidColorBrush(color);
        var material = new System.Windows.Media.Media3D.DiffuseMaterial(brush);
        
        var geometryModel = new System.Windows.Media.Media3D.GeometryModel3D(mesh, material);
        modelGroup.Children.Add(geometryModel);
    }

    var visual3D = new System.Windows.Media.Media3D.ModelVisual3D
    {
        Content = modelGroup
    };

    RgbViewport.Children.Add(visual3D);
}

private void GenerateImageColorCloud(Image<Rgb24>? image)
{
    if (image == null) return;

    
    var pointsList = new List<Point3D>();
    var colorsList = new List<System.Windows.Media.Color>(); 

    int step = Math.Max(8, image.Width / 100); 

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

                switch (_currentSpace)
                {
                    case ColorSpace.RGB:
                        posX = p.R;
                        posY = p.G;
                        posZ = p.B;
                        break;

                    case ColorSpace.HSV:
                        RGBToHSV(rF, gF, bF, out float h, out float s, out float v);
                        double angleRad = h * Math.PI / 180.0;
                        double radius = s * 100.0; 
                        posX = radius * Math.Cos(angleRad);
                        posY = radius * Math.Sin(angleRad);
                        posZ = v * 100.0; 
                        break;

                    case ColorSpace.YCbCr:
                        RGBToYCbCr(rF, gF, bF, out float yL, out float cb, out float cr);
                        posX = (cb - 0.5f) * 200.0;
                        posY = (cr - 0.5f) * 200.0;
                        posZ = yL * 200.0;
                        break;

                    case ColorSpace.CMYK:
                        RGBToCMYK(rF, gF, bF, out float c, out float m, out float yC, out float k);
                        posX = c * 200.0;
                        posY = m * 200.0;
                        posZ = yC * 200.0;
                        break;

                    case ColorSpace.Grayscale:
                        float gray = 0.299f * p.R + 0.587f * p.G + 0.114f * p.B;
                        posX = gray;
                        posY = gray;
                        posZ = gray;
                        break;

                    default:
                        posX = p.R;
                        posY = p.G;
                        posZ = p.B;
                        break;
                }

                pointsList.Add(new Point3D(posX, posY, posZ));
                colorsList.Add(System.Windows.Media.Color.FromRgb(p.R, p.G, p.B));
            }
        }
    });

   
    Dispatcher.BeginInvoke(new Action(() =>
    {
        var wpfPoints = new Point3DCollection(pointsList);
        RenderColorCloud(wpfPoints, colorsList);
    }));
}
    }
}