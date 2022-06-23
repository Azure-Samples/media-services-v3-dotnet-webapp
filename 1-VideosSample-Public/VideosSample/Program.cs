//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System.Text.Json;

var videos = JsonSerializer.Deserialize<Video[]>(File.ReadAllText("../index.json"))!;

var app = WebApplication.CreateBuilder(args).Build();

app.UseHttpsRedirection();

app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new[] { "browse.html" } });

app.UseStaticFiles();

app.MapGet("/videos", () => videos);

app.MapGet("/videos/{id}", (string id) => videos.SingleOrDefault(v => v.VideoId == id) is Video video
    ? Results.Json(video)
    : Results.NotFound());

app.Run();

internal record Video(string VideoId, string Title, string Locator, string? Thumbnail, ICollection<string> Viewers, ICollection<string> ContentKeyIds);
