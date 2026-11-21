# Finding dependencies in VB6, classic ASP and COM code

Research for issue #18. Claims cite Microsoft documentation unless marked **(unverified)**, meaning "unverified against primary source": a well-known convention for which no official page was found.

## Answer

Most pre-.NET dependency evidence is line-oriented text, so a deterministic CLI can extract it:

- **VB6**: `.vbp` is ASCII `key=value`; `Reference=` lines carry type-library GUID, version, path and description; `Name=` plus each class name gives the ProgIDs the project publishes.
- **Classic ASP**: `#include` is resolved before script runs, so the include graph is static; `Server.CreateObject("ProgID")`, `<OBJECT RUNAT=Server PROGID|CLASSID>` and `METADATA TYPE="TypeLib"` are literal text.
- **.NET side**: `COMReference` (type-library GUID + version) and `Reference` to interop DLLs in project files; `ProgId`/`Guid` attributes; manifests with `comClass`/`clrClass`.
- **Database**: literal connection strings, `CommandType = adCmdStoredProc` with a literal `CommandText`.

The CLI should emit *names* (ProgIDs, GUIDs, file names, procedure names) and join them across repos by name. It cannot prove which binary serves a ProgID: that mapping is in the registry of each machine, not the repo. An agent is needed for late-bound objects passed around as `Object`, ProgIDs or SQL built by concatenation, connection strings read from registry/DSN/UDL, and ADO commands with no `CommandType` (ADO guesses at run time).

## (a) What each codebase depends on

**VB6 projects.** `.vbp` files are always saved as ASCII and list forms, modules, classes and references [1]. The documented sample shows the reference shape: `Reference=*\G{00020430-…}#2.0#0#..\..\..\WINDOWS\SYSTEM\STDOLE2.TLB#OLE Automation` — GUID, version, LCID, path, description [1]. Also parseable from the same sample: `Type=`, `Name=`, `Module=`, `Class=`, `Form=`, `CompatibleMode=` [1]. `.vbg` is the project-group file and `.dep`/`.vbr` are deployment/registration files that may be checked in [2].

- **(unverified)** `Object={GUID}#ver#lcid; file.ocx` lines for ActiveX controls; `.vbg` `Project=`/`StartupProject=` lines; `ExeName32=` and `CompatibleEXE32=` keys; `Reference=*\A…\other.vbp` for project-to-project references. Microsoft's format page does not show these; confirm against real files before relying on them.
- The path inside `Reference=` is machine-specific (the sample points into `WINDOWS\SYSTEM`) [1]; treat the GUID and the file's base name as the stable keys.

**ProgIDs.** A VB6 class's ProgID is project name + class name [3], so `Name=` in the `.vbp` plus class names gives the published ProgIDs without the registry. A ProgID is a registry entry associated with a CLSID and is not guaranteed unique [4]. **(unverified)** CLSIDs of VB6 classes are generated at compile time and are not in `.cls` source.

**CreateObject.** `CreateObject(class, [servername])` takes an `appname.objecttype` string; a variable declared `As Object` is late bound [5]. A string-literal argument is greppable; a variable or concatenated argument is not. `servername` literals reveal remote (DCOM) hosts [5].

**Classic ASP.**
- `<!-- #include virtual|file="…" -->`; `file` is relative to the including file, `virtual` starts at a virtual directory; includes happen before script executes, so the name cannot be computed [6]. Resolving `virtual` paths needs the IIS site/virtual-directory layout, which is server configuration (inference from [6]).
- `<SCRIPT RUNAT=SERVER SRC="…">` is a second include form [6].
- Objects are created with `Server.CreateObject("ProgID")` or `<OBJECT RUNAT=Server ID=… PROGID=…|CLASSID="Clsid:…">` [7].
- `<!-- METADATA TYPE="TypeLib" FILE="…" UUID="…" VERSION="…" -->` in `Global.asa` declares a type-library dependency [8].
- Windows Script Components (`.sct`) declare their own `ProgID` in a `<Registration>` element — a ProgID *definition* that lives in source control [7].

## (b) How COM connects VB6, ASP and .NET

