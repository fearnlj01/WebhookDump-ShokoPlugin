namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class Message
{
  private Message()
  {
  }

  public string? Username { get; private set; }
  public string? AvatarUrl { get; private set; }
  public List<Attachment>? Attachments { get; private set; }
  public string? Content { get; private set; }
  public List<Embed>? Embeds { get; private set; }

  private void Validate()
  {
    if (Content == null && (Embeds == null || Embeds.Count == 0))
      throw new InvalidOperationException("Either the `Content` or `Embeds` list must contain a non-null value.");
    if (Embeds is { Count: > 10 })
      throw new InvalidOperationException("A message must not contain more than 10 embeds.");
  }

  public static Builder Create()
  {
    return new Builder();
  }

  public class Builder
  {
    private readonly Message _message = new();

    public Builder SetUsername(string? username)
    {
      if (!string.IsNullOrEmpty(username)) _message.Username = username;
      return this;
    }

    public Builder SetAvatarUrl(string? avatarUrl)
    {
      if (!string.IsNullOrEmpty(avatarUrl)) _message.AvatarUrl = avatarUrl;
      return this;
    }

    public Builder AddAttachment(Attachment attachment)
    {
      _message.Attachments ??= [];
      _message.Attachments.Add(attachment);
      return this;
    }

    public Builder SetAttachments(List<Attachment>? attachments)
    {
      _message.Attachments = attachments;
      return this;
    }

    public Builder SetContent(string? content)
    {
      if (!string.IsNullOrEmpty(content)) _message.Content = content;
      return this;
    }

    public Builder SetEmbeds(IEnumerable<Embed>? embeds)
    {
      _message.Embeds = embeds?.ToList();
      return this;
    }

    public Builder SetEmbeds(Embed embeds)
    {
      _message.Embeds = [embeds];
      return this;
    }

    public Message Build()
    {
      _message.Validate();
      return _message;
    }
  }
}
