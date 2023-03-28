namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookDumpField : IWebhookField
{
  private static AVDumpResult _AVDumpResult;
  public WebhookDumpField(AVDumpResult result)
  {
    _AVDumpResult = result;

    Name = "ED2K:";
    Value = _AVDumpResult?.Ed2k;
  }

  public string Name { get; }
  public string Value { get; }
  public bool Inline { get; }
}