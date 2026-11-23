using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>
/// A repository as one SCM lists it. <c>Id</c> is the location ID: github:&lt;org&gt;/&lt;repo&gt; or azdo:&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;.
/// <c>Skipped</c> is "disabled" or "empty" for a repository that cannot be scanned, otherwise null.
/// </summary>
public sealed record RemoteRepo(string Id, string Name, string? Project, string? DefaultBranch, string? Tip, bool Archived, string? Skipped)
{
    public bool IsGitHub => Id.StartsWith("github:");
    /// <summary>The atlas folder name: &lt;project&gt;-&lt;repo&gt; on Azure DevOps, the repository's own name on GitHub.</summary>
    public string Folder => Ids.Slug(Project is null ? Name : $"{Project}-{Name}");
}

/// <summary>API tokens from the environment or an existing gh / az sign-in. Nothing is stored and nothing prompts.</summary>
public static class Credentials
{
    const string AzureDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";

    public static AuthenticationHeaderValue? GitHub() =>
        (Env("GITHUB_TOKEN") ?? Run("gh", "auth", "token")) is { } token ? new("Bearer", token) : null;

    public static AuthenticationHeaderValue? AzureDevOps() =>
        Env("AZURE_DEVOPS_EXT_PAT") is { } pat ? new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + pat)))
        : Run(OperatingSystem.IsWindows() ? "az.cmd" : "az", "account", "get-access-token", "--resource", AzureDevOpsResource, "--query", "accessToken", "--output", "tsv") is { } token ? new("Bearer", token)
        : null;

    static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v.Trim() : null;

    /// <summary>A command's trimmed output, or null when it is not installed, fails or prints nothing. Its input is closed so it cannot ask.</summary>
    static string? Run(string command, params string[] args)
    {
        var info = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var a in args) info.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(info)!;
            p.StandardInput.Close();
            var error = p.StandardError.ReadToEndAsync();
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            _ = error.Result;
            return p.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
}

/// <summary>The GitHub and Azure DevOps REST calls a listing needs. Requests run one at a time. Pass a handler to replay recorded responses.</summary>
public sealed class Scm(HttpMessageHandler? handler, AuthenticationHeaderValue? gitHub, AuthenticationHeaderValue? azureDevOps)
{
    const string GitHubApi = "https://api.github.com", AzureDevOpsApi = "https://dev.azure.com", ApiVersion = "api-version=7.1";
    readonly HttpClient _http = handler is null ? new HttpClient() : new HttpClient(handler);

    /// <summary>Credentials for the SCMs the config names, from the environment or gh / az.</summary>
    public static Scm FromEnvironment(AtlasConfig config) =>
        new(null, Repos.Orgs(config.Github, config.Allow.Github.Keys).Count == 0 ? null : Credentials.GitHub(),
            Repos.Orgs(config.AzureDevops, config.Allow.AzureDevops.Keys).Count == 0 ? null : Credentials.AzureDevOps());

    /// <summary>https://github.com/&lt;org&gt;/&lt;repo&gt;.git or https://dev.azure.com/&lt;org&gt;/&lt;project&gt;/_git/&lt;repo&gt;.</summary>
    public static string CloneUrl(string locationId)
    {
        var p = locationId[(locationId.IndexOf(':') + 1)..].Split('/').Select(Uri.EscapeDataString).ToArray();
        return locationId.StartsWith("github:") && p.Length == 2 ? $"https://github.com/{p[0]}/{p[1]}.git"
            : locationId.StartsWith("azdo:") && p.Length == 3 ? $"{AzureDevOpsApi}/{p[0]}/{p[1]}/_git/{p[2]}"
            : throw new UsageException($"'{locationId}' is not a location: expected github:<org>/<repo> or azdo:<org>/<project>/<repo>");
    }

    /// <summary>
    /// Every repository of a GitHub organisation, or of a user account when the name is not an organisation. One that
    /// <paramref name="allowed"/> refuses is returned skipped, without reading its tip.
    /// </summary>
    public async Task<List<RemoteRepo>> GitHubRepos(string org, Func<string, bool>? allowed = null)
    {
        var o = Uri.EscapeDataString(org);
        var page = await Get($"{GitHubApi}/orgs/{o}/repos?per_page=100");
        if (page.Status == HttpStatusCode.NotFound) page = await Get($"{GitHubApi}/users/{o}/repos?per_page=100");
        if (page.Status == HttpStatusCode.NotFound)
            throw new UsageException($"api.github.com has no organisation or user '{org}' that this sign-in can see. Check github.organisation in {Atlas.ConfigFile}, and that GITHUB_TOKEN or 'gh auth login' is for an account that belongs to it.");
        var repos = new List<RemoteRepo>();
        while (true)
        {
            foreach (var r in page.Json.EnumerateArray())
            {
                var name = r.GetProperty("name").GetString()!;
                var branch = Str(r, "default_branch");
                string? tip = null, skipped = allowed?.Invoke($"github:{org}/{name}") == false ? "not allowed" : Flag(r, "disabled") ? "disabled" : null;
                if (skipped is null)
                {
                    // ponytail: one request per repository for the tip. GraphQL returns tips with the listing but needs a token; switch when sweeps get slow.
                    var reference = branch is null ? default : await Get($"{GitHubApi}/repos/{o}/{Uri.EscapeDataString(name)}/git/ref/heads/{Segments(branch)}");
                    tip = reference.Status == HttpStatusCode.OK ? reference.Json.GetProperty("object").GetProperty("sha").GetString() : null;
                    if (tip is null) skipped = "empty";
                }
                repos.Add(new($"github:{org}/{name}", name, null, branch, tip, Flag(r, "archived"), skipped));
            }
            if (page.Next is null) return repos;
            page = await Get(page.Next);
        }
    }

