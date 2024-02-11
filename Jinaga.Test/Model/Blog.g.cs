using Jinaga.Facts;
using Jinaga.Serialization;

namespace Jinaga.Test.Model;

public partial record Site : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record GuestBlogger : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record Content : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record ContentV2 : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record Comment : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record Publish : IFactProxy
{
    public FactGraph Graph { get; set; }
}