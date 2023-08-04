namespace Jinaga.Store.SQLite.Test.Models;


[FactType("Blog.Site")]
internal record Site(string domain);


[FactType("Blog.Post")]
internal record Post(Site site, string createdAt);


[FactType("Blog.Post.Title")]
internal record Title(Post post, string value, Title[] prior);

