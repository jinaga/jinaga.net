using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Specifications;
using Jinaga.Visualizers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;
using static Jinaga.Specifications.LinqProcessor;

namespace Jinaga.Test.Specifications;
public class LinqProcessorTest
{
    [Fact]
    public void CanProcessFactSource()
    {
        var source = FactsOfType(new Label("employee", "Employee"), typeof(object));

        TextOf(source.Matches).Should().Be(
            """
            employee: Employee [
            ]

            """
        );

        source.Projection.Should().BeOfType<SimpleProjection>().Which
            .Tag.Should().Be("employee");
    }

    [Fact]
    public void CanProcessComparison()
    {
        var left = new ReferenceContext(
            new Label("employee", "Employee"),
            ImmutableList.Create(new Role("company", "Company"))
        );
        var right = new ReferenceContext(
            new Label("company", "Company"),
            ImmutableList<Role>.Empty
        );
        var predicate = Compare(left, right);

        var condition = predicate.Conditions.Should().ContainSingle().Subject;
        var pathCondition = condition.Should().BeOfType<PathConditionContext>().Subject;
        pathCondition.Left.Should().Be(left);
        pathCondition.Right.Should().Be(right);
    }

    [Fact]
    public void ComparisonTypesMustMatch()
    {
        var left = new ReferenceContext(
            new Label("employee", "Employee"),
            ImmutableList.Create(new Role("company", "Company"))
        );
        var right = new ReferenceContext(
            new Label("company", "Company"),
            ImmutableList.Create(new Role("founder", "User"))
        );
        Action action = () => Compare(left, right);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Cannot join Company to User.");
    }

    [Fact]
    public void CanProcessExistential()
    {
        var predicate = Any(
            Where(
                FactsOfType(new Label("terminated", "Terminated"), typeof(object)),
                Compare(
                    new ReferenceContext(
                        new Label("terminated", "Terminated"),
                        ImmutableList.Create(new Role("employee", "Employee"))
                    ),
                    new ReferenceContext(
                        new Label("employee", "Employee"),
                        ImmutableList<Role>.Empty
                    )
                )
            )
        );

        var condition = predicate.Conditions.Should().ContainSingle().Which
            .Should().BeOfType<ExistentialConditionContext>().Subject;
        TextOf(condition.Matches).Should().Be(
            """
            terminated: Terminated [
                terminated->employee: Employee = employee
            ]

            """
        );
        condition.Exists.Should().BeTrue();
    }

    [Fact]
    public void CanProcessWhere()
    {
        var employees = Where(
            FactsOfType(new Label("employee", "Employee"), typeof(object)),
            Compare(
                new ReferenceContext(
                    new Label("employee", "Employee"),
                    ImmutableList.Create(new Role("company", "Company"))
                ),
                new ReferenceContext(
                    new Label("company", "Company"),
                    ImmutableList<Role>.Empty
                )
            )
        );

        TextOf(employees.Matches).Should().Be(
            """
            employee: Employee [
                employee->company: Company = company
            ]

            """);
        employees.Projection.Should().BeOfType<SimpleProjection>().Which
            .Tag.Should().Be("employee");
    }

    [Fact]
    public void ReversesPathCondition()
    {
        var employees = Where(
            FactsOfType(new Label("employee", "Employee"), typeof(object)),
            Compare(
                new ReferenceContext(
                    new Label("company", "Company"),
                    ImmutableList<Role>.Empty
                ),
                new ReferenceContext(
                    new Label("employee", "Employee"),
                    ImmutableList.Create(new Role("company", "Company"))
                )
            )
        );

        TextOf(employees.Matches).Should().Be(
            """
            employee: Employee [
                employee->company: Company = company
            ]

            """);
    }

    [Fact]
    public void CanApplyNegativeExistentialCondition()
    {
        var specification = Where(
            FactsOfType(new Label("employee", "Employee"), typeof(object)),
            Not(
                Any(
                    Where(
                        FactsOfType(new Label("terminated", "Terminated"), typeof(object)),
                        Compare(
                            new ReferenceContext(
                                new Label("terminated", "Terminated"),
                                ImmutableList.Create(new Role("employee", "Employee"))
                            ),
                            new ReferenceContext(
                                new Label("employee", "Employee"),
                                ImmutableList<Role>.Empty
                            )
                        )
                    )
                )
            )
        );

        TextOf(specification.Matches).Should().Be(
            """
            employee: Employee [
                !E {
                    terminated: Terminated [
                        terminated->employee: Employee = employee
                    ]
                }
            ]
            
            """
        );
    }

    [Fact]
    public void CanProcessSelectMany()
    {
        var specification = SelectMany(
            Where(
                FactsOfType(new Label("employee", "Employee"), typeof(object)),
                Compare(
                    new ReferenceContext(
                        new Label("employee", "Employee"),
                        ImmutableList.Create(new Role("company", "Company"))
                    ),
                    new ReferenceContext(
                        new Label("company", "Company"),
                        ImmutableList<Role>.Empty
                    )
                )
            ),
            FactsOfType(new Label("manager", "Manager"), typeof(object))
        );

        TextOf(specification.Matches).Should().Be(
            """
            employee: Employee [
                employee->company: Company = company
            ]
            manager: Manager [
            ]

            """
        );
    }

