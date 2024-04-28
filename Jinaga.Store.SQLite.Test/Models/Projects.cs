namespace Jinaga.Store.SQLite.Test.Models;

[FactType("Company")]
internal record ProjectCompany() { }

[FactType("Department")]
internal record Department(ProjectCompany company) { }

[FactType("Project")]
internal record Project(Department department) { }

[FactType("Project.Deleted")]
internal record ProjectDeleted(Project project) { }

[FactType("Project.Restored")]
internal record ProjectRestored(ProjectDeleted deleted) { }

[FactType("Project.Name")]
internal record ProjectName(Project project, string value, ProjectName[] prior) { }

[FactType("Assignment")]
internal record Assignment(Project project, User user, DateTime createdAt) { }