//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

public class KeyDeliveryClientOptions
{
    public string? Scope { get; set; }
    public string? Host { get; set; }
}

public class KeyDeliveryClient
{
    private readonly ITokenAcquisition _tokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _scope;
    private readonly string _host;

    public KeyDeliveryClient(ITokenAcquisition tokenProvider, IHttpClientFactory httpClientFactory, IOptions<KeyDeliveryClientOptions> optionsAccessor)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)); ;
        _scope = optionsAccessor?.Value?.Scope ?? throw new ArgumentOutOfRangeException(nameof(optionsAccessor), "Scope not set.");
        _host = optionsAccessor?.Value?.Host ?? throw new ArgumentOutOfRangeException(nameof(optionsAccessor), "Host not set.");
    }

    public async Task<IResult> GetEnvelopeKeyAsync(string contentKeyId)
    {
        var keyDeliveryToken = await _tokenProvider.GetAccessTokenForAppAsync(_scope);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{_host}/?kid={contentKeyId}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", keyDeliveryToken) }
        };

        using var client = _httpClientFactory.CreateClient();

        using var keyResponse = await client.SendAsync(request);

        if (!keyResponse.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)keyResponse.StatusCode);
        }

        var bytes = await keyResponse.Content.ReadAsByteArrayAsync();

        return Results.Bytes(bytes);
    }

    public async Task<IResult> GetPlayReadyKeyAsync(string challenge)
    {
        var keyDeliveryToken = await _tokenProvider.GetAccessTokenForAppAsync(_scope);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{_host}/PlayReady/")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", keyDeliveryToken) },
            Content = new StringContent(challenge, encoding: Encoding.UTF8, mediaType: "text/xml")
        };

        using var client = _httpClientFactory.CreateClient();

        using var keyResponse = await client.SendAsync(request);

        if (!keyResponse.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)keyResponse.StatusCode);
        }

        var bytes = await keyResponse.Content.ReadAsByteArrayAsync();

        return Results.Bytes(bytes);
    }

    public async Task<IResult> GetWidevineKeyAsync(string contentKeyId, byte[] challenge)
    {
        var keyDeliveryToken = await _tokenProvider.GetAccessTokenForAppAsync(_scope);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{_host}/Widevine/?kid={contentKeyId}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", keyDeliveryToken) },
            Content = new ByteArrayContent(challenge)
        };

        using var client = _httpClientFactory.CreateClient();

        using var keyResponse = await client.SendAsync(request);

        if (!keyResponse.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)keyResponse.StatusCode);
        }

        var bytes = await keyResponse.Content.ReadAsByteArrayAsync();

        return Results.Bytes(bytes);
    }

    public static bool TryGetPlayReadyContentKeyId(string body, [NotNullWhen(returnValue: true)] out string? challengeKeyId)
    {
        challengeKeyId = null;
        XDocument challenge;

        try
        {
            challenge = XDocument.Parse(body);
        }
        catch
        {
            return false;
        }

        var ns = new XmlNamespaceManager(new NameTable());
        ns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        ns.AddNamespace("protocols", "http://schemas.microsoft.com/DRM/2007/03/protocols");
        ns.AddNamespace("messages", "http://schemas.microsoft.com/DRM/2007/03/protocols/messages");
        ns.AddNamespace("PlayReadyHeader", "http://schemas.microsoft.com/DRM/2007/03/PlayReadyHeader");

        var challengeKeyIdText = challenge.XPathSelectElement(
            "/soap:Envelope/soap:Body/protocols:AcquireLicense/protocols:challenge" +
            "/messages:Challenge/protocols:LA/protocols:ContentHeader" +
            "/PlayReadyHeader:WRMHEADER/PlayReadyHeader:DATA/PlayReadyHeader:KID",
            ns)?.Value;

        if (challengeKeyIdText == null)
        {
            return false;
        }

        var guidBytes = new byte[16];

        if (!Convert.TryFromBase64String(challengeKeyIdText, guidBytes, out int bytesWritten) || bytesWritten != 16)
        {
            return false;
        }

        challengeKeyId = new Guid(guidBytes).ToString();

        return true;
    }
}

public static class KeyDeliveryClientExtensions
{
    public static IServiceCollection AddKeyDeliveryClient(this IServiceCollection services, Action<KeyDeliveryClientOptions> setupAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions();
        services.Configure(setupAction);
        services.Add(ServiceDescriptor.Scoped<KeyDeliveryClient, KeyDeliveryClient>());

        return services;
    }
}

