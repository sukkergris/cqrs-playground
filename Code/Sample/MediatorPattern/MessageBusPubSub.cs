// CQRS-Style Mediator with Commands and Events in C#
// This file demonstrates the full Command -> Event workflow.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CqrsWithEvents;
    // --- Message Types ---

    // 1. The Command: An instruction to do something. Has one handler.
    public class SendMessageCommand
    {
        public string SenderName { get; set; }
        public string Message { get; set; }

        public SendMessageCommand(string senderName, string message)
        {
            SenderName = senderName;
            Message = message;
        }
    }

    // 2. The Event: A notification that something has happened. Can have many handlers.
    public class NewMessagePublishedEvent
    {
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTime PublishedAt { get; set; }

        public NewMessagePublishedEvent(string author, string content)
        {
            Author = author;
            Content = content;
            PublishedAt = DateTime.UtcNow;
        }
    }

    // --- Handlers ---

    // 3. Command Handler: Processes the command and publishes an event.
    public class SendMessageCommandHandler
    {
        private readonly Dictionary<string, User> _users;
        private readonly IMessageBus _bus; // Now needs the bus to publish events.

        public SendMessageCommandHandler(Dictionary<string, User> users, IMessageBus bus)
        {
            _users = users;
            _bus = bus;
        }

        public void Handle(SendMessageCommand command)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[CommandHandler]: Processing SendMessageCommand from {command.SenderName}.");
            Console.ResetColor();

            // Original logic: broadcast to users.
            foreach (var user in _users.Values.Where(u => u.Name != command.SenderName))
            {
                user.Receive(command.Message, command.SenderName);
            }

            // New logic: publish an event to notify other parts of the system.
            var messageEvent = new NewMessagePublishedEvent(command.SenderName, command.Message);
            _bus.Publish(messageEvent);
        }
    }

    // 4. Event Handlers: React to events. These are completely decoupled from the command.
    public interface IEventHandler<TEvent>
    {
        void Handle(TEvent @event);
    }

    // A handler that simulates sending a push notification.
    public class PushNotificationEventHandler : IEventHandler<NewMessagePublishedEvent>
    {
        public void Handle(NewMessagePublishedEvent @event)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[PushNotifier]: Sending mobile push for message from '{@event.Author}'.");
            Console.ResetColor();
        }
    }

    // A handler that simulates archiving the message.
    public class ArchivingEventHandler : IEventHandler<NewMessagePublishedEvent>
    {
        public void Handle(NewMessagePublishedEvent @event)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"[Archiver]: Storing message '{@event.Content}' in the archive.");
            Console.ResetColor();
        }
    }

    // --- The Message Bus ---

    // 5. The Bus now handles both commands and events.
    public interface IMessageBus
    {
        void RegisterUser(User user);
        void Subscribe<TEvent>(IEventHandler<TEvent> handler);
        void Dispatch(SendMessageCommand command);
        void Publish<TEvent>(TEvent @event);
    }

    public class MessageBus : IMessageBus
    {
        private readonly Dictionary<string, User> _users = new();
        // Stores subscriptions: Event Type -> List of handler instances
        private readonly Dictionary<Type, List<object>> _subscriptions = new();

        public void RegisterUser(User user)
        {
            if (!_users.ContainsValue(user))
            {
                _users[user.Name] = user;
                user.SetMessageBus(this);
            }
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            if (!_subscriptions.ContainsKey(eventType))
            {
                _subscriptions[eventType] = new List<object>();
            }
            _subscriptions[eventType].Add(handler);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[System]: {handler.GetType().Name} is now subscribed to {eventType.Name}.");
            Console.ResetColor();
        }

        public void Dispatch(SendMessageCommand command)
        {
            // In a real library, handler resolution is automatic.
            var handler = new SendMessageCommandHandler(_users, this);
            handler.Handle(command);
        }

        public void Publish<TEvent>(TEvent @event)
        {
            var eventType = typeof(TEvent);
            if (_subscriptions.ContainsKey(eventType))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"[Bus]: Publishing {eventType.Name} to its subscribers...");
                Console.ResetColor();

                foreach (var handler in _subscriptions[eventType].Cast<IEventHandler<TEvent>>())
                {
                    handler.Handle(@event);
                }
            }
        }
    }

    // --- The User (Colleague) --- (No changes needed here!)
    public class User
    {
        private IMessageBus? _bus;
        public string Name { get; }
        public User(string name) { Name = name; }
        public void SetMessageBus(IMessageBus bus) => _bus = bus;

        public void SendMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{this.Name} -> Bus]: Dispatching SendMessageCommand.");
            Console.ResetColor();
            _bus?.Dispatch(new SendMessageCommand(this.Name, message));
        }

        public void Receive(string message, string from)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{this.Name}] received from [{from}]: '{message}'");
            Console.ResetColor();
        }
    }

    // --- Main Program ---
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("--- CQRS Mediator with Events ---");

            // 1. Create the Bus
            IMessageBus messageBus = new MessageBus();

            // 2. Create Event Handlers
            var pushNotifier = new PushNotificationEventHandler();
            var archiver = new ArchivingEventHandler();

            // 3. Subscribe handlers to the bus
            messageBus.Subscribe(pushNotifier);
            messageBus.Subscribe(archiver);
            Console.WriteLine();

            // 4. Register Users
            var alice = new User("Alice");
            var bob = new User("Bob");
            messageBus.RegisterUser(alice);
            messageBus.RegisterUser(bob);
            Console.WriteLine("\n--- Communication Starts ---\n");

            // 5. User dispatches a command. The whole workflow will now trigger.
            alice.SendMessage("Hey Bob, let's ship this feature!");

            Console.WriteLine("\n--- End of Demonstration ---");
        }
    }

