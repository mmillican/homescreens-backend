using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Homescreens.Shared.Models;
using Homescreens.Shared.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Homescreens.ImageProcessor
{
    public class ImageHandler
    {
        private const string OriginalKeyPrefix = "upload/";

        // TODO: Add other device image types
        private readonly List<ImageSize> _sizes = new List<ImageSize>
        {
            new ImageSize("phone", "thumb", 100, 100),
            new ImageSize("phone", "small", 281, 609), // 25%
            new ImageSize("phone", "medium", 563, 1218), // 50%
            new ImageSize("phone", "large", 844, 1827), // 75%

            // This is assuming iPad in Portrait mode (10.5" model)
            new ImageSize("tablet", "thumb", 100, 100),
            new ImageSize("tablet", "small", 417, 556),
            new ImageSize("tablet", "medium", 834, 1112),
            new ImageSize("tablet", "large", 1251, 1668)
        };

        private readonly IAmazonS3 _s3Client;
        private readonly DynamoDbService _ddbService;

        /// <summary>
        /// Default constructor used by Lambda
        /// </summary>

        public ImageHandler()
        {
            _s3Client = new AmazonS3Client();
            _ddbService = new DynamoDbService();
        }

        /// <summary>
        /// Constructor used for testing
        /// </summary>
        public ImageHandler(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
            _ddbService = new DynamoDbService();
        }

        public async Task<string> ProcessImage(S3Event evnt, ILambdaContext context)
        {
            var s3Event = evnt.Records?[0].S3;
            if (s3Event == null)
            {
                context.Logger.LogLine("ERROR: Could not get S3 Event Data");
                return null;
            }

            try
            {
                var objMetadata = await _s3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                var imageId = objMetadata.Metadata["x-amz-meta-imageid"];
                var imageType = objMetadata.Metadata["x-amz-meta-imagetype"];

                context.Logger.LogLine($"Processing image {s3Event.Object.Key}. ImageType: {imageType ?? "unknown"}");

                // Copy the file from the upload folder
                var originalSizeKey = GetResizedFileKey(s3Event.Object.Key);
                await _s3Client.CopyObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key, s3Event.Bucket.Name, originalSizeKey);

                // Ideally, we would use the stream instead, but couldn't figure out how to get
                // it to re-use the same stream
                byte[] imageBytes = null;
                var imageContentType = "";

                using (var objectResp = await _s3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key))
                using (var ms = new MemoryStream())
                {
                    imageContentType = objectResp.Headers.ContentType;

                    await objectResp.ResponseStream.CopyToAsync(ms);
                    imageBytes = ms.ToArray();
                }

                var imageTypeSizes = _sizes.Where(x => x.ImageType == imageType);
                foreach (var imgSize in imageTypeSizes)
                {
                    context.Logger.LogLine($"... Resizing to {imgSize.Key}");

                    IImageFormat imageFormat;
                    using (var image = Image.Load(imageBytes, out imageFormat))
                    {
                        var resizedFileKey = GetResizedFileKey(s3Event.Object.Key, imgSize.Key);

                        using (var outStream = new MemoryStream())
                        {
                            image.Mutate(x => x.Resize(new ResizeOptions
                            {
                                Mode = ResizeMode.Max, // TODO: allow each profile to specify method used
                                Position = AnchorPositionMode.Center,
                                Size = new Size(imgSize.Width, imgSize.Height)
                            }));
                            image.Save(outStream, imageFormat);

                            var putObjectRequest = new PutObjectRequest
                            {
                                Key = resizedFileKey,
                                BucketName = s3Event.Bucket.Name,
                                ContentType = imageContentType,
                                InputStream = outStream
                            };
                            await _s3Client.PutObjectAsync(putObjectRequest);
                        }
                    }

                    context.Logger.LogLine($"... Resized and saved file '{s3Event.Object.Key}' to '{imgSize.Key}'");
                }

                var imageRecord = await _ddbService.GetByIdAsync<ScreenImage>(imageId);
                if (imageRecord != null)
                {
                    imageRecord.IsPublished = true;
                    await _ddbService.SaveAsync(imageRecord);
                }

                // Delete the original
                await _s3Client.DeleteObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);

                return "Ok";
            }
            catch(Exception ex)
            {
                context.Logger.LogLine($"Error processing image s3://{s3Event.Bucket.Name}/{s3Event.Object.Key}. Error: {ex.Message}");
                context.Logger.LogLine(ex.StackTrace);
                throw;
            }
        }

        private string GetResizedFileKey(string originalKey, string size = null)
        {
            var filename = Path.GetFileName(originalKey);
            var newFileKeyPrefix = originalKey.Replace(OriginalKeyPrefix, "").Replace(filename, "");

            if (!string.IsNullOrEmpty(size))
            {
                filename = $"{size}-{filename}";
            }

            return $"{newFileKeyPrefix}{filename}";
        }
    }
}
