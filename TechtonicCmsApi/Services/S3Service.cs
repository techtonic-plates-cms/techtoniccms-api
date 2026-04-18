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
    public string? Region { get; set; } = null;
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

            ForcePathStyle = true
        };

        if (_options.Region is not null)
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_options.Region); 
        }
        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        _s3 = new AmazonS3Client(credentials, config);

        if (!AmazonS3Util.DoesS3BucketExistV2Async(_s3, _options.Bucket).GetAwaiter().GetResult())
        {
            _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }).GetAwaiter().GetResult();
        }
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

    public string GetContentType(string filename)
    {
        var ext = System.IO.Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "svg" => "image/svg+xml",
            "ico" => "image/x-icon",
            "pdf" => "application/pdf",
            "doc" => "application/msword",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xls" => "application/vnd.ms-excel",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ppt" => "application/vnd.ms-powerpoint",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "txt" => "text/plain",
            "csv" => "text/csv",
            "json" => "application/json",
            "xml" => "application/xml",
            "html" => "text/html",
            "css" => "text/css",
            "js" => "text/javascript",
            "zip" => "application/zip",
            "tar" => "application/x-tar",
            "gz" => "application/gzip",
            "mp3" => "audio/mpeg",
            "mp4" => "video/mp4",
            "avi" => "video/x-msvideo",
            "mov" => "video/quicktime",
            "wav" => "audio/wav",
            _ => "application/octet-stream"
        };
    }
}
