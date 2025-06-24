// Refactored to use the actual LiteBus NuGet package.
// This example demonstrates a real-world setup with Dependency Injection.
//
// REQUIRED NUGET PACKAGES:
#:package LiteBus
#:package LiteBus.Extensions.MicrosoftDependencyInjection
#:package Microsoft.Extensions.Hosting@10.0.0-preview.5.25277.114
#:package Microsoft.Extensions.Hosting.Abstractions@10.0.0-preview.5.25277.114

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LiteBus.Commands.Abstractions;
using LiteBus.Events.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CqrsWithLiteBus
{
    // --- 1. Message Definitions (Implementing LiteBus Interfaces) ---
    public class SendMessageCommand : ICommand
    {
        public string SenderName { get; }
        public string Message { get; }
        public SendMessageCommand(string senderName, string message)
        {
            SenderName = senderName;
            Message = message;
        }
    }

    public class NewMessagePublishedEvent : IEvent
    {
        public string Author { get; }
        public string Content { get; }
        public NewMessagePublishedEvent(string author, string content)
        {
            Author = author;
            Content = content;
        }
    }

    // --- 2. Concrete Handlers (Implementing Async LiteBus Interfaces) ---

    // Command Handler
    public class SendMessageCommandHandler : LiteBus.Commands.Abstractions.ICommandHandler<SendMessageCommand>
    {
        private readonly IEventPublisher _eventPublisher;
        private readonly Dictionary<string, User> _userRepository;

        // Dependencies are now injected by the .NET Service Provider
        public SendMessageCommandHandler(IEventPublisher eventPublisher, Dictionary<string, User> userRepository)
        {
            _eventPublisher = eventPublisher;
            _userRepository = userRepository;
        }

        public async Task HandleAsync(SendMessageCommand command, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[CommandHandler]: Processing command from {command.SenderName}.");
            Console.ResetColor();

            // Broadcast to other users (this part remains synchronous for the example)
            foreach (var user in _userRepository.Values.Where(u => u.Name != command.SenderName))
            {
                user.Receive(command.Message, command.SenderName);
            }

            // Publish an event for other parts of the system to react to
            await _eventPublisher.PublishAsync(new NewMessagePublishedEvent(command.SenderName, command.Message), cancellationToken);
        }
    }

    // Event Handlers
    public class PushNotificationEventHandler : LiteBus.Events.Abstractions.IEventHandler<NewMessagePublishedEvent>
    {
        public Task HandleAsync(NewMessagePublishedEvent @event, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[PushNotifier]: Sending push for message from '{@event.Author}'.");
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }

    public class ArchivingEventHandler : LiteBus.Events.Abstractions.IEventHandler<NewMessagePublishedEvent>
    {
        public Task HandleAsync(NewMessagePublishedEvent @event, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"[Archiver]: Storing message '{@event.Content}' in the archive.");
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }

    // --- 3. The User ---
    // The user now depends on LiteBus's specific interfaces for sending/publishing.
    public class User
    {
        private readonly ICommandMediator _commandMediator;
        public string Name { get; }

        public User(string name, ICommandMediator commandMediator)
        {
            Name = name;
            _commandMediator = commandMediator;
        }

        public async Task SendMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{this.Name} -> Bus]: Dispatching SendMessageCommand.");
            Console.ResetColor();
            await _commandMediator.SendAsync(new SendMessageCommand(this.Name, message));
        }

        public void Receive(string message, string from)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{this.Name}] received from [{from}]: '{message}'");
            Console.ResetColor();
        }
    }

    // --- 4. The Client (Main Program with real DI Setup) ---
    public class Program
    {
        public static async Task Main(string[] args)
        {

            await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register the central service as a Singleton so everyone shares the same instance.
        services.AddSingleton<StatusService>();

        // Register the worker as a Hosted Service so the host starts and stops it.
        services.AddHostedService<DataProcessingService>();

        services.AddHostedService<MyCronJobService>();
    })
    .Build()
    .RunAsync();

            var builder = Host.CreateDefaultBuilder(args);

            builder.Services.AddSingleton(new Dictionary<string, User>());
            builder.Services.AddLiteBus(bus =>
            {
                bus.AddInMemoryBus();
                bus.RegisterFromAssembly(Assembly.GetExecutingAssembly());
            });

            using var host = builder.Build();

            Console.WriteLine("--- Mediator with real LiteBus NuGet Package ---");

            var commandMediator = host.Services.GetRequiredService<ICommandMediator>();
            var userRepository = host.Services.GetRequiredService<Dictionary<string, User>>();

            var alice = new User("Alice", commandMediator);
            var bob = new User("Bob", commandMediator);
            userRepository[alice.Name] = alice;
            userRepository[bob.Name] = bob;

            Console.WriteLine("\n--- Communication Starts ---\n");

            await alice.SendMessage("Hey Bob, let's ship this feature with LiteBus!");

            Console.WriteLine("\n--- End of Demonstration ---");

            await host.StopAsync();
        }
    }
}
