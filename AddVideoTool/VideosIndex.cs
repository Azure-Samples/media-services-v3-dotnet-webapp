//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System.Text.Json;

internal static class VideosIndex
{
    internal record Video(string VideoId, string Title, string Locator, string? Thumbnail, ICollection<string> Viewers, ICollection<string> ContentKeyIds);

    public static void AddVideo(string indexFile, string videoId, string title, string locator, string? thumbnail, ICollection<string> viewers, ICollection<string> contentKeyIds)
    {
        using var stream = new FileStream(indexFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        var videos = stream.Length > 0
            ? JsonSerializer.Deserialize<List<Video>>(stream)!
            : new List<Video>();

        videos.Add(new Video(
            videoId,
            title,
            locator,
            thumbnail,
            viewers,
            contentKeyIds));

        stream.SetLength(0);

        JsonSerializer.Serialize(stream, videos, new JsonSerializerOptions { WriteIndented = true });
    }
}
