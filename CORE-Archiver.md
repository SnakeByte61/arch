
# Core Archiver

## Overview
Core Archiver is an Azure Function App built using the .NET 8 Isolated Worker model. Its purpose is to archive messages from an Azure Service Bus Topic Subscription into Azure Blob Storage, applying structured naming conventions and metadata for long-term retention, compliance, traceability, and operational analytics.

The Function App uses:
- Azure Service Bus trigger
- Azure Blob Storage SDK
- Application Insights for telemetry
- Custom helper classes for:
  - Correlation ID extraction
  - Blob name generation
  - Container name generation

This document explains:
- What the Core Archiver function does
- How to use it
- How the helper classes work
- How to configure and deploy it

---

## Architecture
```
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
```

---

## Program.cs Summary
`Program.cs` sets up:
- `BlobServiceClient` with retry policy
- Application Insights
- Dependency injection for the isolated worker runtime

This is where the storage connection string `AzureWebJobsStorage` is injected into the `BlobServiceClient`.

---

## Function: `ArchiverTopicTrigger`
This is the main function implementation.

### Responsibilities
1. Receive a message from a Service Bus topic subscription.
2. Extract the correlation ID from the message.
3. Generate:
   - A container name
   - A blob name
4. Upload the message into Blob Storage with:
   - Metadata
   - Custom Content-Type
5. Log everything to Application Insights.

### Trigger Details
```csharp
[ServiceBusTrigger(
    "sbt-css-rbp-kafka-bts-receive-small-usageresponse",
    "bts-receive-small-usageresponse-archive",
    Connection = "ServiceBusConnection")]
```

### Blob Upload Includes
- Setting content-type from environment variable `msgContextType`
- Adding metadata: `{ "CorrelationId" : "..." }`
- Creating the container if it does not exist

### Exception Handling
Catches and logs:
- `StorageException`
- `ServiceBusException`
- `Exception`

All exceptions include relevant context.

---

## Helper Classes
Descriptions of the three helper classes.

### 1. `CorrelationId.cs`
Extracts the `<correlationId>...</correlationId>` value from the Service Bus message.

#### How it works
- Uses regex to search for `<correlationId>value</correlationId>`.
- If the XML contains the node → returns trimmed value.
- If empty → logs a warning.
- If missing → logs a warning and returns `"UNKNOWN"`.

#### Why this matters
Correlation IDs are essential for:
- Traceability
- Structured blob naming
- Logging correlation
- Root-cause analysis

---

### 2. `BlobNameGenerator.cs`
Generates a unique blob filename for each archived message.

#### Blob Naming Pattern
```
<Guid>_<CorrelationId>_rbp-iai-622_ici-192-simplebillingresponse-mdms-105-sb.xml
```

Where:
- `Guid.NewGuid()` prefix ensures uniqueness
- Suffix adds semantic meaning
- Correlation ID ties the blob to related messages

#### Example
```
c4b42b64-35f2-4f04-a42c-97bfa821e49c_12345_rbp-iai-622_ici-192-simplebillingresponse-mdms-105-sb.xml
```

---

### 3. `ContainerNameGenerator.cs`
Generates the container name used to store the message blobs.

#### How it works
1. Reads environment variable: `riceId`
2. Gets current UTC time and converts to Eastern Time
3. Builds container name:
```
<yyyyMMdd>-<riceId>
```

#### Example
Date: March 30, 2026
RiceId: `CSS01`
```
20260330-CSS01
```

#### Benefits
- Time‑partitioned storage
- Predictable container names
- Supports retention, lifecycle rules, and analytics segmentation

---

## Configuration Requirements
### Required App Settings
| Name | Purpose |
|------|---------|
| AzureWebJobsStorage | Blob Storage + Function runtime |
| ServiceBusConnection | Service Bus trigger connection |
| msgContextType | MIME type (defaults to text/plain) |
| riceId | Required by ContainerNameGenerator |

---

