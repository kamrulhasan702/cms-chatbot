using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using ManualChatbot.Models;
using ManualChatbot.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var importPosition = Array.IndexOf(args, "--import-pdf");
var buildVectors = args.Contains("--build-embeddings");
if (importPosition >= 0 && importPosition + 1 >= args.Length) throw new ArgumentException("Provide a PDF path after --import-pdf.");
var webArgs = args.Where((arg, i) => arg != "--build-embeddings" && i != importPosition && (importPosition < 0 || i != importPosition + 1)).ToArray();
var builder = WebApplication.CreateBuilder(webArgs);
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.Configure<KnowledgeOptions>(builder.Configuration.GetSection("Knowledge"));
builder.Services.AddSingleton<KnowledgeStore>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<FigureService>();
builder.Services.AddHttpClient<IAiService, AiService>(client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddScoped<ChatService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 5 * 1024 * 1024);
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) => await context.HttpContext.Response.WriteAsJsonAsync(
        new { error = "rate_limited", message = "Too many requests. Wait a moment, then retry." }, ct);
    options.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = Math.Clamp(builder.Configuration.GetValue<int?>("Chat:PermitLimit") ?? 20, 1, 100), Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));

});
var app = builder.Build();
var store = app.Services.GetRequiredService<KnowledgeStore>();
var knowledge = app.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
if (importPosition >= 0) {
    await PdfImportService.ImportAsync(Path.GetFullPath(args[importPosition + 1]), store.AbsolutePath(knowledge.SourcePath));
    return;
}
await store.LoadAsync();
await app.Services.GetRequiredService<FigureService>().LoadAsync();
var aiOptions = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;
var search = app.Services.GetRequiredService<SearchService>();
if (buildVectors) {
    var ai = app.Services.GetRequiredService<IAiService>();
    var vectors = new Dictionary<string, float[]>();
    foreach (var batch in store.Chunks.Chunk(16)) {
        var values = await ai.EmbedAsync(batch.Select(c => c.Text).ToArray(), CancellationToken.None);
        for (var i = 0; i < batch.Length; i++) vectors[batch[i].Id] = values[i];
        Console.WriteLine($"Indexed {vectors.Count}/{store.Chunks.Count} chunks");
    }
    var path = store.AbsolutePath(knowledge.EmbeddingCachePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(new EmbeddingCache(store.CorpusHash, aiOptions.EmbeddingModel, vectors), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    File.Move(path + ".tmp", path, true);
    Console.WriteLine("Embedding index saved. Set Ai:UseEmbeddings=true and start the application.");
    return;
}
if (aiOptions.UseEmbeddings) await search.LoadVectorsAsync();
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/" ||
        context.Request.Path.Value?.EndsWith(".html", StringComparison.OrdinalIgnoreCase) == true ||
        context.Request.Path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true ||
        context.Request.Path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
    }
    try { await next(); }
    catch (AiException e) {
        context.Response.StatusCode = e.Status;
        await context.Response.WriteAsJsonAsync(new { error = e.Code, message = e.Message });
    }
    catch (ArgumentException e) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "invalid_request", message = e.Message });
    }
    catch (BadHttpRequestException e) {
        context.Response.StatusCode = e.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = "invalid_body", message = "Send a valid JSON request within the size limit." });
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception e) {
        app.Logger.LogError(e, "Request failed; content and API key are not logged.");
        if (!context.Response.HasStarted) {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "server_error", message = "The server could not complete the request. Please retry." });
        }
    }
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions {
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-store, max-age=0"
});
app.UseRouting();
app.UseRateLimiter();
app.MapGet("/api/status", (HttpContext context) => Results.Ok(new {
    ready = !string.IsNullOrWhiteSpace(aiOptions.ApiKey),
    needsSignIn = false,
    retrieval = aiOptions.UseEmbeddings && search.HasEmbeddings ? "hybrid" : "keyword",
    manualReady = store.Chunks.Count > 0
}));
app.MapGet("/api/version", () => Results.Ok(new { version = "illustrated-r9" }));
app.MapGet("/api/manual-images/{id}", (string id, FigureService figures) => {
    var file = figures.FileFor(id);
    return file is null ? (IResult)Results.NotFound() : Results.File(file, "image/webp");
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/chat", async (ChatRequest request, ChatService service, HttpContext context) => {
    if (request.History?.Count > 12) return Results.BadRequest(new { message = "Conversation history is too long. Start a new chat." });
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    deadline.CancelAfter(TimeSpan.FromSeconds(120));
    try { return Results.Ok(await service.ReplyAsync(request, deadline.Token)); }
    catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) {
        return Results.Json(new { error = "chat_timeout", message = "The answer took too long. Please retry." }, statusCode: 504);
    }
}).RequireRateLimiting("chat");
app.MapFallbackToFile("index.html");
app.Logger.LogInformation("UI release illustrated-r9. Project folder: {ContentRoot}", app.Environment.ContentRootPath);
await app.RunAsync();

public sealed record SignInRequest(string? Password);
public partial class Program { }
