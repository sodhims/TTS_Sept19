using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NAudio.Wave;
using System.Threading;
using System.Threading.Tasks;
using TTS1; // For SSMLSettings

namespace TTS1.WPF.Controls
{
    public partial class WaveformControl : UserControl
    {
        // Properties
        public ObservableCollection<WaveformMarker> Markers { get; set; }
        public ObservableCollection<WaveformSegment> Segments { get; set; }
        public List<string> AvailableVoices { get; set; }
        public Func<string, Task<bool>> SpeakAsyncDelegate { get; set; }
        
        private float[] audioSamples;
        private double audioDuration = 10.0; // Default 10 seconds
        private int sampleRate = 44100;
        private WaveformSegment currentEditingSegment;
        private WaveformSegment selectedSegment;
        private Rectangle selectedSegmentRectangle;
        private Dictionary<WaveformSegment, Rectangle> segmentRectangles;
        private MainViewModel viewModel;
        
        // Playback tracking
        private Rectangle playbackProgressBar;
        private Rectangle currentPlayingSegment;
        private System.Windows.Threading.DispatcherTimer playbackTimer;
        private DateTime playbackStartTime;
        private int currentPlayingSegmentIndex = -1;
        private double playbackPosition = 0;
        private bool ignoreSegmentEndDuringPlayback = false;
        
        // Store the original text for segment extraction
        private string originalText;
        
        public void SetViewModel(MainViewModel vm)
        {
            viewModel = vm;
        }
        
        // Color palette for segments
        private readonly Color[] segmentColors = new[]
        {
            Colors.Blue, Colors.Green, Colors.Purple, 
            Colors.Orange, Colors.Teal, Colors.Magenta
        };

        public WaveformControl()
        {
            InitializeComponent();
            Markers = new ObservableCollection<WaveformMarker>();
            Segments = new ObservableCollection<WaveformSegment>();
            AvailableVoices = new List<string>();
            segmentRectangles = new Dictionary<WaveformSegment, Rectangle>();
            
            Markers.CollectionChanged += (s, e) => UpdateDisplay();
            
            // Initialize with empty waveform
            InitializeEmptyWaveform();
        }

        // Initialize with a blank waveform
        private void InitializeEmptyWaveform()
        {
            DrawPlaceholderWaveform();
            DrawTimeline();
            UpdateSegmentInfo("Ready to generate audio preview");
        }

        // Draw a placeholder waveform pattern
        private void DrawPlaceholderWaveform()
        {
            WaveformCanvas.Children.Clear();
            
            double width = ActualWidth > 0 ? ActualWidth : 800;
            double height = WaveformCanvas.ActualHeight > 0 ? WaveformCanvas.ActualHeight : 150;
            double centerY = height / 2;
            
            // Draw center line
            var centerLine = new Line
            {
                X1 = 0, Y1 = centerY,
                X2 = width, Y2 = centerY,
                Stroke = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                StrokeThickness = 1
            };
            WaveformCanvas.Children.Add(centerLine);
            
            // Draw placeholder text
            var text = new TextBlock
            {
                Text = "Click 'Generate Audio Preview' to visualize",
                Foreground = Brushes.Gray,
                FontSize = 14
            };
            Canvas.SetLeft(text, width / 2 - 100);
            Canvas.SetTop(text, centerY - 10);
            WaveformCanvas.Children.Add(text);
        }

        // Generate synthetic waveform from text length
        public void GenerateSyntheticWaveform(string text, double estimatedDuration = 10.0)
        {
            // Store the original text for later segment extraction
            originalText = text;
            audioDuration = estimatedDuration;
            
            // Generate fake waveform data based on text
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int sampleCount = 800;
            audioSamples = new float[sampleCount];
            
            Random rand = new Random();
            for (int i = 0; i < sampleCount; i++)
            {
                // Create speech-like pattern
                double position = (double)i / sampleCount;
                int wordIndex = (int)(position * words.Length);
                
                if (wordIndex < words.Length)
                {
                    // Higher amplitude for words, lower for spaces
                    float baseAmplitude = (i % 10 < 7) ? 0.5f : 0.1f;
                    audioSamples[i] = baseAmplitude * (float)(rand.NextDouble() * 2 - 1);
                }
            }
            
            DrawWaveform();
            DrawTimeline();
            
            // Parse <split> tags and add markers
            ParseAndAddSplitMarkers(text);
        }

