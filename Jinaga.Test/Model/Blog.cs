using System;

namespace Jinaga.Test.Model;

[FactType("Blog.Site")]
public partial record Site(User creator, string identifier) { }

[FactType("Blog.GuestBlogger")]
public partial record GuestBlogger(Site site, User guest) { }

[FactType("Blog.Content")]
public partial record Content(Site site, string path) { }

[FactType("Blog.Content")]
public partial record ContentV2(Site site, DateTime? createdAt) { }

[FactType("Blog.Comment")]
public partial record Comment(Content content, Guid uniqueId, User author) { }

[FactType("Blog.Content.Publish")]
public partial record Publish(Content content, DateTime date) { }