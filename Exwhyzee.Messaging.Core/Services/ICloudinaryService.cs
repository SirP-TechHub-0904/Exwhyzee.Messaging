using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface ICloudinaryService
    {
        bool IsConfigured { get; }
        Task<string> UploadFileAsync(IFormFile file, string subFolder = "general", string publicIdPrefix = "file");
        Task<string> UploadStreamAsync(Stream stream, string fileName, string subFolder = "general", string publicIdPrefix = "file");
    }
}

