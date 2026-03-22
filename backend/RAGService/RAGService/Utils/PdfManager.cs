using System.Text;
using UglyToad.PdfPig;

namespace RAGService.Utils;

public static class PdfManager
{
    public static string ExtractText(byte[] pdfBytes)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(pdfStream);

        var text = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            text.AppendLine(page.Text);
        }

        return text.ToString();
    }
}