using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CONOR.EIE.CORE.Azure.Archiver
{	
    public class BlobNameGenerator
    {
        private readonly ILogger _logger; // Assuming you have an ILogger instance
	
	    public BlobNameGenerator(ILogger logger)
        {
            _logger = logger;
        }

        public string GenerateBlobName(string correlationId)
        {
            var suffix = "rbp-iai-622_ici-192-simplebillingresponse-mdms-105-sb";
			var contentType = "xml";
                
            // Create a unique name for the blob
            string blobName = $"{Guid.NewGuid()}_{correlationId}_{suffix}.{contentType}";
            
            return blobName.ToString();
        }
    }
}
