# Display vs CellOutput

`Verso.Abstractions` is a default import. Do not treat `.Display()` as a
Polyglot-only API.

**`Display()`** writes rich output immediately. Call it any number of times
in a cell; it does not need to be the last line.

```csharp
Renderer.RenderTypes(typeof(Item)).ToString().Display("text/html");
j.RenderFacts(item).ToString().Display("text/html");
```

The optional argument is a MIME hint (`"text/html"`, `"application/json"`,
`"text/plain"`). `HtmlString.ToString().Display("text/html")` renders as
HTML. A bare `HtmlString` (the return of `Renderer.RenderTypes` /
`RenderFacts`) is formatted as an object tree.

**Collections, not `AsTable`.** Verso's collection formatter renders
`IEnumerable<T>` as an HTML `<table>`. Call `.Display()` on the sequence
(no MIME hint). Do **not** port Polyglot `sequence.AsTable()`.

In Polyglot, `AsTable()` builds a DataFrame whose Interactive formatter
emits `<table>`. Verso has no such formatter. `ShowHtml(rows.AsTable())`
(`ToString().Display("text/html")`) is a plaintext column dump labeled
`text/html`: no `<table>`, columns collide (`intervalsTrue`).
`rows.Display()` is a real table (`thead`, one `<td>` per property,
collapsible `List<T> (n items)`).

```csharp
points.Display();

void ShowTable<T>(IEnumerable<T> rows) =>
    rows.Display();
```

Keep `ShowHtml` for raw HTML. Route `Renderer.RenderTypes` / `RenderFacts`
through `ShowTypes` / `ShowFacts` so the SVG gets a Fit / percent toolbar.

**`CellOutput`** is a value, not a write. It appears only as the cell's last
**expression** — no trailing semicolon. Mid-cell `new CellOutput(...)` is
discarded. A last line with `;` is a statement, so it also shows nothing.

```csharp
// last expression only — omit the semicolon
new CellOutput("text/html", html)

// mid-cell: wrap in Display
new CellOutput("text/html", html).Display();
```

Several graphs in one cell: use `Display`, not `CellOutput`. Helpers that
return `CellOutput` only work if the caller uses that return as the last
expression, or calls `.Display()` on it.
