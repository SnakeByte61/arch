# Core‑Archiver (Multi‑Topic, Multi‑Storage Edition)

## Overview
**Core‑Archiver** is an Azure Function App built using the **Azure Functions .NET 8 Isolated Worker** model.

It provides **durable, scalable, and fully traceable long‑term archiving** of messages from **multiple Azure Service Bus topics**, storing each topic’s messages into **isolated Azure Storage Accounts**.

### Supported Topics

| Topic Name | Purpose | Storage Account |
|-----------|----------|-----------------|
| **sbt-css-rbp-kafka-bts-receive-small-usageresponse** | Existing Kafka small‑usage responses | **Primary Storage** (`AzureWebJobsStorage`) |
| **620-621-Messages** | New integration topic | **Secondary Storage** (`AzureWebJobsStorage_620_621`) |

### Each archived message includes:
- Extracted Correlation ID  
- Deterministic container naming (date + tenant/environment + topic prefix)  
- Topic‑specific blob naming strategy  
- Optional metadata  
- Configurable content‑type (per topic)  
- Full telemetry via Application Insights  

---

## Architecture

*(Insert architecture diagram here if desired)*

### Message Flow
1. Two inbound Service Bus topics deliver messages to Core‑Archiver.
2. Each topic is handled by its own Azure Function trigger.
3. Each trigger uses a dedicated **BlobServiceClient** mapped to its storage account.
4. Messages are archived into containers partitioned by:
   yyyyMMdd + riceId + topicPrefix
5. Message content is uploaded as UTF‑8 with metadata.
6. Application Insights captures logs, correlation IDs, exceptions, and performance details.

---

## Key Features

### ✔ Multi‑Topic Support
- Each topic has its own Function trigger.  
- Independent scaling, isolation, error handling, and observability.

### ✔ Multi‑Storage Support
- Each topic archives to its own Storage Account.  
- Reduces contention and provides workload isolation.

### ✔ Consistent Archival Pipeline
Shared logic across all triggers:
- Correlation ID extraction  
- Container naming  
- Blob naming  
- Metadata injection  
- App Insights instrumentation  

---

## Project Structure

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

---

## Configuration

### local.settings.json / Azure App Settings
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<primary-storage-connection>",
    "AzureWebJobsStorage_620_621": "<secondary-storage-connection>",

    "ServiceBusConnection": "<SB connection>",

    "msgContextType": "application/xml",
    "msgContextType_620_621": "application/json",

    "riceId": "CSS01",

    "Topic_KafkaSmall": "sbt-css-rbp-kafka-bts-receive-small-usageresponse",
    "Subscription_KafkaSmall": "bts-receive-small-usageresponse-archive",

    "Topic_620_621": "620-621-Messages",
    "Subscription_620_621": "archive-620-621"
  }
}


## Function Endpoints
### 1. Kafka Small Usage Messages
C#[Function("ArchiverTopicTrigger")][ServiceBusTrigger(    "sbt-css-rbp-kafka-bts-receive-small-usageresponse",    "bts-receive-small-usageresponse-archive",    Connection = "ServiceBusConnection")]Show more lines

### 2. 620‑621 Messages
C#[Function("ArchiverTopicTrigger_620_621")][ServiceBusTrigger(    "620-621-Messages",    "archive-620-621",    Connection = "ServiceBusConnection")]Show more lines
Each function uses its own BlobServiceClient (DI‑injected).

## Naming Strategy
### Blob Naming
<Guid>_<CorrelationId>_<topicPrefix>-archive.xml

### Container Naming
<yyyyMMdd>-<riceId>-<topicPrefix>

## Topic Prefixes

















TopicPrefixKafka Small Usagekafka620–621 Messages620_621

### Example helper usage:
C#new BlobNameGenerator(_logger, "620_621");new ContainerNameGenerator(_logger, "620_621");Show more lines

## Deployment
### Required Azure Resources

Azure Service Bus (topics + subscriptions)
Primary Storage Account
Secondary Storage Account
Azure Function App (.NET 8 isolated)
Application Insights
(Optional) Key Vault

## Recommended Practices

Use Azure DevOps or GitHub Actions CI/CD
Parameterize all environment‑specific settings
Store secrets in Key Vault
Run integration tests using a test Service Bus namespace


## Monitoring & Observability
Using Application Insights, the system captures:

Request logs
Blob Storage dependency calls
Correlation IDs
Exception telemetry
Custom dimensions (topic, blob name, container name)

### Sample KQL Query
KQLtraces| where customDimensions.TopicName == "620-621-Messages"| order by timestamp descShow more lines

### Error Handling
Each function includes structured exception handling for:

StorageException
ServiceBusException
Exception

Errors include context on:

Topic name
Correlation ID
Blob/container names
Exception details


### Scalability
Azure Functions automatically scale based on message volume.
Using two separate storage accounts ensures:

Lower throttling risk
Distributed load
Improved performance isolation

### Consumption or Premium plans recommended.

## Extensibility
To add additional topics:

Create a new Function trigger
Inject a BlobServiceClient (optionally dedicated)
Reuse helper classes
Add a new topic prefix for naming
