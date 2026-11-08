namespace CyberpunkDb.Domain.Entities;

// bytes live on disk (Media:Root), named by Id
public class MediaAsset
{
    public Guid Id { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime CreatedAt { get; set; }
}
