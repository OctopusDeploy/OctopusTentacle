using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Halibut;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Common;
using ILogger = Serilog.ILogger;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>
/// A stand-in Octopus Server for one test, built with <see cref="FakeOctopusServerBuilder"/>. It has two parts:
/// just enough of the REST API for `tentacle register-with` (via Octopus.Client) to register a deployment target or
/// worker, and a server HalibutRuntime that polling Tentacles connect to and that tests use to call any Tentacle.
///
/// It is cheap to start, so every test gets its own: nothing is shared between tests, and Halibut's and the fake's
/// logs end up in the output of the test that caused them.
///
/// Anything the fake does not know about is answered with a 404 and recorded in <see cref="UnhandledRequests"/>,
/// so a change in what Octopus.Client asks for shows up as a clear failure rather than a mystery.
///
/// Responses are built from Octopus.Client's own resource types, so they have the shapes the client expects. They are
/// serialized to JSON as soon as they are built: Octopus.Client keeps its serializer internal, and lookups, paging and
/// registered machines are simplest to handle as JSON.
/// </summary>
public sealed class FakeOctopusServer : IAsyncDisposable
{
    public const string ServerVersion = "2025.1.0";
    public const string UserId = "Users-1";

    /// <summary>Every space has this worker pool, as a new Octopus Server does.</summary>
    public const string DefaultWorkerPoolName = "Default Worker Pool";
    public const string DefaultWorkerPoolId = "WorkerPools-1";

    static readonly Guid InstallationId = Guid.Empty;

    /// <summary>
    /// The cookies a real server sets on sign-in, suffixed with its installation ID as a real server's are: the session
    /// (whose value is looked up in the sessions) and the anti-forgery token, which Octopus.Client requires.
    /// </summary>
    static readonly string SessionCookieName = $"OctopusIdentificationToken_{InstallationId}";
    static readonly string CsrfCookieName = $"Octopus-Csrf-Token_{InstallationId}";

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

    const string UnhandledItemKey = "FakeOctopusServer.Unhandled";

