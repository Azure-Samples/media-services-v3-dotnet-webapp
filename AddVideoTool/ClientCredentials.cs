//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using Azure.Core;
using Microsoft.Rest;
using System.Net.Http.Headers;

// Class to adapt TokenCredential to ServiceClientCredentials.
internal class ClientCredentials : ServiceClientCredentials
{
    private readonly TokenCredential _tokenCredential;
    private readonly TokenRequestContext _tokenRequestContext;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private AccessToken? _cachedToken;

    public ClientCredentials(TokenCredential tokenCredential, string scope = "https://management.core.windows.net/.default") =>
        (_tokenCredential, _tokenRequestContext) = (tokenCredential, new TokenRequestContext(new[] { scope }));

    public override async Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!TokenValid)
        {
            await _semaphore.WaitAsync(cancellationToken);

            if (!TokenValid)
            {
                try
                {
                    _cachedToken = await _tokenCredential.GetTokenAsync(_tokenRequestContext, cancellationToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken!.Value.Token);

        await base.ProcessHttpRequestAsync(request, cancellationToken);
    }

    private bool TokenValid => _cachedToken != null && _cachedToken.Value.ExpiresOn.AddMinutes(-5) > DateTime.UtcNow;
}
