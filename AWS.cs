using System.Net;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace PakScraper
{
    public class S3
    {
        private const string bucketName = "paknsaveimages";
        // For simplicity the example creates two objects from the same file.
        // You specify key names for these objects.
        private const string keyName1 = "file1";
        private static readonly RegionEndpoint region = RegionEndpoint.APSoutheast2;
        private static IAmazonS3? s3client;
        public static async Task Upload()
        {
            s3client = new AmazonS3Client(region);
            string url = "https://a.fsimg.co.nz/product/retail/fan/image/400x400/5039956.png";
            await downloadImageToS3(url);
            await downloadImageToS3("https://a.fsimg.co.nz/product/retail/fan/image/400x400/5046503.png");
        }

        static async Task downloadImageToS3(string url)
        {
            string fileName = url.Split("/").Last();
            Console.WriteLine("File " + fileName);

            try
            {
                if (await imageAlreadyExists(url)) return;

                HttpClient httpclient = new HttpClient();
                Stream stream = new MemoryStream();

                await stream.WriteAsync(await httpclient.GetByteArrayAsync(url));

                long byteLength = stream.Length;

                var putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    InputStream = stream
                };

                PutObjectResponse response = await s3client!.PutObjectAsync(putRequest);

                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($"New Image Uploaded to S3: {fileName} - byteLength: {byteLength.ToString()}");
                }
                else
                {
                    Console.WriteLine(response.HttpStatusCode);
                }

                stream.Dispose();

            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine(
                        "Error encountered ***. Message:'{0}' when writing an object"
                        , e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "Unknown encountered on server. Message:'{0}' when writing an object"
                    , e.Message);
            }
        }

        static async Task<bool> imageAlreadyExists(string url)
        {
            string fileName = url.Split("/").Last();

            try
            {
                var response = await s3client!.GetObjectAsync(bucketName: bucketName, key: fileName);
                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($"Image {fileName} already exists");
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
    }
}