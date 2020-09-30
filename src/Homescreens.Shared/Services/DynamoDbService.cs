using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Homescreens.Shared.Models;

namespace Homescreens.Shared.Services
{
    public class DynamoDbService
    {
        private readonly IDynamoDBContext _ddbContext;

        public DynamoDbService()
        {
            MapEntities();

            var config = new DynamoDBContextConfig
            {
                Conversion = DynamoDBEntryConversion.V2
            };

            _ddbContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        public DynamoDbService(IAmazonDynamoDB ddbClient)
        {
            MapEntities();

            var config = new DynamoDBContextConfig
            {
                Conversion = DynamoDBEntryConversion.V2
            };

            _ddbContext = new DynamoDBContext(ddbClient, config);
        }

        private static void MapEntities()
        {
            var imageTableName = Environment.GetEnvironmentVariable("ImageTable");
            if (string.IsNullOrEmpty(imageTableName))
            {
                throw new Exception("Missing image table name");
            }

            AWSConfigsDynamoDB.Context.TypeMappings[typeof(ScreenImage)] =
                new Amazon.Util.TypeMapping(typeof(ScreenImage), imageTableName);
        }

        public Task<TModel> GetByIdAsync<TModel>(string id) where TModel : class, new()
            => _ddbContext.LoadAsync<TModel>(id);

        public async Task<IEnumerable<TModel>> GetAsync<TModel>() where TModel : class, new()
        {
            var search = _ddbContext.ScanAsync<TModel>(null);
            var page = await search.GetNextSetAsync();  // Not sure if this will return all the records?
            return page;
        }

        public Task SaveAsync<TModel>(TModel model) where TModel : class, new()
            => _ddbContext.SaveAsync<TModel>(model);
    }
}
