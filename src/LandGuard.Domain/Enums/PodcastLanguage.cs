namespace LandGuard.Domain.Enums;

/// <summary>
/// Language of a fraud-awareness podcast episode (<c>CK_Podcast_Language</c>,
/// FR11/NFR06). LandGuardDB stores podcast titles/descriptions as
/// <c>NVARCHAR</c> specifically so Sinhala and Tamil content is stored
/// natively - this enum only labels which language a given episode is in.
/// </summary>
public enum PodcastLanguage
{
    English = 1,
    Sinhala = 2,
    Tamil = 3
}
