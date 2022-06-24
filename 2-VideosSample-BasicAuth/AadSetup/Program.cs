//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using System.Text.Json;

// Use configuration from the 'appsettings.json' and command line options. Command line
// options can be set like this: '/TenantId 00000000-0000-0000-0000-000000000000'. For more details, see:
// https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#command-line-arguments
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddCommandLine(args)
    .Build();

// Load and validate the options.
var options = config.Get<Options>();
if (options == null || !options.Valid())
{
    Console.WriteLine("Invalid options.");
    return;
}

var credential = new VisualStudioCredential(new VisualStudioCredentialOptions
{
    TenantId = options.TenantId
});

var graphClient = new GraphServiceClient(credential);

Console.WriteLine("Creating API application");
var apiApplication = new Application
{
    DisplayName = $"{options.ApplicationDisplayName} API",
    IdentifierUris = new[]
    {
        $"https://{options.TenantDomain}/{options.ApplicationName}"
    },
    Api = new ApiApplication
    {
        Oauth2PermissionScopes = new[]
        {
            new PermissionScope
            {
                Id = Guid.NewGuid(),
                AdminConsentDescription = "Watch Videos",
                AdminConsentDisplayName = "Watch Videos",
                UserConsentDescription = "Watch Videos",
                UserConsentDisplayName = "Watch Videos",
                IsEnabled = true,
                Value = "Videos.Watch",
                Type = "Admin"
            }
        }
    }
};

apiApplication = await graphClient.Applications
    .Request()
    .AddAsync(apiApplication);

Console.WriteLine("Creating client application");
var clientApplication = new Application
{
    DisplayName = $"{options.ApplicationDisplayName} Client",
    Spa = new SpaApplication
    {
        RedirectUris = new[]
        {
            options.RedirectUri
        }
    },
};

clientApplication = await graphClient.Applications
    .Request()
    .AddAsync(clientApplication);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

apiApplication.Api.PreAuthorizedApplications = new[]
{
    new PreAuthorizedApplication
    {
        AppId = clientApplication.AppId,
        DelegatedPermissionIds = new[]
        {
            apiApplication.Api.Oauth2PermissionScopes.First().Id!.ToString()
        }
    }
};

Console.WriteLine("Pre-authorizing client application");
await graphClient.Applications[apiApplication.Id]
    .Request()
    .UpdateAsync(apiApplication);

Console.WriteLine();
Console.WriteLine(new string('-', 50));
Console.WriteLine();

Console.WriteLine("API configuration (used when running 'az deployment group create'):");
Console.WriteLine($"  tenantId={options.TenantId}");
Console.WriteLine($"  apiApplicationClientId={apiApplication.AppId}");

Console.WriteLine();
Console.WriteLine(new string('-', 50));
Console.WriteLine();

Console.WriteLine("API configuration (set in appsettings.json):");
Console.WriteLine(JsonSerializer.Serialize(
    new
    {
        AzureAd = new
        {
            Audience = apiApplication.AppId,
            Authority = $"https://login.microsoftonline.com/{options.TenantId}",
            Instance = "https://login.microsoftonline.com",
            Domain = options.TenantDomain,
            TenantId = options.TenantId,
            ClientId = apiApplication.AppId
        }
    },
    jsonOptions));

Console.WriteLine();
Console.WriteLine(new string('-', 50));
Console.WriteLine();

Console.WriteLine("Client configuration (set in wwwroot/js/authConfig.js):");
Console.WriteLine(JsonSerializer.Serialize(
    new
    {
        auth = new
        {
            clientId = clientApplication.AppId,
            authority = $"https://login.microsoftonline.com/{options.TenantId}",
            redirectUri = options.RedirectUri
        }
    },
    jsonOptions));

Console.WriteLine(JsonSerializer.Serialize(
    new
    {
        apiConfig = new
        {
            scopes = new[] { $"{apiApplication.IdentifierUris.First()}/{apiApplication.Api.Oauth2PermissionScopes.First().Value}" }
        }
    },
    jsonOptions));
