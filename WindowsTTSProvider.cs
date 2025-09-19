// ============================================
// WindowsTTSProvider.cs - Windows SAPI implementation
// ============================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace TTS1
{
    public class WindowsTTSProvider : ITTSProvider, IDisposable
    {
        private SpeechSynthesizer synth;
        private string currentVoice;

        public string ProviderName => "Windows SAPI";

        public WindowsTTSProvider()
        {
            synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();

            // Set initial voice if available
            var voices = synth.GetInstalledVoices();
            if (voices.Count > 0)
            {
                currentVoice = voices[0].VoiceInfo.Name;
            }
        }

        public List<string> GetAvailableVoices()
        {
            var voices = new List<string>();
            foreach (var voice in synth.GetInstalledVoices())
            {
                if (voice.Enabled)
                {
                    voices.Add(voice.VoiceInfo.Name);
                }
            }
            return voices;
        }

        public void SetVoice(string voiceName)
        {
            try
            {
                // Cancel any ongoing speech first
                synth.SpeakAsyncCancelAll();

                // Reset the synthesizer to default audio
                synth.SetOutputToDefaultAudioDevice();

                // Important: Actually select the voice in the synthesizer
                synth.SelectVoice(voiceName);
                currentVoice = voiceName;

                System.Diagnostics.Debug.WriteLine($"Voice set to: {voiceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting voice {voiceName}: {ex.Message}");

                // If the voice selection fails, try to find a matching voice
                foreach (var voice in synth.GetInstalledVoices())
                {
                    if (voice.VoiceInfo.Name.Contains(voiceName) || voiceName.Contains(voice.VoiceInfo.Name))
                    {
                        try
                        {
                            synth.SpeakAsyncCancelAll();
                            synth.SelectVoice(voice.VoiceInfo.Name);
                            currentVoice = voice.VoiceInfo.Name;
                            System.Diagnostics.Debug.WriteLine($"Fallback voice set to: {voice.VoiceInfo.Name}");
                            break;
                        }
                        catch
                        {
                            // Continue trying other voices
                        }
                    }
                }
            }
        }

        public async Task<bool> SpeakAsync(string text, SSMLSettings settings)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Cancel any previous speech
                    synth.SpeakAsyncCancelAll();

                    // Ensure we're using the correct voice
                    if (!string.IsNullOrEmpty(currentVoice))
                    {
                        try
                        {
                            synth.SelectVoice(currentVoice);
                        }
                        catch
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not select voice: {currentVoice}");
                        }
                    }

                    synth.SetOutputToDefaultAudioDevice();

                    if (settings.UseSSML)
                    {
                        var ssml = BuildSSMLForText(text, settings);
                        // Use synchronous speak to avoid issues
                        synth.SpeakSsml(ssml);
                    }
                    else
                    {
                        // Apply settings even without SSML
                        synth.Rate = Math.Max(-10, Math.Min(10, settings.RatePercent / 10)); // Clamp to valid range
                        synth.Volume = GetVolumeValue(settings.VolumeLevel);
                        synth.Speak(text);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SpeakAsync error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> SaveToWavAsync(string text, string filePath, SSMLSettings settings)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Ensure we're using the correct voice
                    if (!string.IsNullOrEmpty(currentVoice))
                    {
                        try
                        {
                            synth.SelectVoice(currentVoice);
                        }
                        catch { }
                    }

                    synth.SetOutputToWaveFile(filePath);

                    if (settings.UseSSML)
                    {
                        var ssml = BuildSSMLForText(text, settings);
                        synth.SpeakSsml(ssml);
                    }
                    else
                    {
                        synth.Rate = settings.RatePercent / 10;
                        synth.Volume = GetVolumeValue(settings.VolumeLevel);
                        synth.Speak(text);
                    }

                    synth.SetOutputToDefaultAudioDevice();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveToWavAsync error: {ex.Message}");
                    synth.SetOutputToDefaultAudioDevice();
                    return false;
                }
            });
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

        private string BuildSSMLForText(string text, SSMLSettings settings)
        {
            // Escape XML special characters
            text = System.Security.SecurityElement.Escape(text);

            int rate = 100 + settings.RatePercent;
            string pitch = settings.PitchSemitones >= 0 ?
                $"+{settings.PitchSemitones}st" : $"{settings.PitchSemitones}st";

            string prosodyOpen = $"<prosody rate=\"{rate}%\" pitch=\"{pitch}\" volume=\"{settings.VolumeLevel}\">";
            string prosodyClose = "</prosody>";

            string content;
            if (!string.IsNullOrEmpty(settings.EmphasisLevel) && settings.EmphasisLevel != "none")
            {
                content = $"{prosodyOpen}<emphasis level=\"{settings.EmphasisLevel}\">{text}</emphasis>{prosodyClose}";
            }
            else
            {
                content = $"{prosodyOpen}{text}{prosodyClose}";
            }

            string breakTag = settings.BreakMs > 0 ? $"<break time=\"{settings.BreakMs}ms\"/>" : "";

            // Include voice selection in SSML if we have a current voice
            string voiceTag = "";
            if (!string.IsNullOrEmpty(currentVoice))
            {
                voiceTag = $"<voice name=\"{currentVoice}\">";
                content = voiceTag + content + "</voice>";
            }

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">" +
                   $"{content}{breakTag}</speak>";
        }

        private int GetVolumeValue(string volumeLevel)
        {
            return volumeLevel switch
            {
                "silent" => 0,
                "x-soft" => 20,
                "soft" => 40,
                "medium" => 60,
                "loud" => 80,
                "x-loud" => 100,
                _ => 60
            };
        }

        public void Dispose()
        {
            synth?.Dispose();
        }
    }
}