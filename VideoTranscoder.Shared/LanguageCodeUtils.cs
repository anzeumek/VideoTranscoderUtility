using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VideoTranscoder.Shared
{
    public class LanguageCodeUtils
    {
        /// <summary>
        /// Build a mapping from 2-letter ISO codes to 3-letter ISO codes
        /// </summary>
        public static Dictionary<string, string> BuildLanguageMapping(List<string> twoLetterCodes)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var twoLetterCode in twoLetterCodes)
            {
                try
                {
                    var culture = CultureInfo.GetCultures(CultureTypes.AllCultures)
                        .FirstOrDefault(c => c.TwoLetterISOLanguageName.Equals(twoLetterCode, StringComparison.OrdinalIgnoreCase));

                    if (culture != null)
                    {
                        var threeLetterCode = culture.ThreeLetterISOLanguageName;
                        mapping[twoLetterCode] = threeLetterCode;
                    }
                    else
                    {
                        // Fallback: keep the two-letter code
                        mapping[twoLetterCode] = twoLetterCode;
                    }
                }
                catch
                {
                    // If we can't find the culture, just use the two-letter code
                    mapping[twoLetterCode] = twoLetterCode;
                }
            }

            return mapping;
        }

        /// <summary>
        /// Extract the language code from a subtitle filename
        /// </summary>
        public static string ExtractLanguageCode(string srtFileName, string videoFileName)
        {
            // Remove the video filename part
            var remainder = srtFileName.Substring(videoFileName.Length).TrimStart('.');

            if (string.IsNullOrEmpty(remainder))
            {
                return null;
            }

            var parts = remainder.Split('.');

            if (parts.Length > 0)
            {
                // Get the last part (rightmost before .srt)
                var potentialCode = parts[parts.Length - 1];

                // Language codes are typically 2-3 characters, can include hyphen
                // Examples: "en", "eng", "en-US", "pt-BR"
                if (potentialCode.Length >= 2 && potentialCode.Length <= 5 &&
                    potentialCode.All(c => char.IsLetter(c) || c == '-'))
                {
                    return potentialCode;
                }
            }

            return null;
        }

        public static bool IsLanguageMatch(string languageCode, Dictionary<string, string> languageMapping)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return false;
            }

            foreach (var kvp in languageMapping)
            {
                var twoLetterCode = kvp.Key;
                var threeLetterCode = kvp.Value;

                if (languageCode.Equals(twoLetterCode, StringComparison.OrdinalIgnoreCase) ||
                    languageCode.Equals(threeLetterCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
