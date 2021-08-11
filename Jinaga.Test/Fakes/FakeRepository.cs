using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    public class FakeRepository<T>
    {
        private int nextId = 1;
        private readonly Dictionary<int, T> items = new Dictionary<int, T>();

        public IEnumerable<T> Items => items.Values;

        public Task<int> Insert(T item)
        {
            int id = nextId++;
            items.Add(id, item);
            return Task.FromResult(id);
        }

        public Task Delete(int id)
        {
            items.Remove(id);
            return Task.CompletedTask;
        }
    }
}
