//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

internal class Options
{
    public string SubscriptionId { get; set; } = null!;

    public string ResourceGroup { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public string IndexFile { get; set; } = "index.json";

    public string Title { get; set; } = null!;

    public string Asset { get; set; } = null!;

    public string StreamingPolicy { get; set; } = null!;

    public string[]? Viewers { get; set; } = null;

    public string? SourceFile { get; set; }

    public string? SourceUrl { get; set; }

    public string? Transform { get; set; }

    public bool Valid()
    {
        if (Transform != null)
        {
            if (SourceFile == null && SourceUrl == null)
            {
                Console.WriteLine("When a transform is used, either SourceFile or SourceUrl must be set.");
                return false;
            }

            if (SourceFile != null && SourceUrl != null)
            {
                Console.WriteLine("Either SourceFile or SourceUrl must be set, but not both.");
                return false;
            }

            if (SourceFile != null && !File.Exists(SourceFile))
            {
                Console.WriteLine($"'{Path.GetFullPath(SourceFile)}' not found.");
                return false;
            }
        }

        if (Transform == null)
        {
            if (SourceFile != null || SourceUrl != null)
            {
                Console.WriteLine("Transform must be set to use SourceFile or SourceUrl.");
                return false;
            }
        }

        if (Asset == null)
        {
            Console.WriteLine("Asset must be set.");
            return false;
        }

        if (StreamingPolicy == null)
        {
            Console.WriteLine("StreamingPolicy must be set.");
            return false;
        }

        return true;
    }
}
