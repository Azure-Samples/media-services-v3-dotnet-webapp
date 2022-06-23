//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;

// Use configuraiton from the 'appsettings.json' and command line options. Command line
// options can be set like this: '/IndexFile index.json'. For more details, see:
// https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#command-line-arguments
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddCommandLine(args)
    .Build();

// Load and validate the options.
var options = config.Get<Options>();
if (!options.Valid())
{
    return;
}

Console.WriteLine($"Using index file '{Path.GetFullPath(options.IndexFile)}'");

// Create clients for ARM and Media Services using default credentials
var credential = new DefaultAzureCredential();
var arm = new ArmClient(credential);
var mediaServices = new AzureMediaServicesClient(new ClientCredentials(credential))
{
    SubscriptionId = options.SubscriptionId
};

var asset = options.Transform != null
    ? await EncodeContextAsync(mediaServices, options)
    : await GetAssetAsync(mediaServices, options);

if (asset == null)
{
    return;
}

Console.WriteLine("Creating a streaming locator");
var locator = await mediaServices.StreamingLocators.CreateAsync(
    options.ResourceGroup,
    options.AccountName,
    $"{options.Asset}-{DateTime.UtcNow.Ticks}",
    new StreamingLocator
    {
        AssetName = options.Asset,
        StreamingPolicyName = options.StreamingPolicy,
        AlternativeMediaId = Guid.NewGuid().ToString()
    });

Console.WriteLine("Getting streaming locator");
var locatorUri = await BuildLocatorUriAsync(mediaServices, options, locator);

Console.WriteLine("Getting thumbnail URI");
var thumbnailUri = await GetThumbnailUriAsync(arm, mediaServices, options, asset);

Console.WriteLine("Adding the video to the index");
VideosIndex.AddVideo(
    indexFile: options.IndexFile,
    videoId: locator.AlternativeMediaId,
    title: options.Title,
    locator: locatorUri,
    thumbnail: thumbnailUri,
    viewers: options.Viewers ?? new[] { "all" },
    contentKeyIds: locator.ContentKeys.Select(k => k.Id.ToString()).ToArray());

Console.WriteLine("Done");

// Encode media using a transform
static async Task<Asset?> EncodeContextAsync(AzureMediaServicesClient mediaServices, Options options)
{
    Console.WriteLine("Creating the asset");
    var asset = await mediaServices.Assets.CreateOrUpdateAsync(
        options.ResourceGroup,
        options.AccountName,
        options.Asset,
        new Asset());

    var sourceAsset = default(Asset);

    if (options.SourceFile != null)
    {
        Console.WriteLine("Creating a source asset");
        sourceAsset = await mediaServices.Assets.CreateOrUpdateAsync(
            options.ResourceGroup,
            options.AccountName,
            $"{options.Asset}-input",
            new Asset());

        Console.WriteLine("Getting the asset container URL");
        var sourceAssetSas = await mediaServices.Assets.ListContainerSasAsync(
            options.ResourceGroup,
            options.AccountName,
            $"{options.Asset}-input",
            AssetContainerPermission.ReadWrite,
            DateTime.UtcNow.AddDays(1));

        var sasUri = new Uri(sourceAssetSas.AssetContainerSasUrls.First());
        var blobOptions = new BlobClientOptions();
        blobOptions.Retry.NetworkTimeout = TimeSpan.FromMinutes(10);
        var sourceAssetContainer = new BlobContainerClient(sasUri, blobOptions);

        Console.WriteLine("Uploading the media file");
        using var stream = new FileStream(options.SourceFile, FileMode.Open);
        await sourceAssetContainer.UploadBlobAsync(Path.GetFileName(options.SourceFile), stream);
    }

    Console.WriteLine("Creating a job to encode the media file");
    var job = await mediaServices.Jobs.CreateAsync(
        options.ResourceGroup,
        options.AccountName,
        options.Transform,
        $"{options.Asset}-{DateTime.UtcNow.Ticks}",
        new Job
        {
            Input = sourceAsset != null
                ? new JobInputAsset { AssetName = sourceAsset.Name }
                : new JobInputHttp { BaseUri = options.SourceUrl },
            Outputs = new JobOutput[]
            {
                new JobOutputAsset { AssetName = asset.Name }
            }
        });

    while (job.State != JobState.Finished && job.State != JobState.Error)
    {
        Console.WriteLine($"Job state is {job.State}... {job.Outputs[0].Progress}% complete");
        await Task.Delay(5000);
        job = await mediaServices.Jobs.GetAsync(options.ResourceGroup, options.AccountName, options.Transform, job.Name);
    }

    Console.WriteLine($"Job state is {job.State}... {job.Outputs[0].Error?.Message} {job.Outputs[0].Error?.Details}");

    if (job.State == JobState.Error)
    {
        return null;
    }

    return asset;
}

// Get an existing asset
static async Task<Asset> GetAssetAsync(AzureMediaServicesClient mediaServices, Options options)
{
    Console.WriteLine("Getting asset details");
    return await mediaServices.Assets.GetAsync(
        options.ResourceGroup,
        options.AccountName,
        options.Asset);
}

// Builds a locator URI for an asset
static async Task<string> BuildLocatorUriAsync(AzureMediaServicesClient mediaServices, Options options, StreamingLocator locator)
{
    var paths = await mediaServices.StreamingLocators.ListPathsAsync(
    options.ResourceGroup,
    options.AccountName,
    locator.Name);

    var fullPath = paths.StreamingPaths
        .Single(p => p.StreamingProtocol == StreamingPolicyStreamingProtocol.Dash).Paths
        .First();

    var path = fullPath.Substring(0, fullPath.IndexOf('('));

    var streamingEndpoint = await mediaServices.StreamingEndpoints.GetAsync(
        options.ResourceGroup,
        options.AccountName,
        "default");

    return $"//{streamingEndpoint.HostName}{path}";
}

// Creates a SAS URL for the first thumbnail image found in the asset
static async Task<string?> GetThumbnailUriAsync(ArmClient arm, AzureMediaServicesClient mediaServices, Options options, Asset asset)
{
    var account = await mediaServices.Mediaservices.GetAsync(options.ResourceGroup, options.AccountName);

    var storage = arm.GetStorageAccountResource(new ResourceIdentifier(
        account.StorageAccounts.Single(x => x.Id.EndsWith(asset.StorageAccountName)).Id));

    var storageAccount = await storage.GetAsync();

    var storageKeys = await storage.GetKeysAsync();

    var storageCredentials = new StorageSharedKeyCredential(asset.StorageAccountName, storageKeys.Value.Keys[0].Value);

    var blobContainer = new BlobContainerClient(
        new Uri($"{storageAccount.Value.Data.PrimaryEndpoints.Blob}{asset.Container}"),
        storageCredentials);
    
    var thumbnailBlob = await blobContainer.GetBlobsAsync(prefix: "Thumbnail").FirstOrDefaultAsync();

    if (thumbnailBlob == null)
    {
        return null;
    }

    return blobContainer
        .GetBlobClient(thumbnailBlob.Name)
        .GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.AddYears(10))
        .AbsoluteUri;
}