    [Fact]
    public void AppliesPathConditionsToTheCorrectMatch()
    {
        var specification = Where(
            SelectMany(
                FactsOfType(new Label("employee", "Employee"), typeof(object)),
                FactsOfType(new Label("manager", "Manager"), typeof(object))
            ),
            And(
                Compare(
                    new ReferenceContext(
                        new Label("employee", "Employee"),
                        ImmutableList.Create(new Role("company", "Company"))
                    ),
                    new ReferenceContext(
                        new Label("company", "Company"),
                        ImmutableList<Role>.Empty
                    )
                ),
                Compare(
                    new ReferenceContext(
                        new Label("employee", "Employee"),
                        ImmutableList<Role>.Empty
                    ),
                    new ReferenceContext(
                        new Label("manager", "Manager"),
                        ImmutableList.Create(new Role("employee", "Employee"))
                    )
                )
            )
        );

        TextOf(specification.Matches).Should().Be(
            """
            employee: Employee [
                employee->company: Company = company
            ]
            manager: Manager [
                manager->employee: Employee = employee
            ]
            
            """
        );
    }

    [Fact]
    public void AppliesExistentialConditionToTheCorrectMatch()
    {
        var specification = Where(
            SelectMany(
                FactsOfType(new Label("employee", "Employee"), typeof(object)),
                FactsOfType(new Label("manager", "Manager"), typeof(object))
            ),
            And(
                And(
                    Compare(
                        new ReferenceContext(
                            new Label("employee", "Employee"),
                            ImmutableList.Create(new Role("company", "Company"))
                        ),
                        new ReferenceContext(
                            new Label("company", "Company"),
                            ImmutableList<Role>.Empty
                        )
                    ),
                    Compare(
                        new ReferenceContext(
                            new Label("employee", "Employee"),
                            ImmutableList<Role>.Empty
                        ),
                        new ReferenceContext(
                            new Label("manager", "Manager"),
                            ImmutableList.Create(new Role("employee", "Employee"))
                        )
                    )
                ),
                Not(
                    Any(
                        Where(
                            FactsOfType(new Label("terminated", "Terminated"), typeof(object)),
                            Compare(
                                new ReferenceContext(
                                    new Label("terminated", "Terminated"),
                                    ImmutableList.Create(new Role("employee", "Employee"))
                                ),
                                new ReferenceContext(
                                    new Label("employee", "Employee"),
                                    ImmutableList<Role>.Empty
                                )
                            )
                        )
                    )
                )
            )
        );

        TextOf(specification.Matches).Should().Be(
            """
            employee: Employee [
                employee->company: Company = company
                !E {
                    terminated: Terminated [
                        terminated->employee: Employee = employee
                    ]
                }
            ]
            manager: Manager [
                manager->employee: Employee = employee
            ]
            
            """
        );
    }

    [Fact]
    public void PathConditionsComeBeforeExistentialConditions()
    {
        var specification = Where(
            SelectMany(
                FactsOfType(new Label("employee", "Employee"), typeof(object)),
                FactsOfType(new Label("manager", "Manager"), typeof(object))
            ),
            And(
                And(
                    Not(
                        Any(
                            Where(
                                FactsOfType(new Label("terminated", "Terminated"), typeof(object)),
                                Compare(
                                    new ReferenceContext(
                                        new Label("terminated", "Terminated"),
                                        ImmutableList.Create(new Role("employee", "Employee"))
                                    ),
                                    new ReferenceContext(
                                        new Label("employee", "Employee"),
                                        ImmutableList<Role>.Empty
                                    )
                                )
                            )
                        )
                    ),
                    Compare(
                        new ReferenceContext(
                            new Label("employee", "Employee"),
                            ImmutableList.Create(new Role("company", "Company"))
                        ),
                        new ReferenceContext(
                            new Label("company", "Company"),
                            ImmutableList<Role>.Empty
                        )
                    )
                ),
                Compare(
                    new ReferenceContext(
                        new Label("employee", "Employee"),
                        ImmutableList<Role>.Empty
                    ),
                    new ReferenceContext(
                        new Label("manager", "Manager"),
                        ImmutableList.Create(new Role("employee", "Employee"))
                    )
                )
            )
        );

        TextOf(specification.Matches).Should().Be(
            """
            employee: Employee [
                employee->company: Company = company
                !E {
                    terminated: Terminated [
                        terminated->employee: Employee = employee
                    ]
                }
            ]
            manager: Manager [
                manager->employee: Employee = employee
            ]
            
            """
        );
    }

    private static string TextOf(IEnumerable<Match> matches)
    {
        return matches
            .Select(m => m.ToString())
            .Join("")
            .ReplaceLineEndings();
    }
}
