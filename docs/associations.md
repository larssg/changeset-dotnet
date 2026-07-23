# Associations

Nested changesets let a parent changeset cast and validate a single associated
object while preserving structured child state.

Consider these models:

```csharp
public class User
{
    public string Name { get; set; } = "";
    public Address Address { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}
```

## Cast nested input

The association value can be an `IReadOnlyDictionary<string, object?>`, an
`IDictionary<string, object?>`, or a JSON object:

```csharp
var parameters = new Dictionary<string, object?>
{
    ["Name"] = "Ada",
    ["Address"] = new Dictionary<string, object?>
    {
        ["Street"] = "12 St James's Square",
        ["City"] = "London",
        ["Zip"] = "SW1Y 4LB"
    }
};

var changeset = Changeset<User>
    .Cast(parameters, user => user.Name)
    .CastAssoc<User, Address>(
        "Address",
        ["Street", "City", "Zip"]);
```

`CastAssoc` finds the nested value in the parent's original `Params`, casts a
child `Changeset<Address>`, and stores that child changeset under `Address` in
the parent's `Changes`.

If the association key is missing, casting is skipped. If the value is not a
supported dictionary or JSON object, the parent receives:

```text
field:   Address
message: is invalid
code:    invalid_assoc
```

Pass `CastOptions` as the final argument to control the child cast.

## Validate the child

Use `ValidateAssoc` after `CastAssoc`:

```csharp
var changeset = Changeset<User>
    .Cast(parameters, user => user.Name)
    .CastAssoc<User, Address>(
        "Address",
        ["Street", "City", "Zip"])
    .ValidateAssoc<User, Address>(
        "Address",
        address => address.ValidateRequired(["Street", "City"]));
```

Child errors are copied onto the parent using dotted field names:

```csharp
changeset.HasErrorOn("Address.Street");
changeset.ErrorsOn("Address.City");
```

Errors produced while initially casting the child and errors added by
`ValidateAssoc` are both propagated. Child error metadata is not currently
copied to the prefixed parent error; inspect the child changeset when that
metadata is needed.

## Inspect the child

Retrieve the typed child with `GetAssoc`:

```csharp
Changeset<Address>? addressChangeset =
    changeset.GetAssoc<User, Address>("Address");

if (addressChangeset is not null)
{
    string? city = addressChangeset.GetChange<string>("City");
}
```

The method returns `null` when the field has no child changeset or its type does
not match `TAssoc`.

## Updates

For a parent update, `CastAssoc` reads the existing association property through
reflection. When the property contains a `TAssoc`, the child is an update
changeset and unchanged child values are omitted:

```csharp
var changeset = Changeset<User>
    .Cast(existingUser, parameters, user => user.Name)
    .CastAssoc<User, Address>(
        "Address",
        ["Street", "City", "Zip"]);

var address = changeset.GetAssoc<User, Address>("Address");
address?.Action; // ChangesetAction.Update
```

If the existing association is `null`, the child uses insert semantics.

## Materialization limitation

`CastAssoc` stores a `Changeset<TAssoc>` in the parent's `Changes`; the core
`ApplyChanges` method does not recursively materialize it. Applying that parent
directly attempts to assign the child changeset to the association property and
is therefore not supported.

Materialize and attach the child explicitly in application code:

```csharp
var addressChangeset =
    changeset.GetAssoc<User, Address>("Address")
    ?? throw new InvalidOperationException("Address was not cast.");

if (!changeset.IsValid || !addressChangeset.IsValid)
    return;

var address = addressChangeset.ApplyChanges();

var parentWithoutAssoc = changeset with
{
    Changes = changeset.Changes.Remove("Address"),
    CastFields = changeset.CastFields.Remove("Address")
};

var user = parentWithoutAssoc.ApplyChanges();
user.Address = address;
```

Treat nested changesets as a casting and validation facility until recursive
materialization is added to the library.
