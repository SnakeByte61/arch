using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using System.Text.RegularExpressions; //
using System.Text;
using Microsoft.Azure.Storage;
using Azure.Storage;
using Azure.Core;

namespace CONOR.EIE.CORE.Azure.Archiver 
{
    public class ArchiverTopicTrigger
    {
        private readonly ILogger _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly CorrelationId _corrID;
        private readonly BlobNameGenerator _blobNameGenerator;
        private readonly ContainerNameGenerator _containerNameGenerator;
 
        public ArchiverTopicTrigger(ILoggerFactory loggerFactory, BlobServiceClient blobServiceClient)
        {
            _logger = loggerFactory.CreateLogger<ArchiverTopicTrigger>();
            _blobServiceClient = blobServiceClient;
            _corrID = new CorrelationId(_logger);
            _blobNameGenerator = new BlobNameGenerator(_logger);
            _containerNameGenerator = new ContainerNameGenerator(_logger);
        }
 
        [Function("ArchiverTopicTrigger")]
        public async Task RunAsync(
            [ServiceBusTrigger("sbt-css-rbp-kafka-bts-receive-small-usageresponse", "bts-receive-small-usageresponse-archive", Connection = "ServiceBusConnection")] string mySbMsg)
        {
            try
            {
                _logger.LogInformation($"C# ServiceBus topic trigger function processed message: {mySbMsg}");

                string correlationId = _corrID.GetCorrId(mySbMsg);
              
                // get container name
                //var containerNameGenerator = new ContainerNameGenerator();
                var containerName = _containerNameGenerator.GenerateContainerName();
                
                // Get a unique name for the blob
                //var blobNameGenerator = new BlobNameGenerator();
                var blobName = _blobNameGenerator.GenerateBlobName(correlationId);

                // set Conetent type on blob using msgContextType from environment variables. 
                var msgContextType = Environment.GetEnvironmentVariable("msgContextType") ?? "text/plain";
				
                // Get a reference to a container
                BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToString());
                await containerClient.CreateIfNotExistsAsync();
    
                // Get a reference to a blob
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Add metadata to the blob
                var metadata = new Dictionary<string, string>
                        {
                            { "CorrelationId", correlationId }
                        };                      

                        var blobHttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = msgContextType // Set your desired content type here
                        };

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(mySbMsg)))
                {
                    await blobClient.UploadAsync(stream, new BlobUploadOptions
                    {
                        HttpHeaders = blobHttpHeaders,
                        Metadata = metadata
                    });
                }

            }
            catch (StorageException ex)
            {
                _logger.LogError($"Storage exception: {ex.Message}");
                // Handle storage-specific errors
            }
            catch (ServiceBusException ex)
            {
                _logger.LogError($"Service Bus exception: {ex.Message}");
                // Handle Service Bus-specific errors
            }
            catch (Exception ex)
            {
                _logger.LogError($"General exception: {ex.Message}");
                // Handle other types of errors
            }
            

        }
    }
}
