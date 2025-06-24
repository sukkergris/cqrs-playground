using CSharpFunctionalExtensions;

namespace WeKnowMediatr;

public sealed class Messages
{
    private readonly IServiceProvider _provider;

    public Messages(IServiceProvider provider)
    {
        _provider = provider;
    }

    public Result Dispatch(ICommand command)
    {
        Type type = typeof(ICommandHandler<>);
        Type[] typeArgs = { command.GetType() };
        Type handlerType = type.MakeGenericType(typeArgs);

        dynamic handler = _provider.GetService(handlerType);
        Result result = handler.Handle((dynamic)command);

        return result;
    }

    public T Dispatch<T>(IQuery<T> query)
    {
        Type type = typeof(IQueryHandler<,>);
        Type[] typeArgs = { query.GetType(), typeof(T) };
        Type handlerType = type.MakeGenericType(typeArgs);

        dynamic handler = _provider.GetService(handlerType);
        T result = handler.Handle((dynamic)query);

        return result;
    }
}
public interface IQuery<TResult>
{
}

public interface ICommandHandler<TCommand>
    where TCommand : ICommand
{
    Result Handle(TCommand command);
}

public interface ICommand
{
}

public interface IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    TResult Handle(TQuery query);
}