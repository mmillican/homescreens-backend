using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Homescreens.ImageProcessor;
using Xunit;

namespace HomeScreens.ImageProcessor.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task ImageProcessor_CanResizeImage()
        {
            var s3Client = new AmazonS3Client(RegionEndpoint.USEast1);

            var bucketName = $"hs-processor-test-{DateTime.Now.Ticks}";

            await s3Client.PutBucketAsync(bucketName);

            try
            {
                // TODO: Specify a test file path (on your local machine)
                var testFile = new FileInfo(@"");

                string testKey = $"uploaded/{DateTime.Now.Year}/{DateTime.Now.Month}/test.jpg";

                var putObjectRequest = new PutObjectRequest
                {
                    Key = testKey,
                    BucketName = bucketName,
                    InputStream = testFile.OpenRead(),
                    ContentType = "image/jpeg"
                };
                await s3Client.PutObjectAsync(putObjectRequest);

                var s3Event = new S3Event
                {
                    Records = new List<S3EventNotification.S3EventNotificationRecord>
                    {
                        new S3EventNotification.S3EventNotificationRecord
                        {
                            S3 = new S3EventNotification.S3Entity
                            {
                                Bucket = new S3EventNotification.S3BucketEntity { Name = bucketName },
                                Object = new S3EventNotification.S3ObjectEntity { Key = testKey }
                            }
                        }
                    }
                };

                var testFunction = new ImageHandler(s3Client);
                var result = await testFunction.ProcessImage(s3Event, new TestLambdaContext());

                Assert.Equal("Ok", result);
            }
            finally
            {
                // Delete all the test objects so the bucket can be removed
                var bucketObjects = await s3Client.ListObjectsAsync(bucketName);
                foreach(var s3obj in bucketObjects.S3Objects)
                {
                    await s3Client.DeleteObjectAsync(bucketName, s3obj.Key);
                }

                var deleteResponse = await s3Client.DeleteBucketAsync(bucketName);
                Console.WriteLine("Delete bucket response " + deleteResponse.HttpStatusCode);
            }
        }
    }
}
