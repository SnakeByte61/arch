# Multi-Topic Archiver Extension

## Overview

This document describes the multi-topic extension to the Core Archiver Function App. It enables a single Function App to archive messages from **20+ Azure Service Bus topics** to their respective **dedicated Azure Storage Accounts**, driven entirely by configuration — no code changes required to add or remove topics.

The extension coexists alongside the original `ArchiverTopicTrigger` function, which remains unchanged.

-----

## Why a Different Approach

The existing `ArchiverTopicTrigger` uses the `[ServiceBusTrigger]` attribute, which requires the topic name, subscription name, and connection reference to be **compile-time constants**. This approach does not scale to 20+ dynamically-configured topics.

The extension solves this by replacing compile-time trigger attributes with the **Azure Service Bus SDK `ServiceBusProcessor`**, managed inside an `IHostedService`. At startup, the service reads the topic mapping configuration, creates one `ServiceBusProcessor` per topic, and starts them all concurrently. Adding a new topic requires only a configuration update — no code deployment.

-----

## Architecture

```
┌───────────────────────────────────────────────────────────────────────┐
│ Function App Process                                                  │
│                                                                       │
│  ┌─────────────────────────────────┐                                  │
│  │ ArchiverTopicTrigger (existing) │  ◄── [ServiceBusTrigger] attr    │
│  │ Single hardcoded topic          │      unchanged                   │
│  └─────────────────────────────────┘                                  │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ ServiceBusArchiveHostedService (new)                            │  │
│  │                                                                 │  │
│  │  TopicMappingRegistry                                           │  │
│  │  ┌─────────────────────────────────────────────────────────┐   │  │
│  │  │ Topic A → BlobArchiveService → Storage Account A        │   │  │
│  │  │ Topic B → BlobArchiveService → Storage Account B        │   │  │
│  │  │ Topic C → BlobArchiveService → Storage Account C        │   │  │
│  │  │  ...up to N topics...                                   │   │  │
│  │  └─────────────────────────────────────────────────────────┘   │  │
│  │                                                                 │  │
│  │  One ServiceBusProcessor per topic (started at App startup)     │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘

Configuration sources (merged at startup, App Config takes precedence):
  appsettings.json  ──►  IConfiguration  ◄──  Azure App Configuration
```

-----

## New Files

The following files are added to the project. No existing files are modified.

```
Models/
  TopicArchiveMapping.cs          ← POCO for one topic-to-storage mapping

Services/
  IBlobArchiveService.cs          ← Archive contract
  BlobArchiveService.cs           ← Per-topic blob writer
  ITopicMappingRegistry.cs        ← Registry contract
  TopicMappingRegistry.cs         ← Loads config, creates BlobArchiveService instances

HostedServices/
  ServiceBusArchiveHostedService.cs  ← Starts/stops one processor per topic

Program.cs                        ← Updated (additions only, existing code unchanged)
appsettings.json                  ← New (local dev config template)
AppSettingValues.txt              ← Updated (new tokens appended)
```

-----

## Section 1: Configuration Model — `TopicArchiveMapping`

**File:** `Models/TopicArchiveMapping.cs`

Each entry in the `TopicArchiveMappings` array maps one Service Bus topic to one Storage Account.

|Property                 |Required|Description                                                                          |
|-------------------------|--------|-------------------------------------------------------------------------------------|
|`TopicName`              |Yes     |Service Bus topic name. Must match exactly.                                          |
|`SubscriptionName`       |Yes     |Archive subscription name on the topic.                                              |
|`StorageConnectionString`|Yes     |Connection string for the target Storage Account.                                    |
|`MsgContentType`         |No      |MIME type written to the blob. Defaults to `application/json`.                       |
|`RiceId`                 |No      |Per-topic RiceId for container naming. Falls back to the global `riceId` app setting.|

### Why per-topic RiceId

