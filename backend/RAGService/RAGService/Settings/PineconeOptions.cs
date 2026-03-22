using System.ComponentModel.DataAnnotations;

namespace RAGService.Settings;

public sealed class PineconeOptions
{
    public const string SectionName = "Pinecone";
    public const string HttpClientName = "Pinecone";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    [Range(1, 100)]
    public int DefaultTopK { get; init; } = 10;
}
