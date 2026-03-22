using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RAGService.Models;
using RAGService.Settings;

namespace RAGService.Services;

public class PineconeService : IPineconeService
{
    private readonly HttpClient _httpClient;
    private readonly PineconeOptions _options;

    public PineconeService(IHttpClientFactory httpClientFactory, IOptions<PineconeOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient(PineconeOptions.HttpClientName);
        _options = options.Value;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public async Task UpsertVectorsAsync(IReadOnlyList<PineconeVector> vectors, CancellationToken cancellationToken = default)
    {
        if (vectors.Count == 0)
        {
            throw new ArgumentException("At least one vector is required.", nameof(vectors));
        }

        var payload = new
        {
            vectors = vectors.Select(vector => new
            {
                id = vector.Id,
                values = vector.Values,
                metadata = vector.Metadata
            })
        };

        using var request = CreateJsonRequest("vectors/upsert", payload);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Pinecone upsert failed: {response.StatusCode}. Body: {responseBody}");
        }
    }

    public async Task<IReadOnlyList<string>> QueryAsync(
        IReadOnlyList<float> embedding,
        int? topK = null,
        CancellationToken cancellationToken = default)
    {
        if (embedding.Count == 0)
        {
            throw new ArgumentException("Embedding vector cannot be empty.", nameof(embedding));
        }

        var payload = new
        {
            vector = embedding,
            topK = topK ?? _options.DefaultTopK,
            includeMetadata = true
        };

        using var request = CreateJsonRequest("query", payload);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Pinecone query failed: {response.StatusCode}. Body: {responseBody}");
        }

        using var jsonDocument = JsonDocument.Parse(responseBody);

        if (!jsonDocument.RootElement.TryGetProperty("matches", out var matchesElement) || matchesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var chunks = new List<string>();
        foreach (var match in matchesElement.EnumerateArray())
        {
            if (!match.TryGetProperty("metadata", out var metadataElement) || metadataElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!metadataElement.TryGetProperty("chunk", out var chunkElement))
            {
                continue;
            }

            var chunk = chunkElement.GetString();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private HttpRequestMessage CreateJsonRequest(string path, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Api-Key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return request;
    }
}
