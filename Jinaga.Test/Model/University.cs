using Jinaga.Extensions;
using System.Linq;

namespace Jinaga.Test.Model.University;

[FactType("University.Student")]
public record Student(string publicKey);

[FactType("University.Organization")]
public record Organization(string identifier);

[FactType("University.Application")]
public record Application(Student student, Organization organization, DateTime appliedAt);

[FactType("University.Enrollment")]
public record Enrollment(Application application);

[FactType("University.Rejection")]
public record Rejection(Application application);

[FactType("University.Course")]
public record Course(Organization organization, string code, string name);

[FactType("University.Semester")]
public record Semester(Organization organization, int year, string term);

[FactType("University.Instructor")]
public record Instructor(Organization organization, string name);

[FactType("University.Offering")]
public record Offering(Course course, Semester semester, Instructor instructor, string days, string time)
{
    public Relation<OfferingLocation> Locations => Relation.Define<OfferingLocation>(() =>
        from location in this.Successors().OfType<OfferingLocation>(location => location.offering)
        where location.Successors().No<OfferingLocation>(next => next.prior)
        select location
    );
}

[FactType("University.Offering.Location")]
public record OfferingLocation(Offering offering, string building, string room, OfferingLocation[] prior);

[FactType("University.Offering.Delete")]
public record OfferingDelete(Offering offering, DateTime deletedAt);

[FactType("University.Registration")]
public record Registration(Enrollment enrollment, Offering offering);

[FactType("University.Drop")]
public record Drop(Registration registration);

[FactType("University.Fail")]
public record Fail(Registration registration, int grade);

[FactType("University.Complete")]
public record Complete(Registration registration, int grade);

[FactType("SearchIndex.Record")]
public record SearchIndexRecord(Offering offering, Guid recordId);

[FactType("SearchIndex.Record.LocationUpdate")]
public record SearchIndexRecordLocationUpdate(SearchIndexRecord record, OfferingLocation location);
