# Finding internal library dependencies and usages in .NET

Research for issue #3. Primary sources only; each claim links to the page that owns it. Items marked **unverified** or **inference** were not confirmed in a primary source.

## Answer

- **Declared dependencies are readable from files alone, in C# and VB alike.** Parse `*.csproj`/`*.vbproj` (`PackageReference`, `ProjectReference`, `Reference`+`HintPath`, `COMReference`), `Directory.Packages.props`, `packages.config`, `packages.lock.json` and `nuget.config`. No restore or build needed. This gives direct dependencies; versions may be ranges, floating, or conditional.
- **Transitive/resolved graphs need a restore** (`project.assets.json`, `dotnet list package`), except where `packages.lock.json` is committed or the project uses `packages.config` (already flat).
- **"Internal" is not a property of a reference.** Derive it from (1) `packageSourceMapping` patterns, (2) the Azure Artifacts feed API (published-to-feed vs ingested-from-upstream), (3) the set of `PackageId`s the estate's own repos produce. With a single feed plus upstreams (Microsoft's recommended setup), `nuget.config` alone says nothing.
- **Legacy projects weaken identity.** A `HintPath` to a checked-in DLL or a GAC reference names an assembly, not a package or repo; mapping it to an internal library needs an assembly-name-to-producer index.
- **Type-level usage needs Roslyn with a loaded MSBuild workspace** (restored, MSBuild installed; legacy projects likely Windows + Visual Studio MSBuild). Without that, an agent reading code (`using`/`Imports`, namespaces) is the fallback.
- **Config files carry runtime coupling** (WCF endpoints, connection strings) that no reference graph shows.
- VB6 / classic ASP: not researched.

## Sources of declared dependencies (no build)

