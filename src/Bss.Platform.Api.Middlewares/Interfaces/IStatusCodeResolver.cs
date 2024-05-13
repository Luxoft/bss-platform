using System.Net;

namespace Bss.Platform.Api.Middlewares.Interfaces;

public interface IStatusCodeResolver
{
    HttpStatusCode Resolve(Exception exception);
}
