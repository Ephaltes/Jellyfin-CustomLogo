using System.Threading;
using System.Threading.Tasks;

namespace CustomLogo;

public interface ILogoCopyService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}