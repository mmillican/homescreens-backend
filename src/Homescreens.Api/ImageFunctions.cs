using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Homescreens.Api.Models;
using Homescreens.Shared.Models;
using Homescreens.Shared.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace Homescreens.Api
{
    public partial class ImageFunctions
    {
        private const string DYNAMO_TABLE_NAME_KEY = "ImageTable";
        private const string BUCKET_NAME_KEY = "BucketName";
        private const string UPLOAD_KEY_PREFIX = "upload/";
        private const int PRESIGN_URL_EXPIRATION_SECS = 120;

        private readonly IAmazonS3 _s3Client;
        private readonly DynamoDbService _ddbService;
        private readonly string _bucketName;

        private readonly Dictionary<string, string> _defaultHeaders = new Dictionary<string, string>
        {
            {"Content-Type", "application/json"},
            {"Access-Control-Allow-Headers", "Content-Type"},
            {"Access-Control-Allow-Origin", "http://localhost:8080"},
            {"Access-Control-Allow-Methods", "OPTIONS,POST,GET,PUT,DELETE"}
        };

        public ImageFunctions()
        {
            _bucketName = Environment.GetEnvironmentVariable(BUCKET_NAME_KEY);
            if (string.IsNullOrEmpty(_bucketName))
            {
                throw new Exception("Missing S3 bucket name");
            }

            _s3Client = new AmazonS3Client(RegionEndpoint.USEast1); // TODO: Specify from env?
            _ddbService = new DynamoDbService();
        }

        public ImageFunctions(IAmazonDynamoDB ddbClient, IAmazonS3 s3Client, string tableName, string bucketName)
        {
            _s3Client = s3Client;
            _bucketName = bucketName;

            _s3Client = new AmazonS3Client(RegionEndpoint.USEast1); // TODO: Specify from env?
            _ddbService = new DynamoDbService();
        }

        public async Task<APIGatewayProxyResponse> GetImagesAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var images = await _ddbService.GetAsync<ScreenImage>();

            context.Logger.LogLine($"Found {images.Count()} images");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(images),
                Headers = _defaultHeaders
            };

            return response;
        }

        public async Task<APIGatewayProxyResponse> GetImageAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var id = request.PathParameters["id"];

            var response = new APIGatewayProxyResponse();

            var image = await _ddbService.GetByIdAsync<ScreenImage>(id);
            if (image == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return response;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.Headers = _defaultHeaders;
            response.Body = JsonSerializer.Serialize(image);

            return response;
        }

        public async Task<APIGatewayProxyResponse> AddImageAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"POST /images body: {request.Body}");

            var imageRequest = JsonSerializer.Deserialize<AddImageRequestModel>(request?.Body);

            var fileExt = Path.GetExtension(imageRequest.FileName);

            var image = new ScreenImage
            {
                Type = imageRequest.ImageType,
                UploadedOn = DateTime.UtcNow,
                UserName = "test", // TODO: set from auth'd user
                // IsPublished is false until the image has been processed
            };
            image.FileName = $"{image.Id}{fileExt}";

            var keyPrefix = $"{UPLOAD_KEY_PREFIX}{image.UploadedOn.ToString("yyyy/MM")}";
            var imageFileKey = $"{keyPrefix}/{image.FileName}";

            // All of these properties, including meta-data must match what the
            // frontend is sending ootherwise the key will mismatch, resulting
            // in a 403 response from S3
            var presignRequest = new GetPreSignedUrlRequest
            {
                Verb = HttpVerb.PUT,
                BucketName = _bucketName,
                Key = imageFileKey,
                ContentType = imageRequest.ContentType,
                Expires = DateTime.UtcNow.AddSeconds(PRESIGN_URL_EXPIRATION_SECS),
            };

            presignRequest.Metadata.Add("imageId", image.Id);
            presignRequest.Metadata.Add("imageType", image.Type);
            presignRequest.Metadata.Add("userName", image.UserName); // Maybe use user ID instead?

            var presignedUrl = _s3Client.GetPreSignedURL(presignRequest);

            context.Logger.LogLine($"Saving image with ID {image.Id}");
            await _ddbService.SaveAsync(image);

            var addImageResponse = new AddImageResponseModel
            {
                Id = image.Id,
                Key = imageFileKey,
                Type = image.Type,
                UploadUrl = presignedUrl
            };

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.Created,
                Body = JsonSerializer.Serialize(addImageResponse),
                Headers = _defaultHeaders
            };

            return response;
        }

    }
}
