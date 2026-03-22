namespace RAGService.Models;

public sealed record UploadDocumentResponse(
    string FileName,
    int ChunkCount,
    int VectorCount);
