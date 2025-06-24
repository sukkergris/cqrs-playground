// Mediator Pattern Example in C#
// This entire file can be compiled and run as "Program.cs" using `dotnet run Program.cs`

using System;
using System.Collections.Generic;

namespace MediatorPatternExample
{
    // --- 1. The Mediator Interface ---
    // Defines the contract for communication between colleagues.
    // The mediator knows how to route messages.
    public interface IChatroom
    {
        void Register(User user);
        void Send(string message, User originator);
    }

    // --- 2. The Concrete Mediator ---
    // Implements the Mediator interface and coordinates communication between Colleagues.
    // It holds a list of all registered colleagues.
    public class Chatroom : IChatroom
    {
        private readonly Dictionary<string, User> _users = new Dictionary<string, User>();

        /// <summary>
        /// Registers a user with the chatroom.
        /// </summary>
        public void Register(User user)
        {
            if (!_users.ContainsValue(user))
            {
                _users[user.Name] = user;
                
                user.SetChatroom(this); // Each user gets a ref to the Chatroom
                
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[System]: {user.Name} has joined the chat.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Sends a message from an originator user to all other users in the chatroom.
        /// </summary>
        public void Send(string message, User originator)
        {
            // Broadcast the message to all users except the originator
            foreach (var user in _users.Values)
            {
                if (user != originator)
                {
                    user.Receive(message, originator.Name);
                }
            }
        }
    }

    // --- 3. The Colleague Abstract Class ---
    // Represents the objects that will communicate through the mediator.
    // Each colleague has a reference to the mediator (the chatroom).
    public abstract class Colleague
    {
        protected IChatroom? _chatroom;
        public string Name { get; }

        protected Colleague(string name)
        {
            Name = name;
        }

        public void SetChatroom(IChatroom chatroom)
        {
            _chatroom = chatroom;
        }

        /// <summary>
        /// Sends a message via the chatroom.
        /// </summary>
        public void Send(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{this.Name} -> All]: {message}");
            Console.ResetColor();
            // We need to cast 'this' to User, as the interface expects a concrete colleague type.
            // In a more complex system, the Send method might take a Colleague and handle the type internally.
            _chatroom?.Send(message, (User)this);
        }

        /// <summary>
        /// Receives a message from the chatroom.
        /// </summary>
        public abstract void Receive(string message, string from);
    }


    // --- 4. The Concrete Colleague ---
    // A concrete implementation of a colleague that can send and receive messages.
    public class User : Colleague
    {
        public User(string name) : base(name) { }

        /// <summary>
        /// Implements the logic for how a user handles a received message.
        /// </summary>
        public override void Receive(string message, string from)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{this.Name}] received from [{from}]: '{message}'");
            Console.ResetColor();
        }
    }


    // --- 5. The Client (Main Program) ---
    // This is where we set up and run the demonstration.
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("--- Mediator Pattern Chat Room Example ---");
            Console.WriteLine();

            // 1. Create the Mediator (the Chatroom)
            IChatroom chatroom = new Chatroom();

            // 2. Create Colleagues (the Users)
            User alice = new User("Alice");
            User bob = new User("Bob");
            User carol = new User("Carol");

            // 3. Register users with the chatroom
            chatroom.Register(alice);
            chatroom.Register(bob);
            chatroom.Register(carol);

            Console.WriteLine("\n--- Communication Starts ---\n");

            // 4. Users communicate through the mediator
            // Alice sends a message, which the chatroom distributes to Bob and Carol.
            alice.Send("Hello everyone, how's the project going?");

            Console.WriteLine();

            // Bob replies. The chatroom sends his message to Alice and Carol.
            bob.Send("Hi Alice! I'm almost done with my part.");

            Console.WriteLine();

            // Carol sends a message. The chatroom sends it to Alice and Bob.
            carol.Send("Great to hear! I just pushed my latest changes to the repo.");

            Console.WriteLine("\n--- End of Demonstration ---");
        }
    }
}
