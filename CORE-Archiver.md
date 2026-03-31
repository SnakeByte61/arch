Core-Archiver.md
Overview
Core Archiver is an Azure Function App built using the .NET 8 Isolated Worker model.
Its purpose is to archive messages from an Azure Service Bus Topic Subscription into Azure Blob Storage, applying structured naming conventions and metadata for long-term retention, compliance, traceability, and operational analytics.
The Function App uses:
•	Azure Service Bus trigger
•	Azure Blob Storage SDK
•	Application Insights for telemetry
•	Custom helper classes for: 
o	Correlation ID extraction
o	Blob name generation
o	Container name generation
This document explains:
•	What the Core Archiver function does
•	How to use it
•	How the helper classes work
•	How to configure and deploy it
________________________________________
Architecture
┌───────────────────────┐
│ Service Bus Topic     │
│  + Subscription        │
└─────────────┬─────────┘
              │ Trigger
              ▼
┌──────────────────────────────┐
│ Core Archiver Function       │
│  - Extract correlation ID    │
│  - Generate container name   │
│  - Generate blob name        │
│  - Upload message content    │
└─────────────┬────────────────┘
              │ Blob Upload
              ▼
┌──────────────────────────────┐
│ Azure Blob Storage           │
│  + Metadata                  │
└──────────────────────────────┘
________________________________________
Program.cs Summary
Program.cs sets up:
•	BlobServiceClient with retry policy
•	Application Insights
•	Dependency injection for use in the isolated worker runtime
This is where the storage connection string AzureWebJobsStorage is injected into the BlobServiceClient
. [consolidat...epoint.com]
________________________________________
Function: ArchiverTopicTrigger
This is your main function implementation
. [consolidat...epoint.com]
Responsibilities
1.	Receive a message from a Service Bus topic subscription.
2.	Extract correlation ID from the message.
3.	Generate: 
o	A container name
o	A blob name
4.	Upload the message into Blob Storage with: 
o	Metadata
o	Custom Content-Type
5.	Log everything to Application Insights.
Trigger Details
[ServiceBusTrigger(
    "sbt-css-rbp-kafka-bts-receive-small-usageresponse",
    "bts-receive-small-usageresponse-archive",
    Connection = "ServiceBusConnection")]
Blob Upload Includes:
•	Setting content-type from environment variable msgContextType
•	Adding metadata: { "CorrelationId" : "..." }
•	Creating the container if it does not exist
Exception Handling
Catches:
•	StorageException
•	ServiceBusException
•	Exception
All exceptions logged with context.
________________________________________
Helper Classes
Below are the descriptions of your three helper classes, based directly on the uploaded source.
________________________________________
1. CorrelationId.cs
Extracts the <correlationId>...</correlationId> value from the Service Bus message
. [consolidat...epoint.com]
How it works
•	Uses a regular expression to search for: <correlationId>value</correlationId>
•	If the XML contains the node → returns trimmed value.
•	If empty → logs a warning.
•	If missing → logs a warning and returns "UNKNOWN".
Why this matters
Correlation IDs are essential for:
•	Traceability
•	Structured blob naming
•	Logging correlation
•	Root-cause analysis
________________________________________
2. BlobNameGenerator.cs
Generates a unique blob filename for each archived message
. [consolidat...epoint.com]
Blob Naming Pattern
<Guid>_<CorrelationId>_rbp-iai-622_ici-192-simplebillingresponse-mdms-105-sb.xml
Where:
•	The Guid.NewGuid() prefix ensures global uniqueness
•	Suffix (hardcoded business identifier) adds semantic meaning
•	The correlation ID ties the blob to related messages
Result Example
c4b42b64-35f2-4f04-a42c-97bfa821e49c_12345_rbp-iai-622_ici-192-simplebillingresponse-mdms-105-sb.xml
________________________________________
3. ContainerNameGenerator.cs
Generates the container name used to store the message blobs
. [consolidat...epoint.com]
How it works
1.	Reads environment var: riceId
2.	Gets current UTC time, converts it to Eastern Time
3.	Builds a container name:
<yyyyMMdd>-<riceId>
Example
If:
•	Date = March 30, 2026
•	riceId = "CSS01"
Then container becomes:
20260330-CSS01
Benefits
•	Time partitioned storage
•	Predictable container names
•	Supports retention, lifecycle rules, and analytics segmentation
________________________________________
Configuration Requirements
Required App Settings
Name	Purpose
AzureWebJobsStorage	Blob Storage + Function runtime
ServiceBusConnection	Service Bus trigger connection
msgContextType	MIME type (defaults to text/plain)
riceId	Required by ContainerNameGenerator
