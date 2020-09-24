using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Homescreens.Api.Models;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace Homescreens.Api
{
    public class ImageFunctions
    {
        private const string DYNAMO_TABLE_NAME_KEY = "ImageTable";

        private readonly IDynamoDBContext _ddbContext;

        private readonly Dictionary<string, string> _defaultHeaders = new Dictionary<string, string>
        {
            {"Content-Type", "application/json"}
        };

        public ImageFunctions()
        {
            var tableName = Environment.GetEnvironmentVariable(DYNAMO_TABLE_NAME_KEY);
            if (string.IsNullOrEmpty(tableName))
            {
                throw new Exception("Missing image table name");
            }

            AWSConfigsDynamoDB.Context.TypeMappings[typeof(HomeScreenImage)] =
                new Amazon.Util.TypeMapping(typeof(HomeScreenImage), tableName);

            var config = new DynamoDBContextConfig
            {
                Conversion = DynamoDBEntryConversion.V2
            };
            _ddbContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        public ImageFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new Exception("Missing image table name");
            }

            AWSConfigsDynamoDB.Context.TypeMappings[typeof(HomeScreenImage)] =
                new Amazon.Util.TypeMapping(typeof(HomeScreenImage), tableName);

            var config = new DynamoDBContextConfig
            {
                Conversion = DynamoDBEntryConversion.V2
            };
            _ddbContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        public async Task<APIGatewayProxyResponse> GetImagesAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var search = _ddbContext.ScanAsync<HomeScreenImage>(null);
            var page = await search.GetNextSetAsync();

            context.Logger.LogLine($"Found {page.Count} images");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(page),
                Headers = _defaultHeaders
            };

            return response;
        }

        public async Task<APIGatewayProxyResponse> GetImageAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var id = request.PathParameters["id"];

            var response = new APIGatewayProxyResponse();

            var image = await _ddbContext.LoadAsync<HomeScreenImage>(id);
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
            var image = JsonSerializer.Deserialize<HomeScreenImage>(request?.Body);

            context.Logger.LogLine($"Saving image with ID {image.Id}");
            await _ddbContext.SaveAsync<HomeScreenImage>(image);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(image),
                Headers = _defaultHeaders
            };

            return response;
        }

    }
}
