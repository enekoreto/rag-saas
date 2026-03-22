using Microsoft.Extensions.Options;
using RAGService.Services;
using RAGService.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services
    .AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection(OpenAiOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "OpenAI ApiKey is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<PineconeOptions>()
    .Bind(builder.Configuration.GetSection(PineconeOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "Pinecone ApiKey is required.")
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Pinecone BaseUrl must be a valid absolute URL.")
    .Validate(options => options.DefaultTopK > 0, "Pinecone DefaultTopK must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHttpClient(OpenAiOptions.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;

    if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }
});

builder.Services.AddHttpClient(PineconeOptions.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<PineconeOptions>>().Value;

    if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }
});

builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IPineconeService, PineconeService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
