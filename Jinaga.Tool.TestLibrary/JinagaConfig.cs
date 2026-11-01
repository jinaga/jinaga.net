using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Tool.TestLibrary;

[FactType("Blog.Site")]
public record Site(User creator, string identifier);

[FactType("Blog.Content")]
public record Content(Site site, string path);

/// <summary>
/// The class the tool looks for, holding the rules it prints and deploys.
/// </summary>
public static class JinagaConfig
{
    public static string Authorization() => Described(AuthorizationRules.Describe(Authorization));

    public static string Distribution() => Described(DistributionRules.Describe(Distribution));

    public static string Purge() => Described(PurgeConditions.Describe(Purge));

    private static AuthorizationRules Authorization(AuthorizationRules r) => r
        .Any<User>()
        .Type<Site>(site => site.creator)
        .Type<Content>(content => content.site.creator);

    private static DistributionRules Distribution(DistributionRules r) => r
        .Share(Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select content
        ))
        .With(site => site.creator);

    private static PurgeConditions Purge(PurgeConditions r) => r;

    /// <summary>
    /// Touches a type from a package this project does not copy beside itself,
    /// the way a real JinagaConfig touches the logging types a JinagaClient
    /// needs. Without dependency resolution the tool fails here rather than in
    /// the rules themselves.
    /// </summary>
    private static string Described(string rules)
    {
        _ = NullLogger.Instance;
        return rules;
    }
}
