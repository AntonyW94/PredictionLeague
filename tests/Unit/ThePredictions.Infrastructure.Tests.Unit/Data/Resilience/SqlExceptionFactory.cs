using System.Reflection;
using Microsoft.Data.SqlClient;

namespace ThePredictions.Infrastructure.Tests.Unit.Data.Resilience;

/// <summary>
/// Builds a <see cref="SqlException"/> carrying chosen error numbers. SqlException has no public
/// constructor and cannot be produced without a real server round-trip, so the only way to test a
/// retry policy against specific error numbers is to reach for the internal factory the driver
/// itself uses. Confined to this helper so the reflection stays in one place.
/// </summary>
internal static class SqlExceptionFactory
{
    public static SqlException WithErrorNumbers(params int[] errorNumbers)
    {
        var errors = CreateErrorCollection();

        foreach (var number in errorNumbers)
            AddError(errors, CreateError(number));

        var createException = typeof(SqlException).GetMethod(
            "CreateException",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(SqlErrorCollection), typeof(string)],
            modifiers: null)
            ?? throw new InvalidOperationException("SqlException.CreateException(SqlErrorCollection, string) not found.");

        return (SqlException)createException.Invoke(null, [errors, "16.0.1000"])!;
    }

    private static SqlErrorCollection CreateErrorCollection()
    {
        var constructor = typeof(SqlErrorCollection).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)
            ?? throw new InvalidOperationException("SqlErrorCollection parameterless constructor not found.");

        return (SqlErrorCollection)constructor.Invoke(null);
    }

    private static void AddError(SqlErrorCollection errors, SqlError error)
    {
        var add = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SqlErrorCollection.Add not found.");

        add.Invoke(errors, [error]);
    }

    private static SqlError CreateError(int number)
    {
        // The driver has changed this constructor's shape between versions, so pick whichever
        // overload is present rather than pinning to one signature.
        var constructor = typeof(SqlError)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().Any(p => p.Name == "infoNumber" || p.Name == "number"))
            ?? throw new InvalidOperationException("No usable SqlError constructor found.");

        var arguments = constructor.GetParameters().Select(p => ArgumentFor(p, number)).ToArray();

        return (SqlError)constructor.Invoke(arguments);
    }

    private static object? ArgumentFor(ParameterInfo parameter, int number)
    {
        if (parameter.Name is "infoNumber" or "number")
            return number;

        if (parameter.ParameterType == typeof(string))
            return parameter.Name == "message" ? $"Simulated SQL error {number}." : string.Empty;

        if (parameter.ParameterType == typeof(byte))
            return (byte)0;

        if (parameter.ParameterType == typeof(int))
            return 0;

        if (parameter.ParameterType == typeof(uint))
            return 0u;

        if (parameter.ParameterType == typeof(Exception))
            return null;

        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }
}
