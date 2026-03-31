using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CONOR.EIE.CORE.Azure.Archiver
{
	public class CorrelationId
	{
	    private readonly ILogger _logger; // Assuming you have an ILogger instance

	    public CorrelationId(ILogger logger)
	    {
	        _logger = logger;
	    }

	    public string GetCorrId(string mySbMsg)
	    {
	        string corrId = "UNKNOWN";
	        var match = Regex.Match(mySbMsg, @"<correlationId>(.*?)<\/correlationId>");
	        if (match.Success)
	        {
	            string regxMsg = match.Groups[1].Value.Trim();
				if (string.IsNullOrEmpty(regxMsg)) 
	            {
	                _logger.LogWarning("CorrelationId is empty in the message.");
	            }
	            else
	            {
	                _logger.LogInformation($"CorrelationId found: {corrId}");
	                corrId = match.Groups[1].Value;
	            }
	        }
	        else
	        {
	            _logger.LogWarning("CorrelationId not found in the message.");
            
	        }

	        return corrId;
	    }
	}
}