        // Parse text for split tags and add markers
        private void ParseAndAddSplitMarkers(string text)
        {
            Markers.Clear();
            
            var parts = System.Text.RegularExpressions.Regex.Split(text, @"<\s*split\s*/?\s*>");
            if (parts.Length > 1)
            {
                double segmentDuration = audioDuration / parts.Length;
                
                for (int i = 0; i <= parts.Length; i++)
                {
                    double time = i * segmentDuration;
                    var voice = AvailableVoices.Count > 0 ? 
                        AvailableVoices[i % AvailableVoices.Count] : "Default";
                    
                    AddMarker(time, voice);
                }
            }
            else
            {
                // Just add start and end markers
                AddMarker(0);
                AddMarker(audioDuration);
            }
        }

        // Draw the actual waveform
        private void DrawWaveform()
        {
            if (audioSamples == null || audioSamples.Length == 0) return;
            
            WaveformCanvas.Children.Clear();
            
            double width = WaveformCanvas.ActualWidth > 0 ? WaveformCanvas.ActualWidth : 800;
            double height = WaveformCanvas.ActualHeight > 0 ? WaveformCanvas.ActualHeight : 150;
            double centerY = height / 2;
            
            // Draw waveform as filled polygon
            var topPoints = new PointCollection();
            var bottomPoints = new PointCollection();
            
            for (int i = 0; i < audioSamples.Length; i++)
            {
                double x = (double)i / audioSamples.Length * width;
                double amplitude = Math.Abs(audioSamples[i]) * (height / 2) * 0.8;
                
                topPoints.Add(new Point(x, centerY - amplitude));
                bottomPoints.Add(new Point(x, centerY + amplitude));
            }
            
            // Combine points for filled polygon
            var polygon = new Polygon
            {
                Fill = new LinearGradientBrush(Colors.Lime, Colors.Green, 90),
                Opacity = 0.7
            };
            
            foreach (var point in topPoints)
                polygon.Points.Add(point);
            
            for (int i = bottomPoints.Count - 1; i >= 0; i--)
                polygon.Points.Add(bottomPoints[i]);
            
            WaveformCanvas.Children.Add(polygon);
            
            // Draw center line
            var centerLine = new Line
            {
                X1 = 0, Y1 = centerY,
                X2 = width, Y2 = centerY,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                StrokeThickness = 1
            };
            WaveformCanvas.Children.Add(centerLine);
        }

