using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TTS1.WPF.Utils
{
    public class VoiceTagProcessor
    {
        // Structure to hold text segment with voice assignment
        public class VoiceSegment
        {
            public string Text { get; set; }
            public int VoiceIndex { get; set; }
            public string VoiceName { get; set; }
        }

        /// <summary>
        /// Process text with voice tags like <voice=1> and convert to segments with <split> tags
        /// </summary>
        public static string ProcessVoiceTags(string inputText, List<string> availableVoices)
        {
            if (string.IsNullOrEmpty(inputText) || availableVoices == null || availableVoices.Count == 0)
                return inputText;

            // Parse the text for voice tags
            var segments = ParseVoiceSegments(inputText, availableVoices);
            
            // Build output with split tags between voice changes
            return BuildOutputWithSplitTags(segments);
        }

        /// <summary>
        /// Parse text into segments based on voice tags
        /// </summary>
        public static List<VoiceSegment> ParseVoiceSegments(string inputText, List<string> availableVoices)
        {
            var segments = new List<VoiceSegment>();
            
            // Regex to match voice tags like <voice=1>, <voice=2>, etc.
            var voiceTagRegex = new Regex(@"<\s*voice\s*=\s*(\d+)\s*>", RegexOptions.IgnoreCase);
            
            // Find all voice tag matches
            var matches = voiceTagRegex.Matches(inputText);
            
            if (matches.Count == 0)
            {
                // No voice tags found, return the entire text as one segment
                segments.Add(new VoiceSegment 
                { 
                    Text = inputText.Trim(),
                    VoiceIndex = 0,
                    VoiceName = availableVoices.Count > 0 ? availableVoices[0] : "Default"
                });
                return segments;
            }

            int currentPosition = 0;
            int currentVoiceIndex = 0; // Default to first voice
            
            // Process each match
            foreach (Match match in matches)
            {
                // Add text before this voice tag (if any)
                if (match.Index > currentPosition)
                {
                    string textBeforeTag = inputText.Substring(currentPosition, match.Index - currentPosition).Trim();
                    if (!string.IsNullOrEmpty(textBeforeTag))
                    {
                        segments.Add(new VoiceSegment
                        {
                            Text = textBeforeTag,
                            VoiceIndex = currentVoiceIndex,
                            VoiceName = GetVoiceName(currentVoiceIndex, availableVoices)
                        });
                    }
                }
                
                // Update current voice index
                if (int.TryParse(match.Groups[1].Value, out int newVoiceIndex))
                {
                    // Convert to 0-based index (user enters 1-based)
                    currentVoiceIndex = newVoiceIndex - 1;
                    
                    // Ensure it's within bounds
                    if (currentVoiceIndex < 0)
                        currentVoiceIndex = 0;
                    else if (currentVoiceIndex >= availableVoices.Count)
                        currentVoiceIndex = availableVoices.Count - 1;
                }
                
                currentPosition = match.Index + match.Length;
            }
            
            // Add any remaining text after the last voice tag
            if (currentPosition < inputText.Length)
            {
                string remainingText = inputText.Substring(currentPosition).Trim();
                if (!string.IsNullOrEmpty(remainingText))
                {
                    segments.Add(new VoiceSegment
                    {
                        Text = remainingText,
                        VoiceIndex = currentVoiceIndex,
                        VoiceName = GetVoiceName(currentVoiceIndex, availableVoices)
                    });
                }
            }
            
            return segments;
        }

        /// <summary>
        /// Build output text with split tags between voice changes
        /// </summary>
        private static string BuildOutputWithSplitTags(List<VoiceSegment> segments)
        {
            if (segments.Count == 0)
                return string.Empty;
            
            var output = new List<string>();
            
            for (int i = 0; i < segments.Count; i++)
            {
                output.Add(segments[i].Text);
                
                // Add split tag if next segment has different voice
                if (i < segments.Count - 1 && segments[i].VoiceIndex != segments[i + 1].VoiceIndex)
                {
                    output.Add("<split>");
                }
            }
            
            return string.Join(" ", output);
        }

        /// <summary>
        /// Get voice name from index, with bounds checking
        /// </summary>
        private static string GetVoiceName(int index, List<string> availableVoices)
        {
            if (availableVoices == null || availableVoices.Count == 0)
                return "Default";
            
            if (index < 0)
                index = 0;
            else if (index >= availableVoices.Count)
                index = availableVoices.Count - 1;
            
            return availableVoices[index];
        }

        /// <summary>
        /// Get voice assignments for segments after processing
        /// </summary>
        public static Dictionary<int, string> GetVoiceAssignments(string processedText, string originalText, List<string> availableVoices)
        {
            var assignments = new Dictionary<int, string>();
            var segments = ParseVoiceSegments(originalText, availableVoices);
            
            for (int i = 0; i < segments.Count; i++)
            {
                assignments[i] = segments[i].VoiceName;
            }
            
            return assignments;
        }

        /// <summary>
        /// Remove voice tags from text (for display purposes)
        /// </summary>
        public static string RemoveVoiceTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            var voiceTagRegex = new Regex(@"<\s*voice\s*=\s*\d+\s*>", RegexOptions.IgnoreCase);
            return voiceTagRegex.Replace(text, "").Trim();
        }
    }
}