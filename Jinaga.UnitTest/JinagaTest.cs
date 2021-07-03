using System;

namespace Jinaga.UnitTest
{
    public class JinagaTest
    {
        public static Jinaga Create()
        {
            return new Jinaga(new MemoryStore());
        }
    }
}