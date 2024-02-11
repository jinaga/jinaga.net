using Jinaga.Facts;
using Jinaga.Serialization;

namespace Jinaga.Test.Model;

public partial record Airline : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record Passenger : IFactProxy
{
    public FactGraph Graph { get; set; }
}

public partial record PassengerName : IFactProxy
{
    public FactGraph Graph { get; set; }
}