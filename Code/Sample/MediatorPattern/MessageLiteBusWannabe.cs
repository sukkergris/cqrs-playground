// LiteBus-Style Mediator Example in C#
// This file demonstrates a more advanced mediator that mimics library-like handler resolution.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CqrsWithLiteBusStyle
{
    // --- 1. Message Marker Interfaces (Like LiteBus) ---
    public interface ICommand { } // A command that returns no result
    public interface IEvent { }   // An event notification

    // --- 2. Message Definitions ---
    public class SendMessageCommand : ICommand
    {
        public string SenderName { get; set; }
        public string Message { get; set; }
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

    // --- 3. Handler Interfaces (Like LiteBus) ---
    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        void Handle(TCommand command);
    }

    public interface IEventHandler<TEvent> where TEvent : IEvent
    {
        void Handle(TEvent @event);
    }

    // --- 4. The Central Mediator Interface ---
    public interface IMediator
    {
        void Send(ICommand command);
        void Publish(IEvent @event);
    }

    // --- 5. Concrete Handlers ---
    
    // Command Handler
    public class SendMessageCommandHandler : ICommandHandler<SendMessageCommand>
    {
        private readonly IMediator _mediator;
        private readonly Dictionary<string, User> _userRepository;

        // Dependencies are "injected" by our custom Mediator
        public SendMessageCommandHandler(IMediator mediator, Dictionary<string, User> userRepository)
        {
            _mediator = mediator;
            _userRepository = userRepository;
        }

        public void Handle(SendMessageCommand command)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[CommandHandler]: Processing command from {command.SenderName}.");
            Console.ResetColor();

            // Broadcast to other users
            foreach (var user in _userRepository.Values.Where(u => u.Name != command.SenderName))
            {
                user.Receive(command.Message, command.SenderName);
            }
            
            // Publish an event for other parts of the system to react to
            _mediator.Publish(new NewMessagePublishedEvent(command.SenderName, command.Message));
        }
    }

    // Event Handlers
    public class PushNotificationEventHandler : IEventHandler<NewMessagePublishedEvent>
    {
        public void Handle(NewMessagePublishedEvent @event)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[PushNotifier]: Sending push for message from '{@event.Author}'.");
            Console.ResetColor();
        }
    }

    public class ArchivingEventHandler : IEventHandler<NewMessagePublishedEvent>
    {
        public void Handle(NewMessagePublishedEvent @event)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"[Archiver]: Storing message '{@event.Content}' in the archive.");
            Console.ResetColor();
        }
    }

    // --- 6. The "LiteBus" Mediator Implementation ---
    // This class simulates a real mediator library by using a service registry.
    public class Mediator : IMediator
    {
        private readonly Func<Type, object> _serviceFactory;

        public Mediator(Func<Type, object> serviceFactory)
        {
            _serviceFactory = serviceFactory;
        }

        public void Send(ICommand command)
        {
            var commandType = command.GetType();
            // Construct the specific handler type from the generic interface
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
            
            // Resolve the handler using the factory (simulates DI)
            dynamic handler = _serviceFactory(handlerType);
            
            handler.Handle((dynamic)command);
        }

        public void Publish(IEvent @event)
        {
            var eventType = @event.GetType();
            var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
            
            // For events, we resolve all handlers
            dynamic handlers = _serviceFactory(handlerType);

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[Bus]: Publishing {eventType.Name} to {handlers.Count} subscriber(s)...");
            Console.ResetColor();

            foreach (var handler in handlers)
            {
                handler.Handle((dynamic)@event);
            }
        }
    }

    // --- 7. The User ---
    public class User
    {
        private readonly IMediator _mediator;
        public string Name { get; }

        public User(string name, IMediator mediator)
        {
            Name = name;
            _mediator = mediator;
        }

        public void SendMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{this.Name} -> Bus]: Dispatching SendMessageCommand.");
            Console.ResetColor();
            _mediator.Send(new SendMessageCommand(this.Name, message));
        }

        public void Receive(string message, string from)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{this.Name}] received from [{from}]: '{message}'");
            Console.ResetColor();
        }
    }

    // --- 8. The Client (Main Program with Service Setup) ---
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("--- LiteBus-Style Mediator ---");

            // 1. Set up a simple service container/registry
            var services = new Dictionary<Type, Func<object>>();
            var handlers = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetInterfaces().Any(i => IsHandlerInterface(i)))
                .ToList();

            // Shared state / "Singleton" services
            var userRepository = new Dictionary<string, User>();
            
            // This factory function simulates a DI container's service resolution
            Func<Type, object> serviceFactory = (type) =>
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                {
                    var eventType = type.GetGenericArguments()[0];
                    var handlerImplTypes = handlers.Where(h => typeof(IEventHandler<>).MakeGenericType(eventType).IsAssignableFrom(h));
                    return handlerImplTypes.Select(Activator.CreateInstance).ToList();
                }

                var handlerImplType = handlers.First(h => type.IsAssignableFrom(h));
                var constructor = handlerImplType.GetConstructors().First();
                var parameters = constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(IMediator) ? (object)services[typeof(IMediator)]() : userRepository)
                    .ToArray();
                
                return Activator.CreateInstance(handlerImplType, parameters);
            };

            // Register the Mediator itself so it can be injected
            services[typeof(IMediator)] = () => new Mediator(serviceFactory);
            var mediator = (IMediator)services[typeof(IMediator)]();
            
            // 2. Create users (who now get the mediator injected)
            var alice = new User("Alice", mediator);
            var bob = new User("Bob", mediator);
            userRepository[alice.Name] = alice;
            userRepository[bob.Name] = bob;

            Console.WriteLine("\n--- Communication Starts ---\n");

            // 3. User dispatches a command.
            alice.SendMessage("Hey Bob, let's ship this feature!");

            Console.WriteLine("\n--- End of Demonstration ---");
        }
        
        private static bool IsHandlerInterface(Type type)
        {
            if (!type.IsGenericType) return false;
            var definition = type.GetGenericTypeDefinition();
            return definition == typeof(ICommandHandler<>) || definition == typeof(IEventHandler<>);
        }
    }
}
