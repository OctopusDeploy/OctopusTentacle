using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>
/// Just enough of the Octopus Server REST API for `tentacle register-with` (via Octopus.Client) to register a
/// deployment target. It never talks Halibut; the tests do that with their own server HalibutRuntime.
///
/// Spaces stand in for separate servers: each test fixture registers into its own space so fixtures can share one
/// fake server without seeing each other's machines.
///
/// Anything the fake does not know about is answered with a 404 and recorded in <see cref="UnhandledRequests"/>,
/// so a change in what Octopus.Client asks for shows up as a clear failure rather than a mystery.
/// </summary>
public sealed class FakeOctopusServer : IAsyncDisposable
{
    public const string ServerVersion = "2025.1.0";
    public const string UserId = "Users-1";

    static readonly string[] SpaceCollections =
    {
        "environments",
        "machinepolicies",
        "machines",
        "proxies",
        "tagsets",
        "tenants",
        "workerpools",
        "workers"
    };

    static readonly string[] RegistrationCollections = { "machines", "workers" };

    readonly WebApplication app;
    readonly string serverThumbprint;
    readonly ConcurrentDictionary<string, FakeSpace> spacesById = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<(string SpaceName, string MachineName), TaskCompletionSource<RegisteredMachine>> registrations = new();
    readonly ConcurrentQueue<string> unhandledRequests = new();
    readonly ConcurrentQueue<string> requestLog = new();
    int nextSpaceNumber;
    int nextResourceNumber;

    FakeOctopusServer(WebApplication app, string serverThumbprint)
    {
        this.app = app;
        this.serverThumbprint = serverThumbprint;
    }

    public int Port { get; private set; }

    /// <summary>
    /// Raised when a Tentacle registers, before anyone awaiting <see cref="WaitForRegistration"/> sees it. Polling
    /// Tentacles need their thumbprint trusted by the server HalibutRuntime as soon as possible.
    /// </summary>
    public event Action<RegisteredMachine>? MachineRegistered;

    public IReadOnlyCollection<string> UnhandledRequests => unhandledRequests.ToArray();

    /// <summary>
    /// Every request and registration, in order. Requests are served on Kestrel threads, which have no NUnit test
    /// context to log to, so tests write this out themselves.
    /// </summary>
    public IReadOnlyCollection<string> RequestLog => requestLog.ToArray();

