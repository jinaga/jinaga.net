namespace Jinaga.Test.Model
{
    [FactType("Skylane.Passenger.Name")]
    public partial record PassengerName(Passenger passenger, string value, PassengerName[] prior);
}