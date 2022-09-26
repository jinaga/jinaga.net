using Jinaga.Communication;
using Jinaga.Storage;

namespace Jinaga
{
    public static class JinagaClient
    {
        public static Jinaga Create()
        {
            return new Jinaga(new MemoryStore(), new HttpNetwork());
        }
    }
}
