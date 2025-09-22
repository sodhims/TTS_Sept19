using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Microsoft.Win32;
using TTS1.WPF.Controls;
using TTS1.WPF.Dialogs;
using TTS1.WPF.Utils;

namespace TTS1.WPF
{
    public partial class MainWindow : Window
    {
        // Fields
        private WaveformControl waveformControl;
        private MainViewModel viewModel;
        private string storedGoogleApiKey;
        private bool isPlaying = false;
        private Dictionary<int, string> voiceAssignments; // Store voice assignments from tags
        private int currentPlayingSegmentIndex = -1; // Track current segment for highlighting

        // Constructor
        public MainWindow()
        {
            InitializeComponent();

            viewModel = new MainViewModel();
            DataContext = viewModel;

            InitializeDefaults();
            InitializeWaveformControl();
            UpdateProviderMenuCheckmarks();
        }

        // Initialize WaveformControl
        private void InitializeWaveformControl()
        {
            // Create the control
            waveformControl = new WaveformControl();

            // Set up the control with view model and available voices
            waveformControl.SetViewModel(viewModel);
            waveformControl.AvailableVoices = viewModel.AvailableVoices?.ToList() ?? new List<string>();

            // Place the control into the container
            WaveformContainer.Child = waveformControl;
        }

        private void InitializeDefaults()
        {
            // Set default input text
            viewModel.InputText = "This is section 1. <split>\r\nThis is section 2. <split>\r\nThis is section 3.";
        }

        // Event Handlers
        private void StartPlayback()
        {
            isPlaying = true;
            btnStopPlay.IsEnabled = true;
            btnToolbarStopPlay.IsEnabled = true;
            // The colors are now handled by the XAML style triggers
        }

        private void StopPlayback()
        {
            isPlaying = false;
            btnStopPlay.IsEnabled = false;
            btnToolbarStopPlay.IsEnabled = false;
            // The colors are now handled by the XAML style triggers
        }

        private async void btnTestVoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartPlayback();
                viewModel.StatusText = "Testing voice...";

                bool success = await viewModel.TestVoiceAsync();
                
                if (!success && viewModel.IsGoogleProviderSelected)
                {
                    MessageBox.Show(
                        "Google TTS test failed. Please check:\n\n" +
                        "1. Your API key is correct\n" +
                        "2. Text-to-Speech API is enabled in Google Cloud Console\n" +
                        "3. Billing is enabled for your project\n" +
                        "4. You have internet connection\n\n" +
                        "Error details are in the Output window",
                        "Google TTS Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                viewModel.StatusText = success ? "Test completed" : "Test failed";
            }
            finally
            {
                StopPlayback();
            }
        }

        private async void btnSpeakAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnSpeakAll.IsEnabled = false;
                StartPlayback();
                viewModel.StatusText = "Speaking...";
                
                // Pass current segments to viewModel if they exist
                if (waveformControl != null && waveformControl.Segments.Count > 0)
                {
                    viewModel.WaveformSegments = waveformControl.Segments.ToList();
                    viewModel.StatusText = "Speaking multi-voice...";
                    
                    // Start playback visualization
                    waveformControl.StartMultiSegmentPlayback();
                    
                    // Speak with highlighting
                    await SpeakAllWithHighlighting();
                }
                else
                {
                    // Single voice - highlight entire text
                    HighlightEntireText();
                    await viewModel.SpeakAllAsync();
                }
                
