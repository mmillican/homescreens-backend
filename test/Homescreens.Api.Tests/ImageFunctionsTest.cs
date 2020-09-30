using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Homescreens.Api.Models;
using Xunit;

namespace Homescreens.Api.Tests
{
    public class ImageFunctionsTest : IDisposable
    {
        private readonly string _tableName;
        private readonly IAmazonDynamoDB _ddbClient;

        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public ImageFunctionsTest()
        {
            var ticks = DateTime.Now.Ticks;

            _tableName = $"homescreens_test-Images_{ticks}";
            _ddbClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

            _s3Client = new AmazonS3Client(RegionEndpoint.USEast1);
            _bucketName = $"hs_test-{ticks}";

            SetupTableAsync().Wait();
        }

        // [Fact]
        // public async Task GetImage_ReturnsImage_ForValidId()
        // {
        //     var imageFunctions = new ImageFunctions(_ddbClient, _s3Client, _tableName, _bucketName);

        //     var now = DateTime.Now;

        //     // We have to create a record that we can test with
        //     var testImageRequest = new AddImageRequestModel
        //     {
        //         ImageType = "phone",
        //         FileName = $"my-image-{now.Ticks}.png",
        //         ContentType = "image/png"
        //     };

        //     var testId = testImage.Id;

        //     var createTestImageRequest = new APIGatewayProxyRequest
        //     {
        //         Body = JsonSerializer.Serialize(testImageRequest)
        //     };

        //     // Arrange
        //     var context = new TestLambdaContext();
        //     var createTestResponse = await imageFunctions.AddImageAsync(createTestImageRequest, context);

        //     var getImageRequest = new APIGatewayProxyRequest
        //     {
        //         PathParameters = new Dictionary<string, string>
        //         {
        //             { "id" , testId }
        //         }
        //     };

        //     // Act
        //     var getImageResponse = await imageFunctions.GetImageAsync(getImageRequest, context);

        //     // Assert
        //     Assert.Equal(200, getImageResponse.StatusCode);
        //     Assert.NotEmpty(getImageResponse.Body);

        //     var retrievedImage = JsonSerializer.Deserialize<HomeScreenImage>(getImageResponse.Body);
        //     Assert.NotNull(retrievedImage);
        //     Assert.Equal(testId, retrievedImage.Id);
        //     Assert.Equal("phone", retrievedImage.Type);
        //     Assert.NotEmpty(retrievedImage.FileName);
        // }

        [Fact]
        public async Task AddImage_SavesAndReturnsResponse()
        {
            // Arrange

            TestLambdaContext context;
            APIGatewayProxyRequest request;
            APIGatewayProxyResponse response;

            var imageFunctions = new ImageFunctions(_ddbClient, _s3Client, _tableName, _bucketName);

            var testImageRequest = new AddImageRequestModel
            {
                ImageType = "phone",
                FileName = $"my-image.png",
                ContentType = "image/png"
            };

            request = new APIGatewayProxyRequest
            {
                Body = JsonSerializer.Serialize(testImageRequest)
            };

            context = new TestLambdaContext();

            // Act
            response = await imageFunctions.AddImageAsync(request, context);

            // Assert
            Assert.Equal(201, response.StatusCode);
            Assert.NotEmpty(response.Body);

            var testResponse = JsonSerializer.Deserialize<AddImageResponseModel>(response.Body);
            Assert.NotNull(testResponse);

            Assert.NotEmpty(testResponse.Id);
            Assert.NotEmpty(testResponse.Key);
            Assert.NotEmpty(testResponse.UploadUrl);

            // Clean up after the test
            // Note: We don't need to create a bucket to test getting a pre-signed URL
            var deleteTableResponse = await _ddbClient.DeleteTableAsync(_tableName);
            Console.WriteLine($"Delete DDB table response: {deleteTableResponse.HttpStatusCode}");
        }

        private async Task SetupTableAsync()
        {
            var createTableRequest = new CreateTableRequest
            {
                TableName = _tableName,
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 1, // TODO: Not sure?
                    WriteCapacityUnits = 1
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        KeyType = KeyType.HASH,
                        AttributeName = "Id"
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Id",
                        AttributeType = ScalarAttributeType.S
                    }
                }
            };

            await _ddbClient.CreateTableAsync(createTableRequest);

            var describeTableRequest = new DescribeTableRequest
            {
                TableName = _tableName
            };

            DescribeTableResponse describeTableResponse = null;
            do
            {
                Thread.Sleep(1000);
                describeTableResponse = await _ddbClient.DescribeTableAsync(describeTableRequest);
            } while(describeTableResponse.Table.TableStatus != TableStatus.ACTIVE);
        }

        #region IDisposable

        private bool isDisposed = false; // To detect redunant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // _ddbClient.DeleteTableAsync(_tableName).Wait();
                    _ddbClient.Dispose();
                }

                isDisposed = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion
    }
}
