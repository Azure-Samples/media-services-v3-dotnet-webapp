//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

internal class Options
{
    public string TenantId { get; set; } = null!;

    public string TenantDomain { get; set; } = null!;

    public string ApplicationDisplayName { get; set; } = null!;

    public string ApplicationName { get; set; } = null!;

    public string RedirectUri { get; set; } = "https://localhost:7150/";

    public bool Valid()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
        {
            Console.WriteLine("TenantId must be set.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TenantDomain))
        {
            Console.WriteLine("TenantDomain must be set.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApplicationDisplayName))
        {
            Console.WriteLine("ApplicationDisplayName must be set.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApplicationName))
        {
            Console.WriteLine("ApplicationName must be set.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(RedirectUri))
        {
            Console.WriteLine("RedirectUri must be set.");
            return false;
        }


        return true;
    }
}
