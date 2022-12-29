using System;
using Xunit;

namespace Jinaga.Test
{
    public static class Helpers
    {
        public static string Indented(int depth, string str)
        {
            Assert.Equal("\r\n", str.Substring(0, 2));
            Assert.Equal(new String(' ', 4 * depth), str.Substring(str.Length - 4 * depth));
            return str.Substring(2, str.Length - 4 * depth - 2).Replace("\r", "");
        }
    }
}
