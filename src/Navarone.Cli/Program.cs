using Navarone.Cli;
using Navarone.Engine.Events;

var events = new NavaroneEventHandler(NavaroneEventHandler.DefaultFolder(), TimeProvider.System, Console.Error);
return await Commands.RunAsync(args, Directory.GetCurrentDirectory(), events, Wiring.For, Console.Out, Console.Error).ConfigureAwait(false);
