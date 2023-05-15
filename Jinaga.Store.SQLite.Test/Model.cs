namespace Jinaga.Store.SQLite.Test;

[FactType("Company")]
internal record Company() { }

[FactType("Department")]
internal record Department(Company company) { }