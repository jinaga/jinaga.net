---
name: verso-notebooks
description: >
  Authors, ports, and debugs Verso C# notebooks (.verso, .ipynb/.dib in Verso,
  verso run, verso convert). Use whenever the user is in a C# notebook or C#
  kernel, including ports from Polyglot, .NET Interactive, or
  Jinaga.Notebooks. Use when later cells fail with CS1929 RenderFacts/AsTable,
  CS1503 JinagaClient/User path vs assembly, CS0120, CS0051, CS0006,
  FileNotFoundException System.Collections 10.0.0.0, HtmlString object trees,
  Display vs CellOutput, AsTable plaintext dumps, #r nuget vs local DLL,
  factory helpers, one usage cell after #r, or ShowTypes/ShowFacts Graphviz
  scaling. Leave app UI, unit tests, TypeScript, standalone Graphviz, and
  Verso F# cells to other skills.
---

# Verso C# notebooks

Verso's C# kernel is Roslyn `CSharpScript` with cells chained by
`ContinueWith`. `#r "nuget:…"` is stripped and added as session
`MetadataReference`s (file-path identity). A later **usage** cell rebinds
the same NuGet types by assembly name. Roslyn then treats two copies of a
NuGet type (for example `JinagaClient` or `User`) as unrelated, even though
the CLR loaded one DLL.

That split is **compile-time**. Aligning package versions, dropping an
explicit `#r "nuget: Jinaga"`, or loading a single `Jinaga.dll` does **not**
fix it. An MSBuild project is fine because it unifies assemblies; Verso
does not.

The split is between **usage cells**, not between `#r` and first use. A
cell that only has `#r` / `using` / helpers can be followed by usage cells
that create the client and call constructors / extensions — including
`j.RenderFacts(...)` and `new SomeViewModel(j, fact)` — when those calls
go through helpers defined in the `#r` cell. CS1929 / CS1503 appear when
one usage cell binds `JinagaClient` / `User` (or a notebook type that
mentions them) and a later usage cell consumes that value.

Look up `#r "nuget: …"` versions from the repo's `.csproj` `PackageReference`s
when those packages exist. Sample versions below are illustrative.

## Terms

- **`#r` cell** — first code cell: `#r`, `using`, and helpers.
- **usage cell** — any later code cell that constructs NuGet types or calls
  extensions on them.

## Workflow

Copy the matching checklist and check items off.

### Author or extend

- [ ] Pin `#r "nuget:…"` versions from the repo `.csproj`
- [ ] Put `#r` / `using` / helpers in the first code cell (Default below)
- [ ] Read [graph-scaling.md](references/graph-scaling.md) and paste those helpers
- [ ] Declare sketch types in the notebook; do not `#r` the product DLL
- [ ] Graphs: `ShowTypes` / `ShowFacts`. Tables: `rows.Display()`
- [ ] Verify with `verso run path --fail-fast` (not the editor alone)
- [ ] After a CLI rewrite, tell the user to close the tab and reopen.
      Saving the stale tab overwrites the CLI result.

### Port from Polyglot / .NET Interactive

- [ ] `verso convert path --to verso --strip-outputs`
- [ ] Read [display.md](references/display.md); replace `AsTable` /
      `ShowHtml(AsTable)` with `rows.Display()`
- [ ] Route `RenderTypes` / `RenderFacts` through `ShowTypes` / `ShowFacts`
- [ ] Apply Default (helpers) or the one-usage-cell escape hatch
- [ ] Same verify / reopen steps as Author

### Debug a compiler or load error

- [ ] Read [errors.md](references/errors.md) and match the diagnostic
- [ ] Do not "fix" CS1929 / CS1503 by aligning package versions
- [ ] Same verify / reopen steps as Author

`--output json --output-file out.json` shows MIME and HTML: collection
tables contain `<table>`; a failed `AsTable` port is plaintext under
`text/html`. Graphs need Graphviz `dot` on `PATH`.

## What still works across usage cells

- Instance methods on values created earlier: `j.Fact(...)`, `j.Watch(...)`,
  `j.Query(...)`.
- Static methods that do not take a split type as `this`:
  `Renderer.RenderTypes(typeof(...))`.
- Notebook-defined records that only mention **other notebook types**
  (`new Child(parent, ...)` after both types were declared in an earlier
  cell).
