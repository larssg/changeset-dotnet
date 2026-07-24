namespace Changeset;

/// <summary>
/// Indicates whether a changeset creates a new model or updates an existing one.
/// </summary>
public enum ChangesetAction
{
    /// <summary>
    /// The changeset was cast without existing data and will create a new model.
    /// </summary>
    Insert,

    /// <summary>
    /// The changeset was cast against existing data and will update it.
    /// </summary>
    Update
}
