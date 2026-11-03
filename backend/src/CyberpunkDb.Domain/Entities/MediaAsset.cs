namespace CyberpunkDb.Domain.Entities;

/// <summary>An uploaded map or token image. The bytes live on disk (Media:Root), named by <see cref="Id"/>.</summary>
public class MediaAsset
{
    public Guid Id { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime CreatedAt { get; set; }
}
