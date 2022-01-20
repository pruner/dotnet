using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Pruner.Instrumenter;
using Pruner.Instrumenter.Handlers;

var initialColor = Console.ForegroundColor;
        
try
{
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddMiniCoverCore();
    serviceCollection.AddLogging();

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

if (Debugger.IsAttached)
    Console.ReadLine();

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
    private readonly IFileSystem fileSystem;
    private readonly IEnumerable<ICommandHandler> commandHandlers;

    public Instrumenter(
        IFileSystem fileSystem,
        IEnumerable<ICommandHandler> commandHandlers)
    {
        this.fileSystem = fileSystem;
        this.commandHandlers = commandHandlers;
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
        var temporarySettingsDirectory = fileSystem.DirectoryInfo.FromDirectoryName(
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

        var providerWorkingDirectoryPath = fileSystem.DirectoryInfo.FromDirectoryName(Environment.CurrentDirectory);

        var handler = commandHandlers.Single(x => x.CanHandle(command));
        Console.WriteLine($"Executing handler for command {command} in paths ({providerWorkingDirectoryPath}, {temporarySettingsDirectory}).");
            
        await handler.HandleAsync(
            providerWorkingDirectoryPath,
            temporarySettingsDirectory);
    }
}