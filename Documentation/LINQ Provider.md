# LINQ Provider

Jinaga.NET implements a LINQ provider that allows you to write specifications in C#.
It begins with a `Given` clause that specifies the type of fact that you are looking for.
Then it uses a `Match` clause to build the specification.
From there the `SpecificationProcessor` translates either an `IQueryable` or a scalar into a Jinaga specification.

The following constructs are accepted in the `Match` clause:

- source -> facts.OfType<T>()
- source -> facts.OfType<T>(predicate)
- source -> Where(source, predicate)
- source -> Select(source, selector)
- source -> SelectMany(source, selector)
- source -> SelectMany(source, selector, resultSelector)
- predicate -> left == right
- predicate -> left.Contains(right)
- predicate -> predicate && predicate
- predicate -> !predicate
- predicate -> Any(source)
- predicate -> Any(source, predicate)
- predicate -> facts.Any<T>(predicate)
- predicate -> Condition
- left,right -> label (.role)*

A simple successor specification looks like this:

```C#
var specification = Given<Company>.Match((company, facts) =>
  from employee in facts.OfType<Employee>()
  where employee.company == company
  select employee
);
```

It produces the following tree:

```
Select(
  Where(
    facts.OfType<Employee>(),
    employee.company == company
  ),
  employee
)
```

A specification might include a negative existential condition.
  
```C#
var specification = Given<Company>.Match((company, facts) =>
  from employee in facts.OfType<Employee>()
  where employee.company == company
  where !facts.OfType<Terminated>(terminated =>
    terminated.employee == employee
  ).Any()
  select employee
);
```

It produces the following tree:

```
Select(
  Where(
    Where(
        facts.OfType<Employee>(),
        employee.company == company
    ),
    !
      Any(
        Where(
          facts.OfType<Terminated>(),
          terminated.employee == employee
        )
      )
  ),
  employee
)
```

A specification may include a `SelectMany` clause.
This introduces a new unknown into the specification.

```C#
var specification = Given<Company>.Match((company, facts) =>
  from employee in facts.OfType<Employee>()
  where employee.company == company
  from manager in facts.OfType<Manager>()
  where manager.employee == employee
  select manager
);
```

It produces the following tree:

```
Select(
  Where(
    SelectMany(
      Where(
        facts.OfType<Employee>(),
        employee.company == company
      ),
      facts.OfType<Manager>(),
      new { employee, manager }
    ),
    manager.employee == employee
  ),
  manager
)
```

The first construct to execute is `facts.OfType<Employee>()`.
This produces a single match and projection.

```
{
  u1: Employee [
  ]
} => u1
```

The next construct is `employee.company == company`.
This produces a single path condition.

```
[ employee.company = company ]
```

The `Where` construct applies the condition to the match and replaces the label.
It locates the match based on the targets of the path condition.
The path condition targets the labels `employee` and `company`.
Since `company` is a given, the match for `employee` is the later of the two.

```
{
  employee: Employee [
    employee.company = company
  ]
} => employee
```

Next the `facts.OfType<Manager>()` construct produces a single match and projection.

```
{
  u2: Manager [
  ]
} => u2
```

The `SelectMany` construct concatenates the matches and adds the projection.
It takes the label of the `manager` unknown from the result selector.

```
{
  employee: Employee [
    employee.company = company
  ],
  manager: Manager [
  ]
} => new { employee, manager }
```

Next the condition `manager.employee == employee` produces a single path condition.

```
[ manager.employee = employee ]
```

The `Where` construct applies the condition to the match.
It locates the match based on the targets of the path condition.
The path condition targets the labels `manager` and `employee`.
The `manager` match comes later, and therefore receives the condition.

```
{
  employee: Employee [
    employee.company = company
  ],
  manager: Manager [
    manager.employee = employee
  ]
} => new { employee, manager }
```

Finally, the `Select` construct replaces the projection with the `manager` label.

```
{
  employee: Employee [
    employee.company = company
  ],
  manager: Manager [
    manager.employee = employee
  ]
} => manager
```

## Source

A source produces a set of matches and a projection.

### facts.OfType<T>()

Facts from a repository produces a single match.

```
{
  u1: T [
  ]
} => u1
```

### facts.OfType<T>(predicate)

Facts from a repository with a predicate produces a single match.
It applies the conditions from the predicate.
It also sets the label of the match.

```
{
  label: T [
  ]
} apply predicate => label
```

### Where(source, predicate)

Where applies the conditions in the predicate to the appropriate matches of the source.

```
source apply predicate
```

To find the appropriate match, first find the target unknowns of each condition.
An existential condition has only one target unknown.
A path condition has two target unknowns.
Each existential condition is applied to the match with its target unknown.
Each path condition is applied to the later match of the two target unknowns.

When applying a predicate, the label given by the predicate replaces the temporary label of the unknown.

### Select(source, selector)

Select replaces the projection with a new projection.

```
source => selector
```

### SelectMany(source, selector)

SelectMany concatenates the matches of the source with the matches of the selector.
It then replaces the projection with the selector projection.

```
source.matches concat selector.matches => selector.projection
```

### SelectMany(source, selector, resultSelector)

SelectMany with a result selector concatenates the matches of the source with the matches of the selector.
It then replaces the projection with the result selector projection.

```
source.matches concat selector.matches => resultSelector
```

## Predicate

A predicate produces a collection of conditions, either path or existential conditions.

### left == right

A comparison produces a single path condition.

```
[ left = right ]
```

### left.Contains(right)

A containment produces a path condition.

```
[ left = right ]
```

### predicate && predicate

A conjunction produces a collection of existential conditions.

```
predicate concat predicate
```

### !predicate

A negation can only be applied to a single existential condition.
It reverses the sign.

```
!predicate
```

### Any(source)

Any produces a single existential condition.

```
E source.matches
```

### Any(source, predicate)

Any with a predicate produces a single existential condition.
It applies the conditions in the predicate to the appropriate matches.

```
E source.matches apply predicate
```

### facts.Any<T>(predicate)

Any from the repository produces a single existential condition.
It also sets the label of the match.

```
E {
  label: T [
  ]
} apply predicate
```

### Condition

A Condition obtains a predicate from a property of a fact.

## Projection