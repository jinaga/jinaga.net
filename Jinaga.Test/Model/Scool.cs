using System;

namespace Jinaga.Test.Model;

[FactType("School")]
public record School(string name) { }

[FactType("Course")]
public record Course(School school, string identifier) { }

[FactType("Course.Deleted")]
public record CourseDeleted(Course course, DateTime deletedAt) { }

[FactType("Course.Restored")]
public record CourseRestored(CourseDeleted deleted) { }
