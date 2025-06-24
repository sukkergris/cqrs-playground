//
// Komplet CQRS Eksempel med LiteBus i .NET 10 / C# 14
// ---------------------------------------------------
// Formål: At demonstrere den absolutte kerne af et API bygget med CQRS principper.
//


using System.Text.Json;
using LiteBus.Commands.Abstractions;
using LiteBus.Commands.Extensions.MicrosoftDependencyInjection;
using LiteBus.Events.Extensions.MicrosoftDependencyInjection;
using LiteBus.Messaging.Extensions.MicrosoftDependencyInjection;
using LiteBus.Queries.Abstractions;
using LiteBus.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Service Registrering ---

builder.Services.AddLiteBus(liteBus =>
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

builder.Services.AddSingleton<UserRepository>();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

var app = builder.Build();

// --- 2. HTTP Pipeline Konfiguration ---

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

// --- 3. API Endpoints ---
var usersGroup = app.MapGroup("/users");

usersGroup.MapGet("/{id:guid}", async (Guid id, IQueryMediator mediator) =>
{
    var query = new GetUserByIdQuery(id);
    var user = await mediator.QueryAsync(query);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithName("GetUserById")
.Produces<User>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// RETTELSE: Tilføjet try-catch for at håndtere exceptions fra handleren.
usersGroup.MapPost("/", async (CreateUserCommand command, ICommandMediator mediator) =>
{
    try
    {
        var result = await mediator.SendAsync(command);
        return Results.CreatedAtRoute("GetUserById", new { id = result.Id }, result);
    }
    catch (ArgumentException ex)
    {
        // Returner en 400 Bad Request med en meningsfuld fejlbesked.
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateUser")
.Produces<CreateUserCommandResult>(StatusCodes.Status201Created)
.Produces<object>(StatusCodes.Status400BadRequest);
// Tilføjet for at dokumentere fejl-responsen i Swagger

usersGroup.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateUserDetailsDto dto, ICommandMediator mediator) =>
{
    var command = new UpdateUserCommand(id, dto.Name, dto.Email);
    await mediator.SendAsync(command);
    return Results.NoContent();
})
.WithName("UpdateUser")
.Produces(StatusCodes.Status204NoContent);

app.Run();

// --- 4. Modeller & DTOs ---
public record User(Guid Id, string Name, string Email);
public record UpdateUserDetailsDto(string Name, string Email);

// --- 5. Data Persistence ---
public class UserRepository
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "users.json");
    private readonly object _lock = new();

    public Task<User?> GetByIdAsync(Guid id)
    {
        var users = ReadUsersFromFile();
        return Task.FromResult(users.FirstOrDefault(u => u.Id == id));
    }

    public Task AddAsync(User user)
    {
        var users = ReadUsersFromFile();
        users.Add(user);
        WriteUsersToFile(users);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User userToUpdate)
    {
        var users = ReadUsersFromFile();
        var existingUserIndex = users.FindIndex(u => u.Id == userToUpdate.Id);
        if (existingUserIndex != -1)
        {
            users[existingUserIndex] = userToUpdate;
            WriteUsersToFile(users);
        }
        return Task.CompletedTask;
    }

    private List<User> ReadUsersFromFile()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath)) return new List<User>();
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize<List<User>>(stream) ?? new List<User>();
        }
    }

    private void WriteUsersToFile(List<User> users)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonString = JsonSerializer.Serialize(users, options);
        lock (_lock)
        {
            File.WriteAllText(_filePath, jsonString);
        }
    }
}

// --- 6. CQRS - Commands & Results ---
public record CreateUserCommandResult(Guid Id);
public record CreateUserCommand(string Name, string Email) : ICommand<CreateUserCommandResult>;
public record UpdateUserCommand(Guid Id, string Name, string Email) : ICommand;


// --- 7. CQRS - Command Handlers ---
public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, CreateUserCommandResult>
{
    private readonly UserRepository _userRepository;
    public CreateUserCommandHandler(UserRepository userRepository) => _userRepository = userRepository;

    public async Task<CreateUserCommandResult> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // I en rigtig app ville validering ske her, før brugeren oprettes.
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Email))
        {
            // Simpel "guard clause" validering
            throw new ArgumentException("Name and Email cannot be empty.");
        }

        var user = new User(Guid.NewGuid(), command.Name, command.Email);
        await _userRepository.AddAsync(user);
        return new CreateUserCommandResult(user.Id);
    }
}

public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly UserRepository _userRepository;
    public UpdateUserCommandHandler(UserRepository userRepository) => _userRepository = userRepository;

    public async Task HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByIdAsync(command.Id);
        if (existingUser is null) return; // Bør kaste not-found exception

        var updatedUser = existingUser with { Name = command.Name, Email = command.Email };
        await _userRepository.UpdateAsync(updatedUser);
    }
}

// --- 8. CQRS - Queries & Handlers ---
public record GetUserByIdQuery(Guid Id) : IQuery<User?>;

public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, User?>
{
    private readonly UserRepository _userRepository;
    public GetUserByIdQueryHandler(UserRepository userRepository) => _userRepository = userRepository;

    public Task<User?> HandleAsync(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        return _userRepository.GetByIdAsync(query.Id);
    }
}
