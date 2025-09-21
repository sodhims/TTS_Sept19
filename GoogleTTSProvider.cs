// ============================================
// GoogleTTSProvider.cs - Google Cloud TTS implementation
// ============================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using System.Threading.Tasks;

namespace TTS1
{
    public class GoogleTTSProvider : ITTSProvider
    {
        private string apiKey;
        private HttpClient httpClient;
        private string currentVoice = "en-US-Wavenet-D";
        private static readonly Dictionary<string, string> availableVoices = new Dictionary<string, string>
        {
            { "en-US-Wavenet-A (Female)", "en-US-Wavenet-A" },
            { "en-US-Wavenet-B (Male)", "en-US-Wavenet-B" },
            { "en-US-Wavenet-C (Female)", "en-US-Wavenet-C" },
            { "en-US-Wavenet-D (Male)", "en-US-Wavenet-D" },
            { "en-US-Wavenet-E (Female)", "en-US-Wavenet-E" },
            { "en-US-Wavenet-F (Female)", "en-US-Wavenet-F" },
            { "en-US-Neural2-A (Male)", "en-US-Neural2-A" },
            { "en-US-Neural2-C (Female)", "en-US-Neural2-C" },
            { "en-US-Neural2-D (Male)", "en-US-Neural2-D" },
            { "en-US-Neural2-E (Female)", "en-US-Neural2-E" },
            { "en-US-Standard-A (Male)", "en-US-Standard-A" },
            { "en-US-Standard-B (Male)", "en-US-Standard-B" },
            { "en-US-Standard-C (Female)", "en-US-Standard-C" },
            { "en-US-Standard-D (Male)", "en-US-Standard-D" },
            { "en-US-Standard-E (Female)", "en-US-Standard-E" }
        };

        public string ProviderName => "Google Cloud TTS";

        public GoogleTTSProvider(string apiKey)
        {
            this.apiKey = apiKey;
            this.httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // Add timeout
        }

        public string GetApiKey() => apiKey;

        public List<string> GetAvailableVoices()
        {
            return new List<string>(availableVoices.Keys);
        }

