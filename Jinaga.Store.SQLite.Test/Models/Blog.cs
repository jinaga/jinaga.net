namespace Jinaga.Store.SQLite.Test.Models;


[FactType("Blog.Site")]
public record Site(string domain);


[FactType("Blog.Post")]
public record Post(Site site, string createdAt);


[FactType("Blog.Post.Title")]
public record Title(Post post, string value, Title[] prior);

