using RAGService.Models;

namespace RAGService.Services;

public interface IPineconeService
{
    Task UpsertVectorsAsync(
        IReadOnlyList<PineconeVector> vectors,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> QueryAsync(
        IReadOnlyList<float> embedding,
        int? topK = null,
        CancellationToken cancellationToken = default);
}
