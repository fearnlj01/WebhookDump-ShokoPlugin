namespace Shoko.Plugin.WebhookDump.Services;

public interface IInitializable
{
  Task InitializeAsync(CancellationToken cancellationToken = default);
}