**.NET calling COM.** `COMReference` items in project files carry the type-library `Guid`, `VersionMajor`, `VersionMinor`, `WrapperTool` and `Isolated`, and are resolved through the build machine's registry — no file path is recorded [9]. Alternatives are `COMFileReference` (path to a type library) and an ordinary `Reference` to a pre-generated wrapper [9]. Tlbimp output defaults to the type library's name with `.dll` [10]. **(unverified)** the `Interop.<Lib>.dll` naming used by Visual Studio.

**COM calling .NET.** Regasm reads assembly metadata and writes registry entries so COM clients can create .NET classes; `/tlb` emits a type library and `/regfile` a `.reg` file [11]. ProgIDs default to namespace + type name unless `[ProgId]` is set [12], so they are derivable from source. Look for regasm calls in build/deploy scripts.

**Registration-free COM.** Manifests are XML: `comClass` (`clsid`, `progid`, `tlbid`) under `file` for native components [13]; `clrClass` (`clsid`, `progid`, `name`) for .NET components [14]. Where present, they give the ProgID→CLSID→file mapping the registry otherwise hides. They may also be embedded as resources in binaries rather than checked in [13][14].

**Join strategy.** Type-library GUID links `.vbp` `Reference=` to `.csproj`/`.vbproj` `COMReference` [1][9]; ProgID links `CreateObject` calls to VB6 `Name=`+class, `[ProgId]`, `.sct` and manifests [3][7][12][13].

## (c) How this code reaches databases

- ADO connection strings are `argument=value;` pairs; ADO interprets only `Provider=`, `File Name=`, `Remote Provider=`, `Remote Server=`, `URL=` and passes the rest to the provider [15]. `File Name=` points at an external file holding the connection details, and with no provider the default is MSDASQL (ODBC) [15] — in both cases server and database may not be in the code.
- `CommandType`: `adCmdStoredProc` (4) means `CommandText` is a procedure name; `adCmdText` (1) is SQL; `adCmdTable` (2) a table. The default `adCmdUnknown` (8) makes ADO try each interpretation at run time [16], so a bare name in `CommandText` or `Connection.Execute` is ambiguous without reading the database.
- ADO constants are only available in ASP via a type-library declaration [8]; **(unverified)** scripts otherwise include `adovbs.inc` or use the bare numbers, so the CLI should match `4` as well as `adCmdStoredProc`.

## CLI versus agent

| Deterministic CLI | Agent |
| --- | --- |
| `.vbp`/`.vbg` keys, `Reference=` GUIDs | Which repo builds the referenced DLL when names differ |
| `#include` graph, literal `CreateObject` ProgIDs, `<OBJECT>` tags, `METADATA` | ProgIDs/SQL built from variables; objects passed as `Object` |
| `COMReference`, manifests, `[ProgId]`/`[Guid]`, regasm in scripts | Whether a registration actually exists in production |
| Literal connection strings, `adCmdStoredProc` + literal name | Connection info from registry, DSN, UDL, `Application()` variables; tables touched by inline SQL |

## Sources

1. https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-basic-6/aa716294(v=vs.60)
2. https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-basic-6/aa241721(v=vs.60)
3. https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-basic-6/aa242136(v=vs.60)
4. https://learn.microsoft.com/en-us/windows/win32/com/-progid--key
5. https://learn.microsoft.com/en-us/office/vba/language/reference/user-interface-help/createobject-function
6. https://learn.microsoft.com/en-us/previous-versions/iis/6.0-sdk/ms524876(v=vs.90)
7. https://learn.microsoft.com/en-us/previous-versions/iis/6.0-sdk/ms524620(v=vs.90)
8. https://learn.microsoft.com/en-us/previous-versions/iis/6.0-sdk/ms525307(v=vs.90)
9. https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items
10. https://learn.microsoft.com/en-us/dotnet/framework/tools/tlbimp-exe-type-library-importer
11. https://learn.microsoft.com/en-us/dotnet/framework/tools/regasm-exe-assembly-registration-tool
12. https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.progidattribute
13. https://learn.microsoft.com/en-us/windows/win32/sbscs/assembly-manifests
14. https://learn.microsoft.com/en-us/dotnet/framework/interop/configure-net-framework-based-com-components-for-reg
15. https://learn.microsoft.com/en-us/previous-versions/sql/ado/reference/ado-api/connectionstring-property-ado
16. https://learn.microsoft.com/en-us/previous-versions/sql/ado/reference/ado-api/commandtypeenum
