# Public sample repositories (issue #7)

Researched 2026-11-20. Every repository below was checked through the GitHub API (`repos/<o>/<r>`, recursive tree listings, file contents, code search) or the anonymous Azure DevOps REST API. Sizes are the GitHub API `size` field (KB, shown as MB). Needs: **A** library + consumer across repos, **B** legacy .NET Framework, **C** pre-.NET, **D** SQL Server schema in several source forms, **E** Azure DevOps + GitHub, **Bonus** cross-repo code-to-database flow.

## Recommended set

| # | Repository | Covers | Size | Licence | Verifying evidence |
|---|---|---|---|---|---|
| 1 | https://github.com/serilog/serilog | A (library) | 76 MB | Apache-2.0 | `src/Serilog/Serilog.csproj` (the `Serilog` package). Active, default branch `dev`. |
| 2 | https://github.com/serilog/serilog-extensions-hosting | A (consumer) | 0.2 MB | Apache-2.0 | `src/Serilog.Extensions.Hosting/Serilog.Extensions.Hosting.csproj` has `<PackageReference Include="Serilog" Version="4.4.0" />` and `Include="Serilog.Extensions.Logging" Version="11.0.0"` (a second in-org package from a third repo). |
| 3 | https://github.com/dotnet-architecture/eShopModernizing | B, D (EF Core) | 134 MB | MIT | Archived. `eShopLegacyNTier/src/eShopWCFService/CatalogService.svc`, `eShopWCFService.csproj` (non-SDK, `ToolsVersion="15.0"`, `ProjectTypeGuids`, `<HintPath>..\packages\EntityFramework.6.1.3\...`), `packages.config`; `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms/eShopLegacyWebForms.csproj` (Web Forms; 12 `.aspx` repo-wide); `eShopLegacyMVCSolution/eShopPorted/Migrations/20201130044339_Initial.cs` (`table.ForeignKey`). |
| 4 | https://github.com/DNNCommunity/DNN.Feedback | B (VB.NET), D (loose scripts) | 7 MB | MIT | `DotNetNuke.Modules.Feedback.vbproj` (non-SDK, `ToolsVersion="12.0"`, .NET 4.7.2, `<HintPath>packages\DotNetNuke.Core.9.4.0\lib\net45\DotNetNuke.dll`), `packages.config` (`DotNetNuke.Core` 9.4.0), 36 `.vb`, 7 `.ascx`, `web.config`; 14 versioned `Installation/*.SqlDataProvider` scripts with `CREATE PROCEDURE ... Feedback_UpdateFeedback`. |
| 5 | https://github.com/kellyethridge/VBCorLib | C (VB6, COM) | 11 MB | MIT | 20 `.vbp`, 594 `.cls`, 15 `.frm`, 46 `.bas`. `Source/CorLib/CorLib.vbp` is `Type=OleDll` (a COM DLL); `Examples/Misc/SimpleTicker/SimpleTicker.vbp` consumes it by `Reference=*\G{E67787FD-...}#3.1#0#..\..\..\Binaries\Compiled\VBCorLib3.dll`; `TypeLibrary/CorType.odl` / `.tlb`. |
| 6 | https://github.com/OpenCIOC/onlineresources | C (classic ASP), D (loose scripts), B (partly), Bonus | 10 MB | none detected by GitHub; Apache-2.0 headers in files | Active (pushed 2026-11-13). 664 `.asp`, `global.asa`; `copy2.asp` has `<!--#include file="includes/core/adovbs.inc" -->`; `Server.CreateObject` in `recordlist.asp`, `browsebysubj.asp`; `adCmdStoredProc` in `login_sudo.asp`, `numfind.asp`. `python/sql/core/`: 987 files under `Stored Procedures/`, 481 under `Tables/`; `CREATE TRIGGER` in `Tables/dbo.CIC_BT_FT.sql`; `FOREIGN KEY` in `Tables/dbo.CIC_View.sql`. `Cioc.Web.csproj` is non-SDK, .NET 4.8. |
| 7 | https://github.com/OpenCIOC/ciocshared | Bonus, D | 0.06 MB | Apache-2.0 | Shared database in its own repo: `Functions/dbo.fn_SHR_GBL_DateString.sql`, `Stored Procedures/dbo.sp_STP_UnknownErrorCheck.sql`. Used from repo 6: `mailform.asp` and `python/cioc/web/rpc/oppdetails.py` reference `fn_SHR_GBL_DateString`; `python/sql/core/Stored Procedures/dbo.sp_NAICS_d.sql` references `sp_STP_UnknownErrorCheck`, which repo 6 does not define (0 matching paths in its tree). |
| 8 | https://github.com/microsoft/sql-server-samples | D (SSDT, loose scripts) | 507 MB (use a sparse checkout of `samples/databases`) | NOASSERTION (`license.txt`: "Microsoft SQL Server Sample Code") | `samples/databases/wide-world-importers/wwi-ssdt/wwi-ssdt/WideWorldImporters.sqlproj` with 135 files under `Stored Procedures/`, 54 under `Tables/`, `FOREIGN KEY` in `Sales/Tables/Orders.sql`; `samples/databases/northwind-pubs/instnwnd.sql`, `instpubs.sql` (trigger); `workload-drivers/order-insert/.../MultithreadedOrderInsertMain.cs` uses `CommandType.StoredProcedure`. |
| 9 | https://github.com/jasontaylordev/NorthwindTraders | D (EF Core migrations) | 5 MB | MIT | Archived. `Src/Persistence/Migrations/20180607065932_InitialCreate.cs` (`table.ForeignKey`), three later migrations, `NorthwindContextModelSnapshot.cs`. Same Northwind schema as `instnwnd.sql` in repo 8. |
| 10 | https://github.com/nunit/nunit3-vs-adapter and https://dev.azure.com/nunit/NUnit/_git/NUnit3TestAdapter | E | 8 MB GitHub, 2.8 MB Azure DevOps | MIT | Anonymous `GET dev.azure.com/nunit/NUnit/_apis/git/repositories` lists `NUnit3TestAdapter` (default branch `master`, root has `NUnit3TestAdapter.sln`). Its HEAD `f3f9d217ac` (2017-07-19, "Updating version number") exists in the GitHub repo, whose default branch is `main` and was pushed 2026-11-20: one repository, two locations, the Azure DevOps copy stale. |