    /// <summary>
    /// Every repository across an Azure DevOps organisation's projects. Without a credential the organisation cannot be
    /// listed, so only the public projects in <paramref name="knownProjects"/> are read. One that <paramref name="allowed"/>
    /// refuses is returned skipped, without reading its tip.
    /// </summary>
    public async Task<List<RemoteRepo>> AzureDevOpsRepos(string org, IEnumerable<string> knownProjects, Func<string, bool>? allowed = null)
    {
        var o = Uri.EscapeDataString(org);
        var items = new List<JsonElement>();
        var all = await Raw($"{AzureDevOpsApi}/{o}/_apis/git/repositories?{ApiVersion}");
        if (all.Status == HttpStatusCode.Unauthorized && azureDevOps is null && knownProjects.Any())
            foreach (var project in knownProjects.Distinct(StringComparer.OrdinalIgnoreCase))
                items.AddRange(Check(await Raw($"{AzureDevOpsApi}/{o}/{Uri.EscapeDataString(project)}/_apis/git/repositories?{ApiVersion}"), HttpStatusCode.OK).Json.GetProperty("value").EnumerateArray());
        else
            items.AddRange(Check(all, HttpStatusCode.OK).Json.GetProperty("value").EnumerateArray());

        var repos = new List<RemoteRepo>();
        foreach (var r in items)
        {
            var name = r.GetProperty("name").GetString()!;
            var project = r.GetProperty("project").GetProperty("name").GetString()!;
            var reference = Str(r, "defaultBranch");
            string? tip = null, skipped = allowed?.Invoke($"azdo:{org}/{project}/{name}") == false ? "not allowed" : Flag(r, "isDisabled") ? "disabled" : reference is null ? "empty" : null;
            if (skipped is null)
            {
                var refs = await Get($"{AzureDevOpsApi}/{o}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{r.GetProperty("id").GetString()}/refs?filter={Segments(reference!["refs/".Length..])}&{ApiVersion}");
                // The filter is a prefix match, so heads/main also returns heads/main-old.
                tip = refs.Status != HttpStatusCode.OK ? null
                    : refs.Json.GetProperty("value").EnumerateArray().Where(v => Str(v, "name") == reference).Select(v => Str(v, "objectId")).FirstOrDefault();
                if (tip is null) skipped = "empty";
            }
            repos.Add(new($"azdo:{org}/{project}/{name}", name, project, reference?.Replace("refs/heads/", ""), tip, false, skipped));
        }
        return repos;
    }

    /// <summary>Whether a commit exists in a repository.</summary>
    public async Task<bool> HasCommit(RemoteRepo repo, string sha)
    {
        var p = repo.Id[(repo.Id.IndexOf(':') + 1)..].Split('/').Select(Uri.EscapeDataString).ToArray();
        var url = repo.IsGitHub
            ? $"{GitHubApi}/repos/{p[0]}/{p[1]}/commits/{sha}"
            : $"{AzureDevOpsApi}/{p[0]}/{p[1]}/_apis/git/repositories/{p[2]}/commits/{sha}?{ApiVersion}";
        return (await Get(url)).Status == HttpStatusCode.OK;
    }

    /// <summary>
    /// Successful GitHub migration records of an organisation: target repository name to source URL. Empty, without
    /// complaint, when there is no token or the token cannot read them.
    /// </summary>
    public async Task<Dictionary<string, string>> GitHubMigrations(string org)
    {
        const string query = "query($org:String!,$after:String){organization(login:$org){repositoryMigrations(first:100,after:$after){nodes{sourceUrl repositoryName state} pageInfo{hasNextPage endCursor}}}}";
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (gitHub is null) return found;
        for (string? after = null; ;)
        {
            var reply = await Raw($"{GitHubApi}/graphql", JsonSerializer.Serialize(new { query, variables = new { org, after } }));
            if (reply.Status != HttpStatusCode.OK
                || !reply.Json.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("organization", out var organisation) || organisation.ValueKind != JsonValueKind.Object
                || !organisation.TryGetProperty("repositoryMigrations", out var migrations) || migrations.ValueKind != JsonValueKind.Object)
                return found;
            foreach (var m in migrations.GetProperty("nodes").EnumerateArray())
                if (m.ValueKind == JsonValueKind.Object && Str(m, "state") == "SUCCEEDED" && Str(m, "repositoryName") is { } name && Str(m, "sourceUrl") is { } source)
                    found[name] = source;
            var info = migrations.GetProperty("pageInfo");
            if (!Flag(info, "hasNextPage") || (after = Str(info, "endCursor")) is null) return found;
        }
    }

