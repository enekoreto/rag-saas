using Microsoft.AspNetCore.Mvc;
using RAGService.Models;
using RAGService.Services;

namespace RAGService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueriesController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IPineconeService _pineconeService;

    public QueriesController(IEmbeddingService embeddingService, IPineconeService pineconeService)
    {
        _embeddingService = embeddingService;
        _pineconeService = pineconeService;
    }

    [HttpPost("ask")]
    [ProducesResponseType(typeof(AskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Ask([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var embeddings = await _embeddingService.GetEmbeddingsAsync(new[] { request.Question }, cancellationToken);
        if (embeddings.Count == 0)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Embedding provider did not return a question vector.");
        }

        var questionEmbedding = embeddings[0];
        var similarChunks = await _pineconeService.QueryAsync(questionEmbedding, cancellationToken: cancellationToken);

        if (similarChunks.Count == 0)
        {
            return Ok(new AskResponse("Not found in the document."));
        }

        var context = string.Join("\n", similarChunks);
        var prompt =
            "You are a helpful assistant answering questions about the content below. " +
            "Use only the provided context to answer. If the context does not answer the question, respond exactly with: Not found in the document.\n\n" +
            $"Context:\n{context}\n\nQuestion: {request.Question}";

        var answer = await _embeddingService.GetChatCompletionAsync(prompt, cancellationToken);
        return Ok(new AskResponse(answer));
    }
}
