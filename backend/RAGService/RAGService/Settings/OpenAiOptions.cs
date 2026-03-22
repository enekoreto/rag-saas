using System.ComponentModel.DataAnnotations;

namespace RAGService.Settings;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public const string HttpClientName = "OpenAI";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";

    [Required]
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";

    [Required]
    public string ChatModel { get; init; } = "gpt-4o-mini";
}