    static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    static string Segments(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    readonly record struct Reply(Uri Url, HttpStatusCode Status, JsonElement Json, string? Next, bool RateLimited, string Text);

    /// <summary>A GET whose answer must be usable: a refused sign-in or a server error is reported; 404 and 409 are left to the caller.</summary>
    async Task<Reply> Get(string url) => Check(await Raw(url));

    Reply Check(Reply r, HttpStatusCode? required = null)
    {
        var host = r.Url.Host;
        var isGitHub = host == "api.github.com";
        if (r.RateLimited)
            throw new UsageException($"{host} refused the request: the rate limit is used up. " + (gitHub is null
                ? "Requests without a sign-in get 60 an hour: set GITHUB_TOKEN, or sign in with 'gh auth login'."
                : "Wait for the limit to reset (at most an hour) and run the command again."));
        if (r.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UsageException((isGitHub ? gitHub : azureDevOps) is null
                ? isGitHub
                    ? $"{host} needs a sign-in for {r.Url.AbsolutePath}: set GITHUB_TOKEN to a token that can read the organisation's repositories, or sign in with 'gh auth login'."
                    : $"{host} needs a sign-in for {r.Url.AbsolutePath}: set AZURE_DEVOPS_EXT_PAT to a personal access token with the Code (read) scope, or sign in with 'az login'. Without one, only public projects named in a locations mapping in {Atlas.ConfigFile} can be read."
                : isGitHub
                    ? $"{host} refused the sign-in for {r.Url.AbsolutePath} ({(int)r.Status}). Check that GITHUB_TOKEN (or the 'gh auth token' sign-in) is valid, can read the organisation's repositories, and is authorised for its single sign-on."
                    : $"{host} refused the sign-in for {r.Url.AbsolutePath} ({(int)r.Status}). Check that AZURE_DEVOPS_EXT_PAT (or the 'az login' sign-in) is valid, not expired, and has the Code (read) scope for this organisation.");
        if ((int)r.Status >= 500 || (required is { } want && r.Status != want))
            throw new UsageException($"{host} answered {(int)r.Status} for {r.Url.AbsolutePath}. Check the organisation name in {Atlas.ConfigFile}; if it is right, try again later. {Short(r.Text)}".TrimEnd());
        return r;
    }

    static string Short(string text) => text.Length > 200 ? text[..200] : text;

    /// <summary>One request. Only a network failure throws. An Azure DevOps redirect to its sign-in page is reported as 401.</summary>
    async Task<Reply> Raw(string url, string? post = null)
    {
        var uri = new Uri(url);
        var isGitHub = uri.Host == "api.github.com";
        using var request = new HttpRequestMessage(post is null ? HttpMethod.Get : HttpMethod.Post, uri);
        request.Headers.UserAgent.ParseAdd("cartographer/" + Clock.Version);
        request.Headers.Accept.ParseAdd(isGitHub ? "application/vnd.github+json" : "application/json");
        request.Headers.Authorization = isGitHub ? gitHub : azureDevOps;
        if (post is not null) request.Content = new StringContent(post, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            var status = response.StatusCode;
            JsonElement json = default;
            if (response.Content.Headers.ContentType?.MediaType?.EndsWith("json") == true && text.Length > 0)
                try { json = JsonDocument.Parse(text).RootElement.Clone(); } catch (JsonException) { }
            // Azure DevOps answers a request it will not serve with 203 or a redirect to an HTML sign-in page.
            if (!isGitHub && (status is HttpStatusCode.NonAuthoritativeInformation or HttpStatusCode.Found || (status == HttpStatusCode.OK && json.ValueKind == JsonValueKind.Undefined)))
                status = HttpStatusCode.Unauthorized;
            var next = response.Headers.TryGetValues("Link", out var links) && Regex.Match(string.Join(",", links), "<([^>]+)>;\\s*rel=\"next\"") is { Success: true } m ? m.Groups[1].Value : null;
            var limited = isGitHub && status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                && response.Headers.TryGetValues("x-ratelimit-remaining", out var left) && left.FirstOrDefault() == "0";
            return new(uri, status, json, next, limited, text);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new UsageException($"cannot reach {uri.Host}: {e.Message}. Check the network connection, any proxy settings (HTTPS_PROXY), and that the host is not blocked.");
        }
    }
}
