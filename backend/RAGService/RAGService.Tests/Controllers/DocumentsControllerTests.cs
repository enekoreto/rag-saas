using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RAGService.Controllers;
using RAGService.Models;
using RAGService.Services;

namespace RAGService.Tests.Controllers;

public class DocumentsControllerTests
{
    [Fact]
    public async Task UploadDocument_ReturnsBadRequest_WhenFileIsMissing()
    {
        var controller = CreateController();

        var result = await controller.UploadDocument(new DocumentUploadRequest { File = null }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded.", badRequest.Value);
    }

    [Fact]
    public async Task UploadDocument_ReturnsBadRequest_WhenFileTypeUnsupported()
    {
        var controller = CreateController();

        var request = new DocumentUploadRequest
        {
            File = CreateFormFile("hello", "doc.csv", "text/csv")
        };

        var result = await controller.UploadDocument(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Unsupported file type. Please upload a PDF or TXT file.", badRequest.Value);
    }

    [Fact]
    public async Task UploadDocument_ReturnsBadGateway_WhenEmbeddingCountDoesNotMatchChunks()
    {
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(service => service.GetEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IReadOnlyList<float>>());

        var pineconeService = new Mock<IPineconeService>();
        var controller = CreateController(embeddingService, pineconeService);

        var request = new DocumentUploadRequest
        {
            File = CreateFormFile("this is a text document", "doc.txt", "text/plain")
        };

        var result = await controller.UploadDocument(request, CancellationToken.None);

        var badGateway = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, badGateway.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_UpsertsVectorsAndReturnsOk_WhenRequestIsValid()
    {
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(service => service.GetEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> chunks, CancellationToken _) =>
                chunks.Select(_ => (IReadOnlyList<float>)new List<float> { 0.1f, 0.2f }).ToList());

        var pineconeService = new Mock<IPineconeService>();
        var controller = CreateController(embeddingService, pineconeService);

        var request = new DocumentUploadRequest
        {
            File = CreateFormFile("this is a valid plain text file", "doc.txt", "text/plain")
        };

        var result = await controller.UploadDocument(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<UploadDocumentResponse>(ok.Value);
        Assert.Equal("doc.txt", payload.FileName);
        Assert.Equal(payload.ChunkCount, payload.VectorCount);
        Assert.True(payload.ChunkCount > 0);

        pineconeService.Verify(
            service => service.UpsertVectorsAsync(
                It.Is<IReadOnlyList<PineconeVector>>(vectors => vectors.Count == payload.VectorCount),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static DocumentsController CreateController(
        Mock<IEmbeddingService>? embeddingService = null,
        Mock<IPineconeService>? pineconeService = null)
    {
        return new DocumentsController(
            (embeddingService ?? new Mock<IEmbeddingService>()).Object,
            (pineconeService ?? new Mock<IPineconeService>()).Object);
    }

    private static IFormFile CreateFormFile(string content, string fileName, string contentType)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return file;
    }
}
