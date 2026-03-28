using System.Text.Json;
using Changeset;
using Changeset.Validators;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory "database"
var users = new List<User>();
var nextId = 1;

app.MapPost("/users", (JsonElement body) =>
{
    var @params = JsonSerializer.Deserialize<Dictionary<string, object?>>(body.GetRawText())
        ?? new Dictionary<string, object?>();

    var cs = Changeset.Changeset.Cast<User>(@params, ["Name", "Email", "Age"])
        .ValidateRequired(["Name", "Email"])
        .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
        .ValidateLength("Name", min: 2, max: 100)
        .ValidateNumber("Age", greaterThanOrEqual: 0, lessThan: 150);

    if (!cs.IsValid)
        return Results.ValidationProblem(
            cs.ErrorMap.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(e => e.Message).ToArray()));

    var user = cs.ApplyChanges();
    user.Id = nextId++;
    users.Add(user);

    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id:int}", (int id, JsonElement body) =>
{
    var existing = users.FirstOrDefault(u => u.Id == id);
    if (existing is null)
        return Results.NotFound();

    var @params = JsonSerializer.Deserialize<Dictionary<string, object?>>(body.GetRawText())
        ?? new Dictionary<string, object?>();

    var cs = Changeset.Changeset.Cast(existing, @params, ["Name", "Email", "Age"])
        .ValidateRequired(["Name", "Email"])
        .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
        .ValidateLength("Name", min: 2, max: 100)
        .ValidateNumber("Age", greaterThanOrEqual: 0, lessThan: 150);

    if (!cs.IsValid)
        return Results.ValidationProblem(
            cs.ErrorMap.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(e => e.Message).ToArray()));

    var updated = cs.ApplyChanges();
    updated.Id = id;
    var index = users.FindIndex(u => u.Id == id);
    users[index] = updated;

    return Results.Ok(updated);
});

app.MapGet("/users", () => users);
app.MapGet("/users/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.Run();

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
}
