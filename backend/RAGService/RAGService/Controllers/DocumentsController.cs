using System.Text;
using Microsoft.AspNetCore.Mvc;
using RAGService.Models;
using RAGService.Services;
using RAGService.Utils;

namespace RAGService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;
    private const int PineconeBatchSize = 100;

    private readonly IEmbeddingService _embeddingService;
    private readonly IPineconeService _pineconeService;

    public DocumentsController(IEmbeddingService embeddingService, IPineconeService pineconeService)
    {
        _embeddingService = embeddingService;
        _pineconeService = pineconeService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (request.File.Length > MaxFileSizeBytes)
        {
            return BadRequest($"File is too large. Maximum size is {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".txt")
        {
            return BadRequest("Unsupported file type. Please upload a PDF or TXT file.");
        }

        using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        var text = extension == ".pdf"
            ? PdfManager.ExtractText(fileBytes)
            : Encoding.UTF8.GetString(fileBytes);

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("The uploaded file does not contain readable text.");
        }

        var chunks = ChunkHelper.ChunkText(text)
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
            .ToList();

        if (chunks.Count == 0)
        {
            return BadRequest("No chunks could be generated from the document.");
        }

        var embeddings = await _embeddingService.GetEmbeddingsAsync(chunks, cancellationToken);

        if (embeddings.Count != chunks.Count)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Embedding provider returned an invalid number of vectors.");
        }

        var vectors = new List<PineconeVector>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            vectors.Add(new PineconeVector(
                Id: $"{Guid.NewGuid()}-{i + 1}",
                Values: embeddings[i],
                Metadata: new Dictionary<string, object>
                {
                    ["chunk"] = chunks[i],
                    ["fileName"] = request.File.FileName,
                    ["chunkIndex"] = i + 1,
                    ["uploadedAtUtc"] = DateTime.UtcNow.ToString("O")
                }));
        }

        for (var i = 0; i < vectors.Count; i += PineconeBatchSize)
        {
            var batch = vectors.Skip(i).Take(PineconeBatchSize).ToList();
            await _pineconeService.UpsertVectorsAsync(batch, cancellationToken);
        }

        return Ok(new UploadDocumentResponse(request.File.FileName, chunks.Count, vectors.Count));
    }
}
