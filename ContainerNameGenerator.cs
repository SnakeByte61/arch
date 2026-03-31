using System;
using Microsoft.Extensions.Logging;

namespace CONOR.EIE.CORE.Azure.Archiver
{
    public class ContainerNameGenerator
    {
        private readonly ILogger _logger; // Assuming you have an ILogger instance
	
	    public ContainerNameGenerator(ILogger logger)
        {
            _logger = logger;
        }

        public string GenerateContainerName()
        {
            var riceId = Environment.GetEnvironmentVariable("riceId");
			
            // Get the current time in UTC
            DateTime utcNow = DateTime.UtcNow;
            
            // Define the Eastern Time zone
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			
            // Convert UTC time to Eastern Time
            DateTime easternNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, easternZone);
			
            // Format easterNow time
            var currentDate = easternNow.ToString("yyyyMMdd");
            
            //create container name
            var sbConName = new System.Text.StringBuilder();
            sbConName.Append(currentDate);
            sbConName.Append("-");
            sbConName.Append(riceId);

            return sbConName.ToString();
        }
    }
}
