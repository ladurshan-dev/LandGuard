using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.Podcast</c> - multilingual fraud-awareness content
/// (FR11/NFR06). Insert goes through <c>usp_Podcast_Add</c> (admin-only,
/// enforced in T-SQL); read via <c>usp_Podcast_GetAll</c>.
/// </summary>
public class Podcast
{
    public int PodcastId { get; set; }

    public int AdminId { get; set; }

    public string Title { get; set; } = null!;

    public PodcastLanguage Language { get; set; }

    public string? Description { get; set; }

    public string AudioUrl { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public User Admin { get; set; } = null!;
}
