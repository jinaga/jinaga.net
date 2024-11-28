using Jinaga.Projections;

namespace Jinaga.Azure.Functions;

public static class JinagaBindingConfig
{
    private static readonly Dictionary<string, (Specification, object)> Configurations = new();

    public static void RegisterConfiguration<TProjection>(string key, Specification specification, object startingPoint)
    {
        Configurations[key] = (specification, startingPoint);
    }

    public static (Specification, object) GetConfiguration(string key)
    {
        return Configurations[key];
    }
}