The existing `ContainerNameGenerator` reads `Environment.GetEnvironmentVariable("riceId")` — a single global value. With concurrent processors writing to different storage accounts under different RiceIds, mutating environment variables between calls is not thread-safe. `BlobArchiveService` inlines the same `yyyyMMdd-{riceId}` logic using the per-mapping `RiceId` field, with a transparent fallback to the global `riceId` for mappings that share it.

-----

## Section 2: Blob Archive Service — `BlobArchiveService`

**File:** `Services/BlobArchiveService.cs`

One instance of `BlobArchiveService` is created per `TopicArchiveMapping` at startup. It:

- Holds its own `BlobServiceClient` instance pointing to the mapping’s `StorageConnectionString`.
- Uses the existing `BlobNameGenerator` helper for blob naming consistency.
- Applies the same `BlobClientOptions` retry policy (`MaxRetries=5`, exponential backoff) as the original `Program.cs`.
- Writes the blob with `CorrelationId` metadata and `MsgContentType` headers — identical to `ArchiverTopicTrigger`.

### Container name generation

```
{easternTime:yyyyMMdd}-{RiceId}  →  e.g. 20260331-css01
```

The Eastern Time conversion replicates `ContainerNameGenerator` behaviour exactly.

-----

## Section 3: Topic Mapping Registry — `TopicMappingRegistry`

**File:** `Services/TopicMappingRegistry.cs`

The registry is a **singleton** registered in `Program.cs`. At startup it:

1. Reads the `TopicArchiveMappings` section from `IConfiguration` (merged appsettings + App Config).
1. Validates each entry — skips invalid entries with a warning log rather than failing startup.
1. Creates and stores one `BlobArchiveService` per valid mapping.
1. Logs a startup summary of all registered mappings.

#### Startup log example

```
TopicMappingRegistry: found 3 entry/entries in TopicArchiveMappings config section.
Registered mapping. Topic=sbt-topic-one, Subscription=sbt-topic-one-archive, RiceId=CSS01
Registered mapping. Topic=sbt-topic-two, Subscription=sbt-topic-two-archive, RiceId=CSS02
Registered mapping. Topic=sbt-topic-three, Subscription=sbt-topic-three-archive, RiceId=(global riceId fallback)
TopicMappingRegistry initialised. Valid mappings registered: 3.
```

#### Duplicate detection

If two mapping entries share the same `TopicName`, only the first is registered. A warning is logged for the duplicate.

-----

## Section 4: Hosted Service — `ServiceBusArchiveHostedService`

**File:** `HostedServices/ServiceBusArchiveHostedService.cs`

### Startup

On `StartAsync`, the service iterates over all mappings from the registry and creates one `ServiceBusProcessor` per mapping using the shared `ServiceBusClient`.

```csharp
_serviceBusClient.CreateProcessor(
    mapping.TopicName,
    mapping.SubscriptionName,
    new ServiceBusProcessorOptions
    {
        MaxConcurrentCalls         = 1,
        AutoCompleteMessages       = false,
        MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
    });
```

`MaxConcurrentCalls = 1` reflects the low-volume requirement (<100 msg/min per topic) and simplifies correlation in Application Insights traces.

### Message handling

For each message received:

1. Resolves the `IBlobArchiveService` for the topic from the registry.
1. Extracts the correlation ID using the existing `CorrelationId` helper.
1. Calls `ArchiveAsync` to write the blob.
1. Calls `CompleteMessageAsync` on success.
1. Calls `AbandonMessageAsync` on any failure — Service Bus retries up to `MaxDeliveryCount`, then moves the message to DLQ automatically.

### Shutdown

On `StopAsync`, all processors are stopped concurrently via `Task.WhenAll`, then disposed, and the `ServiceBusClient` is disposed. In-flight messages finish processing before shutdown completes.

### Coexistence with `ArchiverTopicTrigger`

Both mechanisms use `ServiceBusConnection` but operate on **different topic subscriptions**. There is no message routing conflict. The hosted service runs entirely in the background — it does not interfere with the Functions runtime trigger cycle.

-----

