using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TTS1.WPF.Controls;

namespace TTS1.WPF
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private WaveformControl waveformControl;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            DataContext = viewModel;
            
            // Initialize with default values
            InitializeDefaults();
            
            // Initialize waveform control
            InitializeWaveformControl();
        }

        private void InitializeDefaults()
        {
            // Set default input text
            viewModel.InputText = "This is section 1. <split>\r\nThis is section 2. <split>\r\nThis is section 3.";
        }
        
        private void InitializeWaveformControl()
        {
            // Create and add the waveform control
            waveformControl = new WaveformControl();
            
            // Pass the view model reference to the waveform control
            waveformControl.SetViewModel(viewModel);
            
            // Set available voices from the view model
            waveformControl.AvailableVoices = viewModel.AvailableVoices.ToList();
            
            // Subscribe to voice changes
            viewModel.AvailableVoices.CollectionChanged += (s, e) =>
            {
                waveformControl.AvailableVoices = viewModel.AvailableVoices.ToList();
            };
            
            // Add to container
            if (WaveformContainer != null)
            {
                WaveformContainer.Child = waveformControl;
            }
        }

        // Event Handlers
        private void txtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            viewModel.GoogleApiKey = ((PasswordBox)sender).Password;
        }

        private async void btnTestVoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnTestVoice.IsEnabled = false;
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
                btnTestVoice.IsEnabled = true;
            }
        }

        private async void btnSpeakAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnSpeakAll.IsEnabled = false;
                viewModel.StatusText = "Speaking...";
                
                // Pass current segments to viewModel if they exist
                if (waveformControl != null && waveformControl.Segments.Count > 0)
                {
                    viewModel.WaveformSegments = waveformControl.Segments.ToList();
                    viewModel.StatusText = "Speaking multi-voice...";
                }

                await viewModel.SpeakAllAsync();

                viewModel.StatusText = "Ready";
            }
            finally
            {
                btnSpeakAll.IsEnabled = true;
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
        
        // Waveform Control Event Handlers
        private void btnGenerateWaveform_Click(object sender, RoutedEventArgs e)
        {
            // Generate preview waveform from current text
            viewModel.StatusText = "Generating audio preview...";
            btnGenerateWaveform.IsEnabled = false;
            
            // Generate synthetic waveform based on text
            waveformControl.GenerateSyntheticWaveform(viewModel.InputText, 10.0);
            
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
				viewModel.StatusText = "Playing selected segment...";
				
				// Call the waveform control's play selected segment method
				if (waveformControl != null)
				{
					await waveformControl.PlaySelectedSegmentAsync();
				}
				else
				{
					viewModel.StatusText = "Waveform control not initialized";
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
                MessageBox.Show("Please add markers to create segments first.", "No Segments", 
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
    }
}