- Absolute `#r "D:/path/to/local.dll"` **alone** (no NuGet type in that
  DLL's public API used across cells), when the DLL TFM matches the Verso
  host. Relative `#r` does not. That is not a reason to `#r` the app's
  class library — see Packages and local DLLs.

## Default — helpers in the `#r` cell

Keep cells for readability. Define helpers in the **same cell as the
`#r "nuget:…"` lines** (or the same cell as the record that mentions the
NuGet type). Public parameters used from later cells are `object`; the
cast happens inside that cell. Prefer helpers that call `Display` so later
cells can show more than one graph.

Paste the helpers from [graph-scaling.md](references/graph-scaling.md)
into this cell after `using`.

```csharp
#r "nuget: Jinaga, 1.1.50"
#r "nuget: Jinaga.Notebooks, 1.1.14"
#r "nuget: Jinaga.UnitTest, 1.1.8"
using Jinaga;
using Jinaga.Notebooks;
using Jinaga.UnitTest;

// Paste graph-scaling.md helpers here (ShowHtml, ShowTypes, ShowFacts,
// ShowTable, ShowGraph, WrapGraphSvg, ExtractSvg, ReadSvgAttr, StripSvgAttr).
```

```csharp
[FactType("Example.Item")]
record Item(User owner);

Item NewItem(object user) => new((User)user);
```

```csharp
class ItemViewModel : IDisposable
{
    public ItemViewModel(object client, Item item)
    {
        var jinaga = (JinagaClient)client;
        _observer = jinaga.Watch(/* ... */);
    }
}
```

Later cells:

```csharp
var j = JinagaTest.Create();
var item = await j.Fact(NewItem(new User("u1")));
ShowFacts(j, item);        // not j.RenderFacts(...); Fit + toolbar
ShowFacts(50, j, item);    // start at 50% of native Graphviz size
ShowTable(points);         // not points.AsTable()
```

A nested class that calls a top-level helper: make that helper `static`.
Omit `public` on notebook sketch types (records and the view-model class).

## Escape hatch — one usage cell after `#r`

Use when helpers would be more ceremony than the notebook is worth. Keep
`#r` and `using` in the first code cell. Put types, `JinagaTest.Create()`,
`j.Fact`, `j.RenderFacts`, and view-model construction in **one** following
code cell. Markdown cells around them are fine. That is one compilation of
the NuGet types that cell consumes. Putting `#r` in that usage cell as well
also works; it is not required.

One source of each sketched type in that usage cell: **either** declare it
there **or** `#r` a DLL the host can load — not both copies of the same
type. One usage cell does **not** make a newer-TFM app DLL load on
`verso run`. Prefer declaring sketch types in the cell.

```csharp
#r "nuget: Jinaga, 1.1.50"
#r "nuget: Jinaga.Notebooks, 1.1.14"
#r "nuget: Jinaga.UnitTest, 1.1.8"
using Jinaga;
using Jinaga.Notebooks;
using Jinaga.UnitTest;
```

```csharp
[FactType("Example.Item")]
record Item(User owner, DateTime createdAt);

var j = JinagaTest.Create();
var item = await j.Fact(new Item(new User("u1"), DateTime.UtcNow));
j.RenderFacts(item).ToString().Display("text/html");  // unscaled; prefer ShowFacts
points.Display();   // not points.AsTable()
```

## Display

`Verso.Abstractions` is a default import. Call `.Display()` any number of
times; it does not need to be the last line. `CellOutput` appears only as
the cell's last **expression** (no trailing semicolon).

- Graphs: `ShowTypes` / `ShowFacts` (a bare `HtmlString` renders as an
  object tree)
- Tables: `rows.Display()` (not Polyglot `AsTable`)

Details: [display.md](references/display.md).

## Packages and local DLLs

Pin `#r "nuget: …"` versions to the same `PackageReference` versions the
app or test project uses, when those projects exist.

**Default.** Declare exploratory types in the notebook. When a type belongs
in the product, add it to the class library and consume it from the app or
tests. Putting it in that library does not replace the notebook type: while
the notebook still uses it, keep the notebook declaration. Do not `#r` the
product DLL to "reuse" it — the DLL's TFM often differs from `verso run`,
and a notebook type plus a library type with the same name are two types.

**When the cell must construct a library-only type.** `#r` a local DLL
built for the host's TFM (typically net8 under `verso run`) in the `#r`
cell, using an **absolute** path. The usage cell then uses the DLL type
and does not also declare it. A `net10.0` (or other newer-than-host) app
library is not that DLL. A relative `#r` does not work in C#. See
[errors.md](references/errors.md) (CS0006, FileNotFoundException).

## References

- Compiler and load errors: [errors.md](references/errors.md)
- Display, CellOutput, AsTable: [display.md](references/display.md)
- Graph Fit / percent toolbar helpers: [graph-scaling.md](references/graph-scaling.md)
