using Jinaga.Records;

namespace Jinaga.Http
{
    public class LoginResponse
    {
        public FactRecord UserFact { get; set; }
        public ProfileRequest Profile { get; set; }
    }
}