using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace TechtonicCmsApi.Services;

public class S3Options
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "techtonic-cms";
}

public class S3Service
{
    private readonly IAmazonS3 _s3;
    private readonly S3Options _options;

    public S3Service(IOptions<S3Options> options)
    {
        _options = options.Value;
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_options.Region),
            ForcePathStyle = true
        };
        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        _s3 = new AmazonS3Client(credentials, config);
    }

    public async Task<string> UploadAsync(string key, Stream body, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = body,
            ContentType = contentType
        };
        await _s3.PutObjectAsync(request);
        return key;
    }

    public async Task<Stream?> DownloadAsync(string key)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key
            };
            var response = await _s3.GetObjectAsync(request);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key
        };
        await _s3.DeleteObjectAsync(request);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key
            };
            await _s3.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NotFound" || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public string GetPresignedUrl(string key, int expiresInSeconds = 3600)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds)
        };
        return _s3.GetPreSignedURL(request);
    }

    public string GenerateS3Key(string filename, Guid userId)
    {
        var sanitized = Regex.Replace(filename, @"[^a-zA-Z0-9._-]", "_");
        var random = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .Replace("=", "")[..8];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"uploads/{userId}/{timestamp}-{random}-{sanitized}";
    }
}
