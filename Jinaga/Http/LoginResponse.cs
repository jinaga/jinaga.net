using Jinaga.Records;

namespace Jinaga.Http
{
    public class LoginResponse
    {
        public FactRecord UserFact { get; set; } = new FactRecord();
        public ProfileRequest Profile { get; set; } = new ProfileRequest();
    }
}