# Graph scaling for `ShowTypes` / `ShowFacts`

Graphviz SVG from `Renderer.RenderTypes` / `RenderFacts` has a native width
in points. Unwrapped, a large type graph overflows the cell. Put these
helpers in the **same cell as `#r "nuget:…"`** (the `#r` cell).

## Contents

- Usage
- Helpers

## Usage

```csharp
ShowTypes(typeof(Item));            // Fit (default): native size, max 100% of cell
ShowTypes(50, typeof(Item));        // start at 50% of native Graphviz size
ShowFacts(j, item);                 // Fit + toolbar
ShowFacts(75, j, item);             // start at 75%
```

Each drawing has a toolbar: **Fit**, **50%**, **75%**, **100%**, **150%**.
Percent is relative to Graphviz native size, not the cell. A custom
`scalePercent` that is not in that list is added as an extra button and
selected. `scalePercent <= 0` is Fit.

The toolbar is CSS (`:has`), so it works without executing scripts in the
HTML output.

## Helpers

Paste into the `#r` cell after the `using`s:

```csharp
void ShowHtml(object value) =>
    (value?.ToString() ?? "").Display("text/html");

void ShowTypes(params Type[] types) =>
    ShowGraph(Renderer.RenderTypes(types));

void ShowTypes(int scalePercent, params Type[] types) =>
    ShowGraph(Renderer.RenderTypes(types), scalePercent);

void ShowFacts(object client, params object[] facts) =>
    ShowGraph(JinagaClientExtensions.RenderFacts((JinagaClient)client, facts));

void ShowFacts(int scalePercent, object client, params object[] facts) =>
    ShowGraph(JinagaClientExtensions.RenderFacts((JinagaClient)client, facts), scalePercent);

void ShowTable<T>(IEnumerable<T> rows) =>
    rows.Display();

void ShowGraph(object value, int scalePercent = 0) =>
    ShowHtml(WrapGraphSvg(value?.ToString() ?? "", scalePercent));

string WrapGraphSvg(string html, int scalePercent)
{
    var svg = ExtractSvg(html);
    if (string.IsNullOrEmpty(svg))
        return html;

    var nativeWidth = ReadSvgAttr(svg, "width") ?? "100%";
    svg = StripSvgAttr(StripSvgAttr(svg, "width"), "height");

    var id = "jg" + Guid.NewGuid().ToString("N");
    var presets = new[] { 50, 75, 100, 150 };
    var useFit = scalePercent <= 0;
    var custom = !useFit && Array.IndexOf(presets, scalePercent) < 0;
    var customFactor = (scalePercent / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture);

    var sb = new System.Text.StringBuilder();
    sb.Append("<div id=\"").Append(id).Append("\" class=\"jg\" style=\"--jw:")
        .Append(System.Net.WebUtility.HtmlEncode(nativeWidth))
        .Append("\" data-verso-interactive>");
    sb.Append("<style>");
    sb.Append('#').Append(id).Append("{border:1px solid var(--verso-border-default,#ccc);border-radius:6px;overflow:hidden;font:12px system-ui,sans-serif}");
    sb.Append('#').Append(id).Append(" .tb{display:flex;flex-wrap:wrap;gap:4px;align-items:center;padding:6px 8px;border-bottom:1px solid var(--verso-border-default,#ccc)}");
    sb.Append('#').Append(id).Append(" .tb label{cursor:pointer;user-select:none}");
    sb.Append('#').Append(id).Append(" .tb input{position:absolute;opacity:0;pointer-events:none}");
    sb.Append('#').Append(id).Append(" .tb span{display:inline-block;padding:2px 8px;border:1px solid var(--verso-border-default,#ccc);border-radius:4px}");
    sb.Append('#').Append(id).Append(" .tb input:checked+span{background:var(--verso-accent-primary,#3366cc);color:#fff;border-color:transparent}");
    sb.Append('#').Append(id).Append(" .vp{overflow:auto;max-height:80vh}");
    sb.Append('#').Append(id).Append(" svg{display:block;height:auto}");
    sb.Append('#').Append(id).Append(":has(.fit:checked) svg{width:var(--jw);max-width:100%}");
    sb.Append('#').Append(id).Append(":has(.p50:checked) svg{width:calc(var(--jw)*0.50);max-width:none}");
    sb.Append('#').Append(id).Append(":has(.p75:checked) svg{width:calc(var(--jw)*0.75);max-width:none}");
    sb.Append('#').Append(id).Append(":has(.p100:checked) svg{width:var(--jw);max-width:none}");
    sb.Append('#').Append(id).Append(":has(.p150:checked) svg{width:calc(var(--jw)*1.50);max-width:none}");
    sb.Append('#').Append(id).Append(":has(.pc:checked) svg{width:calc(var(--jw)*").Append(customFactor).Append(");max-width:none}");
    sb.Append("</style><div class=\"tb\">");

    void Radio(string cls, string label, bool check)
    {
        sb.Append("<label><input type=\"radio\" name=\"").Append(id)
            .Append("\" class=\"").Append(cls).Append('"');
        if (check) sb.Append(" checked");
        sb.Append("><span>").Append(label).Append("</span></label>");
    }

    Radio("fit", "Fit", useFit);
    foreach (var p in presets)
        Radio("p" + p, p + "%", !useFit && !custom && scalePercent == p);
    if (custom)
        Radio("pc", scalePercent + "%", true);

    sb.Append("</div><div class=\"vp\">");
    sb.Append(svg);
    sb.Append("</div></div>");
    return sb.ToString();
}

string ExtractSvg(string html)
{
    var start = html.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
    var end = html.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
    if (start < 0 || end < 0 || end < start)
        return html;
    return html.Substring(start, end + 6 - start);
}

string? ReadSvgAttr(string svg, string name)
{
    var m = System.Text.RegularExpressions.Regex.Match(
        svg,
        @"<svg\b[^>]*\s" + name + @"\s*=\s*[""']([^""']+)[""']",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return m.Success ? m.Groups[1].Value : null;
}

string StripSvgAttr(string svg, string name)
{
    var gt = svg.IndexOf('>');
    if (gt < 0)
        return svg;
    var open = System.Text.RegularExpressions.Regex.Replace(
        svg.Substring(0, gt),
        @"\s+" + name + @"\s*=\s*[""'][^""']*[""']",
        "",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return open + svg.Substring(gt);
}
```
