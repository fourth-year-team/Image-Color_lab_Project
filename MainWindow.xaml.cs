using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageLabProject
{
    public partial class MainWindow : Window
    {
        private Image<Rgb24>? _originalImage;
        private Image<Rgb24>? _workingImage;
        private Image<Rgb24>? _baseImageForChannels;
        
        private CancellationTokenSource? _cts;
        private ColorSpace _currentSpace = ColorSpace.RGB;
        private DateTime _lastUpdate = DateTime.MinValue;
        
        private readonly CloudVisualizer _visualizer;

        public MainWindow()
        {
            InitializeComponent();
            _visualizer = new CloudVisualizer(RgbViewport);
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
                
                Task.Run(() => _visualizer.GenerateImageColorCloud(_workingImage, _currentSpace));
                
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
            DisplayedImage.Source = ImageProcessor.ConvertToBitmap(image);
            PlaceholderText.Visibility = Visibility.Collapsed;
        }

        // ================= REAL TIME SLIDERS =================

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

                    return ImageProcessor.ConvertToBitmap(temp);

                }, token);

                if (!token.IsCancellationRequested)
                    DisplayedImage.Source = bmp;
            }
            catch { }
        }

        // ================= COLOR SPACE SYSTEM =================

        private void ApplyColorSpace(ColorSpace space)
        {
            if (_originalImage == null) return;

            _currentSpace = space;
            _workingImage?.Dispose();
            _workingImage = _originalImage.Clone();

            ImageProcessor.ApplyColorSpaceToImage(_workingImage, space);

            Task.Run(() => _visualizer.GenerateImageColorCloud(_originalImage, _currentSpace));

            _baseImageForChannels?.Dispose();
            _baseImageForChannels = _workingImage.Clone();

            UpdateView(_workingImage);
            ResetSliders();
        }

        // ================= FILTER ENGINE =================

        private void ApplyFilter(Action<IImageProcessingContext> operation)
        {
            if (_originalImage == null) return;

            _workingImage?.Dispose();
            _workingImage = _originalImage.Clone(operation);

            UpdateView(_workingImage);
            ResetSliders();
            Task.Run(() => _visualizer.GenerateImageColorCloud(_workingImage, _currentSpace));
        }

        // ================= BUTTONS =================

        private void BtnToYUV_Click(object sender, RoutedEventArgs e)
        {
            UpdateSliderLabels("YUV CHANNELS", "Luma (Y)", "Chroma (U)", "Chroma (V)");
            ApplyColorSpace(ColorSpace.YUV);
        }

        private void BtnToCMYK_Click(object sender, RoutedEventArgs e)
        {
            UpdateSliderLabels("CMYK CHANNELS (C/M/Y)", "Cyan (C)", "Magenta (M)", "Yellow (Y)");
            ApplyColorSpace(ColorSpace.CMYK);
        }

        private void BtnToLab_Click(object sender, RoutedEventArgs e)
        {
            UpdateSliderLabels("CIELab CHANNELS", "Lightness (L)", "Chroma (a)", "Chroma (b)");
            ApplyColorSpace(ColorSpace.Lab);
        }
        private void BtnGrayscale_Click(object sender, RoutedEventArgs e) => ApplyColorSpace(ColorSpace.Grayscale);

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

            UpdateSliderLabels("RGB CHANNELS", "Red", "Green", "Blue");
            UpdateView(_workingImage);
            ResetSliders();
            
            _visualizer.Clear();
            Task.Run(() => _visualizer.GenerateImageColorCloud(_workingImage, _currentSpace));
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

        private void BtnToHSV_Click(object sender, RoutedEventArgs e)
        {
            UpdateSliderLabels("HSV CHANNELS", "Hue (H)", "Saturation (S)", "Value (V)");
            ApplyColorSpace(ColorSpace.HSV);
        }

        private void BtnToYCbCr_Click(object sender, RoutedEventArgs e)
        {
            UpdateSliderLabels("YCbCr CHANNELS", "Luma (Y)", "Chroma (Cb)", "Chroma (Cr)");
            ApplyColorSpace(ColorSpace.YCbCr);
        }

        private async void ChannelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_baseImageForChannels == null) return;

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
                    Image<Rgb24> temp = _baseImageForChannels.CloneAs<Rgb24>();

                    temp.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height; y++)
                        {
                            Span<Rgb24> pixelRow = accessor.GetRowSpan(y);
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
                                        ColorConverter.RGBToHSV(r / 255f, g / 255f, b / 255f, out float h, out float s, out float v);
                                        h = (h * v1) % 360f;
                                        s = Math.Clamp(s * v2, 0f, 1f);
                                        v = Math.Clamp(v * v3, 0f, 1f);
                                        ColorConverter.HSVToRGB(h, s, v, out r, out g, out b);
                                        break;

                                    case ColorSpace.YCbCr:
                                        ColorConverter.RGBToYCbCr(r / 255f, g / 255f, b / 255f, out float yl, out float cb, out float cr);
                                        yl = Math.Clamp(yl * v1, 0f, 1f);
                                        cb = Math.Clamp((cb - 0.5f) * v2 + 0.5f, 0f, 1f);
                                        cr = Math.Clamp((cr - 0.5f) * v3 + 0.5f, 0f, 1f);
                                        ColorConverter.YCbCrToRGB(yl, cb, cr, out r, out g, out b);
                                        break;

                                    case ColorSpace.Grayscale:
                                        float gray = 0.299f * r + 0.587f * g + 0.114f * b;
                                        r = g = b = Math.Clamp(gray * v1, 0, 255);
                                        break;

                                    case ColorSpace.YUV:
                                        ColorConverter.RGBToYUV(r / 255f, g / 255f, b / 255f, out float yuvY, out float yuvU, out float yuvV);
                                        yuvY = Math.Clamp(yuvY * v1, 0f, 1f);
                                        yuvU = Math.Clamp((yuvU - 0.5f) * v2 + 0.5f, 0f, 1f);
                                        yuvV = Math.Clamp((yuvV - 0.5f) * v3 + 0.5f, 0f, 1f);
                                        ColorConverter.YUVToRGB(yuvY, yuvU, yuvV, out r, out g, out b);
                                        break;

                                    case ColorSpace.CMYK:
                                        ColorConverter.RGBToCMYK(r / 255f, g / 255f, b / 255f, out float c, out float m, out float yc, out float k);
                                        c = Math.Clamp(c * v1, 0f, 1f);
                                        m = Math.Clamp(m * v2, 0f, 1f);
                                        yc = Math.Clamp(yc * v3, 0f, 1f);
                                        ColorConverter.CMYKToRGB(c, m, yc, k, out r, out g, out b);
                                        break;

                                    case ColorSpace.Lab:
                                        ColorConverter.RGBToLab(r / 255f, g / 255f, b / 255f, out float L, out float a, out float bl);
                                        L = Math.Clamp(L * v1, 0f, 100f);
                                        a = a * v2; // 'a' and 'b' can be negative
                                        bl = bl * v3;
                                        ColorConverter.LabToRGB(L, a, bl, out r, out g, out b);
                                        break;
                                }

                                pixelRow[x].R = (byte)Math.Clamp(r, 0, 255);
                                pixelRow[x].G = (byte)Math.Clamp(g, 0, 255);
                                pixelRow[x].B = (byte)Math.Clamp(b, 0, 255);
                            }
                        }
                    });

                    return ImageProcessor.ConvertToBitmap(temp);

                }, token);

                if (!token.IsCancellationRequested)
                    DisplayedImage.Source = bmp;
            }
            catch { }
        }

        private void SliderLevels_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblLevels != null)
            {
                lblLevels.Text = ((int)e.NewValue).ToString();
                
                if (_originalImage != null)
                {
                    _workingImage?.Dispose();
                    _workingImage = ImageProcessor.ApplyColorQuantization(_originalImage, (int)e.NewValue);
                    
                    // If we are in a color space, re-apply it to the quantized image
                    if (_currentSpace != ColorSpace.RGB)
                    {
                        ImageProcessor.ApplyColorSpaceToImage(_workingImage!, _currentSpace);
                    }

                    UpdateView(_workingImage!);
                    Task.Run(() => _visualizer.GenerateImageColorCloud(_workingImage, _currentSpace));
                }
            }
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

            RgbLabel.Text = $"RGB: ({r}, {g}, {b})";

            ColorConverter.RGBToHSV(r / 255f, g / 255f, b / 255f, out float h, out float s, out float v);
            HsvLabel.Text = $"HSV: ({h:F1}°, {s * 100:F0}%, {v * 100:F0}%)";

            ColorConverter.RGBToYCbCr(r / 255f, g / 255f, b / 255f, out float y, out float cb, out float cr);
            YcbcrLabel.Text = $"YCbCr: (Y: {y:F2}, Cb: {cb:F2}, Cr: {cr:F2})";

            float k = 1f - MathF.Max(r / 255f, MathF.Max(g / 255f, b / 255f));
            float c = (k == 1f) ? 0 : (1f - (r / 255f) - k) / (1f - k);
            float m = (k == 1f) ? 0 : (1f - (g / 255f) - k) / (1f - k);
            float yL = (k == 1f) ? 0 : (1f - (b / 255f) - k) / (1f - k);
            CmykLabel.Text = $"CMYK: ({c * 100:F0}%, {m * 100:F0}%, {yL * 100:F0}%, {k * 100:F0}%)";
        }
    }
}
