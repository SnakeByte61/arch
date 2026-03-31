Core‑Archiver (Multi‑Topic, Multi‑Storage Edition)
Overview
Core‑Archiver is an Azure Function App built using the Azure Functions .NET 8 Isolated Worker model.
It provides durable, scalable, and fully traceable long‑term archiving of messages from multiple Azure Service Bus topics, storing them in separate Azure Storage Accounts for isolation and governance.
This edition supports:




















Topic NamePurposeStorage Accountsbt-css-rbp-kafka-bts-receive-small-usageresponseExisting Kafka small‑usage responsesPrimary Storage (AzureWebJobsStorage)620-621-MessagesNew integration topicSecondary Storage (AzureWebJobsStorage_620_621)
Each archived message includes:

Extracted Correlation ID
Deterministic container naming (date + tenant/environment + topic prefix)
Topic‑specific blob naming strategy
Optional metadata
Configurable content-type (per topic)
Full observability via Application Insights


Architecture
(Insert architecture diagram here if desired)
Message Flow

Each inbound topic triggers its own Azure Function.
Each function uses a dedicated BlobServiceClient, mapped to its storage account.
Messages are archived into containers partitioned by:
yyyyMMdd + riceId + topicPrefix


Message body is stored as a UTF‑8 blob with metadata.
Application Insights captures:

Logs
Correlation IDs
Exceptions
Performance traces




Key Features
✔ Multi‑Topic Support

Each Service Bus topic mapped to its own Function trigger
Independent: scaling, error isolation, observability, and deployment flexibility

✔ Multi‑Storage Support

Each topic writes to its own Storage Account
Reduces throttling
Improves workload separation
Enables per‑integration governance

✔ Shared Archival Pipeline
Despite multiple topics and storage accounts, both functions follow a consistent processing flow:

Correlation ID extraction
Container naming
Blob naming
Blob upload with metadata
App Insights logging & tracing


Project Structure
/CoreArchiver
│   Program.cs
│   local.settings.json
│
├── Functions
│     ArchiverTopicTrigger.cs                ← Kafka small message trigger
│     ArchiverTopicTrigger_620_621.cs        ← 620–621 message trigger
│
└── Helpers
      CorrelationId.cs
      BlobNameGenerator.cs
      ContainerNameGenerator.cs


Configuration
local.settings.json / Azure App Settings
JSON{  "IsEncrypted": false,  "Values": {    // Storage Accounts    "AzureWebJobsStorage": "<primary-storage-connection>",    "AzureWebJobsStorage_620_621": "<secondary-storage-connection>",    // Service Bus namespace    "ServiceBusConnection": "<SB connection>",    // Content Types    "msgContextType": "application/xml",    "msgContextType_620_621": "application/json",    // Naming helpers    "riceId": "CSS01",    // Topic & subscription metadata    "Topic_KafkaSmall": "sbt-css-rbp-kafka-bts-receive-small-usageresponse",    "Subscription_KafkaSmall": "btsShow more lines

Function Endpoints
1. Kafka Small Usage Messages
JSON[Function("ArchiverTopicTrigger")][ServiceBusTrigger(    "sbt-css-rbp-kafka-bts-receive-small-usageresponse",    "bts-receive-small-usageresponse-archive",    Connection = "ServiceBusConnection")]Show more lines
2. 620‑621 Messages
C#[Function("ArchiverTopicTrigger_620_621")][ServiceBusTrigger(    "620-621-Messages",    "archive-620-621",    Connection = "ServiceBusConnection")]Show more lines
Each function receives a different BlobServiceClient instance through Dependency Injection.

Naming Strategy
Blob Naming
<Guid>_<CorrelationId>_<topicPrefix>-archive.xml

Container Naming
<yyyyMMdd>-<riceId>-<topicPrefix>

Topic Prefixes

















TopicPrefixKafka Small Usagekafka620–621 Messages620_621
Used when constructing helper instances:
C#new BlobNameGenerator(_logger, "620_621");new ContainerNameGenerator(_logger, "620_621");Show more lines

Deployment
Required Azure Resources

Azure Service Bus topics + subscriptions
Primary Storage Account
Secondary Storage Account
Azure Function App (.NET 8 Isolated)
Application Insights
(Optional) Azure Key Vault

Recommendations

Deploy using Azure DevOps or GitHub Actions
Parameterize all environment‑specific values
Store secrets in Key Vault or App Configuration
Test triggers using a dedicated dev/test Service Bus namespace


Monitoring & Observability
Core‑Archiver uses Application Insights to capture:

Request logs
Blob Storage dependency calls
Correlation IDs
Exception telemetry
Custom dimensions (topic, blob name, container name)

Sample KQL Query
KQLtraces| where customDimensions.TopicName == "620-621-Messages"| order by timestamp descShow more lines

Error Handling
All functions include structured exception handling for:

StorageException
ServiceBusException
Exception

Errors include:

Topic Name
Correlation ID
Blob/Container Name
Exception Details


Scalability
Azure Functions automatically scale based on message load from each topic.
Using two separate storage accounts provides:

Reduced throttling
Better performance
Workload isolation

Consumption or Premium plans recommended depending on throughput.

Extensibility
To add more topics:

Create a new Function trigger
Provide a new DI‑registered BlobServiceClient (if isolation needed)
Reuse helper classes
Add a topic prefix for naming purposes

