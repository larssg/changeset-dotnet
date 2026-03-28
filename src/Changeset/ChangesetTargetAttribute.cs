namespace Changeset;

/// <summary>
/// Marks a type for source generation. The generator will emit a reflection-free
/// property setter and a compile-time set of valid field names for this type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ChangesetTargetAttribute : Attribute;
