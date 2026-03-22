using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RAGService.Controllers;
using RAGService.Models;
using RAGService.Services;

namespace RAGService.Tests.Controllers;

public class QueriesControllerTests
{
    [Fact]
    public async Task Ask_ReturnsBadRequest_WhenQuestionIsEmpty()
    {
        var controller = CreateController();

        var result = await controller.Ask(new QueryRequest { Question = "" }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Question is required.", badRequest.Value);
    }

    [Fact]
    public async Task Ask_ReturnsFallbackAnswer_WhenNoMatchesAreFound()
    {
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(service => service.GetEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IReadOnlyList<float>> { new List<float> { 0.1f, 0.2f } });

        var pineconeService = new Mock<IPineconeService>();
        pineconeService
            .Setup(service => service.QueryAsync(It.IsAny<IReadOnlyList<float>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var controller = CreateController(embeddingService, pineconeService);

        var result = await controller.Ask(new QueryRequest { Question = "What is this about?" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AskResponse>(ok.Value);
        Assert.Equal("Not found in the document.", payload.Answer);

        embeddingService.Verify(
            service => service.GetChatCompletionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ask_ReturnsModelAnswer_WhenContextExists()
    {
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(service => service.GetEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IReadOnlyList<float>> { new List<float> { 0.1f, 0.2f } });
        embeddingService
            .Setup(service => service.GetChatCompletionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is the answer.");

        var pineconeService = new Mock<IPineconeService>();
        pineconeService
            .Setup(service => service.QueryAsync(It.IsAny<IReadOnlyList<float>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Relevant chunk" });

        var controller = CreateController(embeddingService, pineconeService);

        var result = await controller.Ask(new QueryRequest { Question = "What is this about?" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AskResponse>(ok.Value);
        Assert.Equal("This is the answer.", payload.Answer);

        embeddingService.Verify(
            service => service.GetChatCompletionAsync(It.Is<string>(prompt => prompt.Contains("Relevant chunk")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Ask_ReturnsBadGateway_WhenEmbeddingServiceReturnsNoVectors()
    {
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(service => service.GetEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IReadOnlyList<float>>());

        var controller = CreateController(embeddingService, new Mock<IPineconeService>());

        var result = await controller.Ask(new QueryRequest { Question = "Where is the info?" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    }

    private static QueriesController CreateController(
        Mock<IEmbeddingService>? embeddingService = null,
        Mock<IPineconeService>? pineconeService = null)
    {
        return new QueriesController(
            (embeddingService ?? new Mock<IEmbeddingService>()).Object,
            (pineconeService ?? new Mock<IPineconeService>()).Object);
    }
}
