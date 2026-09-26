# Compiler and load errors

Match the diagnostic, then apply the cause. CS1929 / CS1503 are the
compile-time NuGet type split described in SKILL.md — aligning package
versions does not fix them.

## Contents

- CS1929 — extension method on a NuGet type
- CS1503 — constructor / argument type split
- CS0120 — nested class calling a script method
- CS0051 — public sketch type exposing a script-private record
- CS0006 — missing local DLL
- FileNotFoundException — DLL TFM vs Verso host

## CS1929 — extension method on a NuGet type

```text
error CS1929: 'JinagaClient' does not contain a definition for 'RenderFacts'
and the best extension method overload
'JinagaClientExtensions.RenderFacts(JinagaClient, params object[])'
requires a receiver of type 'Jinaga.JinagaClient'
```

**Cause.** `j` is a `JinagaClient` from a previous usage cell; the
extension is bound to `JinagaClient` from this cell's session references.
Same error for `sequence.AsTable()` (`DataFrameExtensions`) or any
extension whose `this` parameter is a NuGet type. Does **not** apply when
`j` is created in the same cell as `j.RenderFacts(...)`.

For tables, do not wrap `AsTable` in `ShowHtml` even when it compiles
(helper in the `#r` cell). Display the sequence: `rows.Display()`.

**Not the cause.** Two physical copies of a package DLL (transitive
dependency on an older version while you pin a newer one) do happen —
Verso keys references by path, not simple name — but CS1929 still fires
with a single aligned DLL.

## CS1503 — constructor / argument type split

```text
error CS1503: Argument 1: cannot convert from
  'Jinaga.User [C:\...\verso-nuget-packages\...\Jinaga.dll]'
to
  'Jinaga.User [Jinaga, Version=1.1.0.0, Culture=neutral, PublicKeyToken=null]'
```

(The path vs assembly-name labels can swap. Any NuGet type can appear,
not only `User`.)

**Cause.** A type declared in one usage cell mentions a NuGet type (`record
Item(User owner)`, `record Holder(JinagaClient client)`, or a class
constructor that takes `JinagaClient`). A **later** usage cell passes a
value whose NuGet type was bound again. `new Item(new User("…"))` and
`new SomeViewModel(j, item)` both hit this when those calls are not in the
same cell as the type (or as `j`).

## CS0120 — nested class calling a script method

```text
error CS0120: An object reference is required for the non-static field,
method, or property 'Helper(...)'
```

**Cause.** Top-level functions in a C# script are instance methods of the
submission. A `class` in that cell cannot call them. Make the helper
`static`.

## CS0051 — public sketch type exposing a script-private record

```text
error CS0051: Inconsistent accessibility: parameter type 'Item' is less
accessible than method 'ItemViewModel.ItemViewModel(object, Item)'
```

**Cause.** C# script records with no modifier are private to the
submission. A `public class` copied from a class library cannot take them
as parameters or return them. Omit `public` on notebook sketch types
(records and the view-model class).

## CS0006 — missing local DLL

```text
error CS0006: Metadata file '../MyApp.Core/bin/Release/net8.0/MyApp.Core.dll'
could not be found
```

**Cause, relative `#r`.** C# `#r "path.dll"` is left for Roslyn. Verso
builds `ScriptOptions.Default` and never sets `WithFilePath` to the
notebook, so a relative path has no script directory to resolve against.
`Environment.CurrentDirectory` (editor `workingDir` or the shell CWD for
`verso run`) does not matter. The same CS0006 appears in the editor and
on the CLI. F# rewrites relative `#r` to an absolute path using the
notebook file; C# does not.

**Cause, other.** The file is not built, or the `#r` path uses the wrong
TFM folder (`.../net8.0/MyApp.Core.dll` when the project outputs
`net10.0`).

**Workarounds (C# local DLL).**

1. **Absolute `#r`.** Machine-specific, but it loads in the editor and
   `verso run` when the file exists and the TFM matches the host.

   ```csharp
   #r "D:/path/to/MyApp.Core/bin/Release/net8.0/MyApp.Core.dll"
   ```

2. **Do not `#r` the product DLL.** Declare sketch types in the notebook
   (the SKILL.md default). Use this unless the cell must construct a type
   that exists only in that library.

## FileNotFoundException — DLL TFM vs Verso host

```text
System.IO.FileNotFoundException: Could not load file or assembly
'System.Collections, Version=10.0.0.0, ...'
```

**Cause.** The `#r` path exists (not CS0006), but the DLL's target
framework is newer than the process loading it (a `net10.0` DLL in a
.NET 8 host). Check the host with `verso info` (Runtime line).

Verso.Cli is typically net8 with `rollForward: Major`: if .NET 8 is
installed it **stays on 8**. The editor host is often net8 with
`rollForward: LatestMajor`: it may jump to the newest installed major
(.NET 10) and load a `net10.0` DLL. Editor success is not CLI success
when the TFMs differ. When the `#r` path is a host-matching TFM (for
example net8.0 under `verso run` on .NET 8), editor and CLI can both
succeed — still confirm with `verso run`, not the editor alone.

**Matching TFM** means the DLL was built for the same .NET generation as
that host process, not merely that the `#r` path contains a `netX.0`
folder.
