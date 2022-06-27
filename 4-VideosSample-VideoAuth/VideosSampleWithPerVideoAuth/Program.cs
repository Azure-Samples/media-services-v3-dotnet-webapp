//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

var videos = JsonSerializer.Deserialize<Video[]>(File.ReadAllText("../index.json"))!;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration)
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization(config =>
{
    config.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireScope("Videos.Watch")
        .Build();
});

builder.Services.AddKeyDeliveryClient(options => builder.Configuration.GetSection("KeyDelivery").Bind(options));
builder.Services.AddScoped<VideoAuthorizationFactory>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new[] { "browse.html" } });

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/videos", async (VideoAuthorizationFactory videoAuthorizationFactory) => {
    var videoAuthorizationValidator = await videoAuthorizationFactory.CreateValidatorAsync();
    return videos.Where(videoAuthorizationValidator.CanView);
});

app.MapGet("/videos/{videoId}", async (VideoAuthorizationFactory videoAuthorizationFactory, string videoId) =>
{
    var video = videos.SingleOrDefault(v => v.VideoId == videoId);

    var videoAuthorizationValidator = await videoAuthorizationFactory.CreateValidatorAsync();

    return videoAuthorizationValidator.CanView(video)
        ? Results.Json(video)
        : Results.Unauthorized();
});

app.MapPost("/envelopeKey", async (VideoAuthorizationFactory videoAuthorizationFactory, KeyDeliveryClient keyDeliveryClient, string videoId, string contentKeyId) =>
{
    var video = videos.SingleOrDefault(v => v.VideoId == videoId);

    var videoAuthorizationValidator = await videoAuthorizationFactory.CreateValidatorAsync();

    if (!videoAuthorizationValidator.CanView(video))
    {
        return Results.Unauthorized();
    }

    if (!video.ContentKeyIds.Contains(contentKeyId))
    {
        return Results.Unauthorized();
    }

    return await keyDeliveryClient.GetEnvelopeKeyAsync(contentKeyId);
});

app.MapPost("/playReadyKey", async (VideoAuthorizationFactory videoAuthorizationFactory, KeyDeliveryClient keyDeliveryClient, string videoId, HttpContext context) =>
{
    var video = videos.SingleOrDefault(v => v.VideoId == videoId);

    var videoAuthorizationValidator = await videoAuthorizationFactory.CreateValidatorAsync();

    if (!videoAuthorizationValidator.CanView(video))
    {
        return Results.Unauthorized();
    }

    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();

    if (!KeyDeliveryClient.TryGetPlayReadyContentKeyId(body, out string? challengeKeyId))
    {
        return Results.BadRequest();
    }

    if (!video.ContentKeyIds.Contains(challengeKeyId))
    {
        return Results.Unauthorized();
    }

    return await keyDeliveryClient.GetPlayReadyKeyAsync(body);
});

app.MapPost("/widevineKey", async (VideoAuthorizationFactory videoAuthorizationFactory, KeyDeliveryClient keyDeliveryClient, string videoId, string contentKeyId, HttpContext context) =>
{
    var video = videos.SingleOrDefault(v => v.VideoId == videoId);

    var videoAuthorizationValidator = await videoAuthorizationFactory.CreateValidatorAsync();

    if (!videoAuthorizationValidator.CanView(video))
    {
        return Results.Unauthorized();
    }

    if (!video.ContentKeyIds.Contains(contentKeyId))
    {
        return Results.Unauthorized();
    }

    using var memoryStream = new MemoryStream();
    await context.Request.Body.CopyToAsync(memoryStream);
    var challenge = memoryStream.ToArray();

    return await keyDeliveryClient.GetWidevineKeyAsync(contentKeyId, challenge);
});

app.Run();

internal record Video(string VideoId, string Title, string Locator, string? Thumbnail, ICollection<string> Viewers, ICollection<string> ContentKeyIds);

class VideoAuthorizationValidator
{
    private readonly HashSet<string> _userObjectIds;

    public VideoAuthorizationValidator(HashSet<string> userObjectIds) => _userObjectIds = userObjectIds;

    public bool CanView([NotNullWhen(returnValue: true)] Video? video) =>
        video != null &&
        (video.Viewers.Contains("all") || _userObjectIds.Overlaps(video.Viewers));
}

class VideoAuthorizationFactory
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly Microsoft.Graph.GraphServiceClient _graphServiceClient;

    public VideoAuthorizationFactory(IHttpContextAccessor contextAccessor, Microsoft.Graph.GraphServiceClient graphServiceClient)
    {
        _contextAccessor = contextAccessor;
        _graphServiceClient = graphServiceClient;
    }

    public async Task<VideoAuthorizationValidator> CreateValidatorAsync()
    {
        var user = _contextAccessor?.HttpContext?.User.GetObjectId();

        if (user == null)
        {
            throw new Exception("No user.");
        }

        var userObjectIds = new HashSet<string> { user };

        var groups = await _graphServiceClient
            .Me
            .GetMemberGroups(securityEnabledOnly: true)
            .Request()
            .PostAsync();

        await Microsoft.Graph.PageIterator<string>
            .CreatePageIterator(
                _graphServiceClient,
                groups,
                group =>
                {
                    userObjectIds.Add(group);
                    return true;
                })
            .IterateAsync();

        return new VideoAuthorizationValidator(userObjectIds);
    }
}
