// CQRS-Style Mediator Pattern Example in C#
// This file introduces Command and Handler concepts, aligning with CQRS principles.

using System;
using System.Collections.Generic;

namespace CqrsStyleMediator
{
    // --- 1. The Command ---
    // Represents an intent to change the system's state. It's a data container.
    // This is the "C" in CQRS.
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

    // --- 2. The Handler ---
    // Contains the business logic to process a specific command.
    // It is decoupled from the user that initiated the command.
    public class SendMessageCommandHandler
    {
        private readonly Dictionary<string, User> _users;

        // The handler needs the system state (the user list) to do its work.
        // In a real app, this would be injected.
        public SendMessageCommandHandler(Dictionary<string, User> users)
        {
            _users = users;
        }

        public void Handle(SendMessageCommand command)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Handler]: Processing SendMessageCommand from {command.SenderName}.");
            Console.ResetColor();

            // The logic that was in the old Mediator is now here.
            foreach (var user in _users.Values)
            {
                // Broadcast the message to all users except the originator
                if (user.Name != command.SenderName)
                {
                    user.Receive(command.Message, command.SenderName);
                }
            }
        }
    }

    // --- 3. The Mediator (now acting as a Message Bus) ---
    // Its responsibility is now to receive a command and dispatch it to the correct handler.
    public interface IMessageBus
    {
        void Register(User user);
        void Dispatch(SendMessageCommand command);
    }

    public class MessageBus : IMessageBus
    {
        // The Bus still manages the state of registered users for this example.
        private readonly Dictionary<string, User> _users = new Dictionary<string, User>();

        public void Register(User user)
        {
            if (!_users.ContainsValue(user))
            {
                _users[user.Name] = user;
                user.SetMessageBus(this);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[System]: {user.Name} has joined the chat.");
                Console.ResetColor();
            }
        }

        // The Dispatch method finds and executes the correct handler for the command.
        public void Dispatch(SendMessageCommand command)
        {
            // For this example, we manually create the handler.
            // A library like LiteBus would automate this handler resolution.
            var handler = new SendMessageCommandHandler(_users);
            handler.Handle(command);
        }
    }

    // --- 4. The Colleague ---
    // The base class now holds a reference to the Message Bus.
    public abstract class Colleague
    {
        protected IMessageBus? _messageBus;
        public string Name { get; }

        protected Colleague(string name)
        {
            Name = name;
        }

        public void SetMessageBus(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        // This is a receiving method, not part of the command flow.
        public abstract void Receive(string message, string from);
    }

    // The Concrete Colleague creates and dispatches commands.
    public class User : Colleague
    {
        public User(string name) : base(name) { }

        /// <summary>
        /// Creates and sends a command to the message bus.
        /// </summary>
        public void SendMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{this.Name} -> Bus]: Dispatching SendMessageCommand.");
            Console.ResetColor();

            var command = new SendMessageCommand(this.Name, message);
            _messageBus?.Dispatch(command);
        }

        public override void Receive(string message, string from)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{this.Name}] received from [{from}]: '{message}'");
            Console.ResetColor();
        }
    }

    // --- 5. The Client (Main Program) ---
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("--- CQRS-Style Mediator Chat Room Example ---");
            Console.WriteLine();

            // 1. Create the Message Bus (our new Mediator)
            IMessageBus messageBus = new MessageBus();

            // 2. Create Colleagues (the Users)
            User alice = new User("Alice");
            User bob = new User("Bob");
            User carol = new User("Carol");

            // 3. Register users with the bus
            messageBus.Register(alice);
            messageBus.Register(bob);
            messageBus.Register(carol);

            Console.WriteLine("\n--- Communication Starts ---\n");

            // 4. Users dispatch commands to the bus.
            // Notice the intent is clearer now: alice.SendMessage instead of a generic .Send
            alice.SendMessage("Hello everyone, how's the project going?");

            Console.WriteLine();

            bob.SendMessage("Hi Alice! I'm almost done with my part.");

            Console.WriteLine();

            carol.SendMessage("Great to hear! I just pushed my latest changes to the repo.");

            Console.WriteLine("\n--- End of Demonstration ---");
        }
    }
}
