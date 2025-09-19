// Add these imports at the top of MainViewModel.cs:

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TTS1; // Import the base namespace for providers and MP3Converter
using TTS1.WPF.Controls; // Import for WaveformSegment
using NAudio.Wave; // Import for audio processing

namespace TTS1.WPF
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ITTSProvider currentProvider;
        private WindowsTTSProvider windowsProvider;
        private GoogleTTSProvider googleProvider;
		private List<WaveformSegment> _waveformSegments;

        // Properties
        private int _selectedProviderIndex = 0;
        public int SelectedProviderIndex
        {
            get => _selectedProviderIndex;
            set
            {
                if (_selectedProviderIndex != value)
                {
                    _selectedProviderIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGoogleProviderSelected));
                    OnProviderChanged();
                }
            }
        }

        public bool IsGoogleProviderSelected => SelectedProviderIndex == 1;

        private string _googleApiKey;
        public string GoogleApiKey
        {
            get => _googleApiKey;
            set
            {
                if (_googleApiKey != value)
                {
                    _googleApiKey = value;
                    OnPropertyChanged();
                    if (IsGoogleProviderSelected && !string.IsNullOrWhiteSpace(value))
                    {
                        InitializeGoogleProvider();
                    }
                }
            }
        }

        private ObservableCollection<string> _availableVoices;
        public ObservableCollection<string> AvailableVoices
        {
            get => _availableVoices;
            set
            {
                _availableVoices = value;
                OnPropertyChanged();
            }
        }

        private string _selectedVoice;
        public string SelectedVoice
        {
            get => _selectedVoice;
            set
            {
                if (_selectedVoice != value)
                {
                    _selectedVoice = value;
                    OnPropertyChanged();
                    if (currentProvider != null && !string.IsNullOrEmpty(value))
                    {
                        currentProvider.SetVoice(value);
                        StatusText = $"Voice set to: {value}";
                    }
                }
            }
        }

        private int _ratePercent = 0;
        public int RatePercent
        {
            get => _ratePercent;
            set
            {
                if (_ratePercent != value)
                {
                    _ratePercent = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private int _pitchSemitones = 0;
        public int PitchSemitones
        {
            get => _pitchSemitones;
            set
            {
                if (_pitchSemitones != value)
                {
                    _pitchSemitones = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private int _breakMs = 0;
        public int BreakMs
        {
            get => _breakMs;
            set
            {
                if (_breakMs != value)
                {
                    _breakMs = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private string _volumeLevel = "medium";
        public string VolumeLevel
        {
            get => _volumeLevel;
            set
            {
                if (_volumeLevel != value)
                {
                    _volumeLevel = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private string _emphasisLevel = "none";
        public string EmphasisLevel
        {
            get => _emphasisLevel;
            set
            {
                if (_emphasisLevel != value)
                {
                    _emphasisLevel = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private bool _useSSML = true;
        public bool UseSSML
        {
            get => _useSSML;
            set
            {
                if (_useSSML != value)
                {
                    _useSSML = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private string _inputText;
        public string InputText
        {
            get => _inputText;
            set
            {
                if (_inputText != value)
                {
                    _inputText = value;
                    OnPropertyChanged();
                    UpdateSSMLPreview();
                }
            }
        }

        private string _ssmlPreview;
        public string SSMLPreview
        {
            get => _ssmlPreview;
            set
            {
                if (_ssmlPreview != value)
                {
                    _ssmlPreview = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }
		public List<WaveformSegment> WaveformSegments
		{
			get => _waveformSegments;
			set
			{
				_waveformSegments = value;
				OnPropertyChanged();
				UpdateSSMLPreview(); // Update preview when segments change
			}
		}

        // Constructor
        public MainViewModel()
        {
            AvailableVoices = new ObservableCollection<string>();
            InitializeProviders();
        }

        // Initialization Methods
        private void InitializeProviders()
        {
            windowsProvider = new WindowsTTSProvider();
            currentProvider = windowsProvider;
            LoadVoices();
        }

        private void InitializeGoogleProvider()
        {
            if (!string.IsNullOrWhiteSpace(GoogleApiKey))
            {
                googleProvider = new GoogleTTSProvider(GoogleApiKey);
                if (IsGoogleProviderSelected)
                {
                    currentProvider = googleProvider;
                    LoadVoices();
                    StatusText = "Google TTS initialized";
                }
            }
        }

        private void OnProviderChanged()
        {
            if (SelectedProviderIndex == 0)
            {
                currentProvider = windowsProvider;
                LoadVoices();
            }
            else if (SelectedProviderIndex == 1)
            {
                if (!string.IsNullOrWhiteSpace(GoogleApiKey))
                {
                    InitializeGoogleProvider();
                }
                else
                {
                    StatusText = "Please enter Google API key";
                }
            }
        }

        private void LoadVoices()
        {
            AvailableVoices.Clear();
            if (currentProvider != null)
            {
                var voices = currentProvider.GetAvailableVoices();
                foreach (var voice in voices)
                {
                    AvailableVoices.Add(voice);
                }

                if (AvailableVoices.Count > 0)
                {
                    SelectedVoice = AvailableVoices[0];
                }
            }
        }

        // Public method to get current provider (for WaveformControl)
        public ITTSProvider GetCurrentProvider()
        {
            return currentProvider;
        }



		// Test voice method
		public async Task<bool> TestVoiceAsync()
		{
			if (currentProvider == null) return false;

			var settings = GetCurrentSettings();
			return await currentProvider.SpeakAsync("This is a test of the selected voice.", settings);
		}
		
		// Add this method to your MainViewModel.cs in the TTS Methods section:

		public async Task<bool> SaveToWavAsync(string filePath)
		{
			if (currentProvider == null) return false;

			var cleanText = InputText?.Replace("<split>", " ").Replace("<split/>", " ") ?? "";
			var settings = GetCurrentSettings();
			return await currentProvider.SaveToWavAsync(cleanText, filePath, settings);
		}

		// Speak all with multi-voice support
		public async Task SpeakAllAsync()
		{
			if (currentProvider == null) return;

			// Check if we have segments defined
			if (WaveformSegments != null && WaveformSegments.Count > 0)
			{
				// Play multi-voice segments
				await SpeakSegmentsAsync();
			}
			else
			{
				// Original single-voice playback
				var cleanText = InputText?.Replace("<split>", " ").Replace("<split/>", " ") ?? "";
				var settings = GetCurrentSettings();
				await currentProvider.SpeakAsync(cleanText, settings);
			}
		}
		// Speak segments with different voices
		private async Task SpeakSegmentsAsync()
		{
			if (currentProvider == null || WaveformSegments == null) return;

			// Split the input text by <split> tags
			var parts = System.Text.RegularExpressions.Regex.Split(InputText ?? "", @"<\s*split\s*/?\s*>");
			
			for (int i = 0; i < WaveformSegments.Count && i < parts.Length; i++)
			{
				var segment = WaveformSegments[i];
				var text = parts[i].Trim();
				
				if (string.IsNullOrEmpty(text)) continue;
				
				// Set voice for this segment
				currentProvider.SetVoice(segment.VoiceName);
				
				// Use segment's settings or default settings
				var settings = segment.Settings ?? GetCurrentSettings();
				
				// Speak this segment
				await currentProvider.SpeakAsync(text, settings);
			}
		}
		// Generate multi-voice audio file
		public async Task<bool> SaveMultiVoiceWavAsync(string filePath)
		{
			if (currentProvider == null || WaveformSegments == null || WaveformSegments.Count == 0) 
				return false;

			try
			{
				// Split the input text by <split> tags
				var parts = System.Text.RegularExpressions.Regex.Split(InputText ?? "", @"<\s*split\s*/?\s*>");
				var tempFiles = new List<string>();
				
				// Generate individual WAV files for each segment
				for (int i = 0; i < WaveformSegments.Count && i < parts.Length; i++)
				{
					var segment = WaveformSegments[i];
					var text = parts[i].Trim();
					
					if (string.IsNullOrEmpty(text)) continue;
					
					string tempFile = Path.GetTempFileName() + ".wav";
					
					// Set voice for this segment
					currentProvider.SetVoice(segment.VoiceName);
					
					// Use segment's settings
					var settings = segment.Settings ?? GetCurrentSettings();
					
					// Save this segment to temporary file
					bool success = await currentProvider.SaveToWavAsync(text, tempFile, settings);
					
					if (success && File.Exists(tempFile))
					{
						tempFiles.Add(tempFile);
					}
				}
				
				// Combine all WAV files into one
				if (tempFiles.Count > 0)
				{
					bool combined = await CombineWavFiles(tempFiles, filePath);
					
					// Clean up temp files
					foreach (var tempFile in tempFiles)
					{
						try { File.Delete(tempFile); } catch { }
					}
					
					return combined;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"SaveMultiVoiceWav error: {ex.Message}");
			}
			
			return false;
		}

		// Helper method to combine multiple WAV files
		private async Task<bool> CombineWavFiles(List<string> inputFiles, string outputFile)
		{
			return await Task.Run(() =>
			{
				try
				{
					using (var writer = new NAudio.Wave.WaveFileWriter(outputFile, new NAudio.Wave.WaveFormat(44100, 16, 1)))
					{
						foreach (var inputFile in inputFiles)
						{
							if (File.Exists(inputFile))
							{
								using (var reader = new NAudio.Wave.WaveFileReader(inputFile))
								{
									// Convert to the same format if necessary
									var resampler = new NAudio.Wave.WaveFormatConversionStream(writer.WaveFormat, reader);
									var buffer = new byte[resampler.WaveFormat.AverageBytesPerSecond];
									int read;
									while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
									{
										writer.Write(buffer, 0, read);
									}
								}
							}
						}
					}
					return true;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"CombineWavFiles error: {ex.Message}");
					return false;
				}
			});
		}


        public async Task<bool> SaveToMp3Async(string filePath)
        {
            if (currentProvider == null) return false;

            string tempWav = Path.GetTempFileName();
            var cleanText = InputText?.Replace("<split>", " ").Replace("<split/>", " ") ?? "";
            var settings = GetCurrentSettings();

            bool success = await currentProvider.SaveToWavAsync(cleanText, tempWav, settings);

            if (success)
            {
                success = await MP3Converter.ConvertWavToMp3(tempWav, filePath);
                try { File.Delete(tempWav); } catch { }
            }

            return success;
        }

        public async Task<List<string>> SaveSplitToWavAsync(string baseFilePath)
        {
            if (currentProvider == null) return new List<string>();

            var settings = GetCurrentSettings();
            return await currentProvider.SaveSplitToWavAsync(InputText ?? "", baseFilePath, settings);
        }

        public async Task<List<string>> SaveSplitToMp3Async(string baseFilePath)
        {
            if (currentProvider == null) return new List<string>();

            string tempBase = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(baseFilePath));
            var settings = GetCurrentSettings();
            var wavFiles = await currentProvider.SaveSplitToWavAsync(InputText ?? "", tempBase + ".wav", settings);

            var mp3Files = new List<string>();
            if (wavFiles.Count > 0)
            {
                string dir = Path.GetDirectoryName(baseFilePath);
                string baseName = Path.GetFileNameWithoutExtension(baseFilePath);

                for (int i = 0; i < wavFiles.Count; i++)
                {
                    string mp3Path = Path.Combine(dir, $"{baseName}_{(i + 1):D3}.mp3");
                    bool success = await MP3Converter.ConvertWavToMp3(wavFiles[i], mp3Path);

                    if (success)
                        mp3Files.Add(mp3Path);

                    try { File.Delete(wavFiles[i]); } catch { }
                }
            }

            return mp3Files;
        }

        // Helper Methods
        private SSMLSettings GetCurrentSettings()
        {
            return new SSMLSettings
            {
                RatePercent = RatePercent,
                PitchSemitones = PitchSemitones,
                VolumeLevel = VolumeLevel ?? "medium",
                EmphasisLevel = EmphasisLevel ?? "none",
                BreakMs = BreakMs,
                UseSSML = UseSSML
            };
        }

		private void UpdateSSMLPreview()
		{
			if (string.IsNullOrEmpty(InputText))
			{
				SSMLPreview = "<speak>Sample text</speak>";
				return;
			}

			var settings = GetCurrentSettings();
			var splitPattern = @"<\s*split\s*/?\s*>";
			var chunks = Regex.Split(InputText, splitPattern);
			
			// Check if we have segments defined
			if (WaveformSegments != null && WaveformSegments.Count > 0)
			{
				// Build multi-voice SSML preview
				var ssml = new StringBuilder();
				ssml.AppendLine("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">");
				
				for (int i = 0; i < WaveformSegments.Count && i < chunks.Length; i++)
				{
					var segment = WaveformSegments[i];
					var text = chunks[i].Trim();
					
					if (string.IsNullOrEmpty(text)) continue;
					
					var segSettings = segment.Settings ?? settings;
					
					ssml.AppendLine($"  <voice name=\"{segment.VoiceName}\">");
					
					int rate = 100 + segSettings.RatePercent;
					string pitch = segSettings.PitchSemitones >= 0 ? 
						$"+{segSettings.PitchSemitones}st" : 
						$"{segSettings.PitchSemitones}st";
					
					ssml.AppendLine($"    <prosody rate=\"{rate}%\" pitch=\"{pitch}\" volume=\"{segSettings.VolumeLevel}\">");
					
					if (segSettings.EmphasisLevel != "none")
					{
						ssml.AppendLine($"      <emphasis level=\"{segSettings.EmphasisLevel}\">");
						ssml.AppendLine($"        {System.Security.SecurityElement.Escape(text)}");
						ssml.AppendLine($"      </emphasis>");
					}
					else
					{
						ssml.AppendLine($"      {System.Security.SecurityElement.Escape(text)}");
					}
					
					ssml.AppendLine("    </prosody>");
					
					if (segSettings.BreakMs > 0)
						ssml.AppendLine($"    <break time=\"{segSettings.BreakMs}ms\"/>");
					
					ssml.AppendLine("  </voice>");
				}
				
				ssml.Append("</speak>");
				SSMLPreview = ssml.ToString();
			}
			else
			{
				// Original single-voice SSML
				string firstChunk = chunks.Length > 0 ? chunks[0].Trim() : "Sample text";
				if (string.IsNullOrEmpty(firstChunk))
					firstChunk = "Sample text";

				var ssml = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">\n";

				int rate = 100 + settings.RatePercent;
				string pitch = settings.PitchSemitones >= 0 ? 
					$"+{settings.PitchSemitones}st" : 
					$"{settings.PitchSemitones}st";

				ssml += $"  <prosody rate=\"{rate}%\" pitch=\"{pitch}\" volume=\"{settings.VolumeLevel}\">\n";

				if (settings.EmphasisLevel != "none")
				{
					ssml += $"    <emphasis level=\"{settings.EmphasisLevel}\">\n";
					ssml += $"      {System.Security.SecurityElement.Escape(firstChunk)}\n";
					ssml += $"    </emphasis>\n";
				}
				else
				{
					ssml += $"    {System.Security.SecurityElement.Escape(firstChunk)}\n";
				}

				ssml += "  </prosody>\n";

				if (settings.BreakMs > 0)
					ssml += $"  <break time=\"{settings.BreakMs}ms\"/>\n";

				ssml += "</speak>";
				SSMLPreview = ssml;
			}
		}
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}