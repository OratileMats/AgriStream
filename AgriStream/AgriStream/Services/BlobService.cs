using System.Text.RegularExpressions;
using AgriStream.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace AgriStream.Services
{
    public interface IBlobService
    {
        Task<string> UploadAsync(IFormFile file, string farm, string uploadedBy);
        Task<List<DroneImage>> ListAsync(int top = 24);
    }

    /// <summary>Azure Blob Storage: drone images / farm media (task c).</summary>
    public class BlobService : IBlobService
    {
        public const string ContainerName = "drone-images";
        private readonly BlobContainerClient _container;

        public BlobService(IConfiguration config)
        {
            var cs = config.GetConnectionString("AzureBlobStorage")
                     ?? throw new InvalidOperationException("ConnectionStrings:AzureBlobStorage is not configured.");
            _container = new BlobContainerClient(cs, ContainerName);
        }

        public async Task<string> UploadAsync(IFormFile file, string farm, string uploadedBy)
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.None);

            var safeName = Regex.Replace(Path.GetFileName(file.FileName), @"[^A-Za-z0-9._-]", "_");
            var blobName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{safeName}";
            var blob = _container.GetBlobClient(blobName);

            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
                // Blob metadata must be ASCII
                Metadata = new Dictionary<string, string>
                {
                    ["farm"] = Ascii(farm),
                    ["uploadedBy"] = Ascii(uploadedBy)
                }
            });
            return blobName;
        }

        public async Task<List<DroneImage>> ListAsync(int top = 24)
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.None);
            var result = new List<DroneImage>();

            await foreach (var item in _container.GetBlobsAsync(new GetBlobsOptions { Traits = BlobTraits.Metadata }))
            {
                var client = _container.GetBlobClient(item.Name);
                var url = client.CanGenerateSasUri
                    ? client.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(30)).ToString()
                    : client.Uri.ToString();

                item.Metadata.TryGetValue("farm", out var farm);
                item.Metadata.TryGetValue("uploadedBy", out var by);

                result.Add(new DroneImage
                {
                    Name = item.Name,
                    SizeBytes = item.Properties.ContentLength ?? 0,
                    UploadedOn = item.Properties.CreatedOn,
                    Farm = farm ?? "",
                    UploadedBy = by ?? "",
                    Url = url
                });
            }

            return result.OrderByDescending(i => i.UploadedOn).Take(top).ToList();
        }

        private static string Ascii(string s) => Regex.Replace(s ?? "", @"[^\x20-\x7E]", "?");
    }
}
