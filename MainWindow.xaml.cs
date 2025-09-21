using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Microsoft.Win32;
using TTS1.WPF.Controls;
using NAudio.Wave;
using System.Speech.Synthesis;

namespace TTS1.WPF
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private WaveformControl waveformControl;
        
        // Add these missing field declarations:
        private bool _isPlaying = false;
        private System.Speech.Synthesis.SpeechSynthesizer _synthesizer;
        private NAudio.Wave.IWavePlayer waveOut = null;
        private System.Threading.CancellationTokenSource _cancellationTokenSource = null;
        // DO NOT add StatusText field - we'll use viewModel.StatusText instead

        public MainWindow()
        {
            InitializeComponent();
            btnStopVoice.Click += StopVoice_Click;
            viewModel = new MainViewModel();
            DataContext = viewModel;
            
            // Initialize with default values
            InitializeDefaults();
            
            // Initialize waveform control
            InitializeWaveformControl();
			
			//initialize for later use
			_cancellationTokenSource = null;
			waveOut = null;
            
            // Initialize the synthesizer
            _synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
        }
		private void StopVoice_Click(object sender, RoutedEventArgs e)
		{
			StopPlayback();
		}

		private void StopPlayback()
		{
			try
			{
				_isPlaying = false;
				
				// Stop the current provider
				var provider = viewModel.GetCurrentProvider();
				if (provider != null)
				{
					provider.Stop();
				}
				
				// Reset UI
				UpdateUIAfterStop();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error stopping playback: {ex.Message}");
			}
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
				btnStopVoice.IsEnabled = true;
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
                _isPlaying = true;
				btnSpeakAll.IsEnabled = false;
				btnStopVoice.IsEnabled = true;
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
				btnStopVoice.IsEnabled = false; //Added to disable stop button.
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
		public void Stop()
		{
			// Google TTS doesn't have built-in stop for the simple implementation
			// You'd need to implement cancellation tokens for true async cancellation
		}
// Update UI after stopping
		private void UpdateUIAfterStop()
		{
			Dispatcher.Invoke(() =>
			{
				// Re-enable play buttons
				btnTestVoice.IsEnabled = true;
				btnSpeakAll.IsEnabled = true;
				btnGenerateMultiVoice.IsEnabled = true;
				
				// Disable stop buttons
				btnStopVoice.IsEnabled = false;
				btnStopAll.IsEnabled = false;
				btnStopMultiVoice.IsEnabled = false;
				
				// Update status
				viewModel.StatusText = "Playback stopped";
			});
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


		// Add these methods to MainWindow.xaml.cs
		#region File Import Feature

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
				ImportFile(dialog.FileName);
			}
		}

		private async void ImportFile(string filePath)
		{
			try
			{
				string fileContent = File.ReadAllText(filePath);
				string extension = Path.GetExtension(filePath).ToLower();

				switch (extension)
				{
					case ".txt":
						ImportPlainText(fileContent, filePath);
						break;
					case ".ssml":
					case ".xml":
						await ImportSSMLFile(fileContent, filePath);
						break;
					default:
						// Try to detect if it's SSML or plain text
						if (IsSSMLContent(fileContent))
							await ImportSSMLFile(fileContent, filePath);
						else
							ImportPlainText(fileContent, filePath);
						break;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error importing file: {ex.Message}", "Import Error", 
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void ImportPlainText(string content, string filePath)
		{
			// Process plain text - look for paragraph breaks and convert to split tags if desired
			var result = MessageBox.Show(
				"Do you want to automatically add <split> tags at paragraph breaks?",
				"Import Text",
				MessageBoxButton.YesNoCancel,
				MessageBoxImage.Question);

			if (result == MessageBoxResult.Cancel)
				return;

			if (result == MessageBoxResult.Yes)
			{
				// Replace double line breaks with split tags
				content = Regex.Replace(content, @"\r?\n\r?\n+", " <split> ");
				
				// Optionally add splits at sentence ends for long paragraphs
				if (MessageBox.Show(
					"Also add <split> tags at sentence boundaries (. ! ?)?",
					"Import Text",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
					content = Regex.Replace(content, @"([.!?])\s+", "$1 <split> ");
				}
			}

			viewModel.InputText = content;
			viewModel.StatusText = $"Imported: {Path.GetFileName(filePath)}";
			
			// Store in recent files
			AddToRecentFiles(filePath);
		}

		private async Task ImportSSMLFile(string content, string filePath)
		{
			try
			{
				var ssmlData = ParseSSMLContent(content);
				
				if (ssmlData.IsValid)
				{
					// Apply SSML settings to the view model
					ApplySSMLSettings(ssmlData);
					
					viewModel.StatusText = $"Imported SSML: {Path.GetFileName(filePath)}";
					AddToRecentFiles(filePath);
					
					// If multi-voice SSML, generate waveform with segments
					if (ssmlData.HasMultipleVoices)
					{
						GenerateWaveformFromSSML(ssmlData);
					}
				}
				else
				{
					MessageBox.Show(
						"The file appears to be SSML but could not be parsed correctly.\n" +
						"It will be imported as plain text.",
						"SSML Parse Warning",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					
					// Fall back to plain text import
					ImportPlainText(content, filePath);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error parsing SSML: {ex.Message}\n\nImporting as plain text.", 
					"SSML Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				ImportPlainText(content, filePath);
			}
		}

		private bool IsSSMLContent(string content)
		{
			// Check if content looks like SSML
			content = content.Trim();
			return content.StartsWith("<?xml") || 
				   content.StartsWith("<speak") ||
				   (content.Contains("<speak") && content.Contains("</speak>"));
		}

		private SSMLData ParseSSMLContent(string content)
		{
			var ssmlData = new SSMLData();
			
			try
			{
				var doc = new System.Xml.XmlDocument();
				doc.LoadXml(content);
				
				var speakNode = doc.SelectSingleNode("//speak");
				if (speakNode == null)
				{
					// Wrap in speak tags if not present
					doc.LoadXml($"<speak>{content}</speak>");
					speakNode = doc.SelectSingleNode("//speak");
				}
				
				ssmlData.IsValid = true;
				
				// Extract text and voice segments
				var segments = new List<SSMLSegment>();
				var currentVoice = "Default";
				var currentProsody = new ProsodySettings();
				
				ProcessSSMLNode(speakNode, segments, currentVoice, currentProsody);
				
				// Convert segments to text with split tags
				var textBuilder = new StringBuilder();
				string lastVoice = null;
				
				foreach (var segment in segments)
				{
					if (segment.Voice != lastVoice && lastVoice != null)
					{
						textBuilder.Append(" <split> ");
					}
					textBuilder.Append(segment.Text);
					lastVoice = segment.Voice;
				}
				
				ssmlData.ExtractedText = textBuilder.ToString();
				ssmlData.Segments = segments;
				ssmlData.HasMultipleVoices = segments.Select(s => s.Voice).Distinct().Count() > 1;
				
				// Extract global prosody settings from first segment
				if (segments.Count > 0)
				{
					ssmlData.GlobalProsody = segments[0].Prosody;
				}
			}
			catch (Exception ex)
			{
				ssmlData.IsValid = false;
				ssmlData.ParseError = ex.Message;
			}
			
			return ssmlData;
		}

		private void ProcessSSMLNode(XmlNode node, List<SSMLSegment> segments, 
			string currentVoice, ProsodySettings currentProsody)
		{
			foreach (XmlNode child in node.ChildNodes)
			{
				switch (child.NodeType)
				{
					case XmlNodeType.Text:
						var text = child.Value?.Trim();
						if (!string.IsNullOrEmpty(text))
						{
							segments.Add(new SSMLSegment
							{
								Text = text,
								Voice = currentVoice,
								Prosody = currentProsody.Clone()
							});
						}
						break;
						
					case XmlNodeType.Element:
						switch (child.Name.ToLower())
						{
							case "voice":
								var voiceName = child.Attributes["name"]?.Value ?? currentVoice;
								ProcessSSMLNode(child, segments, voiceName, currentProsody);
								break;
								
							case "prosody":
								var newProsody = currentProsody.Clone();
								
								// Parse prosody attributes
								if (child.Attributes["rate"] != null)
									newProsody.Rate = ParseRate(child.Attributes["rate"].Value);
								if (child.Attributes["pitch"] != null)
									newProsody.Pitch = ParsePitch(child.Attributes["pitch"].Value);
								if (child.Attributes["volume"] != null)
									newProsody.Volume = child.Attributes["volume"].Value;
									
								ProcessSSMLNode(child, segments, currentVoice, newProsody);
								break;
								
							case "emphasis":
								var emphProsody = currentProsody.Clone();
								emphProsody.Emphasis = child.Attributes["level"]?.Value ?? "moderate";
								ProcessSSMLNode(child, segments, currentVoice, emphProsody);
								break;
								
							case "break":
								// Add a pause marker
								var time = child.Attributes["time"]?.Value;
								if (time != null)
								{
									segments.Add(new SSMLSegment
									{
										Text = " ", // Space for break
										Voice = currentVoice,
										Prosody = currentProsody.Clone(),
										IsBreak = true,
										BreakDuration = ParseBreakTime(time)
									});
								}
								break;
								
							default:
								// Process other elements recursively
								ProcessSSMLNode(child, segments, currentVoice, currentProsody);
								break;
						}
						break;
				}
			}
		}

		private int ParseRate(string rate)
		{
			// Convert SSML rate to percentage
			// Examples: "slow" = -25%, "fast" = +25%, "150%" = +50%
			rate = rate.ToLower().Trim();
			
			if (rate == "x-slow") return -50;
			if (rate == "slow") return -25;
			if (rate == "medium") return 0;
			if (rate == "fast") return 25;
			if (rate == "x-fast") return 50;
			
			if (rate.EndsWith("%"))
			{
				if (int.TryParse(rate.TrimEnd('%'), out int percent))
					return percent - 100; // Convert to offset from 100%
			}
			
			return 0;
		}

		private int ParsePitch(string pitch)
		{
			// Convert SSML pitch to semitones
			pitch = pitch.ToLower().Trim();
			
			if (pitch == "x-low") return -12;
			if (pitch == "low") return -6;
			if (pitch == "medium") return 0;
			if (pitch == "high") return 6;
			if (pitch == "x-high") return 12;
			
			if (pitch.EndsWith("st"))
			{
				if (int.TryParse(pitch.TrimEnd('s', 't'), out int semitones))
					return semitones;
			}
			
			if (pitch.EndsWith("hz"))
			{
				// Convert Hz to semitones (approximate)
				if (double.TryParse(pitch.TrimEnd('h', 'z'), out double hz))
				{
					// Assuming base frequency of 100 Hz
					return (int)(12 * Math.Log2(hz / 100));
				}
			}
			
			return 0;
		}

		private int ParseBreakTime(string time)
		{
			// Parse break duration to milliseconds
			time = time.ToLower().Trim();
			
			if (time.EndsWith("ms"))
			{
				if (int.TryParse(time.TrimEnd('m', 's'), out int ms))
					return ms;
			}
			else if (time.EndsWith("s"))
			{
				if (double.TryParse(time.TrimEnd('s'), out double seconds))
					return (int)(seconds * 1000);
			}
			
			// Strength values
			if (time == "x-weak") return 100;
			if (time == "weak") return 200;
			if (time == "medium") return 500;
			if (time == "strong") return 1000;
			if (time == "x-strong") return 2000;
			
			return 500; // Default
		}

		private void ApplySSMLSettings(SSMLData ssmlData)
		{
			// Apply the extracted text
			viewModel.InputText = ssmlData.ExtractedText;
			
			// Apply global prosody settings if present
			if (ssmlData.GlobalProsody != null)
			{
				viewModel.RatePercent = ssmlData.GlobalProsody.Rate;
				viewModel.PitchSemitones = ssmlData.GlobalProsody.Pitch;
				viewModel.VolumeLevel = ssmlData.GlobalProsody.Volume;
				viewModel.EmphasisLevel = ssmlData.GlobalProsody.Emphasis;
				viewModel.UseSSML = true;
			}
		}

		private void GenerateWaveformFromSSML(SSMLData ssmlData)
		{
			// Generate waveform with voice segments
			waveformControl.GenerateSyntheticWaveform(ssmlData.ExtractedText, 10.0);
			
			// Apply voice assignments to segments
			if (ssmlData.Segments != null && waveformControl.Segments.Count > 0)
			{
				for (int i = 0; i < Math.Min(ssmlData.Segments.Count, waveformControl.Segments.Count); i++)
				{
					var ssmlSegment = ssmlData.Segments[i];
					var waveformSegment = waveformControl.Segments[i];
					
					// Map SSML voice to available voice
					var matchingVoice = viewModel.AvailableVoices
						.FirstOrDefault(v => v.Contains(ssmlSegment.Voice)) 
						?? viewModel.AvailableVoices.FirstOrDefault();
						
					if (matchingVoice != null)
					{
						waveformSegment.VoiceName = matchingVoice;
						waveformSegment.Settings = new SSMLSettings
						{
							RatePercent = ssmlSegment.Prosody.Rate,
							PitchSemitones = ssmlSegment.Prosody.Pitch,
							VolumeLevel = ssmlSegment.Prosody.Volume,
							EmphasisLevel = ssmlSegment.Prosody.Emphasis,
							BreakMs = ssmlSegment.IsBreak ? ssmlSegment.BreakDuration : 0
						};
					}
				}
				
				// Update the view model with segments
				viewModel.WaveformSegments = waveformControl.Segments.ToList();
			}
		}

		#endregion

		#region Supporting Classes

		public class SSMLData
		{
			public bool IsValid { get; set; }
			public string ExtractedText { get; set; }
			public List<SSMLSegment> Segments { get; set; }
			public bool HasMultipleVoices { get; set; }
			public ProsodySettings GlobalProsody { get; set; }
			public string ParseError { get; set; }
		}

		public class SSMLSegment
		{
			public string Text { get; set; }
			public string Voice { get; set; }
			public ProsodySettings Prosody { get; set; }
			public bool IsBreak { get; set; }
			public int BreakDuration { get; set; }
		}

		public class ProsodySettings
		{
			public int Rate { get; set; } = 0;
			public int Pitch { get; set; } = 0;
			public string Volume { get; set; } = "medium";
			public string Emphasis { get; set; } = "none";
			
			public ProsodySettings Clone()
			{
				return new ProsodySettings
				{
					Rate = this.Rate,
					Pitch = this.Pitch,
					Volume = this.Volume,
					Emphasis = this.Emphasis
				};
			}
		}
		#endregion
	// Add these menu event handlers to your MainWindow.xaml.cs file

		#region Menu Event Handlers

		// File Menu
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
					AddToRecentFiles(dialog.FileName);
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

		// Edit Menu
		// private void MenuFind_Click(object sender, RoutedEventArgs e)
		// {
			// // Open find dialog
			// var findDialog = new FindReplaceDialog(false);
			// findDialog.Owner = this;
			// findDialog.TextToSearch = viewModel.InputText;
			// findDialog.ShowDialog();
		// }

		// private void MenuReplace_Click(object sender, RoutedEventArgs e)
		// {
			// // Open replace dialog
			// var replaceDialog = new FindReplaceDialog(true);
			// replaceDialog.Owner = this;
			// replaceDialog.TextToSearch = viewModel.InputText;
			// if (replaceDialog.ShowDialog() == true)
			// {
				// viewModel.InputText = replaceDialog.TextToSearch;
			// }
		// }

		private void MenuInsertSplit_Click(object sender, RoutedEventArgs e)
		{
			// Insert <split> tag at cursor position in the input text
			var textBox = FindName("txtInput") as TextBox;
			if (textBox != null)
			{
				int caretIndex = textBox.CaretIndex;
				viewModel.InputText = viewModel.InputText.Insert(caretIndex, " <split> ");
				textBox.CaretIndex = caretIndex + 9; // Move cursor after the tag
			}
		}

		// Voice Menu
		private void MenuWindowsProvider_Click(object sender, RoutedEventArgs e)
		{
			viewModel.SelectedProviderIndex = 0;
			UpdateVoiceMenu();
		}

		private void MenuGoogleProvider_Click(object sender, RoutedEventArgs e)
		{
			viewModel.SelectedProviderIndex = 1;
			UpdateVoiceMenu();
		}

		// private void MenuVoiceSettings_Click(object sender, RoutedEventArgs e)
		// {
			// var settingsDialog = new VoiceSettingsDialog();
			// settingsDialog.Owner = this;
			// settingsDialog.DataContext = viewModel;
			// settingsDialog.ShowDialog();
		// }

		// Prosody Menu
		private void MenuRate_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem item && item.Tag != null)
			{
				viewModel.RatePercent = int.Parse(item.Tag.ToString());
				UpdateProsodyMenuChecks("Rate", item);
			}
		}

		private void MenuPitch_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem item && item.Tag != null)
			{
				viewModel.PitchSemitones = int.Parse(item.Tag.ToString());
				UpdateProsodyMenuChecks("Pitch", item);
			}
		}

		private void MenuVolume_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem item && item.Tag != null)
			{
				viewModel.VolumeLevel = item.Tag.ToString();
				UpdateProsodyMenuChecks("Volume", item);
			}
		}

		private void MenuEmphasis_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem item && item.Tag != null)
			{
				viewModel.EmphasisLevel = item.Tag.ToString();
				UpdateProsodyMenuChecks("Emphasis", item);
			}
		}

		private void MenuResetProsody_Click(object sender, RoutedEventArgs e)
		{
			viewModel.RatePercent = 0;
			viewModel.PitchSemitones = 0;
			viewModel.VolumeLevel = "medium";
			viewModel.EmphasisLevel = "none";
			viewModel.BreakMs = 0;
			UpdateAllProsodyMenus();
		}

		// Playback Menu
		private void MenuStop_Click(object sender, RoutedEventArgs e)
		{
			// Stop current playback
			var provider = viewModel.GetCurrentProvider();
			if (provider is WindowsTTSProvider windowsProvider)
			{
				// Add stop functionality to provider
				viewModel.StatusText = "Stopped";
			}
		}

		private void MenuPause_Click(object sender, RoutedEventArgs e)
		{
			// Pause functionality
			viewModel.StatusText = "Paused";
		}

		private void MenuResume_Click(object sender, RoutedEventArgs e)
		{
			// Resume functionality
			viewModel.StatusText = "Resumed";
		}

		// Tools Menu
		private void MenuAutoDetectPauses_Click(object sender, RoutedEventArgs e)
		{
			// Auto-detect sentence boundaries and add split tags
			string text = viewModel.InputText;
			text = Regex.Replace(text, @"([.!?])\s+", "$1 <split> ");
			viewModel.InputText = text;
			viewModel.StatusText = "Auto-detected pauses added";
		}

		// private void MenuBatchProcessing_Click(object sender, RoutedEventArgs e)
		// {
			// var batchDialog = new BatchProcessingDialog();
			// batchDialog.Owner = this;
			// batchDialog.ViewModel = viewModel;
			// batchDialog.ShowDialog();
		// }

		// private void MenuOptions_Click(object sender, RoutedEventArgs e)
		// {
			// var optionsDialog = new OptionsDialog();
			// optionsDialog.Owner = this;
			// optionsDialog.ShowDialog();
		// }

		// View Menu
		private void MenuToggleToolbar_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			MainToolBar.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
		}

		private void MenuToggleStatusBar_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			var statusBar = this.FindName("statusBar") as System.Windows.Controls.Primitives.StatusBar;
			if (statusBar != null)
				statusBar.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
		}

		private void MenuToggleSSMLPreview_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			var ssmlGroup = FindName("SSMLPreviewGroup") as GroupBox;
			if (ssmlGroup != null)
				ssmlGroup.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
		}

		private void MenuToggleWaveform_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			var waveformGroup = FindName("WaveformGroup") as GroupBox;
			if (waveformGroup != null)
				waveformGroup.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
		}

		private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
		{
			// Increase font size
			var textBox = FindName("txtInput") as TextBox;
			if (textBox != null && textBox.FontSize < 30)
				textBox.FontSize += 2;
		}

		private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
		{
			// Decrease font size
			var textBox = FindName("txtInput") as TextBox;
			if (textBox != null && textBox.FontSize > 8)
				textBox.FontSize -= 2;
		}

		private void MenuZoomReset_Click(object sender, RoutedEventArgs e)
		{
			// Reset font size
			var textBox = FindName("txtInput") as TextBox;
			if (textBox != null)
				textBox.FontSize = 12;
		}

		// Help Menu
		private void MenuHelp_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(
				"TTS Multi-Provider Help\n\n" +
				"1. Select a TTS Provider (Windows or Google)\n" +
				"2. Choose a voice from the dropdown\n" +
				"3. Enter or load text to speak\n" +
				"4. Use <split> tags to separate segments\n" +
				"5. Adjust prosody settings as needed\n" +
				"6. Click Speak or Save to generate audio\n\n" +
				"For multi-voice: Generate preview, add markers, assign voices",
				"Help",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

		private void MenuShortcuts_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(
				"Keyboard Shortcuts:\n\n" +
				"Ctrl+O - Open file\n" +
				"Ctrl+S - Save file\n" +
				"F5 - Speak all\n" +
				"F6 - Speak selected\n" +
				"F3 - Test voice\n" +
				"Esc - Stop playback\n" +
				"Ctrl+T - Insert split tag\n" +
				"Ctrl+F - Find\n" +
				"Ctrl+H - Replace\n" +
				"Ctrl+Plus - Zoom in\n" +
				"Ctrl+Minus - Zoom out\n" +
				"Ctrl+Click - Add marker (in waveform)",
				"Keyboard Shortcuts",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

		private void MenuGoogleSetup_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "https://cloud.google.com/text-to-speech/docs/quickstart",
				UseShellExecute = true
			});
		}

		private void MenuSSMLReference_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "https://cloud.google.com/text-to-speech/docs/ssml",
				UseShellExecute = true
			});
		}

		private void MenuCheckUpdates_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("You are using the latest version (1.0.0)", "Check for Updates",
				MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void MenuAbout_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(
				"TTS Multi-Provider\nVersion 1.0.0\n\n" +
				"A powerful text-to-speech application supporting:\n" +
				"• Windows SAPI voices\n" +
				"• Google Cloud TTS\n" +
				"• Multi-voice generation\n" +
				"• SSML support\n" +
				"• Audio file splitting\n\n" +
				"© 2024 Your Name",
				"About",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

		#endregion

		#region Helper Methods

		private void UpdateVoiceMenu()
		{
			// Update the Voice menu with available voices
			MenuSelectVoice.Items.Clear();
			foreach (var voice in viewModel.AvailableVoices)
			{
				var menuItem = new MenuItem
				{
					Header = voice,
					IsCheckable = true,
					IsChecked = voice == viewModel.SelectedVoice
				};
				menuItem.Click += (s, e) =>
				{
					viewModel.SelectedVoice = voice;
					UpdateVoiceMenuChecks();
				};
				MenuSelectVoice.Items.Add(menuItem);
			}
		}

		private void UpdateVoiceMenuChecks()
		{
			foreach (MenuItem item in MenuSelectVoice.Items)
			{
				item.IsChecked = item.Header.ToString() == viewModel.SelectedVoice;
			}
		}

		private void UpdateProsodyMenuChecks(string category, MenuItem checkedItem)
		{
			// Uncheck all items in the category, then check the selected one
			if (checkedItem.Parent is MenuItem parent)
			{
				foreach (MenuItem item in parent.Items)
				{
					if (item != null)
						item.IsChecked = item == checkedItem;
				}
			}
		}

		private void UpdateAllProsodyMenus()
		{
			// Update all prosody menu check states to match current values
			// Implementation depends on menu structure
		}

		private void AddToRecentFiles(string filePath)
		{
			// Add to recent files list (store in settings)
			// Update Recent Files menu
		}

		#endregion
	}
}