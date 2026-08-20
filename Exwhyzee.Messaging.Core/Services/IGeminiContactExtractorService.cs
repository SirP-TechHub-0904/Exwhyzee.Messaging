using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Services
{
    public class ExtractedContactDto
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Note { get; set; }
    }

    public interface IGeminiContactExtractorService
    {
        Task<List<ExtractedContactDto>> ExtractFromImageAsync(byte[] imageBytes, string mimeType, string apiKey);
        Task<List<ExtractedContactDto>> ExtractFromTextAsync(string rawText, string apiKey);
        Task<List<ExtractedContactDto>> ExtractFromFileAsync(byte[] fileBytes, string fileName, string apiKey);
        Task<string> PostToGeminiWithFallbackAsync(object payload, string apiKey);
    }
}
