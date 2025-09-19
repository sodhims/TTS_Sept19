// ============================================
// ITTSProvider.cs - Base interface for TTS providers
// ============================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TTS1
{
    public interface ITTSProvider
    {
        string ProviderName { get; }
        List<string> GetAvailableVoices();
        void SetVoice(string voiceName);
        Task<bool> SpeakAsync(string text, SSMLSettings settings);
        Task<bool> SaveToWavAsync(string text, string filePath, SSMLSettings settings);
        Task<List<string>> SaveSplitToWavAsync(string text, string baseFilePath, SSMLSettings settings);
    }

    public class SSMLSettings
    {
        public int RatePercent { get; set; } = 0;
        public int PitchSemitones { get; set; } = 0;
        public string VolumeLevel { get; set; } = "medium";
        public string EmphasisLevel { get; set; } = "none";
        public int BreakMs { get; set; } = 0;
        public bool UseSSML { get; set; } = true;
    }
}