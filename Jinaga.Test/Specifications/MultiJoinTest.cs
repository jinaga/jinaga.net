using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Jinaga.Extensions;
using Jinaga.Test.Model.University;

namespace Jinaga.Test.Specifications;

public class MultiJoinTest
{
    class SearchRecord
    {
        public string CourseCode { get; init; }
        public string CourseName { get; init; }
        public string Days { get; init; }
        public string Time { get; init; }
        public string Instructor { get; init; }
        public string Location { get; set; }
    }

    [Fact]
    public void CanInvertSpecificationWithMultipleJoins()
    {
        // Existential conditions move to their inverses. They attach to the lowest
        // unknown that the condition references.
        var recordsToUpdate = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            from record in offering.Successors().OfType<SearchIndexRecord>(record => record.offering)
            from location in offering.Locations
            where !(
                from update in record.Successors().OfType<SearchIndexRecordLocationUpdate>(update => update.record)
                where update.location == location
                select update
            ).Any()
            select new { record, location }
        );
        var inverses = recordsToUpdate.ComputeInverses().Select(inverse =>
            inverse.InverseSpecification.ToString().ReplaceLineEndings()).ToArray();
        inverses.Should().BeEquivalentTo([
            """
            (location: University.Offering.Location [
                !E {
                    next: University.Offering.Location [
                        next->prior: University.Offering.Location = location
                    ]
                }
            ]) {
                offering: University.Offering [
                    offering = location->offering: University.Offering
                ]
                record: SearchIndex.Record [
                    record->offering: University.Offering = offering
                    !E {
                        update: SearchIndex.Record.LocationUpdate [
                            update->record: SearchIndex.Record = record
                            update->location: University.Offering.Location = location
                        ]
                    }
                ]
                semester: University.Semester [
                    semester = offering->semester: University.Semester
                ]
            } => {
                location = location
                record = record
            }

            """,
            """
            (next: University.Offering.Location) {
                location: University.Offering.Location [
                    location = next->prior: University.Offering.Location
                ]
                offering: University.Offering [
                    offering = location->offering: University.Offering
                ]
                semester: University.Semester [
                    semester = offering->semester: University.Semester
                ]
                record: SearchIndex.Record [
                    record->offering: University.Offering = offering
                    !E {
                        update: SearchIndex.Record.LocationUpdate [
                            update->record: SearchIndex.Record = record
                            update->location: University.Offering.Location = location
                        ]
                    }
                ]
            } => {
                location = location
                record = record
            }

            """,
            """
            (record: SearchIndex.Record) {
                offering: University.Offering [
                    offering = record->offering: University.Offering
                ]
                semester: University.Semester [
                    semester = offering->semester: University.Semester
                ]
                location: University.Offering.Location [
                    location->offering: University.Offering = offering
                    !E {
                        next: University.Offering.Location [
                            next->prior: University.Offering.Location = location
                        ]
                    }
                    !E {
                        update: SearchIndex.Record.LocationUpdate [
                            update->record: SearchIndex.Record = record
                            update->location: University.Offering.Location = location
                        ]
                    }
                ]
            } => {
                location = location
                record = record
            }

            """,
            """
            (update: SearchIndex.Record.LocationUpdate) {
                record: SearchIndex.Record [
                    record = update->record: SearchIndex.Record
                ]
                offering: University.Offering [
                    offering = record->offering: University.Offering
                ]
                semester: University.Semester [
                    semester = offering->semester: University.Semester
                ]
                location: University.Offering.Location [
                    location->offering: University.Offering = offering
                    location = update->location: University.Offering.Location
                    !E {
                        next: University.Offering.Location [
                            next->prior: University.Offering.Location = location
                        ]
                    }
                ]
            } => {
                location = location
                record = record
            }

            """
        ]);
    }

    [Fact]
    public async Task CanRunInverseSpecificationWithMultipleJoins()
    {
        var j = JinagaTest.Create();

        var university = await j.Fact(new Organization("6003"));
        var student = await j.Fact(new Student("---PUBLIC KEY---"));
        var application = await j.Fact(new Application(student, university, DateTime.Parse("2022-02-04")));
        var enrollment = await j.Fact(new Enrollment(application));

        List<Course> courses = [
            await j.Fact(new Course(university, "CS 101", "Introduction to Computer Science")),
            await j.Fact(new Course(university, "CS 201", "Data Structures and Algorithms")),
            await j.Fact(new Course(university, "CS 301", "Software Engineering")),
            await j.Fact(new Course(university, "CS 401", "Artificial Intelligence")),
            await j.Fact(new Course(university, "CS 501", "Machine Learning")),
            await j.Fact(new Course(university, "CS 601", "Quantum Computing"))
        ];

        List<Instructor> instructors = [
            await j.Fact(new Instructor(university, "Dr. Smith")),
            await j.Fact(new Instructor(university, "Dr. Jones")),
            await j.Fact(new Instructor(university, "Dr. Lee")),
            await j.Fact(new Instructor(university, "Dr. Kim")),
            await j.Fact(new Instructor(university, "Dr. Patel")),
            await j.Fact(new Instructor(university, "Dr. Singh"))
        ];

        List<Semester> semesters = [
            await j.Fact(new Semester(university, 2022, "Spring")),
            await j.Fact(new Semester(university, 2022, "Summer")),
            await j.Fact(new Semester(university, 2022, "Fall")),
            await j.Fact(new Semester(university, 2023, "Spring")),
            await j.Fact(new Semester(university, 2023, "Summer")),
            await j.Fact(new Semester(university, 2023, "Fall"))
        ];
        var currentSemester = semesters[1];

        var random = new Random(29693);

        List<Offering> offerings = new List<Offering>();
        string[] possibleDays = new string[] { "MF", "TTr", "MW", "WF" };
        string[] possibleBuildings = new string[] { "Building A", "Building B", "Building C", "Building D" };
        string[] possibleRooms = new string[] { "101", "102", "103", "104" };
        for (int i = 0; i < 100; i++)
        {
            var course = courses[random.Next(courses.Count)];
            var semester = semesters[random.Next(semesters.Count)];
            var instructor = instructors[random.Next(instructors.Count)];
            var days = possibleDays[random.Next(possibleDays.Length)];
            var time = (8 + random.Next(12)).ToString() + ":00";
            var building = possibleBuildings[random.Next(possibleBuildings.Length)];
            var room = possibleRooms[random.Next(possibleRooms.Length)];
            var offering = await j.Fact(new Offering(course, semester, instructor, days, time));
            var location = await j.Fact(new OfferingLocation(offering, building, room, new OfferingLocation[0]));
            offerings.Add(offering);
        }

        // Watch for offerings to index.
        var index = new Dictionary<Guid, SearchRecord>();
        var indexRecordCreatedEvent = new CountdownEvent(19);
        var indexRecordUpdatedEvent = new CountdownEvent(20);

        var offeringsToIndex = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
            where offering.Successors().No<SearchIndexRecord>(record => record.offering)
            select offering);
        var indexInsertSubscription = j.Subscribe(offeringsToIndex, currentSemester, async offering =>
        {
            // Create a record for the offering
            var recordId = Guid.NewGuid();
            index[recordId] = new SearchRecord
            {
                CourseCode = offering.course.code,
                CourseName = offering.course.name,
                Days = offering.days,
                Time = offering.time,
                Instructor = offering.instructor.name,
                Location = "TBA"
            };
            await j.Fact(new SearchIndexRecord(offering, recordId));
            // Count the number of times that an index record is created.
            indexRecordCreatedEvent.Signal();
        });

        // Watch for index records to update.
        var recordsToUpdate = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            from record in offering.Successors().OfType<SearchIndexRecord>(record => record.offering)
            from location in offering.Locations
            where !(
                from update in record.Successors().OfType<SearchIndexRecordLocationUpdate>(update => update.record)
                where update.location == location
                select update
            ).Any()
            select new { record, location }
        );
        var indexUpdateSubscription = j.Subscribe(recordsToUpdate, currentSemester, async work =>
        {
            var record = work.record;
            var location = work.location;
            index[record.recordId].Location = location.building + " " + location.room;
            await j.Fact(new SearchIndexRecordLocationUpdate(record, location));
            // Count the number of times that an index record is updated.
            indexRecordUpdatedEvent.Signal();
        });

        // Verify that the index is in the correct initial state.
        // The location "Building E 105" is not expected to be in the initial state.
        indexRecordCreatedEvent.Wait(5000);
        index.Values.Where(i => i.Location == "Building E 105").Should().BeEmpty();

        // Trigger an index update.
        var offeringAndLocationInSemester = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            from location in offering.Locations
            select new { offering, location }
        );
        var offeringsAndLocations = await j.Query(offeringAndLocationInSemester, currentSemester);
        await j.Fact(new OfferingLocation(offeringsAndLocations[0].offering, "Building E", "105", [offeringsAndLocations[0].location]));

        await j.Unload();

        // Verify that the index was updated.
        // The location "Building E 105" is expected to be in the updated state exactly once.
        indexRecordUpdatedEvent.Wait(5000);
        index.Values.Count(i => i.Location == "Building E 105").Should().Be(1);
    }
}