The whole set except repositories 1, 3 and 8 is under 50 MB combined.

## Alternatives

**A**
- Extend the Serilog chain: `serilog/serilog-extensions-logging` (1.7 MB; `PackageReference` to `Serilog` 4.4.0) and `serilog/serilog-aspnetcore` (1 MB; references `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Settings.Configuration`, `Serilog.Sinks.File`) give a four-level in-org dependency chain. `serilog/serilog-sinks-file` (0.3 MB) is the smallest single consumer.
- `DbUp/DbUp` (33 MB, MIT; `src/dbup-core/dbup-core.csproj`) and `DbUp/dbup-sqlserver` (0.08 MB, MIT; `<PackageReference Include="dbup-core" Version="6.1.1" />`). The latter also carries DbUp scripts (`src/Sample/Scripts/Script0001 - Create tables.sql` and four more), so it doubles as a small D sample.
- `Particular/NServiceBus` (170 MB) and `Particular/NServiceBus.SqlServer` (11 MB) exist and are active, but the licence is not permissive (NOASSERTION) and the package reference was not inspected.

**B**
- `ventrian/News-Articles` (16 MB, MIT, active): `Ventrian.NewsArticles.vbproj` (non-SDK, .NET 4.8.1) with `<HintPath>Libraries\Ventrian.ImageResizer.dll` to checked-in DLLs, `Web References/wsStoryFeed/StoryFeed.wsdl`, 229 `.SqlDataProvider` scripts. Best sample of checked-in binaries.
- `cactusoft/kartris` (227 MB, GPL-2.0): VB.NET Web Forms web-site project (no `.vbproj`; `kartris.sln`, `packages.config`, 508 checked-in `.dll`, `.asmx` and `.svc`), `App_Code/BLL/*.vb` using `CommandType.StoredProcedure`, 50 `Uploads/Resources/UpdateSQL_*.sql` upgrade scripts with triggers. Realistic, but large and copyleft.
- `zblogcn/zblogasp` (53 MB, MIT, archived): `zblogupdate/zblogupdate/zblogupdate.vbproj` targets .NET 2.0.

