using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Pruner.Instrumenter;
using Pruner.Instrumenter.Handlers;

var initialColor = Console.ForegroundColor;
        
try
{
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddMiniCoverCore();
    serviceCollection.AddLogging(x => x
        .AddJsonConsole()
        .SetMinimumLevel(LogLevel.Trace));

    serviceCollection.AddTransient<IFileSystem, FileSystem>();
    serviceCollection.AddTransient<Instrumenter>();

    serviceCollection.AddTransient<ICommandHandler, InstrumentCommandHandler>();
    serviceCollection.AddTransient<ICommandHandler, CollectCommandHandler>();

    await using var serviceProvider = serviceCollection.BuildServiceProvider();

    var settingsId = args.FirstOrDefault();
    if (settingsId == null)
        throw new InvalidOperationException("No settings ID provided.");

    var commandString =
        args.Skip(1).FirstOrDefault() ??
        string.Empty;
    if (!Enum.TryParse<Command>(commandString, out var command))
        throw new InvalidOperationException("Invalid command specified.");

    var program = serviceProvider.GetRequiredService<Instrumenter>();
    await program.RunAsync(
        settingsId,
        command);
}
catch (ValidationException ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    await Console.Error.WriteLineAsync(ex.Message);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    await Console.Error.WriteLineAsync(ex.ToString());
}
finally
{
    Console.ForegroundColor = initialColor;
}

namespace Pruner.Instrumenter
{
    class PrunerSettings
    {
        public TestProvider[]? Providers { get; set; }
    }

    public class TestProvider
    {
        public string WorkingDirectory { get; set; } = null!;
        public string Id { get; set; } = null!;
    }

    public enum Command
    {
        Instrument,
        Collect
    }
    
    class Instrumenter
    {
        private readonly IFileSystem _fileSystem;
        private readonly IEnumerable<ICommandHandler> _commandHandlers;
        private readonly ILogger<Instrumenter> _logger;

        public Instrumenter(
            IFileSystem fileSystem,
            IEnumerable<ICommandHandler> commandHandlers,
            ILogger<Instrumenter> logger)
        {
            _fileSystem = fileSystem;
            _commandHandlers = commandHandlers;
            _logger = logger;
        }

        public async Task RunAsync(
            string settingsId,
            Command command)
        {
            if (string.IsNullOrWhiteSpace(settingsId))
                throw new ValidationException("Settings ID not specified.");

            var prunerPath = Path.Combine(
                Environment.CurrentDirectory,
                ".pruner");
            var temporarySettingsDirectory = _fileSystem.DirectoryInfo.FromDirectoryName(
                Path.Combine(
                    prunerPath,
                    "temp",
                    settingsId));

            var settingsContents = await File.ReadAllTextAsync(Path.Combine(
                prunerPath,
                "settings.json"));
            var settings = JsonConvert.DeserializeObject<PrunerSettings>(settingsContents);
            
            var dotnetSettings = settings?.Providers?.SingleOrDefault(x => x.Id == settingsId);
            if (dotnetSettings == null)
                throw new ValidationException($"The Pruner settings file did not contain a settings object for ID {settingsId}");

            var handler = _commandHandlers.Single(x => x.CanHandle(command));
            _logger.LogInformation(
                "Executing handler for command {Command} with settings path {TemporarySettingsDirectory}",
                command,
                temporarySettingsDirectory);
            
            await handler.HandleAsync(
                dotnetSettings,
                temporarySettingsDirectory);
        }
    }
}