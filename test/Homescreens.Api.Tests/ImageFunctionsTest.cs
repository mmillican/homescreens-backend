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
using Homescreens.Api.Models;
using Xunit;

namespace Homescreens.Api.Tests
{
    public class ImageFunctionsTest : IDisposable
    {
        private readonly string _tableName;
        private readonly IAmazonDynamoDB _ddbClient;

        public ImageFunctionsTest()
        {
            _tableName = $"homescreens_test-Images_{DateTime.Now.Ticks}";
            _ddbClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

            SetupTableAsync().Wait();
        }

        [Fact]
        public async Task GetImage_ReturnsImage_ForValidId()
        {
            var imageFunctions = new ImageFunctions(_ddbClient, _tableName);

            var now = DateTime.Now;

            // We have to create a record that we can test with
            var testImage = new HomeScreenImage
            {
                Type = "phone",
                FileName = $"my-image-{now.Ticks}.png",
                UserName = $"test-{now.Ticks}",
                UploadedOn = now
            };

            var testId = testImage.Id;

            var createTestImageRequest = new APIGatewayProxyRequest
            {
                Body = JsonSerializer.Serialize(testImage)
            };

            // Arrange
            var context = new TestLambdaContext();
            var createTestResponse = await imageFunctions.AddImageAsync(createTestImageRequest, context);

            var getImageRequest = new APIGatewayProxyRequest
            {
                PathParameters = new Dictionary<string, string>
                {
                    { "id" , testId }
                }
            };

            // Act
            var getImageResponse = await imageFunctions.GetImageAsync(getImageRequest, context);

            // Assert
            Assert.Equal(200, getImageResponse.StatusCode);
            Assert.NotEmpty(getImageResponse.Body);

            var retrievedImage = JsonSerializer.Deserialize<HomeScreenImage>(getImageResponse.Body);
            Assert.NotNull(retrievedImage);
            Assert.Equal(testId, retrievedImage.Id);
            Assert.Equal("phone", retrievedImage.Type);
            Assert.NotEmpty(retrievedImage.FileName);
        }

        [Fact]
        public async Task AddImage_SavesAndReturnsResponse()
        {
            // Arrange

            TestLambdaContext context;
            APIGatewayProxyRequest request;
            APIGatewayProxyResponse response;

            var imageFunctions = new ImageFunctions(_ddbClient, _tableName);

            var now = DateTime.Now;
            var image = new HomeScreenImage
            {
                Type = "phone",
                FileName = $"my-image-{now.Ticks}.png",
                UserName = $"test-{now.Ticks}",
                UploadedOn = now
            };

            request = new APIGatewayProxyRequest
            {
                Body = JsonSerializer.Serialize(image)
            };

            context = new TestLambdaContext();

            // Act
            response = await imageFunctions.AddImageAsync(request, context);

            // Assert
            Assert.Equal(200, response.StatusCode);
            Assert.NotEmpty(response.Body);
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