        // Draw timeline
        private void DrawTimeline()
        {
            TimelineRuler.Children.Clear();
            
            double width = TimelineRuler.ActualWidth > 0 ? TimelineRuler.ActualWidth : 800;
            int tickInterval = audioDuration < 30 ? 1 : audioDuration < 120 ? 5 : 10;
            
            for (double seconds = 0; seconds <= audioDuration; seconds += tickInterval)
            {
                double x = (seconds / audioDuration) * width;
                
                var tick = new Line
                {
                    X1 = x, Y1 = 15,
                    X2 = x, Y2 = 25,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                TimelineRuler.Children.Add(tick);
                
                var label = new TextBlock
                {
                    Text = $"{seconds:F0}s",
                    FontSize = 10
                };
                Canvas.SetLeft(label, x - 10);
                Canvas.SetTop(label, 2);
                TimelineRuler.Children.Add(label);
            }
        }

        // Add marker
        public void AddMarker(double timeSeconds, string voiceName = null)
        {
            var marker = new WaveformMarker
            {
                TimeSeconds = Math.Max(0, Math.Min(timeSeconds, audioDuration)),
                VoiceName = voiceName ?? (AvailableVoices.FirstOrDefault() ?? "Default"),
                Id = Guid.NewGuid()
            };
            
            Markers.Add(marker);
        }

        // Public method to get the currently selected segment
        public WaveformSegment GetSelectedSegment()
        {
            return selectedSegment;
        }

        // Update entire display
        public void UpdateDisplay()
        {
            RedrawMarkers();
            RecalculateSegments();
        }

        // Redraw markers
        private void RedrawMarkers()
        {
            // Clear existing markers
            var markersToRemove = OverlayCanvas.Children.OfType<Line>().ToList();
            foreach (var line in markersToRemove)
                OverlayCanvas.Children.Remove(line);
            
            double width = OverlayCanvas.ActualWidth > 0 ? OverlayCanvas.ActualWidth : 800;
            double height = OverlayCanvas.ActualHeight > 0 ? OverlayCanvas.ActualHeight : 150;
            
            foreach (var marker in Markers.OrderBy(m => m.TimeSeconds))
            {
                double x = (marker.TimeSeconds / audioDuration) * width;
                
                var line = new Line
                {
                    X1 = x, Y1 = 0,
                    X2 = x, Y2 = height,
                    Style = FindResource("MarkerStyle") as Style,
                    Tag = marker
                };
                
                OverlayCanvas.Children.Add(line);
            }
        }

        // Recalculate segments and extract text for each segment
        private void RecalculateSegments()
        {
            Segments.Clear();
            segmentRectangles.Clear();
            
            // Clear existing rectangles
            var rectsToRemove = OverlayCanvas.Children.OfType<Rectangle>().ToList();
            foreach (var rect in rectsToRemove)
                OverlayCanvas.Children.Remove(rect);
            
            var sortedMarkers = Markers.OrderBy(m => m.TimeSeconds).ToList();
            if (sortedMarkers.Count < 2) return;
            
            // Extract text parts from the original text if available
            List<string> textParts = new List<string>();
            if (!string.IsNullOrEmpty(originalText))
            {
                var parts = System.Text.RegularExpressions.Regex.Split(originalText, @"<\s*split\s*/?\s*>");
                textParts = parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            }
            
            double width = OverlayCanvas.ActualWidth > 0 ? OverlayCanvas.ActualWidth : 800;
            double height = OverlayCanvas.ActualHeight > 0 ? OverlayCanvas.ActualHeight : 150;
            
            // Create a voice-to-color mapping
            var voiceColors = new Dictionary<string, Color>();
            var distinctVoices = sortedMarkers.Select(m => m.VoiceName).Distinct().ToList();
            
            for (int i = 0; i < distinctVoices.Count; i++)
            {
                voiceColors[distinctVoices[i]] = segmentColors[i % segmentColors.Length];
            }
            
            // Create segments between markers
            for (int i = 0; i < sortedMarkers.Count - 1; i++)
            {
                var segment = new WaveformSegment
                {
                    StartTime = sortedMarkers[i].TimeSeconds,
                    EndTime = sortedMarkers[i + 1].TimeSeconds,
                    VoiceName = sortedMarkers[i].VoiceName,
                    Text = i < textParts.Count ? textParts[i] : "", // Assign text from split parts
                    Settings = new SSMLSettings
                    {
                        RatePercent = sortedMarkers[i].RatePercent,
                        PitchSemitones = sortedMarkers[i].PitchSemitones,
                        VolumeLevel = sortedMarkers[i].VolumeLevel,
                        EmphasisLevel = sortedMarkers[i].EmphasisLevel,
                        BreakMs = sortedMarkers[i].BreakMs
                    }
                };
                
                Segments.Add(segment);
                
                // Create visual rectangle with color based on voice
                double x1 = (segment.StartTime / audioDuration) * width;
                double x2 = (segment.EndTime / audioDuration) * width;
                
                Color segmentColor = voiceColors.TryGetValue(segment.VoiceName, out Color color) 
                    ? color 
                    : segmentColors[i % segmentColors.Length];
                
                var rect = new Rectangle
                {
                    Width = x2 - x1,
                    Height = height,
                    Fill = new SolidColorBrush(segmentColor),
                    Style = FindResource("SegmentStyle") as Style,
                    Tag = segment
                };
                
                Canvas.SetLeft(rect, x1);
                Canvas.SetTop(rect, 0);
                Canvas.SetZIndex(rect, -1);
                
                rect.MouseRightButtonDown += Segment_MouseRightButtonDown;
                rect.MouseEnter += Segment_MouseEnter;
                rect.MouseLeave += Segment_MouseLeave;
                
                OverlayCanvas.Children.Add(rect);
                segmentRectangles[segment] = rect;
            }
            
            UpdateLabels();
            
            // Notify MainViewModel of segment changes
            if (viewModel != null)
            {
                viewModel.WaveformSegments = Segments.ToList();
            }
        }

        // Update voice labels
        private void UpdateLabels()
        {
            LabelsCanvas.Children.Clear();
            
            double width = LabelsCanvas.ActualWidth > 0 ? LabelsCanvas.ActualWidth : 800;
            
            foreach (var segment in Segments)
            {
                double x1 = (segment.StartTime / audioDuration) * width;
                double x2 = (segment.EndTime / audioDuration) * width;
                
                var label = new TextBlock
                {
                    Text = segment.VoiceName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                };
                
                Canvas.SetLeft(label, x1 + 5);
                Canvas.SetTop(label, 5);
                LabelsCanvas.Children.Add(label);
            }
        }

        // Mouse event handlers
        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(OverlayCanvas);
            double time = (clickPos.X / OverlayCanvas.ActualWidth) * audioDuration;
            
            // Check if Ctrl is held for adding marker
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Add marker with Ctrl+Click
                AddMarker(time);
                UpdateSegmentInfo($"Marker added at {time:F1}s");
            }
            else
            {
                // Select segment with regular click
                SelectSegmentAtTime(time);
            }
        }

