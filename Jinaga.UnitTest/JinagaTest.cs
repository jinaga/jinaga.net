using Jinaga.Services;
using Jinaga.Storage;

namespace Jinaga.UnitTest
{
    public class JinagaTest
    {
        public static JinagaClient Create()
        {
            return new JinagaClient(new MemoryStore(), new SimulatedNetwork());
        }
    }
}