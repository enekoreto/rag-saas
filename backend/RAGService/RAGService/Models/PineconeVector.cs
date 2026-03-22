namespace RAGService.Models;

public sealed record PineconeVector(
    string Id,
    IReadOnlyList<float> Values,
    Dictionary<string, object> Metadata);
