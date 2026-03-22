using RAGService.Utils;

namespace RAGService.Tests.Utils;

public class ChunkHelperTests
{
    [Fact]
    public void ChunkText_ReturnsEmpty_WhenInputIsWhitespace()
    {
        var chunks = ChunkHelper.ChunkText("   \n\n  ");

        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkText_Throws_WhenMaxTokensIsInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChunkHelper.ChunkText("hello world", maxTokens: 0));
    }

    [Fact]
    public void ChunkText_SplitsLongTextIntoMultipleChunks()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 40));

        var chunks = ChunkHelper.ChunkText(text, maxTokens: 5, overlap: 1);

        Assert.True(chunks.Count > 1);
        Assert.Contains("word", chunks[0]);
        Assert.Contains("word", chunks[1]);
    }
}
