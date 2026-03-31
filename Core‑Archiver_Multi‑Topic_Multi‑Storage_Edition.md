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
