namespace RAGService.Utils;

public static class ChunkHelper
{
    public static List<string> ChunkText(string text, int maxTokens = 400, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        if (maxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "maxTokens must be greater than zero.");
        }

        if (overlap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overlap), "overlap cannot be negative.");
        }

        var effectiveOverlap = Math.Min(overlap, maxTokens - 1);

        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentTokens = new List<string>();
        var tokenCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                currentTokens.Add(word);
                tokenCount += EstimateTokenCount(word + " ");

                if (tokenCount < maxTokens)
                {
                    continue;
                }

                chunks.Add(string.Join(" ", currentTokens));

                currentTokens = currentTokens
                    .Skip(Math.Max(0, currentTokens.Count - effectiveOverlap))
                    .ToList();

                tokenCount = EstimateTokenCount(string.Join(" ", currentTokens));
            }
        }

        if (currentTokens.Count > 0)
        {
            chunks.Add(string.Join(" ", currentTokens));
        }

        return chunks;
    }

    // Rough estimate: 1 token ~= 4 characters.
    private static int EstimateTokenCount(string text)
    {
        return text.Length / 4;
    }
}
