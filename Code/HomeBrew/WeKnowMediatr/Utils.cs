using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace WeKnowMediatr;

public static class HandlerRegistration
{
    public static void AddHandlers(this IServiceCollection services, object[] assemblymarkers)
    {
        List<Type> handlerTypes =
            assemblymarkers.SelectMany(o => o.GetType().Assembly.GetTypes())
            .Where(x => x.GetInterfaces().Any(y => IsHandlerInterface(y)))
            .Where(x => x.Name.EndsWith("Handler"))
            .ToList();

        foreach (Type type in handlerTypes)
        {
            AddHandler(services, type);
        }
    }

    private static void AddHandler(IServiceCollection services, Type type)
    {
        object[] attributes = type.GetCustomAttributes(false);

        // Filter out system and non-decorator attributes
        var decoratorAttributes = attributes
            .Where(attr => attr.GetType().Namespace != null &&
                           !attr.GetType().Namespace.StartsWith("System.") &&
                           !attr.GetType().Namespace.StartsWith("Microsoft."))
            .ToList();

        List<Type> pipeline = decoratorAttributes
            .Select(x => ToDecorator(x))
            .Concat(new[] { type })
            .Reverse()
            .ToList();

        Type interfaceType = type.GetInterfaces().Single(y => IsHandlerInterface(y));
        Func<IServiceProvider, object> factory = BuildPipeline(pipeline, interfaceType);

        services.AddTransient(interfaceType, factory);
    }

    private static Func<IServiceProvider, object> BuildPipeline(List<Type> pipeline, Type interfaceType)
    {
        List<ConstructorInfo> ctors = pipeline
            .Select(x =>
            {
                Type type = x.IsGenericType ? x.MakeGenericType(interfaceType.GenericTypeArguments) : x;
                return type.GetConstructors().Single();
            })
            .ToList();

        Func<IServiceProvider, object> func = provider =>
        {
            object current = null;

            foreach (ConstructorInfo ctor in ctors)
            {
                List<ParameterInfo> parameterInfos = ctor.GetParameters().ToList();

                object[] parameters = GetParameters(parameterInfos, current, provider);

                current = ctor.Invoke(parameters);
            }

            return current;
        };

        return func;
    }

    private static object[] GetParameters(List<ParameterInfo> parameterInfos, object current, IServiceProvider provider)
    {
        var result = new object[parameterInfos.Count];

        for (int i = 0; i < parameterInfos.Count; i++)
        {
            result[i] = GetParameter(parameterInfos[i], current, provider);
        }

        return result;
    }

    private static object GetParameter(ParameterInfo parameterInfo, object current, IServiceProvider provider)
    {
        Type parameterType = parameterInfo.ParameterType;

        if (IsHandlerInterface(parameterType))
            return current;

        object service = provider.GetService(parameterType);
        if (service != null)
            return service;

        throw new ArgumentException($"Type {parameterType} not found");
    }

    private static Type ToDecorator(object attribute)
    {
        Type type = attribute.GetType();

        // if (type == typeof(DatabaseRetryAttribute))
        //     return typeof(DatabaseRetryDecorator<>);
        //
        // if (type == typeof(AuditLogAttribute))
        //     return typeof(AuditLoggingDecorator<>);

        // other attributes go here

        // throw new ArgumentException(attribute.ToString());

        // Skip unknown attributes (e.g., system/compiler attributes)

        return null;
    }

    private static bool IsHandlerInterface(Type type)
    {
        if (!type.IsGenericType)
            return false;

        Type typeDefinition = type.GetGenericTypeDefinition();

        return typeDefinition == typeof(ICommandHandler<>) || typeDefinition == typeof(IQueryHandler<,>);
    }
}