		public void SetVoice(string voiceName)
		{
			// Handle both display names and actual voice IDs
			if (availableVoices.ContainsKey(voiceName))
			{
				currentVoice = availableVoices[voiceName];
			}
			else if (availableVoices.ContainsValue(voiceName))
			{
				currentVoice = voiceName;
			}
			else
			{
				// Try to find a matching voice
				var matchingVoice = availableVoices.FirstOrDefault(v => 
					v.Key.Contains(voiceName) || v.Value.Contains(voiceName));
				
				if (!string.IsNullOrEmpty(matchingVoice.Value))
				{
					currentVoice = matchingVoice.Value;
				}
				else
				{
					// Default to a standard voice if not found
					currentVoice = "en-US-Standard-A";
					System.Diagnostics.Debug.WriteLine($"Voice '{voiceName}' not found, using default: {currentVoice}");
				}
			}
			
			System.Diagnostics.Debug.WriteLine($"Google TTS voice set to: {currentVoice}");
		}
        public async Task<bool> SpeakAsync(string text, SSMLSettings settings)
        {
            // Google TTS doesn't directly support speaking, need to save and play
            string tempFile = Path.GetTempFileName() + ".wav";
            bool success = await SaveToWavAsync(text, tempFile, settings);

            if (success && File.Exists(tempFile))
            {
                try
                {
                    var player = new System.Media.SoundPlayer(tempFile);
                    player.PlaySync();
                    File.Delete(tempFile);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

		public async Task<bool> SaveToWavAsync(string text, string filePath, SSMLSettings settings)
		{
			try
			{
				string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";

				// Determine the gender based on the current voice
				string gender = "NEUTRAL";
				if (currentVoice.Contains("Female") || currentVoice.Contains("-C") || 
					currentVoice.Contains("-E") || currentVoice.Contains("-F") ||
					currentVoice.Contains("-H"))
				{
					gender = "FEMALE";
				}
				else if (currentVoice.Contains("Male") || currentVoice.Contains("-A") || 
						 currentVoice.Contains("-B") || currentVoice.Contains("-D") ||
						 currentVoice.Contains("-I") || currentVoice.Contains("-J"))
				{
					gender = "MALE";
				}

				var requestBody = new
				{
					input = settings.UseSSML ?
						(object)new { ssml = BuildGoogleSSML(text, settings) } :
						(object)new { text = text },
					voice = new
					{
						languageCode = "en-US",
						name = currentVoice,
						ssmlGender = gender
					},
					audioConfig = new
					{
						audioEncoding = "LINEAR16",  // WAV format
						speakingRate = 1.0 + (settings.RatePercent / 100.0),
						pitch = settings.PitchSemitones,
						volumeGainDb = GetVolumeDb(settings.VolumeLevel)
					}
				};

				var json = JsonSerializer.Serialize(requestBody);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				System.Diagnostics.Debug.WriteLine($"Google TTS: Synthesizing with voice '{currentVoice}' (gender: {gender})");
				
				var response = await httpClient.PostAsync(url, content);

				if (!response.IsSuccessStatusCode)
				{
					string errorContent = await response.Content.ReadAsStringAsync();
					System.Diagnostics.Debug.WriteLine($"Google TTS Error {response.StatusCode}: {errorContent}");

					if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
					{
						System.Diagnostics.Debug.WriteLine("API Key may be invalid or Text-to-Speech API not enabled");
					}
					else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
					{
						System.Diagnostics.Debug.WriteLine($"Bad request - Voice name: {currentVoice}, Gender: {gender}");
					}

					return false;
				}

				var responseJson = await response.Content.ReadAsStringAsync();
				var responseData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);

				if (responseData.ContainsKey("audioContent"))
				{
					string audioContent = responseData["audioContent"].ToString();
					byte[] audioBytes = Convert.FromBase64String(audioContent);

					await File.WriteAllBytesAsync(filePath, audioBytes);
					System.Diagnostics.Debug.WriteLine($"Successfully saved audio to {filePath} using voice {currentVoice}");
					return true;
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("No audio content in response");
					return false;
				}
			}
			catch (HttpRequestException ex)
			{
				System.Diagnostics.Debug.WriteLine($"Network error: {ex.Message}");
				System.Diagnostics.Debug.WriteLine("Check your internet connection");
			}
			catch (TaskCanceledException)
			{
				System.Diagnostics.Debug.WriteLine("Request timed out - check your internet connection");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Google TTS Error: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
			}
			return false;
		}

        public async Task<List<string>> SaveSplitToWavAsync(string text, string baseFilePath, SSMLSettings settings)
        {
            var createdFiles = new List<string>();
            var chunks = SplitTextByTag(text);

            string dir = Path.GetDirectoryName(baseFilePath);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(baseFilePath);

            for (int i = 0; i < chunks.Count; i++)
            {
                string outputPath = Path.Combine(dir, $"{nameWithoutExt}_{(i + 1):D3}.wav");
                bool success = await SaveToWavAsync(chunks[i], outputPath, settings);
                if (success)
                {
                    createdFiles.Add(outputPath);
                }
            }

            return createdFiles;
        }

        private List<string> SplitTextByTag(string text)
        {
            var parts = Regex.Split(text, @"<\s*split\s*/?\s*>", RegexOptions.IgnoreCase);
            var result = new List<string>();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            if (result.Count == 0)
                result.Add("");

            return result;
        }

		private string BuildGoogleSSML(string text, SSMLSettings settings)
		{
			text = System.Security.SecurityElement.Escape(text);

		
			// Note: Google handles rate and pitch differently in the audioConfig, 
			// so we don't include them in SSML prosody tags
			
			string content = text;

			if (!string.IsNullOrEmpty(settings.EmphasisLevel) && settings.EmphasisLevel != "none")
			{
				content = $"<emphasis level=\"{settings.EmphasisLevel}\">{text}</emphasis>";
			}

			if (settings.BreakMs > 0)
			{
				content += $"<break time=\"{settings.BreakMs}ms\"/>";
			}

			return $"<speak>{content}</speak>";
		}
        private double GetVolumeDb(string volumeLevel)
        {
            return volumeLevel switch
            {
                "silent" => -96.0,
                "x-soft" => -12.0,
                "soft" => -6.0,
                "medium" => 0.0,
                "loud" => 6.0,
                "x-loud" => 12.0,
                _ => 0.0
            };
        }
		public void Stop()
		{
			// Google TTS doesn't have built-in stop for the simple implementation
			// You'd need to implement cancellation tokens for true async cancellation
		}		
    }
}