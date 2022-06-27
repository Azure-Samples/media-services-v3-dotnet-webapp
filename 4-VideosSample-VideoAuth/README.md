# Video Streaming with per-video Authorization

This sample contains a .NET web applicaiton for streaming videos. In this version of the sample, access to videos is controlled using
Azure Active Directory users and groups.

## Features

This project framework provides the following features:

* A web application for browsing
* Video playback using Azure Media Player
* User authentication
* Per-video authorization
* Video encryption AES
* DRM protection for videos using PlayReady and Widevine
* Sample code for uploading and configuring Media Services to stream videos

## Getting Started

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [.NET 6](https://dotnet.microsoft.com/en-us/learn/dotnet/hello-world-tutorial/install)
- [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)
- An [Azure Subscription](https://azure.microsoft.com/)

### Azure Active Directory Configuration

```console
cd 4-VideosSample-PerVideoAuth

dotnet run --project AadSetup `
  /TenantId <tenant-id> `
  /TenantDomain "<tenant-domain>" `
  /ApplicationDisplayName "Video Sample" `
  /ApplicationName "video-sample"
```

### Resource Creation

```console
az login

az account set --subscription <subscription-id>

az group create --location westus --name <resource-group-name>

az deployment group create `
  --resource-group <resource-group-name> `
  --template-file .\MediaServices.bicep `
  --parameters baseName=<name> tenantId=<tenant-id> keyDeliveryApplicationClientId=<client-id> `
  --query "properties.outputs"
```

### Preparing Videos

Media Services can encode media content so it can be streamed using a wide variatey of devices. The AddVideoTool included in this sample will:
- Prepare a media file for streaming, the source content may come from a local mp4 file or a URL
  - Existing Media Services Assets may also be used
- Create a Streaming Locator for the video
- Build streaming and thumbnail URLs for the video
- Add the video to an index file

The AddVideoTool uses a Transform and a Streaming Policy created by the deployment template.

To add a video using a local mp4 file:
```console
dotnet run --project ..\AddVideoTool `
  /SubscriptionId <subscription-id> `
  /ResourceGroup <resource-group-name> `
  /AccountName <media-services-account-name> `
  /Transform VideosSampleContentAwareEncodingTransform `
  /StreamingPolicy VideosSampleEncryptionWithKeyProxyStreamingPolicy `
  /Title "All about cars" `
  /SourceFile cars.mp4 `
  /Asset Cars
```

To add a video from a URL:
```console
dotnet run --project ..\AddVideoTool `
  /SubscriptionId <subscription-id> `
  /ResourceGroup <resource-group-name> `
  /AccountName <media-services-account-name> `
  /Transform VideosSampleContentAwareEncodingTransform `
  /StreamingPolicy VideosSampleEncryptionWithKeyProxyStreamingPolicy `
  /Title "All about cars" `
  /SourceFile cars.mp4 `
  /Asset Cars
```

To use an existing Media Services asset (without encoding):
```console
dotnet run --project ..\AddVideoTool `
  /SubscriptionId <subscription-id> `
  /ResourceGroup <resource-group-name> `
  /AccountName <media-services-account-name> `
  /StreamingPolicy VideosSampleEncryptionWithKeyProxyStreamingPolicy `
  /Title "All about cars" `
  /Asset Cars
```

The AddVideoTool may also be configured using the `..\AddVideoTool\appsettings.json` file.

### Starting the default Streaming Endpoint

```console
az ams streaming-endpoint start `
  --resource-group <resource-group-name> `
  --account-name <media-services-account-name> `
  --name default 
```

### Building the Web App

Update appsettings.json and wwwroot/js/authConfig.js.

```console
dotnet run --project VideosSampleWithPerVideoAuth
```

Then open `https://localhost:7150/` in a browser.

### Using DRM

```console
dotnet run --project ..\AddVideoTool `
  /SubscriptionId <subscription-id> `
  /ResourceGroup <resource-group-name> `
  /AccountName <media-services-account-name> `
  /Transform VideosSampleContentAwareEncodingTransform `
  /StreamingPolicy VideosSampleDrmWithKeyProxyStreamingPolicy `
  /Title "All about boats" `
  /SourceFile boats.mp4 `
  /Asset Boats
```