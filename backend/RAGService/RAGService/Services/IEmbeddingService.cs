namespace RAGService.Services;

public interface IEmbeddingService
{
    Task<IReadOnlyList<IReadOnlyList<float>>> GetEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);

    Task<string> GetChatCompletionAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