**C**
- `zblogcn/zblogasp`: 289 `.asp`; `zb_system/FUNCTION/c_system_base.asp` has `Server.CreateObject("ADODB.Connection")`; also one `.vbp`. Uses Access (`zb_install/zblog.mdb`) rather than SQL Server.
- `coldstone/easyasp` (0.8 MB, Apache-2.0): 58 `.asp` library files; contents not inspected beyond the listing.
- `wqweto/VbAsyncSocket` (4 MB, MIT, active): `contrib/dll/HttpRequest.vbp` is `Type=OleDll`.

**D**
- `microsoft/DacFx` (0.4 MB, MIT): `samples/SdkStyleDatabaseProject/sample.sqlproj` for the SDK-style `.sqlproj` variant.
- `DNNCommunity/DNN.Blog` (33 MB, BSD-3-Clause): 24 `.SqlDataProvider` scripts beside C# and VB.NET.

**E**
- https://dev.azure.com/powershell/PowerShell/_git/PowerShell (90 MB) beside `PowerShell/PowerShell` (112 MB, MIT). They share history (commit `4b40e5f60e` is in both) but have diverged: the Azure DevOps HEAD `2f027ba435` (2026-07-30) is not on GitHub. Useful for "same repository, different tips", too large for routine runs.

## Gaps

1. **Azure DevOps is a short-lived stand-in.** Microsoft's documentation states that public projects are retired and that existing ones convert to private starting in 2027, so repository 10 may disappear. Most public projects probed (`dnceng/public`, `azure-sdk/public`, `xamarin/public`, `dotnet/CommunityToolkit`) hold only empty or disabled repositories. Nothing found covers Azure Artifacts feeds, TFVC, or Azure DevOps-internal pipeline references.
2. **No classic ASP page instantiating an in-house VB6 COM class.** VBCorLib shows COM production and consumption within VB6; OpenCIOC shows ASP using stock COM objects (ADODB). The client-typical `Server.CreateObject("Company.Component")` resolved to a `.vbp` in another repository was not found.
3. **No VB6 application doing data access against SQL Server** (ADODB plus stored procedure calls from `.cls`/`.frm`) in a small, licensed repository.
4. **No single schema in all three forms.** Northwind exists as a loose script and as EF Core migrations, WideWorldImporters as SSDT; no schema is available as `.sqlproj`, migrations and DbUp scripts together, and the SSDT sample has no trigger files.
5. **No legacy in-org package consumer.** The `packages.config` samples consume third-party packages; DNN.Feedback's `DotNetNuke.Core` comes from a different organisation (not verified which repository). Private feeds (`nuget.config` with internal sources) are absent.
6. **No WCF service and client in separate repositories.**

A small synthetic fixture organisation (four or five tiny repositories) would close these:

- `fixture-db`: one schema of three tables with a foreign key, a trigger and two stored procedures, expressed as an SSDT `.sqlproj`, as DbUp scripts and as EF Core migrations.
- `fixture-com`: a VB6 ActiveX DLL (`.vbp`, `.cls`) that calls the stored procedures through ADODB.
- `fixture-asp`: classic ASP pages with `#include` and `Server.CreateObject("Fixture.Orders")`.
- `fixture-lib` and `fixture-legacy-web`: a .NET Framework library packed as NuGet, consumed by a VB.NET Web Forms project through `packages.config`, a `HintPath`, one checked-in DLL and a `nuget.config` pointing at an internal feed; plus a WCF `.svc` consumed by a service reference.
- One of these pushed to both GitHub and a private Azure DevOps project, with differing tips.

## Not verified

- Stored procedures inside `instnwnd.sql` (file not reachable through code search); the trigger in `instpubs.sql` rests on a code-search hit for `FOR INSERT`.
- Whether `sp_STP_UnknownErrorCheck` reaches `ciocshared` by synonym, cross-database call or deployment into the same database; only the name reference and the absence of a local definition were checked.
- That any of these projects build; only file presence and content were checked.
