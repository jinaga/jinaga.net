using System;

namespace Jinaga.Test
{
    static class TestingExtensions
    {
        public static T SuchThat<T>(this T obj, Action<T> test)
        {
            test(obj);
            return obj;
        }
    }
}