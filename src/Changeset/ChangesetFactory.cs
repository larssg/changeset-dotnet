using Changeset.Casting;

namespace Changeset;

public static class Changeset
{
    public static Changeset<T> Cast<T>(
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null) where T : class
    {
        return Caster.Cast<T>(null, @params, permitted, options ?? CastOptions.Default);
    }

    public static Changeset<T> Cast<T>(
        T data,
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(data);
        return Caster.Cast(data, @params, permitted, options ?? CastOptions.Default);
    }
}
