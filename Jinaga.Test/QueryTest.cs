using System.Linq;
using Jinaga.Extensions;
using Jinaga.Test.Model;

namespace Jinaga.Test
{
    public class QueryTest
    {
        private readonly JinagaClient j;
        

        public QueryTest()
        {
            j = JinagaTest.Create();            
        }

        [Fact]
        public async Task CanQueryForPredecessors()
        {
            var flight = await j.Fact(new Flight(new AirlineDay(new Airline("IA"), DateTime.Today), 4272));
            var cancellation = await j.Fact(new FlightCancellation(flight, DateTime.UtcNow));

            var specification = Given<FlightCancellation>.Match(
                flightCancellation => flightCancellation.flight
            );
            var flights = await j.Query(specification, cancellation);

            flights.Should().ContainSingle().Which.Should().BeEquivalentTo(flight);
        }

        [Fact]
        public async Task CanQueryForSuccessors()
        {
            var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));
            var flight = await j.Fact(new Flight(airlineDay, 4247));

            var specification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay
                select flight
            );
            var flights = await j.Query(specification, airlineDay);

            flights.Should().ContainSingle().Which.Should().BeEquivalentTo(flight);
        }

        [Fact]
        public async Task CanQueryMultipleSteps()
        {
            var airline = await j.Fact(new Airline("IA"));
            var airlineDay = await j.Fact(new AirlineDay(airline, DateTime.Today));
            var flight = await j.Fact(new Flight(airlineDay, 4247));
            var expectedPassengers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
                BookPassenger(flight)));

            var passengersForAirline = Given<Airline>.Match((airline, facts) =>
                from booking in facts.OfType<Booking>()
                where booking.flight.airlineDay.airline == airline
                from passenger in facts.OfType<Passenger>()
                where passenger == booking.passenger
                select passenger
            );

            var passengers = await j.Query(passengersForAirline, airline);
            passengers.Should().BeEquivalentTo(expectedPassengers);
        }

        private async Task<Passenger> BookPassenger(Flight flight)
        {
            var random = new Random();
            var publicKey = $"--- PUBLIC KEY {random.Next(1000)} ---";
            var passenger = await j.Fact(new Passenger(flight.airlineDay.airline, new User(publicKey)));
            var booking = await j.Fact(new Booking(flight, passenger, DateTime.Now));
            return passenger;
        }

        [Fact]
        public async Task CanQueryBasedOnCondition()
        {
            var airline = await j.Fact(new Airline("IA"));
            var airlineDay = await j.Fact(new AirlineDay(airline, DateTime.Today));
            var flight = await j.Fact(new Flight(airlineDay, 4247));
            var cancelledFlight = await j.Fact(new Flight(airlineDay, 5555));
            await j.Fact(new FlightCancellation(cancelledFlight, DateTime.Now));

            var specification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay
                where !flight.IsCancelled
                select flight
            );

            var flights = await j.Query(specification, airlineDay);
            flights.Should().ContainSingle().Which
                .Should().BeEquivalentTo(flight);            
        }

        [Fact]
        public async Task CanQueryForCurrentValueOfMutableProperty()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));

            var initialName = await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[] { }));
            var updatedName = await j.Fact(new PassengerName(passenger, "Joseph", new PassengerName[] { initialName }));

            var currentNames = await j.Query(Given<Passenger>.Match((passenger, facts) =>
                from name in facts.OfType<PassengerName>()
                where name.passenger == passenger
                where !(
                    from next in facts.OfType<PassengerName>()
                    where next.prior.Contains(name)
                    select next
                ).Any()
                select name
            ), passenger);

            currentNames.Should().ContainSingle().Which
                .value.Should().Be("Joseph");
        }

        [Fact]
        public async Task CanQueryForCurrentValueOfMutablePropertyWithSuccessorsExtensionMethod()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));

            var initialName = await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[] { }));
            var updatedName = await j.Fact(new PassengerName(passenger, "Joseph", new PassengerName[] { initialName }));

            var currentNames = await j.Query(Given<Passenger>.Match(passenger =>
                from name in passenger.Successors().OfType<PassengerName>(name => name.passenger)
                where name.Successors().No<PassengerName>(next => next.prior)
                select name
            ), passenger);

            currentNames.Should().ContainSingle().Which
                .value.Should().Be("Joseph");
        }

        [Fact]
        public async Task CanQueryForCurrentValueOfMutablePropertyWithWhereNo()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));

            var initialName = await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[] { }));
            var updatedName = await j.Fact(new PassengerName(passenger, "Joseph", new PassengerName[] { initialName }));

            var currentNames = await j.Query(Given<Passenger>.Match(passenger =>
                passenger.Successors().OfType<PassengerName>(name => name.passenger)
                    .WhereNo((PassengerName next) => next.prior)
            ), passenger);

            currentNames.Should().ContainSingle().Which
                .value.Should().Be("Joseph");
        }

        [Fact]
        public async Task Query_ProjectTwoFacts()
        {
            var ia = await j.Fact(new Airline("IA"));
            var airlineDay = await j.Fact(new AirlineDay(ia, DateTime.Today));
            var flight = await j.Fact(new Flight(airlineDay, 4247));
            var joe = await j.Fact(new Passenger(ia, new User("--- JOE ---")));
            var booking = await j.Fact(new Booking(flight, joe, DateTime.UtcNow));
            var cancellation = await j.Fact(new FlightCancellation(flight, DateTime.Now));

            var bookingCancellations = await j.Query(Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline
                from booking in facts.OfType<Booking>()
                where booking.flight == flight
                from cancellation in facts.OfType<FlightCancellation>()
                where cancellation.flight == flight
                select new
                {
                    booking,
                    cancellation
                }
            ), ia);

            var pair = bookingCancellations.Should().ContainSingle().Subject;
            pair.booking.Should().BeEquivalentTo(booking);
            pair.cancellation.Should().BeEquivalentTo(cancellation);
        }

        [Fact]
        public async Task Query_NestedFieldProjection()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));
            var title = await j.Fact(new Title(post, "Introduction to Jinaga Replicator", new Title[0]));

            var postTitles = Given<Post>.Match((post, facts) =>
                from title in facts.OfType<Title>()
                where title.post == post
                select title.value
            );


            var specification = Given<Site>.Match((site, facts) =>
                from post in facts.OfType<Post>()
                where post.site == site
                select new
                {
                    postCreatedAt = post.createdAt,
                    titles = facts.Observable(post, postTitles)
                }
            );

            var posts = await j.Query(specification, site);

            posts.Should().ContainSingle().Which
                .titles.Should().ContainSingle().Which
                    .Should().Be("Introduction to Jinaga Replicator");
        }

        [Fact]
        public async Task Query_FieldProjectionOnPredecessor()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));

            var specification = Given<Post>.Match((post, facts) =>
                from site in facts.OfType<Site>()
                where site == post.site
                select site.domain
            );

            var result = await j.Query(specification, post);

            result.Should().ContainSingle().Which
                .Should().Be("michaelperry.net");
        }
        
        [Fact]
        public async Task Query_CompositeProjectionOnPredecessor()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));

            var specification = Given<Post>.Match((post, facts) =>
                from site in facts.OfType<Site>()
                where site == post.site
                select new
                {
                    siteName = site.domain,
                    postCreatedAt = post.createdAt
                }
            );

            var result = await j.Query(specification, post);

            var subject = result.Should().ContainSingle().Subject;
            subject.siteName.Should().Be("michaelperry.net");
            subject.postCreatedAt.Should().Be("2022-09-30T13:40:00Z");
        }

        [Fact]
        public async Task Query_InlineCollectionProjection()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));
            var title = await j.Fact(new Title(post, "Introduction to Jinaga Replicator", new Title[0]));

            var specification = Given<Site>.Match((site, facts) =>
                from post in facts.OfType<Post>()
                where post.site == site
                select new
                {
                    postCreatedAt = post.createdAt,
                    titles = facts.Observable(
                        from title in facts.OfType<Title>()
                        where title.post == post
                        select title.value
                    )
                }
            );

            var posts = await j.Query(specification, site);

            posts.Should().ContainSingle().Which
                .titles.Should().ContainSingle().Which
                    .Should().Be("Introduction to Jinaga Replicator");
        }

        [Fact]
        public async Task Query_InlineIQueryableProjection()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));
            var title = await j.Fact(new Title(post, "Introduction to Jinaga Replicator", new Title[0]));

            var specification = Given<Site>.Match((site, facts) =>
                from post in facts.OfType<Post>()
                where post.site == site
                select new
                {
                    postCreatedAt = post.createdAt,
                    titles =
                        from title in facts.OfType<Title>()
                        where title.post == post
                        select title
                }
            );

            var posts = await j.Query(specification, site);

            posts.Should().ContainSingle().Which
                .titles.Should().ContainSingle().Which
                    .value.Should().Be("Introduction to Jinaga Replicator");
        }

        [Fact]
        public async Task Query_InlineIQueryableFieldProjection()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));
            var title = await j.Fact(new Title(post, "Introduction to Jinaga Replicator", new Title[0]));

            var specification = Given<Site>.Match((site, facts) =>
                from post in facts.OfType<Post>()
                where post.site == site
                select new
                {
                    postCreatedAt = post.createdAt,
                    titles =
                        from title in facts.OfType<Title>()
                        where title.post == post
                        select title.value
                }
            );

            var posts = await j.Query(specification, site);

            posts.Should().ContainSingle().Which
                .titles.Should().ContainSingle().Which
                    .Should().Be("Introduction to Jinaga Replicator");
        }

        [Fact]
        public async Task Query_SpecificationWithTwoGivens()
        {
            var site = await j.Fact(new Site("michaelperry.net"));
            var post = await j.Fact(new Post(site, "2022-09-30T13:40:00Z"));
            var title = await j.Fact(new Title(post, "Introduction to Jinaga Replicator", new Title[0]));
            var visitor = await j.Fact(new User("visitor"));
            var comment = await j.Fact(new Comment(post, visitor, "Great post!"));
            var otherComment = await j.Fact(new Comment(post, new User("other"), "I disagree."));

            var specification = Given<Site, User>.Match((site, user, facts) =>
                from post in facts.OfType<Post>()
                where post.site == site
                select new
                {
                    postCreatedAt = post.createdAt,
                    titles =
                        from title in facts.OfType<Title>()
                        where title.post == post
                        select title.value,
                    myComments =
                        from comment in facts.OfType<Comment>()
                        where comment.post == post
                        where comment.author == user
                        select comment.message
                }
            );

            var posts = await j.Query(specification, site, visitor);

            var result = posts.Should().ContainSingle().Subject;
            result.titles.Should().ContainSingle().Which
                .Should().Be("Introduction to Jinaga Replicator");
            result.myComments.Should().ContainSingle().Which
                .Should().Be("Great post!");
        }

        [Fact]
        public async Task CanQueryForSuccessorsUsingSuccessorsExtension()
        {
            var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));
            var flight = await j.Fact(new Flight(airlineDay, 4247));

            var specification = Given<AirlineDay>.Match(airlineDay =>
                from flight in airlineDay.Successors().OfType<Flight>(f => f.airlineDay)
                select flight
            );
            var flights = await j.Query(specification, airlineDay);

            flights.Should().ContainSingle().Which.Should().BeEquivalentTo(flight);
        }

        [Fact]
        public async Task CanQueryForNestedSuccessorsUsingSuccessorsExtension()
        {
            var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));
            var flight = await j.Fact(new Flight(airlineDay, 4247));
            var booking = await j.Fact(new Booking(flight, new Passenger(new Airline("IA"), new User("--- JOE ---")), DateTime.UtcNow));

            var specification = Given<AirlineDay>.Match(airlineDay =>
                from flight in airlineDay.Successors().OfType<Flight>(f => f.airlineDay)
                select new
                {
                    flight,
                    bookings = flight.Successors().OfType<Booking>(b => b.flight)
                }
            );
            var flights = await j.Query(specification, airlineDay);

            var result = flights.Should().ContainSingle().Subject;
            result.flight.Should().BeEquivalentTo(flight);
            result.bookings.Should().ContainSingle().Which.Should().BeEquivalentTo(booking);
        }

        [Fact]
        public async Task CanProjectGuidField()
        {
            // Arrange - Create a project with a Guid id field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var projectId = Guid.NewGuid();
            var project = await j.Fact(new ConstructionProject(user, projectId));

            // Act - Project the Guid field, which should now work
            var projectsWithGuidProjection = Given<User>.Match(u =>
                u.Successors().OfType<ConstructionProject>(p => p.creator)
                    .Select(p => new
                    {
                        ProjectId = p.id  // This line should now successfully project the Guid
                    })
            );

            var result = await j.Query(projectsWithGuidProjection, user);
            
            // Assert - Verify the Guid was projected correctly
            result.Should().ContainSingle().Which
                .ProjectId.Should().Be(projectId);
        }

        [Fact]
        public async Task CannotProjectUnsupportedTypeField_ThrowsException()
        {
            // Arrange - Create a fact with a completely unsupported type (TimeSpan)
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestTimeSpanFact(user, TimeSpan.FromHours(1)));

            // Act & Assert - Attempt to project the TimeSpan field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestTimeSpanFact>(f => f.creator)
                    .Select(f => new
                    {
                        TimeSpanValue = f.timeSpanValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectDecimalField_ThrowsException()
        {
            // Arrange - Create a fact with a decimal field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestDecimalFact(user, 123.45m));

            // Act & Assert - Attempt to project the decimal field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestDecimalFact>(f => f.creator)
                    .Select(f => new
                    {
                        DecimalValue = f.decimalValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectNullableDecimalField_ThrowsException()
        {
            // Arrange - Create a fact with a nullable decimal field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestNullableDecimalFact(user, 123.45m));

            // Act & Assert - Attempt to project the nullable decimal field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestNullableDecimalFact>(f => f.creator)
                    .Select(f => new
                    {
                        NullableDecimalValue = f.nullableDecimalValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectDateTimeOffsetField_ThrowsException()
        {
            // Arrange - Create a fact with a DateTimeOffset field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestDateTimeOffsetFact(user, DateTimeOffset.Now));

            // Act & Assert - Attempt to project the DateTimeOffset field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestDateTimeOffsetFact>(f => f.creator)
                    .Select(f => new
                    {
                        DateTimeOffsetValue = f.dateTimeOffsetValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectNullableDateTimeOffsetField_ThrowsException()
        {
            // Arrange - Create a fact with a nullable DateTimeOffset field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestNullableDateTimeOffsetFact(user, DateTimeOffset.Now));

            // Act & Assert - Attempt to project the nullable DateTimeOffset field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestNullableDateTimeOffsetFact>(f => f.creator)
                    .Select(f => new
                    {
                        NullableDateTimeOffsetValue = f.nullableDateTimeOffsetValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectNullableIntField_ThrowsException()
        {
            // Arrange - Create a fact with a nullable int field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestNullableIntFact(user, 42));

            // Act & Assert - Attempt to project the nullable int field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestNullableIntFact>(f => f.creator)
                    .Select(f => new
                    {
                        NullableIntValue = f.nullableIntValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectNullableFloatField_ThrowsException()
        {
            // Arrange - Create a fact with a nullable float field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestNullableFloatFact(user, 3.14f));

            // Act & Assert - Attempt to project the nullable float field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestNullableFloatFact>(f => f.creator)
                    .Select(f => new
                    {
                        NullableFloatValue = f.nullableFloatValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectNullableDoubleField_ThrowsException()
        {
            // Arrange - Create a fact with a nullable double field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestNullableDoubleFact(user, 2.718));

            // Act & Assert - Attempt to project the nullable double field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestNullableDoubleFact>(f => f.creator)
                    .Select(f => new
                    {
                        NullableDoubleValue = f.nullableDoubleValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }

        [Fact]
        public async Task CannotProjectNullableBoolField_ThrowsException()
        {
            // Arrange - Create a fact with a nullable bool field
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var testFact = await j.Fact(new TestNullableBoolFact(user, true));

            // Act & Assert - Attempt to project the nullable bool field, which should fail
            var specification = Given<User>.Match(u =>
                u.Successors().OfType<TestNullableBoolFact>(f => f.creator)
                    .Select(f => new
                    {
                        NullableBoolValue = f.nullableBoolValue
                    })
            );

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => j.Query(specification, user));
            exception.Message.Should().Contain("Unknown field type");
        }
    }

    [FactType("Construction.Project")]
    public record ConstructionProject(User creator, Guid id) { }

    [FactType("Test.DecimalFact")]
    public record TestDecimalFact(User creator, decimal decimalValue) { }

    [FactType("Test.NullableDecimalFact")]
    public record TestNullableDecimalFact(User creator, decimal? nullableDecimalValue) { }

    [FactType("Test.DateTimeOffsetFact")]
    public record TestDateTimeOffsetFact(User creator, DateTimeOffset dateTimeOffsetValue) { }

    [FactType("Test.NullableDateTimeOffsetFact")]
    public record TestNullableDateTimeOffsetFact(User creator, DateTimeOffset? nullableDateTimeOffsetValue) { }

    [FactType("Test.NullableIntFact")]
    public record TestNullableIntFact(User creator, int? nullableIntValue) { }

    [FactType("Test.NullableFloatFact")]
    public record TestNullableFloatFact(User creator, float? nullableFloatValue) { }

    [FactType("Test.NullableDoubleFact")]
    public record TestNullableDoubleFact(User creator, double? nullableDoubleValue) { }

    [FactType("Test.NullableBoolFact")]
    public record TestNullableBoolFact(User creator, bool? nullableBoolValue) { }

    [FactType("Test.TimeSpanFact")]
    public record TestTimeSpanFact(User creator, TimeSpan timeSpanValue) { }

    [FactType("Blog.Site")]
    public record Site(string domain) { }

    [FactType("Blog.Post")]
    public record Post(Site site, string createdAt) { }

    [FactType("Blog.Post.Title")]
    public record Title(Post post, string value, Title[] prior) { }

    [FactType("Blog.Comment")]
    public record Comment(Post post, User author, string message) { }
}
