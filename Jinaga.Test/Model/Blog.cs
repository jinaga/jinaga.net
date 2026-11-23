namespace Jinaga.Test.Model;

[FactType("Blog.Site")]
public record Site(User creator, string identifier) { }

[FactType("Blog.GuestBlogger")]
public record GuestBlogger(Site site, User guest) { }

[FactType("Blog.Content")]
public record Content(Site site, string path) { }

[FactType("Blog.Content")]
public record ContentV2(Site site, DateTime? createdAt) { }

[FactType("Blog.Comment")]
public record Comment(Content content, Guid uniqueId, User author) { }

[FactType("Blog.Content.Publish")]
public record Publish(Content content, DateTime date) { }

[FactType("Blog.GuestBlogger.Revoked")]
public record GuestBloggerRevoked(GuestBlogger invitation) { }

// A deliberately lowercase fact type name. `Blog.post` and `Blog.Site` differ in case, which
// is where a linguistic collation and an ordinal one disagree, so a pair like this one is what
// lets a test tell which of the two a sort used.
[FactType("Blog.post")]
public record LowercasePost(Site site, string slug) { }