                // Stop visualization when done
                waveformControl?.StopPlayback();
                ClearTextHighlight();
                viewModel.StatusText = "Ready";
            }
            finally
            {
                btnSpeakAll.IsEnabled = true;
                StopPlayback();
                waveformControl?.StopPlayback();
                ClearTextHighlight();
            }
        }

        // Method to highlight text for the current segment
        private void HighlightSegmentText(int segmentIndex)
        {
            if (segmentIndex < 0 || string.IsNullOrEmpty(viewModel.InputText))
            {
                ClearTextHighlight();
                return;
            }

            // Split the text by <split> tags to find segment boundaries
            var parts = System.Text.RegularExpressions.Regex.Split(
                viewModel.InputText, 
                @"<\s*split\s*/?\s*>", 
                RegexOptions.IgnoreCase);
            
            if (segmentIndex >= parts.Length)
            {
                ClearTextHighlight();
                return;
            }

            // Calculate the start position of this segment
            int startPos = 0;
            string searchText = viewModel.InputText;
            
            for (int i = 0; i < segmentIndex; i++)
            {
                startPos += parts[i].Length;
                // Find and account for the split tag
                int splitIndex = searchText.IndexOf("<split", startPos, StringComparison.OrdinalIgnoreCase);
                if (splitIndex >= 0)
                {
                    int splitEnd = searchText.IndexOf(">", splitIndex);
                    if (splitEnd >= 0)
                    {
                        startPos = splitEnd + 1;
                        // Skip any whitespace after the tag
                        while (startPos < searchText.Length && char.IsWhiteSpace(searchText[startPos]))
                            startPos++;
                    }
                }
            }

            // Get the length of the current segment
            int length = parts[segmentIndex].Trim().Length;

            // Apply highlighting in the TextBox
            Dispatcher.Invoke(() =>
            {
                try
                {
                    txtInput.Focus();
                    txtInput.Select(startPos, length);
                    
                    // Scroll to make selection visible
                    if (startPos < txtInput.Text.Length)
                    {
                        var rect = txtInput.GetRectFromCharacterIndex(startPos);
                        if (rect != Rect.Empty)
                        {
                            txtInput.ScrollToLine(txtInput.GetLineIndexFromCharacterIndex(startPos));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Highlight error: {ex.Message}");
                }
            });
        }

        // Method to highlight entire text
        private void HighlightEntireText()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    txtInput.Focus();
                    txtInput.SelectAll();
                }
                catch { }
            });
        }

        // Method to clear text highlighting
        private void ClearTextHighlight()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    txtInput.Select(0, 0);
                }
                catch { }
            });
        }

        // New method to speak segments with synchronized highlighting
        private async Task SpeakAllWithHighlighting()
        {
            var provider = viewModel.GetCurrentProvider();
            if (provider == null) return;

            var parts = System.Text.RegularExpressions.Regex.Split(
                viewModel.InputText ?? "", 
                @"<\s*split\s*/?\s*>");
            
            string originalVoice = viewModel.SelectedVoice;
            
            try
            {
                for (int i = 0; i < viewModel.WaveformSegments.Count && i < parts.Length; i++)
                {
                    if (!isPlaying) break; // Check if stopped
                    
                    currentPlayingSegmentIndex = i;
                    var segment = viewModel.WaveformSegments[i];
                    var text = parts[i].Trim();
                    
                    if (string.IsNullOrEmpty(text)) continue;
                    
                    // Highlight current segment
                    HighlightSegmentText(i);
                    
                    // Advance waveform visualization
                    if (i > 0) waveformControl?.AdvanceToNextSegment();
                    
                    // Update status
                    viewModel.StatusText = $"Speaking segment {i + 1} of {viewModel.WaveformSegments.Count}";
                    
                    // Set voice for this segment
                    if (!string.IsNullOrEmpty(segment.VoiceName) && 
                        viewModel.AvailableVoices.Contains(segment.VoiceName))
                    {
                        provider.SetVoice(segment.VoiceName);
                        viewModel.SelectedVoice = segment.VoiceName;
                    }
                    
                    var settings = segment.Settings ?? new SSMLSettings
                    {
                        RatePercent = viewModel.RatePercent,
                        PitchSemitones = viewModel.PitchSemitones,
                        VolumeLevel = viewModel.VolumeLevel,
                        EmphasisLevel = viewModel.EmphasisLevel,
                        BreakMs = viewModel.BreakMs,
                        UseSSML = viewModel.UseSSML
                    };
                    
                    // Speak this segment
                    bool success = await provider.SpeakAsync(text, settings);
                    
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to speak segment {i}");
                        break;
                    }
                }
            }
            finally
            {
                // Clear highlighting
                ClearTextHighlight();
                currentPlayingSegmentIndex = -1;
                
                // Restore original voice
                if (!string.IsNullOrEmpty(originalVoice))
                {
                    provider.SetVoice(originalVoice);
                    viewModel.SelectedVoice = originalVoice;
                }
            }
        }

        private async void btnSaveWav_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "WAV files (*.wav)|*.wav",
                DefaultExt = "wav"
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.StatusText = "Saving WAV...";
                
                // Pass current segments to viewModel if they exist
                if (waveformControl != null && waveformControl.Segments.Count > 0)
                {
                    viewModel.WaveformSegments = waveformControl.Segments.ToList();
                }
                
                // Check if we have segments for multi-voice
                if (viewModel.WaveformSegments != null && viewModel.WaveformSegments.Count > 0)
                {
                    // Save multi-voice
                    bool success = await viewModel.SaveMultiVoiceWavAsync(dialog.FileName);
                    viewModel.StatusText = success ? 
                        $"Multi-voice saved: {Path.GetFileName(dialog.FileName)}" : 
                        "Failed to save WAV";
                }
                else
                {
                    // Save single voice
                    bool success = await viewModel.SaveToWavAsync(dialog.FileName);
                    viewModel.StatusText = success ? 
                        $"Saved: {Path.GetFileName(dialog.FileName)}" : 
                        "Failed to save WAV";
                }
            }
        }

        private async void btnSaveMp3_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "MP3 files (*.mp3)|*.mp3",
                DefaultExt = "mp3"
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.StatusText = "Saving MP3...";
                bool success = await viewModel.SaveToMp3Async(dialog.FileName);
                viewModel.StatusText = success ? 
                    $"Saved: {Path.GetFileName(dialog.FileName)}" : 
                    "Failed to save MP3";
            }
        }

        private async void btnSaveSplitWav_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "WAV files (*.wav)|*.wav",
                DefaultExt = "wav",
                Title = "Choose base filename for split WAV files"
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.StatusText = "Splitting to WAV files...";
                var files = await viewModel.SaveSplitToWavAsync(dialog.FileName);

                if (files.Count > 0)
                {
                    viewModel.StatusText = $"Created {files.Count} WAV files";
                    MessageBox.Show(
                        $"Created {files.Count} files:\n" +
                        string.Join("\n", files.Select(f => Path.GetFileName(f))),
                        "Files Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    viewModel.StatusText = "Failed to create WAV files";
                }
            }
        }

        private async void btnSaveSplitMp3_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "MP3 files (*.mp3)|*.mp3",
                DefaultExt = "mp3",
                Title = "Choose base filename for split MP3 files"
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.StatusText = "Splitting to MP3 files...";
                var files = await viewModel.SaveSplitToMp3Async(dialog.FileName);

                if (files.Count > 0)
                {
                    viewModel.StatusText = $"Created {files.Count} MP3 files";
                    MessageBox.Show(
                        $"Created {files.Count} files:\n" +
                        string.Join("\n", files.Select(f => Path.GetFileName(f))),
                        "Files Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    viewModel.StatusText = "Failed to create MP3 files";
                }
            }
        }
        
        private void StopPlay_Click(object sender, RoutedEventArgs e)
        {
            // Stop all types of playback
            
            // Clear text highlighting
            ClearTextHighlight();
            currentPlayingSegmentIndex = -1;
            
            // Stop waveform visualization
            waveformControl?.StopPlayback();
            
            // Stop current TTS provider
            var provider = viewModel?.GetCurrentProvider();
            provider?.Stop();
            
            // Update UI
            StopPlayback();
            viewModel.StatusText = "Playback stopped";
            
            // Re-enable any disabled buttons
            btnSpeakAll.IsEnabled = true;
            btnPlaySegment.IsEnabled = true;
            btnGenerateMultiVoice.IsEnabled = true;
        }

        // Waveform Control Event Handlers
        private void btnProcessVoiceTags_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(viewModel.InputText))
            {
                MessageBox.Show("Please enter some text first.", "No Text", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check if text contains voice tags
            if (!Regex.IsMatch(viewModel.InputText, @"<\s*voice\s*=\s*\d+\s*>", RegexOptions.IgnoreCase))
            {
                MessageBox.Show(
                    "No voice tags found in the text.\n\n" +
                    "Use tags like <voice=1>, <voice=2>, etc. to specify different voices.\n" +
                    "Example: <voice=1>Hello, <voice=2>How are you?",
                    "No Voice Tags", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                // Get available voices
                var availableVoices = viewModel.AvailableVoices?.ToList() ?? new List<string>();
                
                if (availableVoices.Count == 0)
                {
                    MessageBox.Show("No voices available. Please select a TTS provider first.", 
                        "No Voices", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Store the original text with voice tags
                string originalText = viewModel.InputText;
                
                // Process the voice tags
                string processedText = VoiceTagProcessor.ProcessVoiceTags(originalText, availableVoices);
                
                // Get voice assignments for each segment
                voiceAssignments = VoiceTagProcessor.GetVoiceAssignments(processedText, originalText, availableVoices);
                
                // Update the input text with split tags
                viewModel.InputText = processedText;
                
                // Show summary
                var segments = VoiceTagProcessor.ParseVoiceSegments(originalText, availableVoices);
                var summary = $"Processed {segments.Count} voice segments:\n\n";
                for (int i = 0; i < segments.Count && i < 5; i++) // Show first 5 segments
                {
                    var segment = segments[i];
                    var preview = segment.Text.Length > 50 ? 
                        segment.Text.Substring(0, 50) + "..." : segment.Text;
                    summary += $"Segment {i + 1}: Voice {segment.VoiceIndex + 1} ({segment.VoiceName})\n";
                    summary += $"  \"{preview}\"\n\n";
                }
                
                if (segments.Count > 5)
                    summary += $"... and {segments.Count - 5} more segments.";
                
                MessageBox.Show(summary, "Voice Tags Processed", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                viewModel.StatusText = $"Processed {segments.Count} voice segments";
                
                // Optionally auto-generate waveform
                if (MessageBox.Show("Would you like to generate the audio preview now?", 
                    "Generate Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    btnGenerateWaveform_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing voice tags: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnGenerateWaveform_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(viewModel.InputText))
            {
                MessageBox.Show("Please enter some text first.", "No Text", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Generate preview waveform from current text
            viewModel.StatusText = "Generating audio preview...";
            btnGenerateWaveform.IsEnabled = false;
            
            // Update available voices in the waveform control
            waveformControl.AvailableVoices = viewModel.AvailableVoices?.ToList() ?? new List<string>();
            
            // Generate synthetic waveform based on text
            waveformControl.GenerateSyntheticWaveform(viewModel.InputText, 10.0);
            
            // If we have voice assignments from processing tags, apply them
            if (voiceAssignments != null && voiceAssignments.Count > 0 && waveformControl.Segments.Count > 0)
            {
                for (int i = 0; i < waveformControl.Segments.Count; i++)
                {
                    if (voiceAssignments.TryGetValue(i, out string assignedVoice))
                    {
                        waveformControl.Segments[i].VoiceName = assignedVoice;
                        
                        // Update the corresponding marker
                        var sortedMarkers = waveformControl.Markers.OrderBy(m => m.TimeSeconds).ToList();
                        if (i < sortedMarkers.Count)
                        {
                            sortedMarkers[i].VoiceName = assignedVoice;
                        }
                    }
                }
                
                // Force a visual update to show the voice assignments
                waveformControl.UpdateDisplay();
                viewModel.StatusText = "Preview generated with voice assignments from tags";
            }
            
            // Pass segments to viewModel after generation
            if (waveformControl.Segments.Count > 0)
            {
                viewModel.WaveformSegments = waveformControl.Segments.ToList();
            }
            
            // Update info
            SegmentInfoText.Text = "Preview generated. Ctrl+Click to add markers. Left-click to select segment. Right-click to edit voice.";
            viewModel.StatusText = "Preview ready - Ctrl+Click to add markers, Click to select, Right-click to edit";
            
            btnGenerateWaveform.IsEnabled = true;
        }
        
        private async void btnPlaySegment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnPlaySegment.IsEnabled = false;
                StartPlayback();
                viewModel.StatusText = "Playing selected segment...";
                
                // Call the waveform control's play selected segment method
                if (waveformControl != null)
                {
                    await waveformControl.PlaySelectedSegmentAsync();
                }
                else
                {
                    viewModel.StatusText = "No segment selected";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing segment: {ex.Message}", "Playback Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                viewModel.StatusText = "Playback error";
            }
            finally
            {
                btnPlaySegment.IsEnabled = true;
                StopPlayback();
                viewModel.StatusText = "Ready";
            }
        }

        private void btnClearMarkers_Click(object sender, RoutedEventArgs e)
        {
            waveformControl.Markers.Clear();
            SegmentInfoText.Text = "All markers cleared";
            viewModel.StatusText = "Markers cleared";
        }
        
        private async void btnGenerateMultiVoice_Click(object sender, RoutedEventArgs e)
        {
            // Generate multi-voice audio from segments
            if (waveformControl.Segments.Count == 0)
            {
                MessageBox.Show("Please generate a preview and add markers to create segments first.", "No Segments", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var dialog = new SaveFileDialog
            {
                Filter = "WAV files (*.wav)|*.wav",
                DefaultExt = "wav",
                Title = "Save Multi-Voice Audio"
            };
            
            if (dialog.ShowDialog() == true)
            {
                viewModel.StatusText = "Generating multi-voice audio...";
                btnGenerateMultiVoice.IsEnabled = false;
                
                try
                {
                    // Pass segments to viewModel
                    viewModel.WaveformSegments = waveformControl.Segments.ToList();
                    
                    // Generate multi-voice WAV
                    bool success = await viewModel.SaveMultiVoiceWavAsync(dialog.FileName);
                    
                    if (success)
                    {
                        viewModel.StatusText = $"Multi-voice audio saved: {Path.GetFileName(dialog.FileName)}";
                        MessageBox.Show($"Multi-voice audio saved successfully to:\n{dialog.FileName}", 
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        viewModel.StatusText = "Failed to generate multi-voice audio";
                        MessageBox.Show("Failed to generate multi-voice audio. Check the Output window for details.", 
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating multi-voice audio:\n{ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    viewModel.StatusText = "Error generating multi-voice audio";
                }
                finally
                {
                    btnGenerateMultiVoice.IsEnabled = true;
                }
            }
        }

        #region Menu Event Handlers

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    viewModel.InputText = File.ReadAllText(dialog.FileName);
                    viewModel.StatusText = $"Loaded: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuSaveText_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, viewModel.InputText);
                    viewModel.StatusText = $"Saved text: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuInsertSplit_Click(object sender, RoutedEventArgs e)
        {
            var textBox = FindName("txtInput") as TextBox;
            if (textBox != null)
            {
                int caretIndex = textBox.CaretIndex;
                viewModel.InputText = viewModel.InputText.Insert(caretIndex, " <split> ");
                textBox.CaretIndex = caretIndex + 9;
            }
        }

        private void MenuWindowsProvider_Click(object sender, RoutedEventArgs e)
        {
            viewModel.SelectedProviderIndex = 0;
            UpdateProviderMenuCheckmarks();
            viewModel.StatusText = "Switched to Windows SAPI";
        }

        private async void MenuGoogleProvider_Click(object sender, RoutedEventArgs e)
        {
            // Show Google API key dialog
            var dialog = new GoogleApiKeyDialog(storedGoogleApiKey);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                storedGoogleApiKey = dialog.ApiKey;
                
                if (!string.IsNullOrWhiteSpace(storedGoogleApiKey))
                {
                    viewModel.GoogleApiKey = storedGoogleApiKey;
                    viewModel.SelectedProviderIndex = 1;
                    UpdateProviderMenuCheckmarks();
                    viewModel.StatusText = "Switched to Google Cloud TTS";
                }
                else
                {
                    // User didn't enter a key, stay on Windows provider
                    viewModel.SelectedProviderIndex = 0;
                    UpdateProviderMenuCheckmarks();
                    viewModel.StatusText = "Google API key required - using Windows SAPI";
                }
            }
            else
            {
                // User cancelled, revert menu selection
                UpdateProviderMenuCheckmarks();
            }
        }

        private void UpdateProviderMenuCheckmarks()
        {
            MenuWindowsProvider.IsChecked = viewModel.SelectedProviderIndex == 0;
            MenuGoogleProvider.IsChecked = viewModel.SelectedProviderIndex == 1;
        }

        private void MenuStop_Click(object sender, RoutedEventArgs e)
        {
            StopPlay_Click(sender, e);
        }

        // Help Menu Handlers
        private void MenuGettingStarted_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Welcome to SpeechCraft!\n\n" +
                "Quick Start:\n" +
                "1. Type or paste your text in the input area\n" +
                "2. Select a voice from the toolbar dropdown\n" +
                "3. Click 'Speak All' to hear your text\n" +
                "4. Use <split> tags to create segments\n\n" +
                "For multi-voice narration:\n" +
                "1. Add <split> tags in your text\n" +
                "2. Click 'Generate Audio Preview'\n" +
                "3. Right-click segments to assign different voices\n" +
                "4. Click 'Generate Multi-Voice Audio' to create your file\n\n" +
                "Tips:\n" +
                "• Adjust Rate and Pitch for different effects\n" +
                "• Use Google Cloud TTS for more natural voices\n" +
                "• Save as MP3 for smaller file sizes",
                "Getting Started with SpeechCraft",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuUserGuide_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "SpeechCraft User Guide\n\n" +
                "FEATURES:\n" +
                "• Multi-provider support (Windows SAPI & Google Cloud)\n" +
                "• Multi-voice generation with segment control\n" +
                "• SSML support for advanced speech control\n" +
                "• Export to WAV and MP3 formats\n" +
                "• Visual waveform editor\n\n" +
                "TEXT FORMATTING:\n" +
                "• Use <split> tags to separate segments\n" +
                "• Each segment can have its own voice\n" +
                "• SSML tags supported when enabled\n\n" +
                "VOICE SETTINGS:\n" +
                "• Rate: Speed of speech (-50% to +50%)\n" +
                "• Pitch: Voice pitch (-12 to +12 semitones)\n" +
                "• Volume: Output volume level\n" +
                "• Emphasis: Text emphasis level\n\n" +
                "WAVEFORM EDITOR:\n" +
                "• Ctrl+Click: Add marker\n" +
                "• Click: Select segment\n" +
                "• Right-Click: Edit segment properties\n\n" +
                "For detailed documentation, visit our website.",
                "SpeechCraft User Guide",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuKeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "SpeechCraft Keyboard Shortcuts:\n\n" +
                "FILE OPERATIONS:\n" +
                "Ctrl+O - Open text file\n" +
                "Ctrl+S - Save text file\n" +
                "Ctrl+Shift+S - Save as...\n" +
                "Alt+F4 - Exit SpeechCraft\n\n" +
                "EDITING:\n" +
                "Ctrl+Z - Undo\n" +
                "Ctrl+Y - Redo\n" +
                "Ctrl+X - Cut\n" +
                "Ctrl+C - Copy\n" +
                "Ctrl+V - Paste\n" +
                "Ctrl+A - Select all\n" +
                "Ctrl+T - Insert split tag\n\n" +
                "PLAYBACK:\n" +
                "F5 - Speak all\n" +
                "F6 - Speak selected segment\n" +
                "F3 - Test current voice\n" +
                "Esc - Stop playback\n\n" +
                "WAVEFORM EDITOR:\n" +
                "Ctrl+Click - Add marker at position\n" +
                "Delete - Remove selected marker\n" +
                "Space - Play/Pause selected segment\n\n" +
                "HELP:\n" +
                "F1 - Show keyboard shortcuts",
                "Keyboard Shortcuts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuGoogleSetup_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Would you like to open the Google Cloud Text-to-Speech setup guide in your browser?\n\n" +
                "You'll need:\n" +
                "1. A Google Cloud account\n" +
                "2. Billing enabled on your project\n" +
                "3. Text-to-Speech API enabled\n" +
                "4. An API key created",
                "Google Cloud Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://cloud.google.com/text-to-speech/docs/quickstart",
                    UseShellExecute = true
                });
            }
        }

        private void MenuSSMLReference_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Would you like to open the SSML reference documentation in your browser?\n\n" +
                "SSML (Speech Synthesis Markup Language) allows you to:\n" +
                "• Control pronunciation\n" +
                "• Add pauses and breaks\n" +
                "• Emphasize words\n" +
                "• Control prosody\n" +
                "• And much more!",
                "SSML Reference",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://cloud.google.com/text-to-speech/docs/ssml",
                    UseShellExecute = true
                });
            }
        }

        private void MenuReportIssue_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Report an Issue\n\n" +
                "Found a bug or have a suggestion?\n\n" +
                "Please report issues to:\n" +
                "• Email: support@speechcraft.app\n" +
                "• GitHub: github.com/speechcraft/issues\n\n" +
                "When reporting, please include:\n" +
                "• SpeechCraft version (1.0.0)\n" +
                "• Windows version\n" +
                "• Steps to reproduce the issue\n" +
                "• Any error messages\n\n" +
                "Thank you for helping improve SpeechCraft!",
                "Report Issue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "SpeechCraft Update Check\n\n" +
                "You are using SpeechCraft version 1.0.0\n" +
                "This is the latest version.\n\n" +
                "SpeechCraft checks for updates automatically.\n" +
                "You can also check manually at:\n" +
                "www.speechcraft.app/updates",
                "Check for Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "SpeechCraft\n" +
                "Version 1.0.0\n\n" +
                "Professional Text-to-Speech Studio\n\n" +
                "Features:\n" +
                "• Multi-provider support (Windows SAPI & Google Cloud TTS)\n" +
                "• Multi-voice narration with segment control\n" +
                "• Visual waveform editor\n" +
                "• SSML support for advanced control\n" +
                "• Export to WAV and MP3\n" +
                "• Real-time preview and editing\n\n" +
                "© 2024 SpeechCraft\n" +
                "All rights reserved.\n\n" +
                "Built with .NET 8.0 and WPF\n" +
                "Powered by NAudio and Google Cloud",
                "About SpeechCraft",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuImportFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "All Supported|*.txt;*.ssml;*.xml|" +
                         "Text files (*.txt)|*.txt|" +
                         "SSML files (*.ssml)|*.ssml|" +
                         "XML files (*.xml)|*.xml|" +
                         "All files (*.*)|*.*",
                DefaultExt = "txt",
                Title = "Import Text or SSML File"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(dialog.FileName);
                    
                    // Check if it contains split tags
                    if (!content.Contains("<split"))
                    {
                        var result = MessageBox.Show(
                            "Do you want to automatically add <split> tags at paragraph breaks?",
                            "Import Text",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            content = Regex.Replace(content, @"\r?\n\r?\n+", " <split> ");
                        }
                    }
                    
                    viewModel.InputText = content;
                    viewModel.StatusText = $"Imported: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing file: {ex.Message}", "Import Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}