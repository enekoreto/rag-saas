using System.ComponentModel.DataAnnotations;

namespace RAGService.Models;

public class QueryRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Question { get; set; } = string.Empty;
}
