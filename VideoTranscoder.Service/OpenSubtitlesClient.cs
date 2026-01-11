using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VideoTranscoder.Service
{
    public class OpenSubtitlesClient : IDisposable
    {
        private FileLogger _logger;

        private readonly HttpClient _httpClient;
        private readonly HttpClient _fileHttpClient;  // For file downloads without auth headers
        private readonly string _apiKey;
        private readonly string _appName;
        private string _authToken;
        private bool disposedValue;
        private bool _fixCorrupted;
        private const string BaseUrl = "https://api.opensubtitles.com/api/v1";
        //private const string BaseUrl = "https://vip-api.opensubtitles.com/api/v1/download"; //vip server
        //private const string BaseUrl = "https://stoplight.io/mocks/opensubtitles/opensubtitles-api/2781383"; //mock server for testing

        private OpenSubtitlesClient(HttpClient httpClient, HttpClient fileHttpClient, string appName, string apiKey, bool fixCorrupted, FileLogger logger)
        {
            _fixCorrupted = fixCorrupted;
            _logger = logger;
            _apiKey = apiKey;
            _appName = appName;
            _httpClient = httpClient;
            _fileHttpClient = fileHttpClient;
            _httpClient.DefaultRequestHeaders.Add("Api-Key", _apiKey);
            //_httpClient.DefaultRequestHeaders.Add("User-Agent", "VideoTranscoder_v1.0");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"{_appName} v1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public static async Task<OpenSubtitlesClient> CreateAsync(
            HttpClient httpClient, 
            HttpClient fileHttpClient, 
            string appName,
            string apiKey,
            string username,
            string password,
            bool fixCorrupted,
            FileLogger logger)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("Application name is required and cannot be empty", nameof(appName));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required and cannot be empty", nameof(apiKey));


            var client = new OpenSubtitlesClient(httpClient, fileHttpClient, appName, apiKey, fixCorrupted, logger);
            await client.LoginAsync(username, password);
            return client;
        }

        private async Task LoginAsync(string username, string password)
        {

            if(string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogToFile("Username and/or password is empty. Not logging-in to OpenSubtitles API.");
                return;
            }

            var loginData = new
            {
                username = username,
                password = password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginData),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync($"{BaseUrl}/login", content);
            var responseString = await response.Content.ReadAsStringAsync();

            var loginResponse = JsonSerializer.Deserialize<JsonElement>(responseString);
            _authToken = loginResponse.GetProperty("token").GetString();

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authToken}");
            _logger.LogToFile("Logged in to OpenSubtitles API successfully.");
        }

        public async Task LogoutAsync()
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                _logger.LogToFile("No active session to logout from.");
                return;
            }

            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/logout");
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogToFile("Logged out from OpenSubtitles API successfully.");
                }
                else
                {
                    _logger.LogToFile($"Logout failed with status {response.StatusCode}: {responseString}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogToFile($"Error during logout: {ex.Message}");
            }
            finally
            {
                // Clear the auth token regardless of API response
                if (_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                {
                    _httpClient.DefaultRequestHeaders.Remove("Authorization");
                }
                _authToken = null;
            }
        }

        // Compute OpenSubtitles hash for a video file
        public static string ComputeMovieHash(string filePath)
        {
            byte[] buffer = new byte[sizeof(long)];
            long fileSize = 0;
            long hash = 0;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fileSize = fs.Length;
                hash = (long)fileSize;

                if (fileSize < 65536 * 2)
                {
                    throw new ArgumentException("File is too small (must be at least 128KB)");
                }

                // Read first 64KB
                for (int i = 0; i < 65536 / sizeof(long); i++)
                {
                    fs.Read(buffer, 0, sizeof(long));
                    hash += BitConverter.ToInt64(buffer, 0);
                }

                // Read last 64KB
                fs.Position = fileSize - 65536;
                for (int i = 0; i < 65536 / sizeof(long); i++)
                {
                    fs.Read(buffer, 0, sizeof(long));
                    hash += BitConverter.ToInt64(buffer, 0);
                }
            }

            return hash.ToString("x16");
        }

        // Search subtitles by movie file hash
        public async Task<List<Subtitle>> SearchSubtitlesByHashAsync(string movieFilePath, string language = "en")
        {
            var fileInfo = new FileInfo(movieFilePath);
            var hash = ComputeMovieHash(movieFilePath);

            return await SearchSubtitlesAsync(
                movieHash: hash,
                movieByteSize: fileInfo.Length,
                language: language
            );
        }

        public async Task<List<Subtitle>> SearchSubtitlesAsync(string movieName = null, string imdbId = null, string movieHash = null, long? movieByteSize = null, string language = "en")
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(movieName))
                queryParams.Add($"query={Uri.EscapeDataString(movieName)}");

            if (!string.IsNullOrEmpty(imdbId))
                queryParams.Add($"imdb_id={imdbId}");

            if (!string.IsNullOrEmpty(movieHash))
                queryParams.Add($"moviehash={movieHash}");

            if (movieByteSize.HasValue)
                queryParams.Add($"moviebytesize={movieByteSize.Value}");

            queryParams.Add($"languages={language}");

            var url = $"{BaseUrl}/subtitles?{string.Join("&", queryParams)}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"OpenSubtitles API returned {response.StatusCode}: {responseString}");
                }

                var searchResponse = JsonSerializer.Deserialize<JsonElement>(responseString);

                if (!searchResponse.TryGetProperty("data", out var data))
                {
                    _logger.LogToFile("Unexpected API response format: {Response}", responseString);
                    return new List<Subtitle>();
                }

                var subtitles = new List<Subtitle>();

                foreach (var item in data.EnumerateArray())
                {
                    try
                    {
                        var subtitle = ParseSubtitleFromJson(item);
                        if (subtitle != null)
                        {
                            subtitles.Add(subtitle);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogToFile("Failed to parse subtitle entry");
                        // Continue with other entries
                    }
                }

                return subtitles;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogToFile("HTTP error while searching subtitles");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogToFile("Failed to parse OpenSubtitles API response");
                throw new Exception("Invalid response from OpenSubtitles API", ex);
            }
        }

        private Subtitle ParseSubtitleFromJson(JsonElement item)
        {
            var attributes = item.GetProperty("attributes");
            var files = attributes.GetProperty("files");

            int? fileId = null;
            string fileName = null;

            if (files.GetArrayLength() > 0)
            {
                var firstFile = files[0];
                if (firstFile.TryGetProperty("file_id", out var fileIdElement))
                    fileId = fileIdElement.GetInt32();

                if (firstFile.TryGetProperty("file_name", out var fileNameElement))
                    fileName = fileNameElement.GetString();
            }

            var featureDetails = attributes.GetProperty("feature_details");
            string movieName = featureDetails.GetProperty("movie_name").GetString();

            return new Subtitle
            {
                SubtitleId = item.GetProperty("id").GetString(),
                FileId = fileId,
                MovieName = movieName,
                Language = attributes.GetProperty("language").GetString(),
                FileName = fileName,
                DownloadCount = attributes.GetProperty("download_count").GetInt32(),
                Rating = attributes.GetProperty("ratings").GetDouble(),
                FromTrusted = attributes.GetProperty("from_trusted").GetBoolean()
            };
        }

        public async Task<string> DownloadSubtitleAsync(int fileId, string outputPath, string language, int maxRetries = 1)
        {
            var downloadData = new
            {
                file_id = fileId
            };

            // To avoid hitting rate limits
            await Task.Delay(TimeSpan.FromSeconds(1));

            var content = new StringContent(
                JsonSerializer.Serialize(downloadData),
                Encoding.UTF8,
                "application/json"
            );

            /*
            _fileHttpClient.DefaultRequestHeaders.Add("User-Agent", "VideoTranscoder_v1.0");
            _fileHttpClient.DefaultRequestHeaders.Add("Api-Key", "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
            _fileHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authToken}");
            _fileHttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            */

            //_logger.LogToFile("Fetching download link for file_id: {fileId}", fileId.ToString());
            var response = await _httpClient.PostAsync($"{BaseUrl}/download", content);

            //_logger.LogToFile("Fetching download link returned a response");
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Download Error: {response.StatusCode} - {responseString}");
            }
            //_logger.LogToFile("Response is successful");

            var downloadResponse = JsonSerializer.Deserialize<JsonElement>(responseString);
            var downloadLink = downloadResponse.GetProperty("link").GetString();

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogToFile($"Downloading subtitle file (attempt {attempt + 1}/{maxRetries + 1})");

                    var fileResponse = await _fileHttpClient.GetAsync(downloadLink);

                    // Check if the response is successful
                    if (!fileResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await fileResponse.Content.ReadAsStringAsync();
                        _logger.LogToFile($"File download failed with status {fileResponse.StatusCode}: {errorContent}");

                        if (attempt < maxRetries)
                        {
                            _logger.LogToFile("Retrying in 5 seconds...");
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            continue;
                        }

                        throw new Exception($"File download failed: {fileResponse.StatusCode} - {errorContent}");
                    }

                    var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();

                    // Check if the downloaded content looks like HTML (error page)
                    if (fileBytes.Length > 0 && IsHtmlContent(fileBytes))
                    {
                        var htmlContent = Encoding.UTF8.GetString(fileBytes);
                        _logger.LogToFile($"Downloaded content appears to be HTML error page: {htmlContent.Substring(0, Math.Min(200, htmlContent.Length))}");

                        if (attempt < maxRetries)
                        {
                            _logger.LogToFile("Retrying in 60 seconds...");
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            continue;
                        }

                        throw new Exception("Downloaded file is an HTML error page instead of subtitle file");
                    }

                    if(_fixCorrupted)
                    {
                        fileBytes = FixCorruptedCharacters(fileBytes, language);
                    }

                    // Success - write the file
                    await File.WriteAllBytesAsync(outputPath, fileBytes);
                    _logger.LogToFile($"Subtitle downloaded successfully to {outputPath}");
                    return outputPath;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogToFile($"HTTP error during file download: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        _logger.LogToFile("Retrying in 60 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        continue;
                    }

                    throw new Exception($"Failed to download subtitle after {maxRetries + 1} attempts", ex);
                }
            }

            throw new Exception("Download failed - should not reach here");
        }

        private byte[] FixCorruptedCharacters(byte[] fileBytes, string language)
        {
            // First, try to detect the encoding
            var detector = new Ude.CharsetDetector();
            detector.Feed(fileBytes, 0, fileBytes.Length);
            detector.DataEnd();

            var detectedEncoding = detector.Charset;
            var confidence = detector.Confidence;

            _logger.LogToFile($"Detected encoding: {detectedEncoding ?? "unknown"} (confidence: {confidence})");

            // Check for corrupted Slovenian characters (valid UTF-8 but wrong characters)
            
            if (string.Equals("sl", language) && HasSlovenianCorruption(fileBytes))
            {
                _logger.LogToFile("Detected Slovenian character corruption, repairing...");
                return RepairSlovenianCorruption(fileBytes);
            }

            /*
            if (string.IsNullOrEmpty(detectedEncoding))
            {
                _logger.LogToFile("Could not detect encoding, assuming Windows-1250 for safety");
                detectedEncoding = "windows-1250";
            }

            try
            {
                // Convert from detected encoding to UTF-8
                Encoding sourceEncoding = Encoding.GetEncoding(detectedEncoding);
                string text = sourceEncoding.GetString(fileBytes);
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);

                _logger.LogToFile($"Converted from {detectedEncoding} to UTF-8");
                return utf8Bytes;
            }
            catch (Exception ex)
            {
                _logger.LogToFile($"Failed to convert encoding: {ex.Message}. Returning original bytes.");
                return fileBytes;
            }
            */
            return fileBytes;
        }

        private bool HasSlovenianCorruption(byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);

            // Check if we see Latin-1 characters where Slovenian characters should be
            // Common pattern: è instead of č, æ instead of ć, etc.

            bool suspicious = false;
            string corruptedPatern = "";

            if (HasMoreThan(text, 'è', 20))
            {
                suspicious = true;
                corruptedPatern = "è";
            }
            if (!suspicious && HasMoreThan(text, 'æ', 20))
            {
                suspicious = true;
                corruptedPatern = "æ";
            }
            if (!suspicious && HasMoreThan(text, '¹', 20))
            {
                suspicious = true;
                corruptedPatern = "¹";
            }
            if (!suspicious && HasMoreThan(text, '¾', 20))
            {
                suspicious = true;
                corruptedPatern = "¾";
            }

            if (suspicious)
            {
                _logger.LogToFile($"Found Slovenian corruption pattern: {corruptedPatern}");
            }

            return suspicious;
        }

        private static bool HasMoreThan(string text, char character, int threshold)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c == character && ++count > threshold)
                    return true;
            }
            return false;
        }

        private byte[] RepairSlovenianCorruption(byte[] bytes)
        {
            try
            {
                // Read as UTF-8 (it's valid UTF-8, just wrong characters)
                string text = Encoding.UTF8.GetString(bytes);

                // Apply character mappings for common Slovenian corruption patterns
                // These map Latin-1 characters to Slovenian characters
                var replacements = new Dictionary<string, string>
        {
            { "è", "č" },  // U+00E8 -> U+010D
            { "È", "Č" },  // U+00C8 -> U+010C
            { "æ", "ć" },  // U+00E6 -> U+0107
            { "Æ", "Ć" },  // U+00C6 -> U+0106
            { "¹", "š" },  // U+00B9 -> U+0161
            { "©", "Š" },  // U+00A9 -> U+0160
            { "¾", "ž" },  // U+00BE -> U+017E
            { "®", "Ž" },  // U+00AE -> U+017D
            { "ð", "đ" },  // U+00F0 -> U+0111
            { "Ð", "Đ" },  // U+00D0 -> U+0110
        };

                int replacementCount = 0;
                foreach (var replacement in replacements)
                {
                    int count = text.Split(new[] { replacement.Key }, StringSplitOptions.None).Length - 1;
                    if (count > 0)
                    {
                        text = text.Replace(replacement.Key, replacement.Value);
                        replacementCount += count;
                    }
                }

                // Convert back to UTF-8 bytes
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);

                _logger.LogToFile($"Successfully repaired Slovenian character corruption ({replacementCount} replacements)");
                return utf8Bytes;
            }
            catch (Exception ex)
            {
                _logger.LogToFile($"Failed to repair Slovenian corruption: {ex.Message}");
                return bytes;
            }
        }

        // Check if content looks like Windows-1250 (common for Slovenian)
        private bool LooksLikeWindows1250(byte[] bytes)
        {
            try
            {
                string testText = Encoding.GetEncoding("windows-1250").GetString(bytes);

                // Look for common Slovenian characters that would be garbled if read as UTF-8
                // Common Slovenian letters: č, š, ž, Č, Š, Ž
                int slovenianCharCount = testText.Count(c => "čšžČŠŽćđĐ".Contains(c));

                // Also check for common garbled patterns when Windows-1250 is read as UTF-8
                string asUtf8 = Encoding.UTF8.GetString(bytes);
                int garbledCount = 0;

                // These are common garbled patterns: è (should be č), æ (should be ć)
                foreach (char c in asUtf8)
                {
                    if ("èæ".Contains(c))
                        garbledCount++;
                }

                // If we see many potential Slovenian chars when decoded as Win-1250,
                // and garbled chars when decoded as UTF-8, it's likely Win-1250
                return slovenianCharCount > 5 && garbledCount > 3;
            }
            catch
            {
                return false;
            }
        }

        // Check if content looks like ISO-8859-2
        private bool LooksLikeISO88592(byte[] bytes)
        {
            try
            {
                string testText = Encoding.GetEncoding("iso-8859-2").GetString(bytes);
                int slovenianCharCount = testText.Count(c => "čšžČŠŽćđĐ".Contains(c));
                return slovenianCharCount > 5;
            }
            catch
            {
                return false;
            }
        }
        // Verify if bytes are valid UTF-8
        private bool IsValidUtf8(byte[] bytes)
        {
            try
            {
                var decoder = Encoding.UTF8.GetDecoder();
                int charCount = decoder.GetCharCount(bytes, 0, bytes.Length, true);

                // Also check that decoded text doesn't contain replacement characters
                string text = Encoding.UTF8.GetString(bytes);
                return !text.Contains('\ufffd'); // Unicode replacement character
            }
            catch
            {
                return false;
            }
        }

        // Helper method to detect HTML content
        private bool IsHtmlContent(byte[] content)
        {
            if (content.Length < 15)
                return false;

            var start = Encoding.UTF8.GetString(content, 0, Math.Min(100, content.Length)).ToLower();
            return start.Contains("<!doctype html") || start.Contains("<html") || start.Contains("<head");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Logout before disposing
                    if (!string.IsNullOrEmpty(_authToken))
                    {
                        LogoutAsync().GetAwaiter().GetResult();
                    }
                    //_httpClient?.Dispose(); DON'T dispose HttpClient if it came from factory
                    _authToken = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class Subtitle
    {
        public string SubtitleId { get; set; }
        public int? FileId { get; set; }
        public string MovieName { get; set; }
        public string Language { get; set; }
        public string FileName { get; set; }
        public int DownloadCount { get; set; }
        public double Rating { get; set; }
        public bool FromTrusted { get; set; }

        public override string ToString()
        {
            return $"{MovieName} - {FileName} (Downloads: {DownloadCount}, Rating: {Rating:F1}, Trusted: {FromTrusted})";
        }
    }
}