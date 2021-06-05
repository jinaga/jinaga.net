namespace Jinaga.Test.Model
{
    [FactType("Skylane.Passenger")]
    public class Passenger
    {
        public Airline Airline { get; set; }
        public User User { get; set; }
    }
}