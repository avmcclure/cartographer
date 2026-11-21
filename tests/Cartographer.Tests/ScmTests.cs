using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Cartographer.Tests;

/// <summary>
/// Replays recorded/&lt;name&gt;.json: a map of request URL to {status, link, body}. Later files override earlier ones.
/// A URL that is not recorded answers 404. A body that is a string is served as an HTML page.
/// </summary>
sealed class Recorded : HttpMessageHandler
{
    readonly Dictionary<string, JsonElement> _replies = [];
    public List<string> Requests { get; } = [];

    public Recorded(params string[] names) { foreach (var name in names) Add(name); }

    public void Add(string name, [CallerFilePath] string here = "")
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "recorded", name + ".json")));
        foreach (var p in doc.RootElement.EnumerateObject()) _replies[p.Name] = p.Value.Clone();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add(url);
        if (!_replies.TryGetValue(url, out var r))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"message\":\"Not Found\"}", Encoding.UTF8, "application/json") });
        var body = r.GetProperty("body");
        var response = new HttpResponseMessage(r.TryGetProperty("status", out var s) ? (HttpStatusCode)s.GetInt32() : HttpStatusCode.OK)
        {
            Content = body.ValueKind == JsonValueKind.String ? new StringContent(body.GetString()!, Encoding.UTF8, "text/html") : new StringContent(body.GetRawText(), Encoding.UTF8, "application/json"),
        };
        if (r.TryGetProperty("link", out var link)) response.Headers.TryAddWithoutValidation("Link", link.GetString());
        return Task.FromResult(response);
    }
}

public class ScmTests(ITestOutputHelper output)
{
    const string AcmeConfig = """
        organisation: Acme
        github: {organisation: acme}
        azure-devops: {organisation: acme}
        exclude: ["*-archive", "sandbox-*"]
        locations:
          repositories/web: ["github:acme/website"]
        """;
    static readonly AuthenticationHeaderValue Token = new("Bearer", "recorded");
    static string Sha(string two) => string.Concat(Enumerable.Repeat(two, 20));

