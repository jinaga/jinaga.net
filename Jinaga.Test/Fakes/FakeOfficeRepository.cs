using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    public class FakeOfficeRepository
    {
        private int nextOfficeId = 1;
        private readonly Dictionary<int, OfficeRow> offices = new();

        public IEnumerable<OfficeRow> Offices => offices.Values;

        public Task<int> InsertOffice(string city)
        {
            var officeId = nextOfficeId++;
            offices.Add(officeId, new OfficeRow
            {
                OfficeId = officeId,
                City = city,
                Name = ""
            });
            return Task.FromResult(officeId);
        }

        public Task<OfficeRow> GetOffice(int officeId)
        {
            return Task.FromResult(offices[officeId]);
        }

        public Task<int> UpdateOffice(int officeId, string name)
        {
            offices[officeId].Name = name;
            return Task.FromResult(officeId);
        }

        public Task<int> DeleteOffice(int officeId)
        {
            offices.Remove(officeId);
            return Task.FromResult(officeId);
        }
    }
}
