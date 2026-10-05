namespace Jinaga.Notebooks.Test.Models;

// Deletion and restoration markers, and near misses that must not be read as markers.
// A marker's fact type name is its parent's name followed by ".Deleted" or ".Restored".

[FactType("Corporate.Employee.Deleted")]
public record EmployeeDeleted(Employee employee, DateTime deletedAt);

[FactType("Corporate.Office.Deleted")]
public record OfficeDeleted(Office office);

[FactType("Corporate.Office.Restored")]
public record OfficeRestored(OfficeDeleted deleted);

// Ends in "Deleted", but is not named for its parent.
[FactType("Corporate.AuditDeleted")]
public record AuditDeleted(Company company);

// Named for its parent, but without the dot.
[FactType("Corporate.CompanyDeleted")]
public record CompanyDeleted(Company company);

// Records who deleted it, so it is more than a marker.
[FactType("Corporate.Badge.Deleted")]
public record BadgeDeleted(Badge badge, Employee by);

// Has a successor that is not a restoration.
[FactType("Corporate.Assignment.Deleted")]
public record AssignmentDeleted(Assignment assignment);

[FactType("Corporate.Assignment.Deleted.Reason")]
public record AssignmentDeletionReason(AssignmentDeleted deleted, string reason);

// The restoration records who restored it, so it is more than a marker.
[FactType("Corporate.Transfer.Deleted")]
public record TransferDeleted(Transfer transfer);

[FactType("Corporate.Transfer.Restored")]
public record TransferRestored(TransferDeleted deleted, Employee by);