## Section 5: Program.cs Changes

The additions to `Program.cs` follow this structure. Existing registrations are preserved verbatim.

```
HostBuilder
├── ConfigureFunctionsWebApplication()          ← unchanged
├── ConfigureAppConfiguration()                 ← NEW: adds Azure App Config provider
└── ConfigureServices()
    ├── AddApplicationInsightsTelemetryWorkerService()  ← unchanged
    ├── ConfigureFunctionsApplicationInsights()         ← unchanged
    ├── AddSingleton<BlobServiceClient>(...)            ← unchanged (ArchiverTopicTrigger)
    ├── AddAzureAppConfiguration()                      ← NEW: refresh middleware
    ├── AddSingleton<ITopicMappingRegistry, TopicMappingRegistry>()  ← NEW
    └── AddHostedService<ServiceBusArchiveHostedService>()           ← NEW
```

-----

## Section 6: Configuration Reference

### appsettings.json (local development)

```json
{
  "AppConfigurationEndpoint": "",
  "TopicArchiveMappings": [
    {
      "TopicName": "sbt-topic-one",
      "SubscriptionName": "sbt-topic-one-archive",
      "StorageConnectionString": "UseDevelopmentStorage=true",
      "MsgContentType": "application/json",
      "RiceId": "CSS01"
    }
  ]
}
```

Leave `AppConfigurationEndpoint` empty to run entirely from local config.

### AppSettingValues.txt — new tokens (appended)

The `__` separator is the .NET configuration array indexer convention for environment variables and Azure App Service application settings.

```
"AppConfigurationEndpoint"="#{AppConfigurationEndpoint}#";

"TopicArchiveMappings__0__TopicName"="#{Topic0Name}#";
"TopicArchiveMappings__0__SubscriptionName"="#{Topic0SubscriptionName}#";
"TopicArchiveMappings__0__StorageConnectionString"="#{Topic0StorageConnectionString}#";
"TopicArchiveMappings__0__MsgContentType"="#{Topic0MsgContentType}#";
"TopicArchiveMappings__0__RiceId"="#{Topic0RiceId}#";
```

Increment the index (`__1__`, `__2__`, etc.) for each additional topic.

### Full app settings reference

|Setting                     |Scope         |Purpose                                                             |
|----------------------------|--------------|--------------------------------------------------------------------|
|`AzureWebJobsStorage`       |Existing      |Runtime storage + ArchiverTopicTrigger blob target                  |
|`ServiceBusConnection`      |Existing + New|Used by both ArchiverTopicTrigger and ServiceBusArchiveHostedService|
|`msgContextType`            |Existing      |Content type for ArchiverTopicTrigger only                          |
|`riceId`                    |Existing + New|Global RiceId fallback for mappings without a per-topic override    |
|`AppConfigurationEndpoint`  |New           |Azure App Config URI. Leave empty to skip App Config entirely.      |
|`TopicArchiveMappings__N__*`|New           |Per-topic mapping fields (N = 0-based index)                        |

-----

## Section 7: Azure App Configuration Setup

Azure App Configuration is **optional**. If `AppConfigurationEndpoint` is empty or absent, all mappings come from `appsettings.json` / Azure App Service application settings only.

### When to use App Configuration

Use App Configuration when you need to add, remove, or update topic mappings **without redeploying the Function App**.

### Key structure in App Configuration

Store the mappings as individual flat keys using the `TopicArchiveMappings:N:Property` format:

```
TopicArchiveMappings:0:TopicName          = sbt-topic-one
TopicArchiveMappings:0:SubscriptionName   = sbt-topic-one-archive
TopicArchiveMappings:0:StorageConnectionString = DefaultEndpointsProtocol=https;...
TopicArchiveMappings:0:MsgContentType     = application/json
TopicArchiveMappings:0:RiceId             = CSS01

TopicArchiveMappings:Sentinel             = 1
```

### Triggering a refresh

