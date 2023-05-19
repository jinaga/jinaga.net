namespace Jinaga.Store.SQLite.Test;

[FactType("Company")]
internal record Company() { }

[FactType("Department")]
internal record Department(Company company) { }

[FactType("Project")]
internal record Project(Department department) { }

[FactType("Project.Deleted")]
internal record ProjectDeleted(Project project) { }