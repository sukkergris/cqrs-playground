#:package LiteBus@2.0.0
#:package LiteBus.Commands@2.0.0
#:package LiteBus.Commands.Abstractions@2.0.0
#:package LiteBus.Commands.Extensions.MicrosoftDependencyInjection@2.0.0
#:package LiteBus.Events@2.0.0
#:package LiteBus.Events.Abstractions@2.0.0
#:package LiteBus.Events.Extensions.MicrosoftDependencyInjection@2.0.0
#:package LiteBus.Extensions.MicrosoftDependencyInjection@2.0.0
#:package LiteBus.Messaging@2.0.0
#:package LiteBus.Messaging.Abstractions@2.0.0
#:package LiteBus.Messaging.Extensions.MicrosoftDependencyInjection@2.0.0
#:package LiteBus.Queries@2.0.0
#:package LiteBus.Queries.Abstractions@2.0.0
#:package LiteBus.Queries.Extensions.MicrosoftDependencyInjection@2.0.0
#:package Microsoft.Extensions.DependencyInjection@10.0.0-preview.5.25277.114
#:package Microsoft.Extensions.Hosting@10.0.0-preview.5.25277.114
#:package Microsoft.Extensions.Hosting.Abstractions@10.0.0-preview.5.25277.114

using LiteBus.Commands.Abstractions;
using LiteBus.Commands.Extensions.MicrosoftDependencyInjection;
using LiteBus.Events.Abstractions;
using LiteBus.Events.Extensions.MicrosoftDependencyInjection;
using LiteBus.Queries.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LiteBus.Messaging.Extensions.MicrosoftDependencyInjection;
using LiteBus.Queries.Extensions.MicrosoftDependencyInjection;

namespace CqrsWithLiteBus.ConsoleService;

// --- Main Program Entry Point ---
public class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // --- 1. Service Registration ---
                services.AddSingleton<UserRepository>();

                services.AddLiteBus(liteBus =>
                {


                    var assembly = typeof(Program).Assembly;

                    liteBus.AddCommandModule(module =>
                    {
                        module.RegisterFromAssembly(assembly);
                    });

                    liteBus.AddQueryModule(module =>
                    {
                        module.RegisterFromAssembly(assembly);
                    });

                    // Registrer Event Module (selvom det ikke bruges i dette eksempel,
                    // er det god praksis at have med, hvis systemet skal udvides)
                    liteBus.AddEventModule(module =>
                    {
                        module.RegisterFromAssembly(assembly);
                    });
                });

                // Register the main application logic as a Hosted Service.
                // The Host will now manage its lifecycle (StartAsync/StopAsync).
                services.AddHostedService<ChatSimulatorService>();
            })
            .Build()
            .RunAsync();

    }

    // --- 2. The Application Logic as a Hosted Service ---
    public class ChatSimulatorService : IHostedService
    {
        private readonly ILogger<ChatSimulatorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _appLifetime;

        public ChatSimulatorService(
            ILogger<ChatSimulatorService> logger,
            IServiceProvider serviceProvider,
            IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _appLifetime = appLifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            _logger.LogInformation("--- Chat Simulator Service Starting ---");
            Console.ResetColor();

            // Create a DI scope to resolve services
            using (var scope = _serviceProvider.CreateScope())
            {
                var commandMediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();
                var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();

                // Bootstrap users
                await userRepository.AddAsync(new User("Alice"));
                await userRepository.AddAsync(new User("Bob"));

                Console.ForegroundColor = ConsoleColor.Green;
                _logger.LogInformation("--- Users Initialized. Starting Simulation ---");
                Console.ResetColor();

                var command = new SendMessageCommand("Alice", "Hey Bob, this works great in a console service!");
                await commandMediator.SendAsync(command, cancellationToken);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            _logger.LogInformation("--- Simulation Complete. Application will now shut down. ---");
            Console.ResetColor();

            // Trigger a graceful shutdown of the host
            _appLifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("--- Chat Simulator Service Stopping ---");
            return Task.CompletedTask;
        }
    }

    // --- 3. Models & DTOs ---
    public record User(string Name);
    public record UserDto(string Name); // DTO can still be useful for decoupling

    // --- 4. Data Persistence (In-Memory Repository) ---
    public class UserRepository
    {
        private readonly Dictionary<string, User> _users = new();

        public Task AddAsync(User user)
        {
            _users.TryAdd(user.Name, user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByNameAsync(string name)
        {
            _users.TryGetValue(name, out var user);
            return Task.FromResult(user);
        }

        public Task<IEnumerable<User>> GetAllAsync()
        {
            return Task.FromResult(_users.Values.AsEnumerable());
        }
    }

    // --- 5. CQRS - Commands ---
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

    // --- 6. CQRS - Command Handlers ---
    public class SendMessageCommandHandler : ICommandHandler<SendMessageCommand>
    {
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<SendMessageCommandHandler> _logger;

        public SendMessageCommandHandler(IEventPublisher eventPublisher, ILogger<SendMessageCommandHandler> logger)
        {
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task HandleAsync(SendMessageCommand command, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            _logger.LogInformation("[CommandHandler]: Processing message from {SenderName}.", command.SenderName);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
            _logger.LogInformation("[{Sender} -> Others]: '{Message}'", command.SenderName, command.Message);
            Console.ResetColor();
            await _eventPublisher.PublishAsync(new NewMessagePublishedEvent(command.SenderName, command.Message), cancellationToken);
        }
    }

    // --- 7. CQRS - Events ---
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

    // --- 8. CQRS - Event Handlers ---
    public class PushNotificationEventHandler : IEventHandler<NewMessagePublishedEvent>
    {
        private readonly ILogger<PushNotificationEventHandler> _logger;
        public PushNotificationEventHandler(ILogger<PushNotificationEventHandler> logger) => _logger = logger;
        public Task HandleAsync(NewMessagePublishedEvent @event, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            _logger.LogInformation("[PushNotifier]: Sending push for message from '{Author}'.", @event.Author);
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }

    public class ArchivingEventHandler : IEventHandler<NewMessagePublishedEvent>
    {
        private readonly ILogger<ArchivingEventHandler> _logger;
        public ArchivingEventHandler(ILogger<ArchivingEventHandler> logger) => _logger = logger;
        public Task HandleAsync(NewMessagePublishedEvent @event, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            _logger.LogInformation("[Archiver]: Storing message '{Content}' in the archive.", @event.Content);
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }

    // --- 9. CQRS - Queries & Handlers ---
    public record GetAllUsersQuery : IQuery<IEnumerable<UserDto>>;

    public class GetAllUsersQueryHandler : IQueryHandler<GetAllUsersQuery, IEnumerable<UserDto>>
    {
        private readonly UserRepository _userRepository;
        public GetAllUsersQueryHandler(UserRepository userRepository) => _userRepository = userRepository;

        public async Task<IEnumerable<UserDto>> HandleAsync(GetAllUsersQuery query, CancellationToken cancellationToken)
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(u => new UserDto(u.Name));
        }
    }
}