1. Update or add mapping keys in App Configuration.
1. Increment the value of `TopicArchiveMappings:Sentinel` (e.g. `1` → `2`).
1. Within 5 minutes (the configured refresh interval), `IConfiguration` will reflect the new values.
1. **Restart the Function App** to rebuild the `TopicMappingRegistry` singleton and apply the new processors.

> A sentinel key increment alone does not restart processors — the registry is built once at startup. Restart is required to activate new mappings. Future enhancement: implement a background refresh watcher in `ServiceBusArchiveHostedService` that reloads the registry on sentinel change.

-----

## Build and Test Checkpoints

### Checkpoint 1: Project compiles

Add the required NuGet package if not already present:

```
dotnet add package Microsoft.Extensions.Configuration.AzureAppConfiguration
```

Then verify:

```
dotnet build
```

Expected: no errors. The new files add no breaking changes to the existing code.

### Checkpoint 2: Registry loads from local config

Run locally with Azurite and a local Service Bus emulator (or a dev namespace).

Set `appsettings.json` with one mapping using `"StorageConnectionString": "UseDevelopmentStorage=true"`.

Start the Function App:

```
func start
```

Expected startup logs:

```
TopicMappingRegistry: found 1 entry/entries in TopicArchiveMappings config section.
Registered mapping. Topic=sbt-topic-one, ...
TopicMappingRegistry initialised. Valid mappings registered: 1.
ServiceBusArchiveHostedService starting. Creating 1 topic processor(s).
Processor started. Topic=sbt-topic-one, Subscription=sbt-topic-one-archive
```

### Checkpoint 3: ArchiverTopicTrigger still fires

Send a message to the original topic (`sbt-css-rbp-kafka-bts-receive-small-usageresponse`). Verify the existing trigger fires and writes to `AzureWebJobsStorage` as before. The hosted service should log nothing for this topic — it is not registered in `TopicArchiveMappings`.

### Checkpoint 4: Multi-topic message archived

Send a message to one of the configured topics. Expected log sequence:

```
Message received. Topic=sbt-topic-one, MessageId=..., DeliveryCount=1
Archiving message. Topic=sbt-topic-one, Container=20260331-css01, Blob=..., CorrelationId=...
Blob archived successfully. Topic=sbt-topic-one, Blob=...
Message archived and completed. Topic=sbt-topic-one, MessageId=..., CorrelationId=...
```

Verify the blob appears in the correct storage account under the `yyyyMMdd-{riceId}` container.

### Checkpoint 5: DLQ behaviour

Temporarily break the `StorageConnectionString` for one mapping (e.g. invalid account name). Send a message to that topic. Verify:

- `Failed to archive message` error is logged on each delivery attempt.
- After `MaxDeliveryCount` attempts, the message appears in the topic’s DLQ in Service Bus Explorer.
- Other topics continue processing without interruption.

-----

## Operational Notes

### Adding a new topic

1. Add a new `TopicArchiveMappings__N__*` entry to Azure App Service application settings (or Azure App Configuration).
1. Restart the Function App.
1. Verify the new processor appears in startup logs.

No code changes. No redeploy.

### Message volume scaling

The current configuration uses `MaxConcurrentCalls = 1` per processor, appropriate for the low-volume requirement (<100 msg/min per topic). If volume increases on specific topics, raise `MaxConcurrentCalls` for those mappings. A future enhancement could expose this as a per-mapping configuration field.

### Application Insights

All log statements use structured logging with named properties (`Topic`, `MessageId`, `CorrelationId`, `Container`, `Blob`). These are queryable in Application Insights Log Analytics:

```kusto
traces
| where message contains "Archiving message"
| project timestamp, message, customDimensions
| order by timestamp desc
```

### Poison messages and DLQ

Dead-lettering relies entirely on Service Bus `MaxDeliveryCount` (default: 10). No custom DLQ handler is implemented. Monitor DLQ depth per subscription via Azure Monitor or Service Bus Explorer. A DLQ depth alert is recommended for each archive subscription.
