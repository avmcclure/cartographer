namespace Cartographer.Tests;

public class DotNetTests
{
    /// <summary>Writes files into a temp directory and runs the whole mechanical pass over it as repository "acme-app".</summary>
    static Atlas Scan(params (string Path, string Text)[] files)
    {
        var atlasDir = Directory.CreateTempSubdirectory("atlas-").FullName;
        File.WriteAllText(Path.Combine(atlasDir, Atlas.ConfigFile), "organisation: Acme\ninternal-prefixes: [\"Acme.*\"]\n");
        var atlas = Atlas.Load(atlasDir);
        Scanner.Run(atlas, Repo(files));
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
        return atlas;
    }

    static Checkout Repo(params (string Path, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("repo-").FullName;
        foreach (var (path, text) in files)
        {
            var to = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.WriteAllText(to, text);
        }
        return Checkout.Open("acme-app", dir);
    }

    static Entry Component(Atlas atlas, string name) => atlas.Entries[Ids.Component("acme-app", name)];
    static Link Link(Entry e, string name) => e.Links!.Single(l => l.Name == name);

    [Fact]
    public void Legacy_package_consumer_fixture() => Fixtures.AssertMatchesExpected(Fixtures.Scan("legacy-package-consumer"), "legacy-package-consumer");

    [Fact]
    public void Wcf_pair_fixture() => Fixtures.AssertMatchesExpected(Fixtures.Scan("wcf-pair"), "wcf-pair");

    [Fact]
    public void Sdk_project_takes_versions_from_the_attribute_the_child_element_and_the_central_file()
    {
        var atlas = Scan(
            ("Directory.Packages.props", """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Acme.Central" Version="3.0.0" />
                    <PackageVersion Include="Acme.Overridden" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """),
            ("Directory.Build.props", "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>"),
            ("src/Api/Acme.Api.csproj", """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Acme.Central" />
                    <PackageReference Include="Acme.Overridden" VersionOverride="2.0.0" />
                    <PackageReference Include="Acme.Attribute" Version="[1.2.0,2.0.0)" />
                    <PackageReference Include="Acme.Element">
                      <Version>4.5.6</Version>
                    </PackageReference>
                    <PackageReference Include="Acme.Variable" Version="$(AcmeVersion)" />
                  </ItemGroup>
                </Project>
                """));
        var api = Component(atlas, "Acme.Api");
        Assert.Equal("service", api.Type);
        Assert.Equal(["csharp", "net8.0"], api.Stack);
        Assert.Equal("3.0.0", Link(api, "Acme.Central").Version);
        Assert.Equal("2.0.0", Link(api, "Acme.Overridden").Version);
        Assert.Equal("[1.2.0,2.0.0)", Link(api, "Acme.Attribute").Version);
        Assert.Equal("4.5.6", Link(api, "Acme.Element").Version);
        Assert.Null(Link(api, "Acme.Variable").Version);
        Assert.Equal("proj#L3", Link(api, "Acme.Central").Source);
        Assert.All(api.Links!, l => Assert.Equal(Basis.Extracted, l.Basis));
        Assert.DoesNotContain(api.Provides!, p => p.As == "nuget");
    }

    [Fact]
    public void Conditional_references_are_all_read_and_a_repeated_one_is_listed_once()
    {
        var atlas = Scan(("Acme.Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net472;net8.0</TargetFrameworks>
                <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
              </PropertyGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net472'">
                <PackageReference Include="Acme.Compat" Version="1.0.0" />
                <PackageReference Include="Acme.Both" Version="1.0.0" />
                <Reference Include="System.Web" />
              </ItemGroup>
              <Choose>
                <When Condition="'$(TargetFramework)' == 'net8.0'">
                  <ItemGroup>
                    <PackageReference Include="Acme.Both" Version="2.0.0" />
                    <PackageReference Include="Acme.Modern" Version="5.0.0" />
                  </ItemGroup>
                </When>
              </Choose>
            </Project>
            """));
        var lib = Component(atlas, "Acme.Lib");
        Assert.Equal(["Acme.Both", "Acme.Compat", "Acme.Modern"], lib.Links!.Select(l => l.Name));
        Assert.Equal("1.0.0", Link(lib, "Acme.Both").Version);
        Assert.Equal(["csharp", "net472", "net8.0"], lib.Stack);
        Assert.Equal("library", lib.Type);
        Assert.Contains(lib.Provides!, p => p is { Name: "Acme.Lib", As: "nuget", Source: "proj#L4" });
    }

    [Fact]
    public void Legacy_project_reads_packages_config_hint_paths_com_and_project_references()
    {
        var atlas = Scan(
            ("App/Acme.Desk.csproj", """
                <?xml version="1.0" encoding="utf-8"?>
                <Project ToolsVersion="12.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <OutputType>WinExe</OutputType>
                    <AssemblyName>AcmeDesk</AssemblyName>
                    <RootNamespace>Acme.Desk</RootNamespace>
                    <TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include="System.Windows.Forms" />
                    <Reference Include="PresentationCore" />
                    <Reference Include="Acme.Gac.Thing" />
                    <Reference Include="Acme.Vendor, Version=1.2.3.4, Culture=neutral">
                      <HintPath>..\libs\Acme.Vendor.dll</HintPath>
                    </Reference>
                    <Reference Include="EntityFramework.SqlServer">
                      <HintPath>..\packages\EntityFramework.6.4.4\lib\net45\EntityFramework.SqlServer.dll</HintPath>
                    </Reference>
                    <COMReference Include="ADODB">
                      <Guid>{2a75196c-d9eb-4129-b803-931327f72d5c}</Guid>
                      <VersionMajor>2</VersionMajor>
                      <VersionMinor>8</VersionMinor>
                    </COMReference>
                    <ProjectReference Include="..\Data\Acme.Data.vbproj" />
                    <ProjectReference Include="..\..\elsewhere\Acme.Outside.csproj" />
                  </ItemGroup>
                </Project>
                """),
            ("App/packages.config", """
                <packages>
                  <package id="EntityFramework" version="6.4.4" targetFramework="net452" />
                </packages>
                """),
            ("Data/Acme.Data.vbproj", """
                <Project ToolsVersion="12.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <OutputType>Library</OutputType>
                    <RootNamespace>Acme.Data</RootNamespace>
                    <TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>
                  </PropertyGroup>
                </Project>
                """),
            ("Data/Acme.Data.nuspec", "<package><metadata><id>Acme.Data.Package</id></metadata></package>"));

        var desk = Component(atlas, "AcmeDesk");
        Assert.Equal("desktop-app", desk.Type);
        Assert.Equal(["csharp", "net452"], desk.Stack);
        Assert.Equal(["Acme.Desk|namespace", "AcmeDesk|assembly"], desk.Provides!.Select(p => $"{p.Name}|{p.As}"));
        Assert.Contains("legacy style", desk.Body);
        // The package is public: the resolver moved it to packages. A type library is never a package: it stays an unresolved reference with a question.
        Assert.Equal(["EntityFramework 6.4.4"], desk.Packages);
        Assert.Null(Link(desk, "{2A75196C-D9EB-4129-B803-931327F72D5C}").To);
        Assert.Contains("name {2A75196C-D9EB-4129-B803-931327F72D5C}", desk.Body);
        Assert.Equal("1.2.3.4", Link(desk, "Acme.Vendor").Version);
        Assert.NotNull(Link(desk, "Acme.Gac.Thing"));
        Assert.NotNull(Link(desk, "Acme.Outside"));
        Assert.Contains(desk.Links!, l => l.Type == "depends-on" && l.To == Ids.Component("acme-app", "Acme.Data"));
        Assert.DoesNotContain(desk.Links!, l => l.Name?.StartsWith("System") == true || l.Name?.StartsWith("Presentation") == true);

        var data = Component(atlas, "Acme.Data");
        Assert.Equal(["vbnet", "net452"], data.Stack);
        Assert.Contains(data.Provides!, p => p is { Name: "Acme.Data.Package", As: "nuget", Source: "nuspec#L1" });
    }

    [Fact]
    public void Component_type_follows_the_sdk_output_type_and_nearby_files()
    {
        static string Project(string sdk, string output = "") => $"<Project Sdk=\"{sdk}\"><PropertyGroup>{output}</PropertyGroup></Project>";
        var atlas = Scan(
            ("Site/Site.csproj", Project("Microsoft.NET.Sdk.Web")), ("Site/Pages/Index.cshtml", "<h1>Hi</h1>"),
            ("Api/Api.csproj", Project("Microsoft.NET.Sdk.Web")),
            ("Worker/Worker.csproj", Project("Microsoft.NET.Sdk.Worker")),
            ("Tool/Tool.fsproj", Project("Microsoft.NET.Sdk", "<OutputType>Exe</OutputType>")),
            ("Host/Host.csproj", Project("Microsoft.NET.Sdk", "<OutputType>WinExe</OutputType>")),
            ("Host/App.config", "<configuration><system.serviceModel><services><service name=\"Acme.Host.Jobs\"><endpoint contract=\"Acme.Host.IJobs\" binding=\"netTcpBinding\" /></service></services></system.serviceModel></configuration>"));
        Assert.Equal(["service", "service", "site", "job", "service"], new[] { "Api", "Host", "Site", "Tool", "Worker" }.Select(n => Component(atlas, n).Type));
        Assert.Equal(["fsharp"], Component(atlas, "Tool").Stack);
        Assert.Contains(Component(atlas, "Host").Provides!, p => p is { Name: "Acme.Host.IJobs", As: "contract" });
    }

    [Fact]
    public void Test_projects_are_skipped_and_counted()
    {
        var atlas = Scan(
            ("src/Acme.Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../tests/Acme.Lib.Tests.csproj\" /></ItemGroup></Project>"),
            ("tests/Acme.Lib.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.3\" /></ItemGroup></Project>"),
            ("flagged/Acme.Flagged.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>"),
            ("old/Acme.Old.Tests.csproj", "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"><ItemGroup><Reference Include=\"nunit.framework, Version=3.5.0.0\"><HintPath>..\\packages\\NUnit.3.5.0\\lib\\nunit.framework.dll</HintPath></Reference></ItemGroup></Project>"),
            ("broken/Acme.Broken.csproj", "<Project><oops"));
        Assert.Equal([Ids.Component("acme-app", "Acme.Lib")], atlas.OfKind(Kinds.Component).Select(c => c.Id));
        Assert.Null(Component(atlas, "Acme.Lib").Links);
        Assert.Contains("- .NET projects: 1. Test projects skipped: 3. Not readable as XML: 1.", atlas.Entries[Ids.Repository("acme-app")].Body);
    }

    [Fact]
    public void Credentials_never_reach_an_entry()
    {
        var atlas = Scan(
            ("nuget.config", """
                <configuration>
                  <packageSources>
                    <add key="acme" value="https://builder:feedhunter2@nuget.acme.example/v3/index.json?sig=feedhunter3" />
                  </packageSources>
                  <packageSourceCredentials><acme><add key="ClearTextPassword" value="feedhunter4" /></acme></packageSourceCredentials>
                </configuration>
                """),
            ("Web/Acme.Web.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />"),
            ("Web/web.config", """
                <configuration>
                  <connectionStrings>
                    <add name="Sales" connectionString="Server=sql01;Database=Sales;User Id=app;Password=hunter2secret" />
                    <add name="Model" connectionString="metadata=res://*/M.csdl;provider=System.Data.SqlClient;provider connection string=&quot;data source=sql02;initial catalog=Stock;user id=app;pwd=hunter3secret&quot;" />
                    <add name="Trusted" connectionString="Data Source=sql03;Initial Catalog=Audit;Integrated Security=SSPI" />
                    <add name="Odd" connectionString="hunter4secret" />
                  </connectionStrings>
                  <system.serviceModel>
                    <client><endpoint address="https://svc:hunter5secret@orders.acme.example/Orders.svc" binding="basicHttpBinding" contract="Acme.IOrders" /></client>
                  </system.serviceModel>
                </configuration>
                """),
            ("Web/appsettings.Production.json", """
                {
                  // comments are allowed
                  "ConnectionStrings": {
                    "Queue": "Endpoint=sb://acme.servicebus.example/;SharedAccessKeyName=app;SharedAccessKey=hunter6secret",
                    "Warehouse": "Host=pg01;Database=Warehouse;Username=app;Password=hunter7secret"
                  }
                }
                """));
        var web = Component(atlas, "Acme.Web");
        var text = EntryFile.Write(web);
        Assert.DoesNotContain("hunter", text);
        Assert.Equal(["Audit", "Sales", "Stock", "Warehouse"], web.Links!.Where(l => l.Type == "uses").Select(l => l.Name));
        Assert.Contains("Connection string `Sales` (`Web/web.config` line 3): server `sql01`, database `Sales`, a credential is present (not recorded).", text);
        Assert.Contains("Connection string `Model` (`Web/web.config` line 4): server `sql02`, database `Stock`, a credential is present (not recorded).", text);
        Assert.Contains("Connection string `Trusted` (`Web/web.config` line 5): server `sql03`, database `Audit`, no credential.", text);
        Assert.Contains("Connection string `Odd` (`Web/web.config` line 6): not in key=value form; nothing recorded.", text);
        Assert.Contains("Connection string `Queue` (`Web/appsettings.Production.json` line 4): server not given, database not given, a credential is present (not recorded).", text);
        Assert.Contains("Connection string `Warehouse` (`Web/appsettings.Production.json` line 5): server `pg01`", text);
        Assert.Contains("`acme` https://nuget.acme.example/v3/index.json.", text);
        Assert.Contains("address `https://orders.acme.example/Orders.svc`", text);
        var call = web.Links!.Single(l => l.Type == "calls");
        Assert.Equal(("Acme.IOrders", "wcf", "extracted"), (call.Name, call.How, call.Basis));
    }

    [Fact]
    public void Candidates_are_lines_naming_the_library_namespace_or_its_public_types()
    {
        var library = Repo(
            ("src/Acme.Core/Money.cs", "namespace Acme.Core;\npublic readonly record struct Money(decimal Amount);\ninternal class Hidden { }\npublic static partial class Rounding { }\n"),
            ("src/Acme.Core/Ledger.vb", "Public NotInheritable Class Ledger\nEnd Class\n"),
            ("src/Other/Elsewhere.cs", "public class Elsewhere { }\n"));
        Assert.Equal(["Ledger", "Money", "Rounding"], UsageSearch.PublicTypes(library, "src/Acme.Core"));

        var consumer = Repo(
            ("Web/Home.cs", "using Acme.Core;\nusing Acme.CoreTools;\nclass Home { Money total; int moneyCount; }\nvar x = new Acme.Core.Ledger();\n"),
            ("Web/Report.vb", "Imports acme.core\nDim l As New LEDGER()\n"),
            ("Web/readme.md", "Acme.Core Money"));
        var found = UsageSearch.Candidates(consumer, ["Acme.Core"], ["Ledger", "Money", "Rounding"]);
        Assert.Equal(["Web/Home.cs:1", "Web/Home.cs:3", "Web/Home.cs:4", "Web/Report.vb:1", "Web/Report.vb:2"], found.Select(c => $"{c.File}:{c.Line}"));
    }
}
