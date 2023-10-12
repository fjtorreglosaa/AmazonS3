using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.AspNetCore.Mvc;

namespace DemoAmazonS3.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly IAmazonS3 _s3Client;


        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IAmazonS3 s3Client)
        {
            _logger = logger;
            _s3Client = s3Client;
        }

        public string folderName = "Images/PyxelEdit/";
        public string BuketName = "udemy-course-buckets-amazon-s3-ftorreglosa";

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("getfile")]
        public async Task<IActionResult> GetFile(string filename)
        {
            //var s3Client = new AmazonS3Client();
            var response = await _s3Client.GetObjectAsync(BuketName, filename);

            using (var reader = new StreamReader(response.ResponseStream))
            {
                var fileContents = await reader.ReadToEndAsync();
            }

            return File(response.ResponseStream, response.Headers.ContentType);
        }

        [HttpGet("getfiles")]
        public async Task<IActionResult> GetFiles(string prefix)
        {
            //var s3Client = new AmazonS3Client();

            var request = new ListObjectsV2Request 
            { 
                BucketName = BuketName,
                Prefix = prefix 
            };

            var response = await _s3Client.ListObjectsV2Async(request);

            var presignedURLs = response.S3Objects.Select(x => {

                var request = new GetPreSignedUrlRequest
                {
                    BucketName = BuketName,
                    Key = x.Key,
                    Expires = DateTime.UtcNow.AddSeconds(60)
                };

                return _s3Client.GetPreSignedURL(request);

            });

            return Ok(presignedURLs);
        }

        [HttpPost]
        public async Task Post(IFormFile formfile)
        {
            //var s3Client = new AmazonS3Client();

            var bucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, BuketName);

            if (!bucketExist)
            {
                var bucketRequest = new PutBucketRequest

                {
                    BucketName = BuketName,
                    UseClientRegion = true,
                };

                await _s3Client.PutBucketAsync(bucketRequest);
            }

            var objectRequest = new PutObjectRequest()
            {
                BucketName = BuketName,
                Key = $"{folderName}{Guid.NewGuid()}_{formfile.FileName}",
                InputStream = formfile.OpenReadStream(),
                StorageClass = S3StorageClass.Standard // Revisar el pricing de cada tipo de clase de almacenamiento
            };

            //await SetBucketPolicyAsync(_s3Client, BuketName);

            objectRequest.Metadata.Add("Test", "Metadata");

            var response = await _s3Client.PutObjectAsync(objectRequest);
        }

        [HttpDelete]
        public async Task Delete(string filename)
        {
            await _s3Client.DeleteObjectAsync(BuketName, filename);
        }

        private static async Task SetBucketPolicyAsync(IAmazonS3 s3Client, string bucketName)
        {
            var policy = new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Sid = "PublicReadForGetBucketObjects",
                        Effect = "Allow",
                        Principal = "*",
                        Action = "s3:GetObject",
                        Resource = $"arn:aws:s3:::{bucketName}/*"
                    }
                }
            };

            var request = new PutBucketPolicyRequest
            {
                BucketName = bucketName,
                Policy = Newtonsoft.Json.JsonConvert.SerializeObject(policy)
            };

            await s3Client.PutBucketPolicyAsync(request);
        }
    }
}