    /// <param name="serverThumbprint">The thumbprint registering Tentacles will trust, i.e. the certificate the
    /// tests' server HalibutRuntime presents.</param>
    public static async Task<FakeOctopusServer> Start(string serverThumbprint, ILogger logger)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        // Any rather than Loopback: Tentacle containers reach us through the Docker host gateway.
        builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Any, 0));

        var app = builder.Build();
        var server = new FakeOctopusServer(app, serverThumbprint);
        server.MapRoutes();

        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        server.Port = new Uri(address).Port;
        logger.ForContext<FakeOctopusServer>().Information("Fake Octopus Server listening on {Address}", address);

        return server;
    }

    /// <summary>Adds a space with the given environments, plus the default machine policy every Tentacle asks for.</summary>
    public void AddSpace(string name, params string[] environmentNames)
    {
        var id = $"Spaces-{Interlocked.Increment(ref nextSpaceNumber)}";
        var space = new FakeSpace(id, name);

        foreach (var collection in SpaceCollections)
        {
            space.Collections[collection] = new List<JsonObject>();
        }

        space.Collections["machinepolicies"].Add(new JsonObject
        {
            ["Id"] = "MachinePolicies-1",
            ["Name"] = "Default Machine Policy",
            ["IsDefault"] = true,
            ["SpaceId"] = id,
            ["Links"] = Links(("Self", $"/api/{id}/machinepolicies/MachinePolicies-1"))
        });

        foreach (var environmentName in environmentNames)
        {
            var environmentId = $"Environments-{Interlocked.Increment(ref nextResourceNumber)}";
            space.Collections["environments"].Add(new JsonObject
            {
                ["Id"] = environmentId,
                ["Name"] = environmentName,
                ["SpaceId"] = id,
                ["SortOrder"] = 0,
                ["UseGuidedFailure"] = false,
                ["AllowDynamicInfrastructure"] = true,
                ["Links"] = Links(("Self", $"/api/{id}/environments/{environmentId}"))
            });
        }

        if (spacesById.Values.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A space named '{name}' already exists");
        }

        spacesById[id] = space;
    }

    public Task<RegisteredMachine> WaitForRegistration(string spaceName, string machineName)
        => RegistrationFor(spaceName, machineName).Task;

    TaskCompletionSource<RegisteredMachine> RegistrationFor(string spaceName, string machineName)
        => registrations.GetOrAdd((spaceName.ToLowerInvariant(), machineName.ToLowerInvariant()), _ => new TaskCompletionSource<RegisteredMachine>(TaskCreationOptions.RunContinuationsAsynchronously));

    void MapRoutes()
    {
        app.Use(async (context, next) =>
        {
            await next();
            requestLog.Enqueue($"{DateTimeOffset.UtcNow:HH:mm:ss.fff} {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode}");
        });

        app.MapGet("/api", () => Json(RootDocument()));

        app.MapGet("/api/users/me", () => Json(User()));
        app.MapGet("/api/users/{id}", (string id) => id == UserId ? Json(User()) : NotFound($"User {id} not found"));
        app.MapGet("/api/users/{id}/spaces", (string id) => Json(new JsonArray(spacesById.Values.OrderBy(s => s.Id).Select(SpaceResource).ToArray<JsonNode?>())));

        app.MapGet("/api/spaces", () => Json(Page("Space", "/api/spaces", spacesById.Values.OrderBy(s => s.Id).Select(SpaceResource).ToList())));
        app.MapGet("/api/spaces/all", () => Json(new JsonArray(spacesById.Values.OrderBy(s => s.Id).Select(SpaceResource).ToArray<JsonNode?>())));
        app.MapGet("/api/spaces/{spaceId}", (string spaceId) => WithSpace(spaceId, space => Json(SpaceResource(space))));

        app.MapGet("/api/configuration/certificates/certificate-global", () => Json(new JsonObject
        {
            ["Id"] = "certificate-global",
            ["Name"] = "Octopus Server",
            ["Thumbprint"] = serverThumbprint,
            ["Links"] = Links(("Self", "/api/configuration/certificates/certificate-global"))
        }));

        app.MapGet("/api/{spaceId}", (string spaceId) => WithSpace(spaceId, space => Json(SpaceRootDocument(space))));

        app.MapGet("/api/{spaceId}/{collection}", (string spaceId, string collection, HttpRequest request)
            => WithCollection(spaceId, collection, (space, items) => Json(Page(collection, $"/api/{space.Id}/{collection}", Filter(items, request.Query)))));

        app.MapGet("/api/{spaceId}/{collection}/all", (string spaceId, string collection, HttpRequest request)
            => WithCollection(spaceId, collection, (_, items) => Json(new JsonArray(Filter(items, request.Query).Select(i => (JsonNode)i.DeepClone()).ToArray<JsonNode?>()))));

        app.MapGet("/api/{spaceId}/{collection}/{id}", (string spaceId, string collection, string id)
            => WithCollection(spaceId, collection, (_, items) =>
            {
                lock (items)
                {
                    var item = items.FirstOrDefault(i => string.Equals((string?)i["Id"], id, StringComparison.OrdinalIgnoreCase));
                    return item is null ? NotFound($"{collection}/{id} not found") : Json(item.DeepClone());
                }
            }));

        app.MapPost("/api/{spaceId}/{collection}", (string spaceId, string collection, JsonObject body)
            => WithRegistrationCollection(spaceId, collection, (space, items) => Register(space, collection, items, body, existingId: null)));

        // Octopus.Client sends updates as a POST with an X-HTTP-Method-Override: PUT header, so accept both.
        app.MapMethods("/api/{spaceId}/{collection}/{id}", new[] { HttpMethods.Put, HttpMethods.Post }, (string spaceId, string collection, string id, JsonObject body)
            => WithRegistrationCollection(spaceId, collection, (space, items) => Register(space, collection, items, body, existingId: id)));

        app.MapFallback((HttpContext context) =>
        {
            var request = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
            unhandledRequests.Enqueue(request);
            return NotFound($"The fake Octopus Server does not handle {request}");
        });
    }

    IResult Register(FakeSpace space, string collection, List<JsonObject> items, JsonObject body, string? existingId)
    {
        var prefix = collection == "workers" ? "Workers" : "Machines";
        var id = existingId ?? $"{prefix}-{Interlocked.Increment(ref nextResourceNumber)}";

        body["Id"] = id;
        body["SpaceId"] = space.Id;
        body["Links"] = Links(("Self", $"/api/{space.Id}/{collection}/{id}"));

        lock (items)
        {
            items.RemoveAll(i => string.Equals((string?)i["Id"], id, StringComparison.OrdinalIgnoreCase));
            items.Add(body);
        }

        var machine = RegisteredMachine.From(space, body);
        requestLog.Enqueue($"{DateTimeOffset.UtcNow:HH:mm:ss.fff} Registered {machine.Name} in {space.Name}: {machine.CommunicationStyle} {machine.Uri} {machine.Thumbprint}");

        MachineRegistered?.Invoke(machine);
        RegistrationFor(space.Name, machine.Name).TrySetResult(machine);

        return Json(body.DeepClone(), existingId is null ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }

    IResult WithSpace(string spaceId, Func<FakeSpace, IResult> action)
        => spacesById.TryGetValue(spaceId, out var space) ? action(space) : NotFound($"Space {spaceId} not found");

    IResult WithCollection(string spaceId, string collection, Func<FakeSpace, List<JsonObject>, IResult> action)
        => WithSpace(spaceId, space => space.Collections.TryGetValue(collection.ToLowerInvariant(), out var items)
            ? action(space, items)
            : NotFound($"The fake Octopus Server does not have a {collection} collection"));

    IResult WithRegistrationCollection(string spaceId, string collection, Func<FakeSpace, List<JsonObject>, IResult> action)
        => RegistrationCollections.Contains(collection.ToLowerInvariant())
            ? WithCollection(spaceId, collection, action)
            : NotFound($"The fake Octopus Server only accepts writes to {string.Join(", ", RegistrationCollections)}");

    /// <summary>Applies the query parameters Octopus.Client uses to look things up. Unknown parameters are ignored.</summary>
    static List<JsonObject> Filter(List<JsonObject> items, IQueryCollection query)
    {
        lock (items)
        {
            IEnumerable<JsonObject> result = items;

            if (query.TryGetValue("ids", out var ids) && !string.IsNullOrEmpty(ids))
            {
                var wanted = ids.SelectMany(i => i!.Split(',', StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                result = result.Where(i => wanted.Contains((string?)i["Id"] ?? ""));
            }

            if (query.TryGetValue("partialName", out var partialName) && !string.IsNullOrEmpty(partialName))
            {
                result = result.Where(i => ((string?)i["Name"] ?? "").Contains(partialName!, StringComparison.OrdinalIgnoreCase));
            }

            if (query.TryGetValue("name", out var name) && !string.IsNullOrEmpty(name))
            {
                result = result.Where(i => string.Equals((string?)i["Name"], name, StringComparison.OrdinalIgnoreCase));
            }

            if (query.TryGetValue("thumbprint", out var thumbprint) && !string.IsNullOrEmpty(thumbprint))
            {
                result = result.Where(i => string.Equals(RegisteredMachine.ThumbprintOf(i), thumbprint, StringComparison.OrdinalIgnoreCase));
            }

            return result.Select(i => (JsonObject)i.DeepClone()).ToList();
        }
    }

    static JsonObject Page(string itemType, string self, List<JsonObject> items)
        => new()
        {
            ["ItemType"] = itemType,
            ["TotalResults"] = items.Count,
            ["ItemsPerPage"] = Math.Max(items.Count, 30),
            ["NumberOfPages"] = 1,
            ["LastPageNumber"] = 0,
            ["Items"] = new JsonArray(items.ToArray<JsonNode?>()),
            ["Links"] = Links(("Self", self), ("Page.All", self + "/all"), ("Page.Current", self))
        };

    JsonObject RootDocument()
        => new()
        {
            ["Application"] = "Octopus Deploy",
            ["Version"] = ServerVersion,
            ["ApiVersion"] = "3.0.0",
            ["InstallationId"] = "00000000-0000-0000-0000-000000000000",
            ["Links"] = Links(
                ("Self", "/api"),
                ("CurrentUser", "/api/users/me"),
                ("Users", "/api/users{/id}{?skip,take,ids,filter}"),
                ("Spaces", "/api/spaces{/id}{?skip,ids,take,partialName}"),
                ("SpaceHome", "/api/{spaceId}"),
                ("CertificateConfiguration", "/api/configuration/certificates{/id}{?skip,take}"))
        };

    static JsonObject SpaceRootDocument(FakeSpace space)
    {
        var links = Links(("Self", $"/api/{space.Id}"));
        foreach (var (name, collection, extraParameters) in new[]
                 {
                     ("Environments", "environments", ""),
                     ("MachinePolicies", "machinepolicies", ""),
                     ("Machines", "machines", ",thumbprint,roles,isDisabled,healthStatuses,commStyles,tenantIds,tenantTags,environmentIds,deploymentId,shellNames,deploymentTargetTypes"),
                     ("Proxies", "proxies", ""),
                     ("TagSets", "tagsets", ""),
                     ("Tenants", "tenants", ",projectId,tags,clone"),
                     ("WorkerPools", "workerpools", ""),
                     ("Workers", "workers", ",thumbprint,isDisabled,healthStatuses,commStyles,workerPoolIds,shellNames")
                 })
        {
            links[name] = $"/api/{space.Id}/{collection}{{/id}}{{?skip,take,ids,name,partialName{extraParameters}}}";
        }

        return new JsonObject { ["Links"] = links };
    }

    static JsonObject SpaceResource(FakeSpace space)
        => new()
        {
            ["Id"] = space.Id,
            ["Name"] = space.Name,
            ["Description"] = "",
            ["IsDefault"] = string.Equals(space.Name, "Default", StringComparison.OrdinalIgnoreCase),
            ["TaskQueueStopped"] = false,
            ["SpaceManagersTeams"] = new JsonArray(),
            ["SpaceManagersTeamMembers"] = new JsonArray(),
            ["Links"] = Links(("Self", $"/api/spaces/{space.Id}"), ("SpaceHome", $"/api/{space.Id}"))
        };

    static JsonObject User()
        => new()
        {
            ["Id"] = UserId,
            ["Username"] = "tentacle-docker-tests",
            ["DisplayName"] = "Tentacle Docker Tests",
            ["IsActive"] = true,
            ["IsService"] = true,
            ["Links"] = Links(("Self", $"/api/users/{UserId}"), ("Spaces", $"/api/users/{UserId}/spaces"))
        };

    static JsonObject Links(params (string Name, string Href)[] links)
    {
        var result = new JsonObject();
        foreach (var (name, href) in links)
        {
            result[name] = href;
        }

        return result;
    }

    static IResult Json(JsonNode node, int statusCode = StatusCodes.Status200OK)
        => Results.Content(node.ToJsonString(), "application/json", statusCode: statusCode);

    static IResult NotFound(string message)
        => Json(new JsonObject { ["ErrorMessage"] = message, ["Errors"] = new JsonArray(message) }, StatusCodes.Status404NotFound);

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    public sealed class FakeSpace
    {
        public FakeSpace(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public Dictionary<string, List<JsonObject>> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>What a Tentacle told the fake server about itself when it registered.</summary>
public sealed record RegisteredMachine(
    string Id,
    string SpaceName,
    string Name,
    string? CommunicationStyle,
    string? Uri,
    string? Thumbprint,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> EnvironmentIds,
    string Json)
{
    internal static RegisteredMachine From(FakeOctopusServer.FakeSpace space, JsonObject resource)
    {
        var endpoint = resource["Endpoint"] as JsonObject;
        return new RegisteredMachine(
            (string)resource["Id"]!,
            space.Name,
            (string?)resource["Name"] ?? "",
            (string?)endpoint?["CommunicationStyle"] ?? (string?)resource["CommunicationStyle"],
            (string?)endpoint?["Uri"] ?? (string?)resource["Uri"],
            ThumbprintOf(resource),
            Strings(resource["Roles"]),
            Strings(resource["EnvironmentIds"]),
            resource.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static string? ThumbprintOf(JsonObject resource)
        => (string?)(resource["Endpoint"] as JsonObject)?["Thumbprint"] ?? (string?)resource["Thumbprint"];

    static IReadOnlyList<string> Strings(JsonNode? node)
        => node is JsonArray array ? array.Select(n => (string?)n ?? "").ToList() : Array.Empty<string>();
}