        private void SelectSegmentAtTime(double time)
        {
            // Clear previous selection visually
            if (selectedSegmentRectangle != null)
            {
                selectedSegmentRectangle.Opacity = 0.3; // Reset to default opacity
                selectedSegmentRectangle = null;
            }
            
            // Find segment at this time
            selectedSegment = Segments.FirstOrDefault(s => time >= s.StartTime && time <= s.EndTime);
            
            if (selectedSegment != null)
            {
                // Find the rectangle for this segment
                if (segmentRectangles.TryGetValue(selectedSegment, out Rectangle rect))
                {
                    selectedSegmentRectangle = rect;
                    selectedSegmentRectangle.Opacity = 0.6; // Highlight selected segment
                    UpdateSegmentInfo($"Selected: {selectedSegment.StartTime:F1}s - {selectedSegment.EndTime:F1}s | Voice: {selectedSegment.VoiceName}");
                }
            }
            else
            {
                selectedSegment = null;
                selectedSegmentRectangle = null;
                UpdateSegmentInfo("Click on a segment to select");
            }
        }

        private void OverlayCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click handled by segment rectangles
        }

        private void Segment_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var rect = sender as Rectangle;
            var segment = rect.Tag as WaveformSegment;
            
            // Set as selected segment when right-clicking
            if (segment != null)
            {
                selectedSegment = segment;
                selectedSegmentRectangle = rect;
                currentEditingSegment = segment;
                
                // Find the corresponding marker for this segment's start time
                var marker = Markers.FirstOrDefault(m => Math.Abs(m.TimeSeconds - segment.StartTime) < 0.01);
                
                // Populate popup with current settings
                PopupVoiceCombo.ItemsSource = AvailableVoices;
                PopupVoiceCombo.SelectedItem = segment.VoiceName ?? marker?.VoiceName;
                
                PopupRateSlider.Value = marker?.RatePercent ?? segment.Settings?.RatePercent ?? 0;
                PopupPitchSlider.Value = marker?.PitchSemitones ?? segment.Settings?.PitchSemitones ?? 0;
                PopupBreakSlider.Value = marker?.BreakMs ?? segment.Settings?.BreakMs ?? 0;
                
                // Set combo box selections
                SelectComboBoxItem(PopupVolumeCombo, marker?.VolumeLevel ?? segment.Settings?.VolumeLevel ?? "medium");
                SelectComboBoxItem(PopupEmphasisCombo, marker?.EmphasisLevel ?? segment.Settings?.EmphasisLevel ?? "none");
                
                // Show popup
                SegmentEditorPopup.IsOpen = true;
            }
            
            e.Handled = true;
        }

        private void Segment_MouseEnter(object sender, MouseEventArgs e)
        {
            var rect = sender as Rectangle;
            var segment = rect.Tag as WaveformSegment;
            
            if (segment != null && segment != selectedSegment)
            {
                UpdateSegmentInfo($"Hover: {segment.StartTime:F1}s - {segment.EndTime:F1}s | Voice: {segment.VoiceName}");
            }
        }

        private void Segment_MouseLeave(object sender, MouseEventArgs e)
        {
            if (selectedSegment != null)
            {
                UpdateSegmentInfo($"Selected: {selectedSegment.StartTime:F1}s - {selectedSegment.EndTime:F1}s | Voice: {selectedSegment.VoiceName}");
            }
            else
            {
                UpdateSegmentInfo("Click on a segment to select");
            }
        }

        // Popup button handlers
        private void ApplySegmentSettings_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditingSegment != null)
            {
                string newVoiceName = PopupVoiceCombo.SelectedItem as string ?? "Default";
                
                // Update the segment
                currentEditingSegment.VoiceName = newVoiceName;
                
                if (currentEditingSegment.Settings == null)
                    currentEditingSegment.Settings = new SSMLSettings();
                
                currentEditingSegment.Settings.RatePercent = (int)PopupRateSlider.Value;
                currentEditingSegment.Settings.PitchSemitones = (int)PopupPitchSlider.Value;
                currentEditingSegment.Settings.BreakMs = (int)PopupBreakSlider.Value;
                currentEditingSegment.Settings.VolumeLevel = (PopupVolumeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "medium";
                currentEditingSegment.Settings.EmphasisLevel = (PopupEmphasisCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "none";
                
                // Update the corresponding marker
                var sortedMarkers = Markers.OrderBy(m => m.TimeSeconds).ToList();
                int segmentIndex = Segments.IndexOf(currentEditingSegment);
                
                if (segmentIndex >= 0 && segmentIndex < sortedMarkers.Count)
                {
                    var marker = sortedMarkers[segmentIndex];
                    marker.VoiceName = newVoiceName;
                    marker.RatePercent = currentEditingSegment.Settings.RatePercent;
                    marker.PitchSemitones = currentEditingSegment.Settings.PitchSemitones;
                    marker.VolumeLevel = currentEditingSegment.Settings.VolumeLevel;
                    marker.EmphasisLevel = currentEditingSegment.Settings.EmphasisLevel;
                    marker.BreakMs = currentEditingSegment.Settings.BreakMs;
                }
                
                UpdateDisplay();
                UpdateSegmentInfo($"Applied voice '{newVoiceName}' to segment {segmentIndex + 1}");
                
                if (viewModel != null)
                {
                    viewModel.WaveformSegments = Segments.ToList();
                }
            }
            
            SegmentEditorPopup.IsOpen = false;
        }

        private void DeleteMarker_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditingSegment != null)
            {
                var markerToDelete = Markers.FirstOrDefault(m => 
                    Math.Abs(m.TimeSeconds - currentEditingSegment.StartTime) < 0.01);
                
                if (markerToDelete != null && Markers.Count > 2)
                {
                    Markers.Remove(markerToDelete);
                    UpdateSegmentInfo($"Marker deleted");
                }
            }
            
            SegmentEditorPopup.IsOpen = false;
        }

        // Play selected segment
        public async Task<bool> PlaySelectedSegmentAsync()
        {
            if (selectedSegment == null || string.IsNullOrWhiteSpace(selectedSegment.Text))
            {
                if (viewModel != null)
                {
                    viewModel.StatusText = "No text in selected segment";
                }
                return false;
            }

            if (viewModel != null)
            {
                var provider = viewModel.GetCurrentProvider();
                if (provider != null)
                {
                    // Apply voice settings for this segment
                    if (!string.IsNullOrEmpty(selectedSegment.VoiceName))
                    {
                        provider.SetVoice(selectedSegment.VoiceName);
                    }
                    
                    // Start visual playback
                    int segmentIndex = Segments.IndexOf(selectedSegment);
                    StartSegmentPlayback(segmentIndex);
                    
                    // Speak the text
                    bool success = await provider.SpeakAsync(
                        selectedSegment.Text, 
                        selectedSegment.Settings ?? new SSMLSettings()
                    );
                    
                    // Stop visual playback
                    StopPlayback();
                    
                    return success;
                }
            }
            
            return false;
        }

        private async void PlaySegment_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditingSegment != null)
            {
                SegmentEditorPopup.IsOpen = false;
                selectedSegment = currentEditingSegment;
                await PlaySelectedSegmentAsync();
            }
        }

        private void CancelSegmentEdit_Click(object sender, RoutedEventArgs e)
        {
            SegmentEditorPopup.IsOpen = false;
        }

        // Playback visualization methods
        public void StartSegmentPlayback(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= Segments.Count) return;
            
            currentPlayingSegmentIndex = segmentIndex;
            var segment = Segments[segmentIndex];
            
            // Highlight the playing segment
            if (segmentRectangles.TryGetValue(segment, out Rectangle rect))
            {
                rect.Tag = rect.Opacity;
                rect.Opacity = 0.7;
                rect.Stroke = Brushes.Yellow;
                rect.StrokeThickness = 3;
            }
            
            // Create progress bar if it doesn't exist
            if (playbackProgressBar == null)
            {
                playbackProgressBar = new Rectangle
                {
                    Width = 2,
                    Fill = Brushes.Red,
                    Opacity = 0.8,
                    IsHitTestVisible = false
                };
                Canvas.SetZIndex(playbackProgressBar, 100);
                OverlayCanvas.Children.Add(playbackProgressBar);
            }
            
            // Position progress bar at segment start
            double width = OverlayCanvas.ActualWidth > 0 ? OverlayCanvas.ActualWidth : 800;
            double startX = (segment.StartTime / audioDuration) * width;
            Canvas.SetLeft(playbackProgressBar, startX);
            Canvas.SetTop(playbackProgressBar, 0);
            playbackProgressBar.Height = OverlayCanvas.ActualHeight;
            playbackProgressBar.Visibility = Visibility.Visible;
            
            // Start animation timer
            playbackStartTime = DateTime.Now;
            playbackPosition = segment.StartTime;
            
            if (playbackTimer == null)
            {
                playbackTimer = new System.Windows.Threading.DispatcherTimer();
                playbackTimer.Interval = TimeSpan.FromMilliseconds(50);
                playbackTimer.Tick += PlaybackTimer_Tick;
            }
            
            playbackTimer.Start();
        }

        public void StopPlayback()
        {
            playbackTimer?.Stop();
            
            if (playbackProgressBar != null)
            {
                playbackProgressBar.Visibility = Visibility.Collapsed;
            }
            
            if (currentPlayingSegmentIndex >= 0 && currentPlayingSegmentIndex < Segments.Count)
            {
                var segment = Segments[currentPlayingSegmentIndex];
                if (segmentRectangles.TryGetValue(segment, out Rectangle rect))
                {
                    if (rect.Tag is double originalOpacity)
                    {
                        rect.Opacity = originalOpacity;
                    }
                    else
                    {
                        rect.Opacity = 0.3;
                    }
                    rect.Stroke = null;
                    rect.StrokeThickness = 0;
                }
            }
            
            currentPlayingSegmentIndex = -1;
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (currentPlayingSegmentIndex < 0 || currentPlayingSegmentIndex >= Segments.Count)
            {
                StopPlayback();
                return;
            }
            
            var segment = Segments[currentPlayingSegmentIndex];
            var elapsed = DateTime.Now - playbackStartTime;
            
            playbackPosition = segment.StartTime + elapsed.TotalSeconds;
            
            if (!ignoreSegmentEndDuringPlayback && playbackPosition > segment.EndTime)
            {
                StopPlayback();
                return;
            }
            
            double width = OverlayCanvas.ActualWidth > 0 ? OverlayCanvas.ActualWidth : 800;
            double x = (playbackPosition / audioDuration) * width;
            Canvas.SetLeft(playbackProgressBar, x);
        }

        public void StartMultiSegmentPlayback()
        {
            currentPlayingSegmentIndex = 0;
            if (Segments.Count > 0)
            {
                StartSegmentPlayback(0);
            }
        }

        public void AdvanceToNextSegment()
        {
            StopPlayback();
            currentPlayingSegmentIndex++;
            if (currentPlayingSegmentIndex < Segments.Count)
            {
                StartSegmentPlayback(currentPlayingSegmentIndex);
            }
        }

        // Helper methods
        private void SelectComboBoxItem(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content.ToString() == value)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private void UpdateSegmentInfo(string info)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                var infoText = mainWindow.FindName("SegmentInfoText") as TextBlock;
                if (infoText != null)
                {
                    infoText.Text = info;
                }
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            if (audioSamples != null)
            {
                DrawWaveform();
                DrawTimeline();
                UpdateDisplay();
            }
        }
    }

    // Data models
    public class WaveformMarker
    {
        public Guid Id { get; set; }
        public double TimeSeconds { get; set; }
        public string VoiceName { get; set; }
        public int RatePercent { get; set; }
        public int PitchSemitones { get; set; }
        public string VolumeLevel { get; set; } = "medium";
        public string EmphasisLevel { get; set; } = "none";
        public int BreakMs { get; set; }
    }

    public class WaveformSegment
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public string VoiceName { get; set; }
        public string Text { get; set; }
        public SSMLSettings Settings { get; set; }
    }
}