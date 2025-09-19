// ============================================
// MP3Converter.cs - WAV to MP3 conversion using NAudio
// ============================================
using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Lame;

namespace TTS1
{
    public static class MP3Converter
    {
        public static async Task<bool> ConvertWavToMp3(string wavPath, string mp3Path, int bitrate = 128)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var reader = new WaveFileReader(wavPath))
                    {
                        using (var writer = new LameMP3FileWriter(mp3Path, reader.WaveFormat, bitrate))
                        {
                            reader.CopyTo(writer);
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MP3 Conversion error: {ex.Message}");
                    return false;
                }
            });
        }
    }
}