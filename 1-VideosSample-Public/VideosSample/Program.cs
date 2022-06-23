using System.Text.Json;

var videos = JsonSerializer.Deserialize<Video[]>(File.ReadAllText("../index.json"))!;

var app = WebApplication.CreateBuilder(args).Build();

app.UseHttpsRedirection();

app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new[] { "browse.html" } });

app.UseStaticFiles();

app.MapGet("/videos", () => videos);

app.MapGet("/videos/{id:int}", (int id) => videos.SingleOrDefault(v => v.Id == id) is Video video
    ? Results.Json(video)
    : Results.NotFound());

app.Run();

internal record Video(int Id, string Title, string Locator, string Thumbnail);
