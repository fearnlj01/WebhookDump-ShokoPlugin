using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.WebhookDump.Services;

namespace Shoko.Plugin.WebhookDump.Extensions;

public static class ServiceCollectionExtensions
{
  extension(IServiceCollection services)
  {
    public IServiceCollection AddInitializedSingleton<T>()
      where T : class, IInitializable
    {
      services.AddSingleton<T>();
      services.AddSingleton<IInitializable>(sp => sp.GetRequiredService<T>());
      return services;
    }
  }
}
