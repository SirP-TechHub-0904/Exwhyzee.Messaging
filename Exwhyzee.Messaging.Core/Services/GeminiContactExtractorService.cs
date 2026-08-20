using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Exwhyzee.Messaging.Core.Services
{
    public class GeminiContactExtractorService : IGeminiContactExtractorService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeminiContactExtractorService> _logger;

        public GeminiContactExtractorService()
        {
            _logger = null;
            _httpClientFactory = null;
        }

        public GeminiContactExtractorService(ILogger<GeminiContactExtractorService> logger, IHttpClientFactory httpClientFactory = null)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient GetHttpClient()
        {
            return _httpClientFactory != null ? _httpClientFactory.CreateClient() : new HttpClient();
        }

        public async Task<List<ExtractedContactDto>> ExtractFromImageAsync(byte[] imageBytes, string mimeType, string apiKey)
        {
            if (imageBytes == null || imageBytes.Length == 0 || string.IsNullOrWhiteSpace(apiKey))
            {
                return new List<ExtractedContactDto>();
            }

            try
            {
                string base64Image = Convert.ToBase64String(imageBytes);
                string prompt = @"You are a phone number and contact extraction assistant. 
Extract all valid phone numbers and any associated contact names or labels/notes from this image (e.g. from chat screenshots, phone books, business cards, paper lists, posters).
Rules:
1. Standardize phone numbers to clean numeric format (e.g. 08012345678, 070..., 090..., 081..., +234..., or international format).
2. Remove spaces, dashes, brackets, and any non-numeric symbols from the phone field.
3. If a name or title is visible near the phone number, extract it in the name field. If not, use 'Contact'.
4. Deduplicate duplicate phone numbers.
5. Return ONLY a valid JSON array of objects with no Markdown backticks or commentary:
[
  { ""name"": ""John Doe"", ""phone"": ""08012345678"", ""note"": ""Screenshot"" }
]";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType,
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        response_mime_type = "application/json"
                    }
                };

                string responseBody = await PostToGeminiWithFallbackAsync(payload, apiKey);
                return ParseGeminiResponse(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting contacts from image via Gemini");
                throw;
            }
        }

        public async Task<string> PostToGeminiWithFallbackAsync(object payload, string apiKey)
        {
            using var httpClient = GetHttpClient();
            string key = apiKey?.Trim() ?? "";
            string lastError = "";

            // 1. Dynamic Discovery via ModelService.ListModels
            var discoveredModels = new List<string>();
            try
            {
                var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";
                var listResponse = await httpClient.GetAsync(listUrl);
                if (listResponse.IsSuccessStatusCode)
                {
                    string listJson = await listResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(listJson);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                    {
                        foreach (var m in modelsArray.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                string name = nameProp.GetString() ?? ""; // e.g. "models/gemini-1.5-flash"
                                if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                                {
                                    bool canGenerate = false;
                                    foreach (var method in methods.EnumerateArray())
                                    {
                                        if (method.GetString() == "generateContent") canGenerate = true;
                                    }
                                    if (canGenerate && !string.IsNullOrEmpty(name))
                                    {
                                        discoveredModels.Add(name);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    string errContent = await listResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("ListModels returned ({StatusCode}): {Err}", listResponse.StatusCode, errContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ListModels discovery request failed");
            }

            // 2. Build target execution list: prefer flash models from discovered list first
            var targetModels = new List<string>();
            if (discoveredModels.Any())
            {
                // Order: flash first, then pro, then any other generateContent model
                var flash = discoveredModels.Where(m => m.Contains("flash")).ToList();
                var pro = discoveredModels.Where(m => m.Contains("pro")).ToList();
                var rest = discoveredModels.Except(flash).Except(pro).ToList();

                targetModels.AddRange(flash);
                targetModels.AddRange(pro);
                targetModels.AddRange(rest);
            }
            else
            {
                // Fallback default names
                targetModels.AddRange(new[]
                {
                    "models/gemini-1.5-flash",
                    "models/gemini-1.5-flash-latest",
                    "models/gemini-2.0-flash",
                    "models/gemini-1.5-pro",
                    "models/gemini-pro"
                });
            }

            // 3. Execute generateContent against discovered models (testing v1beta then v1)
            var apiVersions = new[] { "v1beta", "v1" };

            foreach (var modelPath in targetModels)
            {
                string cleanModel = modelPath.StartsWith("models/") ? modelPath : $"models/{modelPath}";

                foreach (var version in apiVersions)
                {
                    try
                    {
                        string url = $"https://generativelanguage.googleapis.com/{version}/{cleanModel}:generateContent?key={key}";
                        var request = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                        };

                        var response = await httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsStringAsync();
                        }

                        string err = await response.Content.ReadAsStringAsync();
                        lastError = err;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }
                }
            }

            // Parse detailed error if JSON
            string humanError = lastError;
            try
            {
                using var errDoc = JsonDocument.Parse(lastError);
                if (errDoc.RootElement.TryGetProperty("error", out var errObj) && errObj.TryGetProperty("message", out var msgProp))
                {
                    humanError = msgProp.GetString();
                }
            }
            catch { }

            throw new Exception(humanError);
        }

        public async Task<List<ExtractedContactDto>> ExtractFromTextAsync(string rawText, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new List<ExtractedContactDto>();
            }

            // If API key is available, use Gemini for high accuracy name + phone parsing
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    string prompt = @"You are a phone number and contact extraction assistant.
Extract all phone numbers and associated contact names from the following raw text/document.
Rules:
1. Standardize phone numbers to clean numeric format (e.g. 08012345678, 23480..., or international format).
2. Remove spaces, dashes, brackets, and any non-numeric symbols from the phone field.
3. Deduplicate numbers.
4. Return ONLY a valid JSON array of objects with no Markdown backticks:
[
  { ""name"": ""Name"", ""phone"": ""08012345678"", ""note"": ""Text"" }
]

Input Text:
" + rawText;

                    var payload = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = prompt } } }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1,
                            response_mime_type = "application/json"
                        }
                    };

                    string body = await PostToGeminiWithFallbackAsync(payload, apiKey);
                    return ParseGeminiResponse(body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gemini Text extraction failed, falling back to regex parser");
                }
            }

            // Fallback: Regex extraction
            return RegexExtractPhoneNumbers(rawText);
        }

        public async Task<List<ExtractedContactDto>> ExtractFromFileAsync(byte[] fileBytes, string fileName, string apiKey)
        {
            if (fileBytes == null || fileBytes.Length == 0) return new List<ExtractedContactDto>();

            string ext = Path.GetExtension(fileName ?? "").ToLower();

            // Images
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".bmp")
            {
                string mime = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
                return await ExtractFromImageAsync(fileBytes, mime, apiKey);
            }

            // Plain text or CSV
            if (ext == ".txt" || ext == ".csv")
            {
                string text = Encoding.UTF8.GetString(fileBytes);
                return await ExtractFromTextAsync(text, apiKey);
            }

            // Word (.docx)
            if (ext == ".docx")
            {
                string extractedDocxText = ExtractTextFromDocx(fileBytes);
                return await ExtractFromTextAsync(extractedDocxText, apiKey);
            }

            // Excel (.xlsx)
            if (ext == ".xlsx")
            {
                string extractedXlsxText = ExtractTextFromXlsx(fileBytes);
                return await ExtractFromTextAsync(extractedXlsxText, apiKey);
            }

            // Generic fallback
            string genericText = Encoding.UTF8.GetString(fileBytes);
            return await ExtractFromTextAsync(genericText, apiKey);
        }

        private List<ExtractedContactDto> ParseGeminiResponse(string jsonResponse)
        {
            var results = new List<ExtractedContactDto>();
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                // Extract text from Gemini candidates
                string textContent = "";
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var content = candidates[0].GetProperty("content");
                    var parts = content.GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        textContent = parts[0].GetProperty("text").GetString() ?? "";
                    }
                }
                else
                {
                    textContent = jsonResponse;
                }

                // Clean markdown code fence if present
                textContent = textContent.Trim();
                if (textContent.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    textContent = textContent.Substring(7);
                }
                if (textContent.StartsWith("```"))
                {
                    textContent = textContent.Substring(3);
                }
                if (textContent.EndsWith("```"))
                {
                    textContent = textContent.Substring(0, textContent.Length - 3);
                }
                textContent = textContent.Trim();

                using var listDoc = JsonDocument.Parse(textContent);
                if (listDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in listDoc.RootElement.EnumerateArray())
                    {
                        string name = item.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                        string phone = item.TryGetProperty("phone", out var p) ? (p.GetString() ?? "") : "";
                        string note = item.TryGetProperty("note", out var nt) ? (nt.GetString() ?? "") : "";

                        // Clean phone
                        phone = Regex.Replace(phone ?? "", @"[^\d+]", "");
                        if (phone.Length >= 8)
                        {
                            results.Add(new ExtractedContactDto
                            {
                                Name = string.IsNullOrWhiteSpace(name) ? "Contact" : name.Trim(),
                                Phone = phone,
                                Note = string.IsNullOrWhiteSpace(note) ? "AI Extracted" : note.Trim()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JSON from Gemini response");
            }

            // Deduplicate by phone
            return results.GroupBy(x => x.Phone).Select(g => g.First()).ToList();
        }

        private List<ExtractedContactDto> RegexExtractPhoneNumbers(string text)
        {
            var results = new List<ExtractedContactDto>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            // Pattern for Nigerian & International phone numbers
            var phoneRegex = new Regex(@"(?:\+?234|0)[789][01]\d{8}|(?:\+?234|0)[1-9]\d{8,10}|\b\d{10,14}\b");

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = phoneRegex.Match(line);
                if (match.Success)
                {
                    string cleanedPhone = Regex.Replace(match.Value, @"[^\d+]", "");
                    if (cleanedPhone.Length >= 8)
                    {
                        // Check if line is CSV / tabular (e.g. Name, Phone, Note)
                        var parts = line.Split(new[] { ',', '\t', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(p => p.Trim()).ToList();

                        string name = "Contact";
                        string note = "Imported";

                        if (parts.Count > 1)
                        {
                            // Find which part contains the phone
                            int phoneIdx = parts.FindIndex(p => phoneRegex.IsMatch(p));
                            if (phoneIdx != -1)
                            {
                                if (phoneIdx > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                                {
                                    name = parts[0];
                                    if (parts.Count > 2) note = string.Join(" ", parts.Skip(2));
                                }
                                else if (parts.Count > 1 && !string.IsNullOrWhiteSpace(parts[1]) && !phoneRegex.IsMatch(parts[1]))
                                {
                                    name = parts[1];
                                    if (parts.Count > 2) note = parts[2];
                                }
                            }
                        }

                        results.Add(new ExtractedContactDto
                        {
                            Name = name,
                            Phone = cleanedPhone,
                            Note = note
                        });
                    }
                }
            }

            // Also run global fallback if line parsing missed any raw unformatted numbers
            if (results.Count == 0)
            {
                var allMatches = phoneRegex.Matches(text);
                foreach (Match m in allMatches)
                {
                    string cleaned = Regex.Replace(m.Value, @"[^\d+]", "");
                    if (cleaned.Length >= 8)
                    {
                        results.Add(new ExtractedContactDto
                        {
                            Name = "Contact",
                            Phone = cleaned,
                            Note = "Imported"
                        });
                    }
                }
            }

            return results.GroupBy(x => x.Phone).Select(g => g.First()).ToList();
        }

        private string ExtractTextFromDocx(byte[] docxBytes)
        {
            try
            {
                using var ms = new MemoryStream(docxBytes);
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
                var docEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase));
                if (docEntry != null)
                {
                    using var stream = docEntry.Open();
                    var xdoc = XDocument.Load(stream);
                    var sb = new StringBuilder();
                    foreach (var p in xdoc.Descendants().Where(e => e.Name.LocalName == "p"))
                    {
                        var text = string.Concat(p.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.AppendLine(text);
                        }
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse .docx file");
            }
            return "";
        }

        private string ExtractTextFromXlsx(byte[] xlsxBytes)
        {
            try
            {
                using var ms = new MemoryStream(xlsxBytes);
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
                
                // 1. Read shared strings properly by <si> item index
                var strings = new List<string>();
                var stringsEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
                if (stringsEntry != null)
                {
                    using var sStream = stringsEntry.Open();
                    var xdoc = XDocument.Load(sStream);
                    foreach (var si in xdoc.Descendants().Where(e => e.Name.LocalName == "si"))
                    {
                        var text = string.Concat(si.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                        strings.Add(text);
                    }
                }

                // 2. Read all sheets matching xl/worksheets/sheet*.xml
                var sheetEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.FullName)
                    .ToList();

                var rowBuilder = new StringBuilder();

                foreach (var sheetEntry in sheetEntries)
                {
                    using var stream = sheetEntry.Open();
                    var xdoc = XDocument.Load(stream);

                    foreach (var row in xdoc.Descendants().Where(e => e.Name.LocalName == "row"))
                    {
                        var rowValues = new List<string>();
                        foreach (var cell in row.Elements().Where(e => e.Name.LocalName == "c"))
                        {
                            string type = (string)cell.Attribute("t");
                            string val = "";

                            if (type == "inlineStr")
                            {
                                var isElem = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "is");
                                if (isElem != null)
                                {
                                    val = string.Concat(isElem.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                                }
                            }
                            else
                            {
                                var valElem = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
                                if (valElem != null)
                                {
                                    string rawVal = valElem.Value;
                                    if (type == "s" && int.TryParse(rawVal, out int idx) && idx >= 0 && idx < strings.Count)
                                    {
                                        val = strings[idx];
                                    }
                                    else
                                    {
                                        // If stored in scientific notation (e.g. 2.34803E+12), convert to plain integer string
                                        if (double.TryParse(rawVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal) && (rawVal.Contains('E') || rawVal.Contains('e')))
                                        {
                                            val = numVal.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
                                        }
                                        else
                                        {
                                            val = rawVal;
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                rowValues.Add(val.Trim());
                            }
                        }

                        if (rowValues.Count > 0)
                        {
                            rowBuilder.AppendLine(string.Join(", ", rowValues));
                        }
                    }
                }

                return rowBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract text from .xlsx file");
            }
            return "";
        }
    }
}
