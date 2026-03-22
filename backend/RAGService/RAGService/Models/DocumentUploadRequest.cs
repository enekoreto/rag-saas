using System.ComponentModel.DataAnnotations;

namespace RAGService.Models;

public class DocumentUploadRequest
{
    [Required]
    public IFormFile? File { get; set; }
}
