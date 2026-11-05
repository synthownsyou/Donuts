using System.Threading;

namespace Donuts.Compat;

public interface ICancellable
{
    CancellationToken GetCancelToken();
}
