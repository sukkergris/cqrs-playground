using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WeKnowMediatr.Test;

public class ResolveAndFireICommands
{
    [Fact]
    public void Shout_Command_With_Success()
    {
        var services = new ServiceCollection();
        
        services.AddSingleton<Messages>();
        services.AddHandlers([new Assemblymarker()]);
        
        var serviceProvider = services.BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<Messages>();

        var residual = mediator.Dispatch(new ShoutCommand("These are the things I can do without!"));
        
        Assert.True(residual.IsSuccess);

        var response = mediator.Dispatch(new StrangeQuery("Who are you?"));
        
        Assert.True(response.IsSuccess);
        Assert.NotEmpty(response.Value);
    }
}

public sealed class StrangeQuery(string query) : IQuery<Result<string>>
{
    public string Query { get; } = query;
}

public sealed class StrangeQureyHandler : IQueryHandler<StrangeQuery, Result<string>>
{
    public Result<string> Handle(StrangeQuery query)
    {
        return Result.Success($"You queried {query.Query}!");
    }
}

public sealed class ShoutCommand(string words) : ICommand
{
    public string Words { get; } = words;
}

public sealed class ShoutCommandHandler : ICommandHandler<ShoutCommand>
{
    public Result Handle(ShoutCommand command)
    {
        Console.WriteLine($"Shout! Let it all out! {command.Words}");
        return new Result();
    }
}
