using System;
using System.Collections.Generic;
using System.Text;
using VideoTranscoder.Shared;

namespace VideoTranscoder.Service
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    public class SubtitleChecker
    {
        /// <summary>
        /// Finds which subtitle languages are missing for a given video file
        /// </summary>
        /// <param name="outputFile">Full path to the video file</param>
        /// <param name="subtitleLanguages">List of ISO 639-1 2-letter language codes to check</param>
        /// <returns>List of language codes where subtitles are missing</returns>
        public static List<string> FindMissingSubtitles(string outputFile, List<string> subtitleLanguages)
        {
            /* If code is run before video is generated, this check is not valid
             
            if (!File.Exists(outputFile))
            {
                throw new FileNotFoundException("Output file not found", outputFile);
            }
            */

            var directory = Path.GetDirectoryName(outputFile);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputFile);

            // Folders to search: main folder, "subs" subfolder, "subtitle" subfolder
            var foldersToSearch = new List<string>
        {
            directory,
            Path.Combine(directory, "subs"),
            Path.Combine(directory, "subtitle")
        };

            // Build a mapping of 2-letter codes to their 3-letter equivalents
            var languageMapping = LanguageCodeUtils.BuildLanguageMapping(subtitleLanguages);

            // Find which languages already have subtitles
            var foundLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in foldersToSearch.Where(Directory.Exists))
            {
                // Search for .srt files in this folder
                var srtFiles = Directory.GetFiles(folder, "*.srt", SearchOption.TopDirectoryOnly);

                foreach (var srtFile in srtFiles)
                {
                    var srtFileName = Path.GetFileNameWithoutExtension(srtFile);

                    // Check if this subtitle file belongs to our video
                    // It should start with the video filename
                    if (!srtFileName.StartsWith(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Extract the language code from the filename
                    var languageCode = LanguageCodeUtils.ExtractLanguageCode(srtFileName, fileNameWithoutExt);

                    if (!string.IsNullOrEmpty(languageCode))
                    {
                        // Check if this matches any of our requested languages
                        foreach (var kvp in languageMapping)
                        {
                            var twoLetterCode = kvp.Key;
                            var threeLetterCode = kvp.Value;

                            if (languageCode.Equals(twoLetterCode, StringComparison.OrdinalIgnoreCase) ||
                                languageCode.Equals(threeLetterCode, StringComparison.OrdinalIgnoreCase))
                            {
                                foundLanguages.Add(twoLetterCode);
                                break;
                            }
                        }
                    }
                }
            }

            // Return the languages that were NOT found
            return subtitleLanguages
                .Where(lang => !foundLanguages.Contains(lang))
                .ToList();
        }
    }
}