    /// <summary>
    /// Writes Octopus.Client resources the way its own (internal) Newtonsoft settings do: links as plain strings and
    /// enums by name.
    /// </summary>
    static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new HrefConverter(), new JsonStringEnumConverter() }
    };

    readonly WebApplication app;
    readonly HalibutRuntime halibut;
    readonly string serverThumbprint;
    readonly ConcurrentDictionary<string, FakeSpace> spacesById = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<(string SpaceName, string MachineName), TaskCompletionSource<RegisteredMachine>> registrations = new();
    readonly ConcurrentQueue<string> unhandledRequests = new();
    readonly ConcurrentDictionary<(string SpaceName, string MachineName), int> registrationCounts = new();
    readonly ConcurrentDictionary<string, string> sessions = new();
    readonly ConcurrentQueue<LoggedRequest> requestLog = new();
    readonly object spacesLock = new();
    int nextSpaceNumber;
    int nextResourceNumber;

    FakeOctopusServer(WebApplication app, HalibutRuntime halibut, string serverThumbprint)
    {
        this.app = app;
        this.halibut = halibut;
        this.serverThumbprint = serverThumbprint;
    }

    /// <summary>The port the REST API listens on.</summary>
    public int Port { get; private set; }

    /// <summary>The port polling Tentacles connect to, i.e. what a real server exposes as 10943.</summary>
    public int PollingPort { get; private set; }

    /// <summary>The server side of Halibut, for building TentacleClients that talk to registered Tentacles.</summary>
    public HalibutRuntime Halibut => halibut;

    public IReadOnlyCollection<string> UnhandledRequests => unhandledRequests.ToArray();

    /// <summary>
    /// The requests and registrations the server has seen, in order. Requests are served on Kestrel threads, which
    /// have no NUnit test context to log to, so this is written to the test's output when the server is disposed.
    /// </summary>
    public IReadOnlyList<string> RequestLog => requestLog.Select(r => $"{r.Timestamp:HH:mm:ss.fff} {r.Text}").ToList();

    internal static async Task<FakeOctopusServer> Start(IReadOnlyDictionary<string, string[]> spaces, ILogger logger)
    {
        var halibut = TestServerHalibut.BuildServerHalibutRuntime();
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            // Any rather than Loopback: Tentacle containers reach us through the Docker host gateway.
            builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Any, 0));

            var app = builder.Build();
            // Registering Tentacles trust the certificate the server HalibutRuntime presents.
            var server = new FakeOctopusServer(app, halibut, TestCertificates.ServerPublicThumbprint);
            server.MapRoutes();

            foreach (var (name, environmentNames) in spaces)
            {
                server.AddSpace(name, environmentNames);
            }

            // Any rather than Loopback, for the same reason as the REST API.
            server.PollingPort = halibut.Listen(new IPEndPoint(IPAddress.Any, 0));

            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            server.Port = new Uri(address).Port;
            logger.ForContext<FakeOctopusServer>().Information("Fake Octopus Server listening on {Address}, polling port {PollingPort}", address, server.PollingPort);

            return server;
        }
        catch
        {
            await halibut.DisposeAsync();
            throw;
        }
    }

    /// <summary>Adds a space with the given environments, plus the default machine policy every Tentacle asks for.</summary>
    void AddSpace(string name, string[] environmentNames)
    {
        lock (spacesLock)
        {
            if (spacesById.Values.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A space named '{name}' already exists");
            }

            var space = CreateSpace($"Spaces-{++nextSpaceNumber}", name, environmentNames);
            spacesById[space.Id] = space;
        }
    }

    FakeSpace CreateSpace(string id, string name, string[] environmentNames)
    {
        var space = new FakeSpace(id, name);

        foreach (var collection in SpaceCollections)
        {
            space.Collections[collection] = new List<JsonObject>();
        }

        var machinePolicy = new MachinePolicyResource
        {
            Id = "MachinePolicies-1",
            Name = "Default Machine Policy",
            IsDefault = true,
            SpaceId = id
        };
        machinePolicy.Links.Add("Self", $"/api/{id}/machinepolicies/{machinePolicy.Id}");
        space.Collections["machinepolicies"].Add(ToJson(machinePolicy));

        foreach (var environmentName in environmentNames)
        {
            var environment = new EnvironmentResource
            {
                Id = $"Environments-{Interlocked.Increment(ref nextResourceNumber)}",
                Name = environmentName,
                SpaceId = id,
                AllowDynamicInfrastructure = true
            };
            environment.Links.Add("Self", $"/api/{id}/environments/{environment.Id}");
            space.Collections["environments"].Add(ToJson(environment));
        }

        var workerPool = new StaticWorkerPoolResource
        {
            Id = DefaultWorkerPoolId,
            Name = DefaultWorkerPoolName,
            IsDefault = true,
            SpaceId = id
        };
        workerPool.Links.Add("Self", $"/api/{id}/workerpools/{workerPool.Id}");
        space.Collections["workerpools"].Add(ToJson(workerPool));

        return space;
    }

    /// <summary>How many times a machine has registered (created or updated itself), e.g. to check a restart did not.</summary>
    public int RegistrationCount(string spaceName, string machineName)
        => registrationCounts.TryGetValue((spaceName.ToLowerInvariant(), machineName.ToLowerInvariant()), out var count) ? count : 0;

    public Task<RegisteredMachine> WaitForRegistration(string spaceName, string machineName)
        => RegistrationFor(spaceName, machineName).Task;

    TaskCompletionSource<RegisteredMachine> RegistrationFor(string spaceName, string machineName)
        => registrations.GetOrAdd((spaceName.ToLowerInvariant(), machineName.ToLowerInvariant()), _ => new TaskCompletionSource<RegisteredMachine>(TaskCreationOptions.RunContinuationsAsynchronously));

    void MapRoutes()
    {
        app.Use(async (context, next) =>
        {
            var timestamp = DateTimeOffset.UtcNow;
            await next();
            var unhandled = context.Items.ContainsKey(UnhandledItemKey) ? " (unhandled)" : "";
            requestLog.Enqueue(new LoggedRequest(
                timestamp,
                $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode}{unhandled}"));
        });

        app.MapGet("/api", () => Json(ToJson(RootDocument())));

        app.MapGet("/api/users/me", () => Json(ToJson(User())));

        // Username/password sign-in. Any credentials are accepted; what matters is recording which ones Tentacle sent.
        app.MapPost("/api/users/login", (JsonObject body, HttpContext context) =>
        {
            var token = $"session-{Guid.NewGuid():N}";
            sessions[token] = $"user {(string?)body["Username"]} with password {(string?)body["Password"]}";
            context.Response.Cookies.Append(SessionCookieName, token, new CookieOptions { HttpOnly = true, Path = "/" });
            context.Response.Cookies.Append(CsrfCookieName, Guid.NewGuid().ToString("N"), new CookieOptions { Path = "/" });
            return Json(ToJson(User()));
        });
        app.MapGet("/api/users/{id}", (string id) => id == UserId ? Json(ToJson(User())) : NotFound($"User {id} not found"));
        app.MapGet("/api/users/{id}/spaces", (string id) => Json(new JsonArray(Spaces().ToArray<JsonNode?>())));

        app.MapGet("/api/spaces", () => Json(Page("Space", "/api/spaces", Spaces())));
        app.MapGet("/api/spaces/all", () => Json(new JsonArray(Spaces().ToArray<JsonNode?>())));
        app.MapGet("/api/spaces/{spaceId}", (string spaceId) => WithSpace(spaceId, space => Json(ToJson(SpaceResource(space)))));

        app.MapGet("/api/configuration/certificates/certificate-global", () => Json(ToJson(Certificate())));

        app.MapGet("/api/{spaceId}", (string spaceId) => WithSpace(spaceId, space => Json(ToJson(SpaceRootDocument(space)))));

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

        app.MapPost("/api/{spaceId}/{collection}", (string spaceId, string collection, JsonObject body, HttpContext context)
            => WithRegistrationCollection(spaceId, collection, (space, items) => Register(space, collection, items, body, existingId: null, context.Request)));

        // Octopus.Client sends updates as a POST with an X-HTTP-Method-Override: PUT header, so accept both.
        app.MapMethods("/api/{spaceId}/{collection}/{id}", new[] { HttpMethods.Put, HttpMethods.Post }, (string spaceId, string collection, string id, JsonObject body, HttpContext context)
            => WithRegistrationCollection(spaceId, collection, (space, items) => Register(space, collection, items, body, existingId: id, context.Request)));

        app.MapFallback(() => Unhandled());
    }

    /// <summary>
    /// How a request authenticated: an API key, a bearer token, or a session from signing in with a username and
    /// password. The fake accepts anything, so this is how tests check the right credentials reached it.
    /// </summary>
    string DescribeAuthentication(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Octopus-ApiKey", out var apiKey) && !string.IsNullOrEmpty(apiKey))
        {
            return $"api key {apiKey}";
        }

        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return $"bearer token {authorization["Bearer ".Length..]}";
        }

        if (request.Cookies.TryGetValue(SessionCookieName, out var token) && token is not null && sessions.TryGetValue(token, out var session))
        {
            return session;
        }

        return "none";
    }

    IResult Register(FakeSpace space, string collection, List<JsonObject> items, JsonObject body, string? existingId, HttpRequest request)
    {
        var prefix = collection == "workers" ? "Workers" : "Machines";
        var id = existingId ?? $"{prefix}-{Interlocked.Increment(ref nextResourceNumber)}";

        body["Id"] = id;
        body["SpaceId"] = space.Id;
        body["Links"] = new JsonObject { ["Self"] = $"/api/{space.Id}/{collection}/{id}" };

        lock (items)
        {
            items.RemoveAll(i => string.Equals((string?)i["Id"], id, StringComparison.OrdinalIgnoreCase));
            items.Add(body);
        }

        var machine = RegisteredMachine.From(space, collection, body, DescribeAuthentication(request));
        requestLog.Enqueue(new LoggedRequest(DateTimeOffset.UtcNow, $"Registered {machine.Name} in {space.Name}: {machine.CommunicationStyle} {machine.Uri} {machine.Thumbprint}"));

        // A real server trusts a Tentacle once it is registered. Polling Tentacles start connecting straight after
        // registration, so trust them before anyone waiting for the registration hears about it.
        if (machine.Thumbprint is not null)
        {
            halibut.Trust(machine.Thumbprint);
        }

        registrationCounts.AddOrUpdate((space.Name.ToLowerInvariant(), machine.Name.ToLowerInvariant()), 1, (_, count) => count + 1);
        RegistrationFor(space.Name, machine.Name).TrySetResult(machine);

        return Json(body.DeepClone(), existingId is null ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }

    // An unknown space or collection is almost always a path the fake does not implement (e.g. the generic
    // /api/{spaceId}/{collection}/{id} route catching /api/configuration/certificates/x), so treat it as unhandled
    // rather than as an ordinary 404.
    IResult WithSpace(string spaceId, Func<FakeSpace, IResult> action)
        => spacesById.TryGetValue(spaceId, out var space) ? action(space) : Unhandled();

    IResult WithCollection(string spaceId, string collection, Func<FakeSpace, List<JsonObject>, IResult> action)
        => WithSpace(spaceId, space => space.Collections.TryGetValue(collection.ToLowerInvariant(), out var items)
            ? action(space, items)
            : Unhandled());

    IResult WithRegistrationCollection(string spaceId, string collection, Func<FakeSpace, List<JsonObject>, IResult> action)
        => RegistrationCollections.Contains(collection.ToLowerInvariant())
            ? WithCollection(spaceId, collection, action)
            : Unhandled();

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
            ["Links"] = new JsonObject { ["Self"] = self, ["Page.All"] = self + "/all", ["Page.Current"] = self }
        };

    static RootResource RootDocument()
    {
        var root = new RootResource
        {
            Application = "Octopus Deploy",
            Version = ServerVersion,
            ApiVersion = "3.0.0",
            InstallationId = InstallationId
        };
        root.Links.Add("Self", "/api");
        root.Links.Add("CurrentUser", "/api/users/me");
        root.Links.Add("SignIn", "/api/users/login");
        root.Links.Add("Users", "/api/users{/id}{?skip,take,ids,filter}");
        root.Links.Add("Spaces", "/api/spaces{/id}{?skip,ids,take,partialName}");
        root.Links.Add("SpaceHome", "/api/{spaceId}");
        root.Links.Add("CertificateConfiguration", "/api/configuration/certificates{/id}{?skip,take}");
        return root;
    }

    CertificateConfigurationResource Certificate()
    {
        var certificate = new CertificateConfigurationResource
        {
            Id = "certificate-global",
            Name = "Octopus Server",
            Thumbprint = serverThumbprint
        };
        certificate.Links.Add("Self", "/api/configuration/certificates/certificate-global");
        return certificate;
    }

    static SpaceRootResource SpaceRootDocument(FakeSpace space)
    {
        var spaceRoot = new SpaceRootResource();
        spaceRoot.Links.Add("Self", $"/api/{space.Id}");
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
            spaceRoot.Links.Add(name, $"/api/{space.Id}/{collection}{{/id}}{{?skip,take,ids,name,partialName{extraParameters}}}");
        }

        return spaceRoot;
    }

    List<JsonObject> Spaces() => spacesById.Values.OrderBy(s => s.Id).Select(s => ToJson(SpaceResource(s))).ToList();

    static SpaceResource SpaceResource(FakeSpace space)
    {
        var resource = new SpaceResource
        {
            Id = space.Id,
            Name = space.Name,
            Description = "",
            IsDefault = string.Equals(space.Name, "Default", StringComparison.OrdinalIgnoreCase),
            TaskQueueStopped = false
        };
        resource.Links.Add("Self", $"/api/spaces/{space.Id}");
        resource.Links.Add("SpaceHome", $"/api/{space.Id}");
        return resource;
    }

    static UserResource User()
    {
        var user = new UserResource
        {
            Id = UserId,
            Username = "tentacle-docker-tests",
            DisplayName = "Tentacle Docker Tests",
            IsActive = true,
            IsService = true
        };
        user.Links.Add("Self", $"/api/users/{UserId}");
        user.Links.Add("Spaces", $"/api/users/{UserId}/spaces");
        return user;
    }

    static JsonObject ToJson(Resource resource)
        => JsonSerializer.SerializeToNode(resource, resource.GetType(), SerializerOptions)!.AsObject();

    static IResult Json(JsonNode node, int statusCode = StatusCodes.Status200OK)
        => Results.Content(node.ToJsonString(), "application/json", statusCode: statusCode);

    static IResult NotFound(string message)
        => Json(new JsonObject { ["ErrorMessage"] = message, ["Errors"] = new JsonArray(message) }, StatusCodes.Status404NotFound);

    public async ValueTask DisposeAsync()
    {
        TestContext.Out.WriteLine("---- fake Octopus Server requests ----");
        foreach (var request in RequestLog)
        {
            TestContext.Out.WriteLine(request);
        }

        try
        {
            await halibut.DisposeAsync();
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    /// <summary>Octopus.Client's links are <see cref="Href"/>s, which it writes as plain strings.</summary>
    sealed class HrefConverter : JsonConverter<Href>
    {
        public override Href Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString());

        public override void Write(Utf8JsonWriter writer, Href value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.AsString());
    }

    IResult Unhandled() => new UnhandledResult(this);

    /// <summary>A 404 for a request the fake does not implement, recorded in <see cref="UnhandledRequests"/>.</summary>
    sealed class UnhandledResult : IResult
    {
        readonly FakeOctopusServer server;

        public UnhandledResult(FakeOctopusServer server)
        {
            this.server = server;
        }

        public Task ExecuteAsync(HttpContext context)
        {
            var request = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
            server.unhandledRequests.Enqueue(request);
            context.Items[UnhandledItemKey] = true;
            return NotFound($"The fake Octopus Server does not handle {request}").ExecuteAsync(context);
        }
    }

    sealed record LoggedRequest(DateTimeOffset Timestamp, string Text);

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
    bool IsWorker,
    IReadOnlyList<string> WorkerPoolIds,
    string Authentication,
    string Json)
{
    internal static RegisteredMachine From(FakeOctopusServer.FakeSpace space, string collection, JsonObject resource, string authentication)
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
            string.Equals(collection, "workers", StringComparison.OrdinalIgnoreCase),
            Strings(resource["WorkerPoolIds"]),
            authentication,
            resource.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static string? ThumbprintOf(JsonObject resource)
        => (string?)(resource["Endpoint"] as JsonObject)?["Thumbprint"] ?? (string?)resource["Thumbprint"];

    static IReadOnlyList<string> Strings(JsonNode? node)
        => node is JsonArray array ? array.Select(n => (string?)n ?? "").ToList() : Array.Empty<string>();
}
