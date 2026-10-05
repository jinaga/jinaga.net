using System.Globalization;
using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.FactGraphs;

public class FactLabelTest
{
    private readonly JinagaClient j = JinagaTest.Create();

    private static readonly Site site = new(new Region("North"), null);

    [Fact]
    public void EveryNullFieldIsShownAsNull()
    {
        var measurement = new Measurement(site, null, null, null, null, null, null);

        var graph = RenderedFactGraph.Of(j, measurement);

        graph.ShouldShow(measurement)
            .WithField(nameof(Measurement.count), "null")
            .WithField(nameof(Measurement.average), "null")
            .WithField(nameof(Measurement.valid), "null")
            .WithField(nameof(Measurement.takenAt), "null")
            .WithField(nameof(Measurement.token), "null")
            .WithField(nameof(Measurement.note), "null");
    }

    [Fact]
    public void EmptyStringIsDistinguishableFromNull()
    {
        var empty = new Measurement(site, null, null, null, null, null, "");

        var graph = RenderedFactGraph.Of(j, empty);

        graph.ShouldShow(empty).WithField(nameof(Measurement.note), "");
    }

    [Fact]
    public void FactTypeNameWithAnAmpersandIsShownAsWritten()
    {
        var ampersand = new Ampersand("x");

        var graph = RenderedFactGraph.Of(j, ampersand);

        graph.ShouldShow(ampersand).OfItsType();
    }

    [Fact]
    public void FactTypeNameOutsideAsciiIsShownAsWritten()
    {
        var unicode = new Unicode("x");

        var graph = RenderedFactGraph.Of(j, unicode);

        graph.ShouldShow(unicode).OfItsType();
    }

    [Fact]
    public void DottedFactTypeNameIsShownUnchanged()
    {
        var dallas = new Office(new Company("Acme"), "Dallas");

        var graph = RenderedFactGraph.Of(j, dallas);

        graph.ShouldShow(dallas).OfItsType();
    }

    [Fact]
    public void DoubleFieldIsShownTheSameUnderAnyCulture()
    {
        var measurement = new Measurement(site, null, 1.5, null, null, null, null);

        var german = InCulture("de-DE", () => Render(measurement));
        var invariant = InCulture(CultureInfo.InvariantCulture, () => Render(measurement));

        german.Should().Be(invariant);
    }

    private string Render(params object[] projections) =>
        Jinaga.Notebooks.Dot.JinagaClientExtensions.RenderFacts(j, projections);

    private static T InCulture<T>(string name, Func<T> render) =>
        InCulture(CultureInfo.GetCultureInfo(name), render);

    private static T InCulture<T>(CultureInfo culture, Func<T> render)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            return render();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
