using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace MP4toGIFConverter
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // FFmpeg path
        private string FFmpegPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        
        // Properties
        private string _inputFilePath = string.Empty;
        public string InputFilePath
        {
            get => _inputFilePath;
            set
            {
                _inputFilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFileLoaded));
                LoadVideoPreview();
            }
        }
        
        private string _outputPath = string.Empty;
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                _outputPath = value;
                OnPropertyChanged();
            }
        }
        
        private double _startTime = 0;
        public double StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                OnPropertyChanged();
            }
        }
        
        private double _endTime = 16;
        public double EndTime
        {
            get => _endTime;
            set
            {
                _endTime = value;
                OnPropertyChanged();
            }
        }
        
        private int _frameRate = 10;
        public int FrameRate
        {
            get => _frameRate;
            set
            {
                _frameRate = value;
                OnPropertyChanged();
            }
        }
        
        private string _fileInfo = string.Empty;
        public string FileInfo
        {
            get => _fileInfo;
            set
            {
                _fileInfo = value;
                OnPropertyChanged();
                FileInfoText.Text = value;
            }
        }
        
        private bool _isFileLoaded = false;
        public bool IsFileLoaded
        {
            get => _isFileLoaded;
            set
            {
                _isFileLoaded = value;
                OnPropertyChanged();
            }
        }
        
        private bool _isVideoPlaying = false;
        
        private DispatcherTimer _videoTimer;
        
        // Constructor
        public MainWindow()
        {
            InitializeComponent();
            
            // Validate application integrity
            if (!SecurityValidator.ValidateApplication(this))
            {
                Environment.Exit(1);
            }
            
            DataContext = this;
            
            // Check for FFmpeg
            if (!File.Exists(FFmpegPath))
            {
                MessageBox.Show("FFmpeg not found. Please ensure ffmpeg.exe is in the application directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            // Set default output path
            OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "output.gif");
            
            // Initialize video timer
            _videoTimer = new DispatcherTimer();
            _videoTimer.Interval = TimeSpan.FromMilliseconds(500);
            _videoTimer.Tick += VideoTimer_Tick;
            
            // Setup drag and drop
            PreviewDragOver += MainWindow_PreviewDragOver;
            PreviewDrop += MainWindow_PreviewDrop;
        }
        
        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // Video preview methods
        private void LoadVideoPreview()
        {
            if (string.IsNullOrEmpty(InputFilePath) || !File.Exists(InputFilePath))
            {
                IsFileLoaded = false;
                FileInfo = string.Empty;
                return;
            }
            
            IsFileLoaded = true;
            
            try
            {
                UpdateStatus("Loading video preview...");
                
                // Stop current video if playing
                VideoPreview.Stop();
                _videoTimer.Stop();
                
                // Set new video source
                VideoPreview.Source = new Uri(InputFilePath);
                
                // Load video info
                var fileInfo = new FileInfo(InputFilePath);
                
                // Get video duration using FFmpeg
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = $"-i \"{InputFilePath}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                var process = Process.Start(startInfo);
                var output = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                // Parse duration
                var durationMatch = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d+):(\d+):(\d+\.\d+)");
                if (durationMatch.Success)
                {
                    int hours = int.Parse(durationMatch.Groups[1].Value);
                    int minutes = int.Parse(durationMatch.Groups[2].Value);
                    double seconds = double.Parse(durationMatch.Groups[3].Value);
                    
                    var duration = new TimeSpan(hours, minutes, 0).Add(TimeSpan.FromSeconds(seconds));
                    EndTime = duration.TotalSeconds;
                    
                    // Get dimensions
                    var dimensionsMatch = System.Text.RegularExpressions.Regex.Match(output, @"(\d+x\d+)");
                    var dimensions = dimensionsMatch.Success ? dimensionsMatch.Groups[1].Value : "Unknown";
                    
                    FileInfo = $"File: {Path.GetFileName(InputFilePath)}, Size: {fileInfo.Length / 1024 / 1024:F2}MB, Dimensions: {dimensions}, Duration: {duration:hh\\:mm\\:ss}";
                }
                
                // Play preview
                VideoPreview.Play();
                PlayPauseButton.Content = "⏸";
                _isVideoPlaying = true;
                _videoTimer.Start();
                
                UpdateStatus("Ready");
            }
            catch (Exception ex)
            {
                IsFileLoaded = false;
                UpdateStatus($"Error loading video: {ex.Message}");
                MessageBox.Show($"Error loading video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void VideoTimer_Tick(object sender, EventArgs e)
        {
            // Update current position
            if (VideoPreview.NaturalDuration.HasTimeSpan)
            {
                var currentPos = VideoPreview.Position.TotalSeconds;
                var totalDuration = VideoPreview.NaturalDuration.TimeSpan.TotalSeconds;
                
                // If we're at the end, loop the video
                if (Math.Abs(currentPos - totalDuration) < 0.5)
                {
                    VideoPreview.Position = TimeSpan.Zero;
                }
            }
        }
        
        private void VideoPreview_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Video loaded successfully
            VideoPlaceholder.Visibility = Visibility.Collapsed;
        }
        
        private void VideoPreview_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Loop the video
            VideoPreview.Position = TimeSpan.Zero;
            VideoPreview.Play();
        }
        
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isVideoPlaying)
            {
                VideoPreview.Pause();
                PlayPauseButton.Content = "▶";
                _isVideoPlaying = false;
                _videoTimer.Stop();
            }
            else
            {
                VideoPreview.Play();
                PlayPauseButton.Content = "⏸";
                _isVideoPlaying = true;
                _videoTimer.Start();
            }
        }
        
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview.Stop();
            PlayPauseButton.Content = "▶";
            _isVideoPlaying = false;
            _videoTimer.Stop();
        }
        
        // Drag and drop handling
        private void MainWindow_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".mp4" || extension == ".avi" || extension == ".mov" || extension == ".mkv")
                    {
                        e.Effects = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            
            e.Effects = DragDropEffects.None;
        }
        
        private void MainWindow_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".mp4" || extension == ".avi" || extension == ".mov" || extension == ".mkv")
                    {
                        InputFilePath = files[0];
                    }
                }
            }
        }
        
        // Event handlers
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi;*.mov;*.mkv)|*.mp4;*.avi;*.mov;*.mkv|All files (*.*)|*.*",
                Title = "Select a video file"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                InputFilePath = openFileDialog.FileName;
            }
        }
        
        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputFilePath))
            {
                MessageBox.Show("Please select input file first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                UpdateStatus("Preparing conversion...");
                
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "GIF files (*.gif)|*.gif",
                    Title = "Save GIF as",
                    FileName = Path.GetFileNameWithoutExtension(InputFilePath) + ".gif"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    OutputPath = saveFileDialog.FileName;
                    
                    // Show loading overlay
                    LoadingOverlay.Visibility = Visibility.Visible;
                    GifPlaceholder.Visibility = Visibility.Collapsed;
                    ConversionProgress.Value = 0;
                    ProgressText.Text = "0%";
                    ConversionStatus.Text = "Converting video to GIF...";
                    
                    // Get start and end times from text boxes
                    if (double.TryParse(StartTimeBox.Text, out double startTime))
                        StartTime = startTime;
                    
                    if (double.TryParse(EndTimeBox.Text, out double endTime))
                        EndTime = endTime;
                    
                    // Get selected frame rate
                    string selectedFrameRate = ((ComboBoxItem)FrameRateComboBox.SelectedItem).Content.ToString();
                    FrameRate = int.Parse(selectedFrameRate.Split(' ')[0]);
                    
                    // Get selected size option
                    string sizeOption = ((ComboBoxItem)SizeComboBox.SelectedItem).Content.ToString();
                    
                    UpdateStatus("Converting video to GIF...");
                    await ConvertToGifAsync(sizeOption);
                    
                    // Show GIF preview
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    ShowGifPreview();
                    
                    UpdateStatus("Conversion completed successfully!");
                    MessageBox.Show("Conversion completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show($"Error during conversion: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ToWebPButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("WebP conversion will be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ToAPNGButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("APNG conversion will be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void CropVideoButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Crop video feature will be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputFilePath))
            {
                MessageBox.Show("Please select a video file first.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "GIF files (*.gif)|*.gif",
                Title = "Save GIF as",
                FileName = Path.GetFileNameWithoutExtension(InputFilePath) + ".gif"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                OutputPath = saveFileDialog.FileName;
                MessageBox.Show($"Output will be saved to: {OutputPath}", "Output Path Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private async Task ConvertToGifAsync(string sizeOption)
        {
            // Get the scale filter based on size option
            string scaleFilter = GetScaleFilter(sizeOption);
            
            // Build FFmpeg command
            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegPath,
                Arguments = $"-hwaccel auto -i \"{InputFilePath}\" -ss {StartTime} -to {EndTime} " +
                             $"-vf \"fps={FrameRate},{scaleFilter},split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" " +
                             $"-y \"{OutputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            var process = new Process { StartInfo = startInfo };
            
            // Progress reporting
            var duration = EndTime - StartTime;
            var progressHandler = new DataReceivedEventHandler((sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;
                
                var timeMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.\d+)");
                if (timeMatch.Success)
                {
                    int hours = int.Parse(timeMatch.Groups[1].Value);
                    int minutes = int.Parse(timeMatch.Groups[2].Value);
                    double seconds = double.Parse(timeMatch.Groups[3].Value);
                    
                    var currentTime = TimeSpan.FromHours(hours).TotalSeconds + 
                                     TimeSpan.FromMinutes(minutes).TotalSeconds + 
                                     seconds;
                    
                    var progress = Math.Min((currentTime / duration) * 100, 99);
                    
                    Dispatcher.Invoke(() =>
                    {
                        ConversionProgress.Value = progress;
                        ProgressText.Text = $"{progress:F0}%";
                    });
                }
            });
            
            process.ErrorDataReceived += progressHandler;
            
            process.Start();
            process.BeginErrorReadLine();
            
            await Task.Run(() => process.WaitForExit());
            
            Dispatcher.Invoke(() =>
            {
                ConversionProgress.Value = 100;
                ProgressText.Text = "100%";
                ConversionStatus.Text = "Conversion complete!";
            });
            
            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg exited with code {process.ExitCode}");
            }
        }
        
        private string GetScaleFilter(string sizeOption)
        {
            switch (sizeOption)
            {
                case "Original (up to 800px)":
                    return "scale=min(iw\\,800):min(ih\\,800):force_original_aspect_ratio=decrease";
                case "600xAUTO":
                    return "scale=600:-1";
                case "540xAUTO (for Tumblr)":
                    return "scale=540:-1";
                case "500xAUTO":
                    return "scale=500:-1";
                case "480xAUTO":
                    return "scale=480:-1";
                case "400xAUTO":
                    return "scale=400:-1";
                case "320xAUTO":
                    return "scale=320:-1";
                case "AUTOx480":
                    return "scale=-1:480";
                case "AUTOx320":
                    return "scale=-1:320";
                case "up to 1200x300 (for wide banner)":
                    return "scale=min(iw\\,1200):min(ih\\,300):force_original_aspect_ratio=decrease";
                case "up to 300x1200 (for skyscraper banner)":
                    return "scale=min(iw\\,300):min(ih\\,1200):force_original_aspect_ratio=decrease";
                default:
                    return "scale=min(iw\\,800):min(ih\\,800):force_original_aspect_ratio=decrease";
            }
        }
        
        private void ShowGifPreview()
        {
            if (File.Exists(OutputPath))
            {
                // Create a BitmapImage for the GIF
                var gifImage = new BitmapImage();
                gifImage.BeginInit();
                gifImage.UriSource = new Uri(OutputPath);
                gifImage.CacheOption = BitmapCacheOption.OnLoad;
                gifImage.EndInit();
                
                // Enable animation
                ImageBehavior.SetAnimatedSource(GifPreview, gifImage);
                GifPlaceholder.Visibility = Visibility.Collapsed;
            }
        }
        
        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }
    }
    
    // Helper class for GIF animation in WPF
    public static class ImageBehavior
    {
        public static readonly DependencyProperty AnimatedSourceProperty =
            DependencyProperty.RegisterAttached("AnimatedSource", typeof(ImageSource), typeof(ImageBehavior),
                new PropertyMetadata(null, AnimatedSourceChanged));
        
        public static void SetAnimatedSource(Image image, ImageSource value)
        {
            image.SetValue(AnimatedSourceProperty, value);
        }
        
        public static ImageSource GetAnimatedSource(Image image)
        {
            return (ImageSource)image.GetValue(AnimatedSourceProperty);
        }
        
        private static void AnimatedSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image image)
            {
                image.Source = e.NewValue as ImageSource;
            }
        }
    }
}