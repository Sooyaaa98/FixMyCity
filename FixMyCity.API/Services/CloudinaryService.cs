// FixMyCity.API/Services/CloudinaryService.cs
//
// Phase-1 SKELETON for image storage migration. Wraps the Cloudinary SDK so
// ComplaintController.UploadComplaintImage can push photos to a private
// Cloudinary folder instead of local disk.
//
// Singleton lifetime — the Cloudinary client is thread-safe and re-creating
// it per request is wasteful.
//
// IsConfigured() lets the controller branch between Cloudinary (configured)
// and the legacy disk path (placeholder/empty config) so Phase-2 can ship
// with a feature flag rather than a hard cut-over. See risk_analysis.md R1.

using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace FixMyCity.API.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary? _cloudinary;
        private readonly string _uploadFolder;
        private readonly ILogger<CloudinaryService> _logger;
        private readonly bool _configured;

        public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
        {
            _logger       = logger;
            _uploadFolder = config["Cloudinary:UploadFolder"] ?? "fixmycity/complaints";

            var cloudName = config["Cloudinary:CloudName"];
            var apiKey    = config["Cloudinary:ApiKey"];
            var apiSecret = config["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName)
                || string.IsNullOrWhiteSpace(apiKey)
                || string.IsNullOrWhiteSpace(apiSecret))
            {
                _configured = false;
                _cloudinary = null;
                _logger.LogInformation(
                    "[CloudinaryService] Not configured — controller will use disk fallback.");
                return;
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
            _configured = true;
        }

        /// <summary>
        /// True only when CloudName, ApiKey, and ApiSecret are all set. Used by
        /// ComplaintController to decide between Cloudinary upload and local disk.
        /// </summary>
        public bool IsConfigured => _configured;

        /// <summary>
        /// Uploads an image stream to Cloudinary as a private resource.
        /// Returns the secure HTTPS URL. Throws on failure — callers must
        /// catch and fall back to disk or return an error to the citizen.
        /// </summary>
        public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
        {
            if (!_configured || _cloudinary is null)
                throw new InvalidOperationException("Cloudinary is not configured.");

            // Public ID: <folder>/<basename-no-ext>_<guid>. Guid suffix avoids
            // collisions when two citizens upload files with the same name.
            var publicId =
                $"{_uploadFolder}/{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}";

            var uploadParams = new ImageUploadParams
            {
                File      = new FileDescription(fileName, imageStream),
                PublicId  = publicId,
                Overwrite = false,
                Type      = "private",
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.StatusCode != System.Net.HttpStatusCode.OK)
                throw new InvalidOperationException(
                    $"Cloudinary upload failed: {result.Error?.Message}");

            _logger.LogInformation("Cloudinary upload OK: {publicId}", publicId);
            return result.SecureUrl.ToString();
        }

        /// <summary>
        /// Generates a short-lived signed URL for serving a private image.
        /// Falls back to the input URL on error so callers always get something
        /// renderable (the browser will then 401 if the URL is truly private —
        /// at which point Phase-2 ServeImage redirect will be necessary).
        /// </summary>
        public string GetSignedUrl(string cloudinaryUrl, int expiresInSeconds = 3600)
        {
            if (!_configured || _cloudinary is null) return cloudinaryUrl;

            try
            {
                // Extract publicId from a Cloudinary URL of the form:
                //   https://res.cloudinary.com/<cloud>/image/private/v1234567890/<folder>/<id>.<ext>
                var uri    = new Uri(cloudinaryUrl);
                var parts  = uri.AbsolutePath.Split('/');
                var vIdx   = Array.FindIndex(parts,
                    p => p.Length > 1 && p[0] == 'v' && char.IsDigit(p[1]));
                var rest   = vIdx >= 0
                    ? string.Join("/", parts[(vIdx + 1)..])
                    : string.Join("/", parts[2..]);
                var publicId = Path.ChangeExtension(rest, null);

                return _cloudinary.Api.UrlImgUp
                    .ResourceType("image")
                    .Type("private")
                    .Secure(true)
                    .Signed(true)
                    .Action("download")
                    .BuildUrl(publicId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[CloudinaryService] GetSignedUrl failed for {url}", cloudinaryUrl);
                return cloudinaryUrl;
            }
        }
    }
}
