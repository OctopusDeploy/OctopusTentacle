using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Communications
{
    public interface IMyEchoService
    {
        string SayHello(string name);
    }

    public interface IAsyncClientMyEchoService
    {
        Task<string> SayHelloAsync(string name);
    }

    public interface IAsyncMyEchoService
    {
        Task<string> SayHelloAsync(string name, CancellationToken cancellationToken);
    }
}