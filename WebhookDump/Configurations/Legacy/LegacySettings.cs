namespace Shoko.Plugin.WebhookDump.Configurations.Legacy;

public readonly struct LegacySettings
{
  public ShokoSettings Shoko { get; init; }
  public WebhookSettings Webhook { get; init; }

  public readonly struct ShokoSettings
  {
    public int? PublicPort { get; init; }
    public string PublicUrl { get; init; }
    public AutomaticMatchSettings AutomaticMatch { get; init; }

    public readonly struct AutomaticMatchSettings
    {
      public bool Enabled { get; init; }
      public bool WatchReactions { get; init; }
      public int MaxAttempts { get; init; }
    }
  }

  public readonly struct WebhookSettings
  {
    public bool Enabled { get; init; }
    public string Url { get; init; }
    public string Username { get; init; }
    public string AvatarUrl { get; init; }

    public WebhookMessageSettings Matched { get; init; }
    public WebhookMessageSettings Unmatched { get; init; }
    public WebhookRestrictionSettings Restrictions { get; init; }

    public readonly struct WebhookMessageSettings
    {
      public string? MessageText { get; init; }
      public string? EmbedText { get; init; }
      public string EmbedColor { get; init; }
    }

    public readonly struct WebhookRestrictionSettings
    {
      public bool ShowRestrictedTitles { get; init; }
      public bool PostIfTopMatchRestricted { get; init; }
    }
  }
}
