using System.Net;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using static PakScraper.Program;

namespace PakScraper
{
    public partial class S3
    {
        private static string? bucket;
        private static RegionEndpoint region = RegionEndpoint.APSoutheast2;
        private static IAmazonS3? s3client;
        private static HttpClient? httpclient;
        public static void EstablishConnection(string bucketName)
        {
            bucket = bucketName;
            s3client = new AmazonS3Client(region);
            httpclient = new HttpClient();
        }

        public static void Dispose()
        {
            try
            {
                httpclient!.Dispose();
                s3client!.Dispose();
            }
            catch (Exception)
            {
                // Ignore exceptions when trying to dispose
            }
        }

        public static async Task UploadImageToS3(string url)
        {
            string fileName = url.Split("/").Last();

            try
            {
                if (await imageAlreadyExists(url)) return;

                Stream stream = new MemoryStream();
                await stream.WriteAsync(await httpclient!.GetByteArrayAsync(url));

                long byteLength = stream.Length;

                var putRequest = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = fileName,
                    InputStream = stream
                };

                PutObjectResponse response = await s3client!.PutObjectAsync(putRequest);

                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    Log(ConsoleColor.DarkGray, $"   New Image: {fileName} - Size: {printFileSize(byteLength)} - {url}");
                }
                else
                {
                    Console.WriteLine(response.HttpStatusCode);
                }

                stream.Dispose();

            }
            catch (AmazonS3Exception e)
            {
                Log(ConsoleColor.Red, "S3 Exception: " + e.Message);
            }
            catch (Exception e)
            {
                Log(ConsoleColor.Red, e.Message);
            }
        }

        // Check if image already exists on S3, returns true if exists, else throws exception and returns false
        private static async Task<bool> imageAlreadyExists(string url)
        {
            string fileName = url.Split("/").Last();

            try
            {
                var response = await s3client!.GetObjectAsync(bucketName: bucket, key: fileName);
                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    //Log(ConsoleColor.DarkGray, $"   Image {fileName} already exists - {url}");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // Takes a byte length such as 38043260 and returns a nicer string such as 38 MB
        private static string printFileSize(long byteLength)
        {
            if (byteLength < 1) return "0 KB";

            if (byteLength >= 1 && byteLength < 1000) return "1 KB";

            string longString = byteLength.ToString();

            if (byteLength >= 1000 && byteLength < 1000000)
                return longString.Substring(0, longString.Length - 3) + " KB";

            else return longString.Substring(0, longString.Length - 6) + " MB";
        }
    }
}