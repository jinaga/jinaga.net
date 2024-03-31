using System.Collections.Generic;

namespace Jinaga.Test.Fakes
{
    public class FakeOfficeRepository
    {
        private int nextOfficeId = 1;
        private int nextManagerId = 1;
        private readonly Dictionary<int, OfficeRow> offices = new();
        private readonly Dictionary<int, ManagerRow> managers = new();

        public IEnumerable<OfficeRow> Offices => offices.Values;
        public IEnumerable<ManagerRow> Managers => managers.Values;

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

        public Task<int> UpdateOfficeName(int officeId, string name)
        {
            offices[officeId].Name = name;
            return Task.FromResult(officeId);
        }

        public Task UpdateOfficeHeadcount(int officeId, int value)
        {
            offices[officeId].Headcount = value;
            return Task.CompletedTask;
        }

        public Task<int> DeleteOffice(int officeId)
        {
            offices.Remove(officeId);
            return Task.FromResult(officeId);
        }

        public Task<int> InsertManager(int officeId, int employeeNumber)
        {
            var managerId = nextManagerId++;
            managers.Add(managerId, new ManagerRow
            {
                ManagerId = managerId,
                OfficeId = officeId,
                EmployeeNumber = employeeNumber,
                Name = ""
            });
            return Task.FromResult(managerId);
        }

        public Task UpdateManagerName(int managerId, string name)
        {
            managers[managerId].Name = name;
            return Task.CompletedTask;
        }

        public Task<int> DeleteManager(int managerId)
        {
            managers.Remove(managerId);
            return Task.FromResult(managerId);
        }
    }
}
