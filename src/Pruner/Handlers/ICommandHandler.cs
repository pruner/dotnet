using System.IO.Abstractions;
using System.Threading.Tasks;

namespace Pruner.Instrumenter.Handlers
{
    public interface ICommandHandler
    {
        bool CanHandle(Command command);

        Task HandleAsync(
            IDirectoryInfo workingDirectory,
            IDirectoryInfo settingsDirectory);
    }
}