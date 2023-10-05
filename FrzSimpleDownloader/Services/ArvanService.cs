using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Threading.Tasks;
using System.Reflection;
using Amazon.S3.Transfer;

namespace FrzSimpleDownloader.Services
{
	public class ArvanService
	{
		private static IAmazonS3 _s3Client;

		private const string AccessKey = "c6d3603f-5d6f-43bf-92c3-0a21e67aa6e0";
		private const string SecretKey = "49e5a2b8214b4ccb528e84a30c7639c181852295ff15d31a082d78fe5dc4c96f";
		public const string BUCKET_NAME = "vocavers";

		public ArvanService()
		{
			var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(AccessKey, SecretKey);
			var config = new AmazonS3Config { ServiceURL = "https://s3.ir-thr-at1.arvanstorage.ir" };
			_s3Client = new AmazonS3Client(awsCredentials, config);
		}

		/// <summary>
		/// This method uploads a file to an Amazon S3 bucket. This
		/// example method also adds metadata for the uploaded file.
		/// </summary>
		/// <param name="client">An initialized Amazon S3 client object.</param>
		/// <param name="bucketName">The name of the S3 bucket to upload the
		/// file to.</param>
		/// <param name="objectName">The destination file name.</param>
		/// <param name="filePath">The full path, including file name, to the
		/// file to upload. This doesn't necessarily have to be the same as the
		/// name of the destination file.</param>
		private static async Task UploadObjectFromFileAsync(
			IAmazonS3 client,
			string bucketName,
			string objectName,
			string filePath)
		{
			try
			{
				var putRequest = new PutObjectRequest
				{
					BucketName = bucketName,
					Key = objectName,
					FilePath = filePath,
					ContentType = "text/plain"
				};

				putRequest.Metadata.Add("x-amz-meta-title", "someTitle");

				PutObjectResponse response = await client.PutObjectAsync(putRequest);

				foreach (PropertyInfo prop in response.GetType().GetProperties())
				{
					Console.WriteLine($"{prop.Name}: {prop.GetValue(response, null)}");
				}

				Console.WriteLine($"Object {objectName} added to {bucketName} bucket");
			}
			catch (AmazonS3Exception e)
			{
				Console.WriteLine($"Error: {e.Message}");
			}
		}

		public void UploadByteArrayToS3(byte[] data, string bucketName, string keyName)
		{
			try
			{
				using var ms = new MemoryStream(data);
				var fileTransferUtility = new TransferUtility(_s3Client);
				var uploadRequest = new TransferUtilityUploadRequest
				{
					InputStream = ms,
					BucketName = bucketName,
					Key = keyName,
					CannedACL = S3CannedACL.PublicRead, // Set the ACL to PublicRead
				};
				fileTransferUtility.Upload(uploadRequest);

				Console.WriteLine($"Uploaded {keyName} to S3 bucket {bucketName}");
			}
			catch (AmazonS3Exception e)
			{
				Console.WriteLine($"Amazon S3 Error: {e.Message}");
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error: {e.Message}");
			}
		}

		/// <summary>
		/// Uses the client object to get a  list of the objects in the S3
		/// bucket in the bucketName parameter.
		/// </summary>
		/// <param name="vocavers"></param>
		/// <param name="bucketName">The bucket name for which you want to
		///     retrieve a list of objects.</param>
		/// <param name="client">The initialized S3 client obect used to call
		/// the ListObjectsAsync method.</param>
		public async Task<Dictionary<string, long>> ListingObjectsAsync(string bucketName, string folder)
		{
			var objects = new Dictionary<string, long>();
			try
			{
				ListObjectsRequest request = new()
				{
					BucketName = bucketName,
					Prefix = folder,
					MaxKeys = 500,
				};

				do
				{
					var response = await _s3Client.ListObjectsAsync(request);

					// Process the response.
					response.S3Objects
						.ForEach(obj =>
						{
							//Console.WriteLine($"{obj.Key,-35}{obj.LastModified.ToShortDateString(),10}{obj.Size,10}");
							objects.Add(obj.Key, obj.Size);
						});

					// If the response is truncated, set the marker to get the next
					// set of keys.
					if (response.IsTruncated)
					{
						request.Marker = response.NextMarker;
					}
					else
					{
						request = null;
					}
				} while (request != null);
			}
			catch (AmazonS3Exception ex)
			{
				Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
			}

			return objects;
		}
		public async Task<int> GetFolderFilesCount(string bucketName, string folderPath)
		{

			try
			{
				ListObjectsV2Request request = new ListObjectsV2Request
				{
					BucketName = bucketName,
					Prefix = folderPath,
					MaxKeys = Int32.MaxValue
				};

				var response = await _s3Client.ListObjectsV2Async(request);

				var fileCount = response.S3Objects.Count;
				return fileCount;
				Console.WriteLine($"Number of files in folder '{folderPath}': {fileCount}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
			}
			return 0;
		}

		public async Task<bool> ItemExist(string bucketName, string folderPath,string objectKey)
		{
			try
			{
				GetObjectMetadataRequest metadataRequest = new GetObjectMetadataRequest
				{
					BucketName = bucketName,
					
					Key = $"{folderPath.TrimEnd('/')}/{objectKey}",
				};

				// Attempt to get the object metadata
				var response = await _s3Client.GetObjectMetadataAsync(metadataRequest);

				// If the object metadata request succeeded, the item exists
				Console.WriteLine($"Item '{objectKey}' exists in folder '{folderPath}'.");
				return true;
			}
			catch (AmazonS3Exception ex)
			{
				// If the exception indicates that the object was not found, it does not exist
				Console.WriteLine(ex.StatusCode == System.Net.HttpStatusCode.NotFound
					? $"Item '{objectKey}' does not exist in folder '{folderPath}'."
					: $"Error: {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
				return false;
			}
		}

	}
}

