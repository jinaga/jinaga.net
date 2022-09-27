using Jinaga.Communication;
using Jinaga.Storage;
using System;

namespace Jinaga
{
    public static class JinagaClient
    {
        public static Jinaga Create()
        {
            Uri baseUrl = new Uri("http://localhost:8080/jinaga");
            return new Jinaga(new MemoryStore(), new HttpNetwork(baseUrl));
        }
    }
}
