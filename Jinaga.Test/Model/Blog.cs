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