| Source | What it gives | Caveat |
| --- | --- | --- |
| `PackageReference` in project file | Package ID + version | Version is a minimum or range, can float (`3.6.*`), and items can be conditional on `TargetFramework` ([PackageReference](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)) |
| `Directory.Packages.props` (CPM) | Versions as `PackageVersion`; project items then omit `Version` | Closest file up the tree wins, parents only via explicit `Import`; `VersionOverride` and `GlobalPackageReference` exist ([CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)) |
| `packages.config` | Exact versions, in the project root | "Flat list of all direct and transitive packages", so direct vs transitive is not distinguishable ([packages.config](https://learn.microsoft.com/en-us/nuget/reference/packages-config), [source mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)) |
| `packages.lock.json` | Full resolved closure | Opt-in; guidance is to commit for apps, not libraries ([locking](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)) |
| `ProjectReference` | Path to another project | In-repo only; pack turns it into a package dependency by default ([pack target](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#project-to-project-references)) |
| `nuget.config` | Feed URLs (`packageSources`), ID-to-feed patterns (`packageSourceMapping`) | Merged across repo, user and machine files unless `<clear />` ([nuget.config](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file)) |

Raw XML parsing misses anything set through imports or MSBuild properties. `msbuild -getItem:PackageReference -getProperty:PackageId` evaluates the project "without executing the build" ([MSBuild CLI](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference)); it needs the SDK installed. Minimum MSBuild version: **unverified**.

## What needs a restore

- Restore writes `obj/project.assets.json`, "the dependency graph of all package references" ([restore outputs](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#restore-outputs)).
- `dotnet list package --include-transitive --format json` reads those assets; before .NET 10 "you first need to build/restore the project" ([dotnet package list](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list)). It reports requested/resolved versions, not the feed a package came from.
- The restored source per package is in the `.nupkg.metadata` files of the global packages folder ([source mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)).
- Restore needs authenticated access to every feed ("all sources need to be available for all restores"), and `packages.config` restore is "not with `dotnet.exe`" ([PackageReference](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files), [msbuild targets](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets)).

## Telling internal from public

1. **`packageSourceMapping`**: patterns such as `Contoso.*` under a private source state which IDs come from which feed; exact ID beats prefix, longest prefix wins ([source mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)). Deterministic, but opt-in.
2. **Azure Artifacts feed API**: a feed holds both packages "published directly to the feed" and those "saved from an upstream source" ([upstream sources](https://learn.microsoft.com/en-us/azure/devops/artifacts/concepts/upstream-sources)). `Get Packages` returns `directUpstreamSourceId` per version ("Upstream source this package was ingested from") and accepts a `directUpstreamId` filter ([REST](https://learn.microsoft.com/en-us/rest/api/azure/devops/artifacts/artifact-details/get-packages)). Treating versions without that ID as internal is an **inference**.
3. **Producer index**: `PackageId` defaults to `$(AssemblyName)`, and `RepositoryUrl` can record the source repo ([pack target](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#pack-target)). `IsPackable` defaults to `true`, so it does not prove a project is published.

Microsoft recommends one feed with upstreams and `<clear />` ([upstream sources](https://learn.microsoft.com/en-us/azure/devops/artifacts/concepts/upstream-sources)); in that setup only options 2 and 3 work.

## Usage of a library's types

- Roslyn's `SymbolFinder.FindReferencesAsync(ISymbol, Solution)` "finds all references to a symbol throughout a solution" ([API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder.findreferencesasync)). The `Solution` comes from `MSBuildWorkspace`, "a workspace that can be populated by opening MSBuild solution and project files" ([dotnet/roslyn](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/MSBuild/Core/MSBuild/MSBuildWorkspace.cs)); load problems surface in its `Diagnostics`, not as exceptions.
- It is scoped to one loaded solution; cross-repo usage means loading each consumer repo. That semantic results require restored packages is an **inference** (references resolve from restore output), not a quoted statement.
- Without a workspace: syntax-only parsing or text search for namespaces. Cheap, but cannot resolve which assembly a type belongs to. This is the agent-reading-code tier.

## Visual Basic

- Same dependency sources: CPM "applies to all `<PackageReference>`-based MSBuild projects (including legacy CSPROJ)" ([CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)), and the common MSBuild items are shared across project types ([common items](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)). VB adds project-level `Import` items (namespaces imported for every file), so a file may use a library with no `Imports` line.
- Roslyn's workspace APIs have "full fidelity with the C# and Visual Basic languages" ([workspace model](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-workspace)); `SymbolFinder` works on language-neutral `ISymbol`. Which extra package `MSBuildWorkspace` needs to load `.vbproj`: **unverified**.

## Legacy .NET Framework (non-SDK-style) projects

- .NET Framework projects "currently default to `packages.config`" ([PackageReference](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)). Packages install under `$(SolutionDir)/packages` unless `repositoryPath` says otherwise ([nuget.config](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file)), so the project also carries `<Reference>` items whose `HintPath` points there.
- `<Reference>` is "a .NET assembly (managed) reference"; `HintPath` is an optional path ([common items](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)). Resolution searches `{HintPathFromItem}`, registry folders, `{GAC}` and more, in order ([ResolveAssemblyReference](https://learn.microsoft.com/en-us/visualstudio/msbuild/resolveassemblyreference-task)). So a `HintPath` into `lib/` identifies a checked-in DLL by assembly name only, and a reference with no `HintPath` may be framework or GAC: undecidable from files. The CLI should emit these as "binary reference, origin unknown".
- .NET Framework project references "aren't transitive" ([common items](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)).
- Tooling: the .NET Core build of `Microsoft.Build.Locator` "can only find" .NET SDK MSBuild, "not Visual Studio installations"; the .NET Framework build sees only Visual Studio ([MSBuild Locator](https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions)). That loading legacy projects therefore needs Windows + Visual Studio/Build Tools is an **inference**.

## Other legacy coupling readable from files

- **WCF clients**: `system.serviceModel/client/endpoint` with `address`, `binding`, `contract` in app/web.config ([WCF client config](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/client-configuration)). Addresses can also be set in code.
- **Connection strings**: `connectionStrings/add` (`name`, `connectionString`, `providerName`); may live in a `configSource` file or be encrypted ([ADO.NET](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/connection-strings-and-configuration-files)).
- **Web Site projects**: "no project file (.csproj or .vbproj)"; every file in the folder is in the site, and C# and VB can be mixed ([Web Application vs Web Site](https://learn.microsoft.com/en-us/previous-versions/aspnet/dd547590(v=vs.110))). Detect by `web.config` without a project file; where such sites record assembly references: **unverified**.
- **COM interop**: `COMReference` carries `Guid` and version and resolves through the build machine's registry; `COMFileReference` points at a type library file ([common items](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)).
- **.asmx web references / WCF "Service Reference" artefacts**: not researched; file formats **unverified**.
- **VB6 (`.vbp`) and classic ASP**: not researched.
