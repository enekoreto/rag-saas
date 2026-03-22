using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RAGService.Settings;

namespace RAGService.Services;

public class EmbeddingService : IEmbeddingService
{
    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(response =>
            response.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public EmbeddingService(IHttpClientFactory httpClientFactory, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient(OpenAiOptions.HttpClientName);
        _options = options.Value;
    }

    public async Task<IReadOnlyList<IReadOnlyList<float>>> GetEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        var sanitizedInputs = inputs
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .ToArray();

        if (sanitizedInputs.Length == 0)
        {
            throw new ArgumentException("At least one non-empty input is required.", nameof(inputs));
        }

        var requestBody = new
        {
            input = sanitizedInputs,
            model = _options.EmbeddingModel
        };

        using var response = await RetryPolicy.ExecuteAsync(async token =>
        {
            using var request = CreateJsonRequest("embeddings", requestBody);
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI embeddings request failed: {response.StatusCode}. Body: {responseBody}");
        }

        using var jsonDocument = JsonDocument.Parse(responseBody);
        if (!jsonDocument.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OpenAI embeddings response did not contain a valid 'data' array.");
        }

        var embeddings = new List<IReadOnlyList<float>>();

        foreach (var item in dataElement.EnumerateArray())
        {
            if (!item.TryGetProperty("embedding", out var embeddingElement) || embeddingElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("OpenAI embeddings response contained an item without an 'embedding' array.");
            }

            embeddings.Add(embeddingElement.EnumerateArray().Select(value => value.GetSingle()).ToList());
        }

        return embeddings;
    }

    public async Task<string> GetChatCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        var requestBody = new
        {
            model = _options.ChatModel,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = prompt }
            }
        };

        using var response = await RetryPolicy.ExecuteAsync(async token =>
        {
            using var request = CreateJsonRequest("chat/completions", requestBody);
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI chat completion request failed: {response.StatusCode}. Body: {responseBody}");
        }

        using var jsonDocument = JsonDocument.Parse(responseBody);

        var answer = jsonDocument.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return answer?.Trim() ?? string.Empty;
    }

    private HttpRequestMessage CreateJsonRequest(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }
}
