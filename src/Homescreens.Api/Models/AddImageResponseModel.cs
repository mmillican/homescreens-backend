using Amazon.Lambda.Core;

namespace Homescreens.Api.Models
{
    public class AddImageResponseModel
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Key { get; set; }

        public string UploadUrl { get; set; }
    }
}