    static Atlas NewAtlas(string config)
    {
        var dir = Directory.CreateTempSubdirectory("atlas-").FullName;
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile), config);
        return Atlas.Load(dir);
    }

    static async Task<(Atlas Atlas, Listing Listing, Recorded Http)> Acme(Action<Atlas>? before = null)
    {
        var atlas = NewAtlas(AcmeConfig);
        before?.Invoke(atlas);
        var http = new Recorded("acme");
        return (atlas, await Repos.Refresh(atlas, new Scm(http, Token, Token)), http);
    }

    static string[] Ids_(ScanState s) => s.Locations.Select(l => l.Id).ToArray();

    [Fact]
    public async Task Listing_follows_pages_and_reads_each_default_branch_tip()
    {
        var github = await new Scm(new Recorded("acme"), null, null).GitHubRepos("acme");
        Assert.Equal(["Finance-billing", "Finance-Core-ledger", "Ops-tools", "Legacy-Apps-payroll", "website", "empty-repo", "old-mirror", "docs-archive"], github.Select(r => r.Name));
        Assert.Equal(("github:acme/website", "main", Sha("77"), null), github.Where(r => r.Name == "website").Select(r => (r.Id, r.DefaultBranch, r.Tip, r.Skipped)).Single());
        Assert.Equal("empty", github.Single(r => r.Name == "empty-repo").Skipped);
        Assert.Equal("disabled", github.Single(r => r.Name == "old-mirror").Skipped);
        Assert.True(github.Single(r => r.Name == "docs-archive").Archived);

        var azdo = await new Scm(new Recorded("acme"), null, Token).AzureDevOpsRepos("acme", []);
        Assert.Equal(8, azdo.Count);
        var payroll = azdo.Single(r => r.Name == "payroll");
        Assert.Equal(("azdo:acme/Legacy Apps/payroll", "legacy-apps-payroll", "master", Sha("e1")), (payroll.Id, payroll.Folder, payroll.DefaultBranch, payroll.Tip));
        Assert.Equal(Sha("a1"), azdo.Single(r => r.Name == "billing").Tip);   // not main-old, which the prefix filter also returns
        Assert.Equal("disabled", azdo.Single(r => r.Name == "old-site").Skipped);
        Assert.Equal("empty", azdo.Single(r => r.Name == "scratch").Skipped);
    }

    [Fact]
    public async Task A_name_that_is_a_user_account_is_listed_through_the_user_endpoint()
    {
        var repos = await new Scm(new Recorded("user"), null, null).GitHubRepos("jane");
        Assert.Equal(("github:jane/dotfiles", Sha("41")), (repos.Single().Id, repos.Single().Tip));
    }

    [Fact]
    public async Task Refresh_records_linked_locations_and_lists_what_it_left_out()
    {
        var (atlas, listing, _) = await Acme();
        Assert.Equal(["finance-billing", "finance-core-ledger", "finance-core-ledger-2", "legacy-apps-payroll", "ops-tools", "ops-tools-github", "web"], ScanState.All(atlas));
        Assert.Equal(["azdo:acme/Ops/old-site", "azdo:acme/Ops/scratch", "github:acme/empty-repo", "github:acme/old-mirror"], listing.Skipped.Select(r => r.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["docs-archive", "sandbox-play"], listing.Excluded.Order(StringComparer.Ordinal));

        var billing = ScanState.Load(atlas, "finance-billing");
        Assert.Equal(["github:acme/Finance-billing", "azdo:acme/Finance/billing"], Ids_(billing));
        Assert.Equal("github:acme/Finance-billing", billing.Primary!.Id);
        Assert.Equal((Sha("a1"), "main", Fixtures.Date, false), (billing.Primary.Tip, billing.Primary.DefaultBranch, billing.Primary.Seen, billing.Primary.Gone));
        Assert.Equal("main", billing.Default!.Value.Name);
        Assert.Null(billing.Default.Value.State.ScannedCommit);

        // A mapping a person wrote decides the folder.
        Assert.Equal(["github:acme/website"], Ids_(ScanState.Load(atlas, "web")));

        var text = File.ReadAllText(ScanState.PathOf(atlas, "finance-billing"));
        Assert.Contains("  - id: github:acme/Finance-billing\n    primary: true\n    gone: false\n    default-branch: main\n    tip: " + Sha("a1") + "\n    seen: \"2026-01-01\"\n", text);
    }

    [Fact]
    public async Task The_naming_rule_matches_the_longest_project_name()
    {
        var (atlas, _, http) = await Acme();
        // Finance-Core-ledger could be Finance/Core-ledger or Finance-Core/ledger; the longer project name is tried first and shares the tip.
        Assert.Equal(["github:acme/Finance-Core-ledger", "azdo:acme/Finance-Core/ledger"], Ids_(ScanState.Load(atlas, "finance-core-ledger")));
        Assert.Equal(["azdo:acme/Finance/Core-ledger"], Ids_(ScanState.Load(atlas, "finance-core-ledger-2")));
        // Equal tips are a shared commit already: nothing is looked up.
        Assert.DoesNotContain(http.Requests, u => u.Contains("/commits/") && u.Contains("ledger"));
    }

    [Fact]
    public async Task A_name_match_without_a_shared_commit_stays_separate_and_asks()
    {
        var entry = new Entry { Id = Ids.Repository("ops-tools-github"), Kind = Kinds.Repository, Name = "Ops-tools", Summary = "Tools." };
        var (atlas, listing, http) = await Acme(a => a.Entries[entry.Id] = entry);
        Assert.Equal(["azdo:acme/Ops/tools"], Ids_(ScanState.Load(atlas, "ops-tools")));
        Assert.Equal(["github:acme/Ops-tools"], Ids_(ScanState.Load(atlas, "ops-tools-github")));
        Assert.Contains($"https://api.github.com/repos/acme/Ops-tools/commits/{Sha("d1")}", http.Requests);
        Assert.Contains($"https://dev.azure.com/acme/Ops/_apis/git/repositories/tools/commits/{Sha("99")}?api-version=7.1", http.Requests);
        Assert.Contains("azdo:acme/Ops/tools", listing.Repositories.Single(r => r.Folder == "ops-tools-github").Question);

        // The question is on the GitHub repository's entry, once, however often the listing is refreshed.
        await Repos.Refresh(atlas, new Scm(new Recorded("acme"), Token, Token));
        var question = Assert.Single(QuestionList.Parse(Atlas.Load(atlas.Root).Find(entry.Id)!.Body).Items);
        Assert.Equal(("needs-person", "cli", "person"), (question.State, question.RaisedBy, question.WaitingOn));
        Assert.Equal("Whether github:acme/Ops-tools is the same repository as azdo:acme/Ops/tools: the names match but no commit is shared.", question.Missing);
        Assert.DoesNotContain(Validator.Check(Atlas.Load(atlas.Root)), i => i.Error && i.Id == entry.Id);
    }

    [Fact]
    public async Task A_dismissed_look_alike_is_not_asked_about_again()
    {
        var entry = new Entry { Id = Ids.Repository("ops-tools-github"), Kind = Kinds.Repository, Name = "Ops-tools", Summary = "Tools.", Body = "## Open questions\n\n### Dismissed\n\n- Same as azdo:acme/Ops/tools: no, a rewrite." };
        var (atlas, _, _) = await Acme(a => a.Entries[entry.Id] = entry);
        Assert.Empty(QuestionList.Parse(atlas.Find(entry.Id)!.Body).Items);
    }

    [Fact]
    public async Task A_left_behind_copy_whose_tip_is_behind_is_linked_with_GitHub_primary()
    {
        var (atlas, _, http) = await Acme();
        var payroll = ScanState.Load(atlas, "legacy-apps-payroll");
        // GitHub has moved on to e2; the Azure DevOps tip e1 is still a commit on GitHub, which links them.
        Assert.Contains($"https://api.github.com/repos/acme/Legacy-Apps-payroll/commits/{Sha("e1")}", http.Requests);
        Assert.Equal([("github:acme/Legacy-Apps-payroll", true, Sha("e2")), ("azdo:acme/Legacy Apps/payroll", false, Sha("e1"))], payroll.Locations.Select(l => (l.Id, l.Primary, l.Tip!)));
        Assert.Equal("main", payroll.Default!.Value.Name);
    }

    [Fact]
    public async Task Excluded_folders_are_never_written()
    {
        var (atlas, _, _) = await Acme();
        Assert.False(File.Exists(ScanState.PathOf(atlas, "docs-archive")));
        Assert.False(File.Exists(ScanState.PathOf(atlas, "sandbox-play")));
        Assert.True(Repos.IsExcluded(atlas.Config, "sandbox-a-b"));
        Assert.False(Repos.IsExcluded(atlas.Config, "my-sandbox-x"));
    }

    [Fact]
    public async Task A_location_that_is_no_longer_listed_is_marked_gone()
    {
        var (atlas, _, _) = await Acme();
        var scanned = ScanState.Load(atlas, "web");
        scanned.Branches["main"].ScannedCommit = Sha("77");
        scanned.Save(atlas);
        Assert.Equal("unchanged", Repos.StateOf(scanned));

        await Repos.Refresh(atlas, new Scm(new Recorded("acme", "acme-later"), Token, Token));

        // The Azure DevOps copy of payroll was deleted: GitHub carries on.
        var payroll = ScanState.Load(atlas, "legacy-apps-payroll");
        Assert.Equal([("github:acme/Legacy-Apps-payroll", true, false), ("azdo:acme/Legacy Apps/payroll", false, true)], payroll.Locations.Select(l => (l.Id, l.Primary, l.Gone)));
        // Ops/tools had one location; with it gone the repository is gone, and its state file is kept.
        var tools = ScanState.Load(atlas, "ops-tools");
        Assert.True(tools.Locations.Single().Gone);
        Assert.Null(tools.Primary);

        // Columns are padded to line up; the padding is squeezed here.
        var table = System.Text.RegularExpressions.Regex.Replace(Repos.Table(atlas), " {2,}", " | ").Split('\n');
        Assert.Equal("FOLDER | LOCATIONS | BRANCH | TIP | STATE", table[0]);
        Assert.Contains("ops-tools | azdo:acme/Ops/tools (gone) | main | d1d1d1d | gone", table);
        Assert.Contains("ops-tools-github | github:acme/Ops-tools | main | 9999999 | new", table);
        Assert.Contains("web | github:acme/website | main | 7878787 | changed", table);
        Assert.Contains("finance-billing | github:acme/Finance-billing, azdo:acme/Finance/billing | main | a1a1a1a | new", table);
        Assert.Equal(Repos.Table(atlas).Split('\n')[0].IndexOf("STATE"), Repos.Table(atlas).Split('\n')[1].IndexOf("new"));
    }

    [Fact]
    public async Task A_migration_record_links_a_repository_that_was_renamed()
    {
        var atlas = NewAtlas("organisation: Acme\ngithub: {organisation: acme}\nazure-devops: {organisation: acme}\n");
        await Repos.Refresh(atlas, new Scm(new Recorded("migration"), Token, Token));
        Assert.Equal(["platform-api"], ScanState.All(atlas));
        Assert.Equal([("github:acme/gateway", true), ("azdo:acme/Platform/api", false)], ScanState.Load(atlas, "platform-api").Locations.Select(l => (l.Id, l.Primary)));

        // Without a token the record cannot be read: no request is made and nothing is linked.
        var anonymous = new Recorded("migration");
        atlas = NewAtlas("organisation: Acme\ngithub: {organisation: acme}\nazure-devops: {organisation: acme}\n");
        await Repos.Refresh(atlas, new Scm(anonymous, null, Token));
        Assert.Equal(["gateway", "platform-api"], ScanState.All(atlas));
        Assert.DoesNotContain("https://api.github.com/graphql", anonymous.Requests);
    }

    [Theory]
    [InlineData("32", "gateway", true)]            // the old copy's tip is the GitHub tip: one repository, in the folder it already had
    [InlineData("55", "gateway-github", false)]    // nothing shared: a look-alike, kept apart
    public async Task A_GitHub_repository_joins_an_unlisted_copy_only_on_a_shared_commit(string oldTip, string folder, bool joined)
    {
        var atlas = NewAtlas("organisation: Acme\ngithub: {organisation: acme}\n");
        new ScanState { Repository = Ids.Repository("gateway"), Locations = [new LocationState { Id = "azdo:acme/Platform/gateway", Primary = true, DefaultBranch = "main", Tip = Sha(oldTip) }] }.Save(atlas);
        var listing = await Repos.Refresh(atlas, new Scm(new Recorded("migration"), null, null));
        Assert.Equal("github:acme/gateway", ScanState.Load(atlas, folder).Primary!.Id);
        Assert.True(ScanState.Load(atlas, "gateway").Locations[0].Gone);
        Assert.Equal(joined, listing.Repositories.Single().Question is null);
        Assert.Equal(joined ? 2 : 1, ScanState.Load(atlas, "gateway").Locations.Count);
    }

    [Fact]
    public async Task A_missing_credential_says_what_to_set_up()
    {
        var e = await Assert.ThrowsAsync<UsageException>(() => new Scm(new Recorded("signin"), null, null).AzureDevOpsRepos("acme", []));
        Assert.Contains("dev.azure.com", e.Message);
        Assert.Contains("AZURE_DEVOPS_EXT_PAT", e.Message);
        Assert.Contains("az login", e.Message);
    }

    sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("nodename nor servname provided, or not known");
    }

    [Fact]
    public async Task A_network_failure_names_the_host()
    {
        var e = await Assert.ThrowsAsync<UsageException>(() => new Scm(new Offline(), null, null).GitHubRepos("acme"));
        Assert.StartsWith("cannot reach api.github.com:", e.Message);
    }

    [Fact]
    public void Clone_urls_follow_the_location_id()
    {
        Assert.Equal("https://github.com/acme/Finance-billing.git", Scm.CloneUrl("github:acme/Finance-billing"));
        Assert.Equal("https://dev.azure.com/acme/Legacy%20Apps/_git/payroll", Scm.CloneUrl("azdo:acme/Legacy Apps/payroll"));
        Assert.Throws<UsageException>(() => Scm.CloneUrl("gitlab:acme/x"));
    }

    static string Git(string dir, params string[] args) =>
        Checkout.Git(dir, ["-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", .. args]) ?? throw new InvalidOperationException($"git {string.Join(' ', args)} failed in {dir}");

    /// <summary>Sets environment variables and returns what puts them back.</summary>
    static Action Env(params (string Name, string? Value)[] variables)
    {
        var old = variables.Select(v => (v.Name, Value: Environment.GetEnvironmentVariable(v.Name))).ToArray();
        foreach (var (name, value) in variables) Environment.SetEnvironmentVariable(name, value);
        return () => { foreach (var (name, value) in old) Environment.SetEnvironmentVariable(name, value); };
    }

    [Fact]
    public void Ensure_clones_the_primary_then_fetches_and_checks_out_the_tip()
    {
        // github.com is redirected to a local directory through git's own url.<base>.insteadOf, so nothing leaves the machine.
        var remotes = Directory.CreateTempSubdirectory("remotes-").FullName;
        var source = Directory.CreateDirectory(Path.Combine(remotes, "acme", "website.git")).FullName;
        Git(source, "init", "-q", "-b", "main");
        File.WriteAllText(Path.Combine(source, "index.html"), "one");
        Git(source, "add", "-A");
        Git(source, "commit", "-q", "-m", "One");
        var restore = Env(("CARTOGRAPHER_CACHE", Directory.CreateTempSubdirectory("cache-").FullName),
            ("GIT_CONFIG_COUNT", "1"), ("GIT_CONFIG_KEY_0", $"url.{new Uri(remotes + "/").AbsoluteUri}.insteadOf"), ("GIT_CONFIG_VALUE_0", "https://github.com/"));
        try
        {
            var atlas = NewAtlas("organisation: Acme Ltd\n");
            Assert.Contains("repos list --refresh", Assert.Throws<UsageException>(() => Clones.Ensure(atlas, "web")).Message);
            new ScanState { Repository = Ids.Repository("web"), Locations = [new LocationState { Id = "github:acme/website", Primary = true, DefaultBranch = "main" }] }.Save(atlas);

            var clone = Clones.Ensure(atlas, "web");
            Assert.Equal(Path.Combine(Clones.CacheRoot, "acme-ltd", "web"), clone);
            Assert.Equal("one", File.ReadAllText(Path.Combine(clone, "index.html")));
            Assert.Equal("HEAD", Git(clone, "rev-parse", "--abbrev-ref", "HEAD").Trim());   // detached

            File.WriteAllText(Path.Combine(source, "index.html"), "two");
            Git(source, "commit", "-q", "-am", "Two");
            Assert.Equal(clone, Clones.Ensure(atlas, "web"));
            Assert.Equal("two", File.ReadAllText(Path.Combine(clone, "index.html")));
            Assert.Equal(Git(source, "rev-parse", "HEAD"), Git(clone, "rev-parse", "HEAD"));

            Clones.Clear();
            Assert.False(Directory.Exists(Clones.CacheRoot));
        }
        finally { restore(); }
    }

    /// <summary>Against the public twin github.com/nunit/nunit3-vs-adapter and dev.azure.com/nunit/NUnit/_git/NUnit3TestAdapter. Run with CARTOGRAPHER_LIVE_TESTS=1.</summary>
    [Fact]
    public async Task Live_list_link_and_clone_the_public_NUnit_twin()
    {
        if (Environment.GetEnvironmentVariable("CARTOGRAPHER_LIVE_TESTS") != "1") return;
        var restore = Env(("CARTOGRAPHER_CACHE", Directory.CreateTempSubdirectory("cache-").FullName));
        try
        {
            var atlas = NewAtlas("""
                organisation: NUnit
                github: {organisation: nunit}
                azure-devops: {organisation: nunit}
                locations:
                  repositories/nunit-nunit3testadapter: ["azdo:nunit/NUnit/NUnit3TestAdapter", "github:nunit/nunit3-vs-adapter"]
                """);
            var scm = Scm.FromEnvironment(atlas.Config);
            var listing = await Repos.Refresh(atlas, scm);
            output.WriteLine(Repos.Table(atlas));
            foreach (var r in listing.Skipped) output.WriteLine($"skipped ({r.Skipped}): {r.Id}");

            var twin = listing.Repositories.Single(r => r.Folder == "nunit-nunit3testadapter");
            var (github, azdo) = (twin.Locations[0], twin.Locations[1]);
            Assert.Equal(("github:nunit/nunit3-vs-adapter", "azdo:nunit/NUnit/NUnit3TestAdapter"), (github.Id, azdo.Id));
            Assert.Equal("github:nunit/nunit3-vs-adapter", ScanState.Load(atlas, twin.Folder).Primary!.Id);
            output.WriteLine($"github tip {github.Tip} on {github.DefaultBranch}; azdo tip {azdo.Tip} on {azdo.DefaultBranch}");
            output.WriteLine($"azdo tip exists on github: {await scm.HasCommit(github, azdo.Tip!)}");
            output.WriteLine($"github tip exists on azdo: {await scm.HasCommit(azdo, github.Tip!)}");

            var clone = Clones.Ensure(atlas, twin.Folder);
            Assert.Equal(github.Tip, Git(clone, "rev-parse", "HEAD").Trim());
            Assert.NotEmpty(Directory.EnumerateFiles(clone));
            output.WriteLine($"cloned to {clone}");
        }
        finally { restore(); }
    }
}
