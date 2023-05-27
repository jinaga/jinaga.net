namespace Jinaga.Test.Model;

[FactType("Department")]
internal record Department(Company company) { }

[FactType("Project")]
internal record Project(Department department) { }

[FactType("Project.Deleted")]
internal record ProjectDeleted(Project project) { }

[FactType("Project.Restored")]
internal record ProjectRestored(ProjectDeleted deleted) { }