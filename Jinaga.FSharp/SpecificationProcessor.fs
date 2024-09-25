namespace Jinaga.FSharp

open System
open System.Collections.Immutable
open System.Linq.Expressions
open Jinaga.Pipelines
open Jinaga.Projections
open Jinaga.Specifications
open Jinaga.Repository
open System.Linq
open Jinaga

type Label(recommendedName: string, factType: string) =
    member val Name = recommendedName
    member val FactType = factType

type SimpleProjection(tag: string, factType: Type) =
    member val Tag = tag
    member val FactType = factType

type CompoundProjection(fields: ImmutableDictionary<string, obj>, factType: Type) =
    member val Fields = fields
    member val FactType = factType
    member this.GetProjection(name: string) = fields.[name]

type FieldProjection(tag: string, factType: Type, fieldName: string, fieldType: Type) =
    member val Tag = tag
    member val FactType = factType
    member val FieldName = fieldName
    member val FieldType = fieldType

type CollectionProjection(matches: ImmutableList<Match>, projection: Projection, collectionType: Type) =
    member val Matches = matches
    member val Projection = projection
    member val CollectionType = collectionType

type HashProjection(tag: string, factType: Type) =
    member val Tag = tag
    member val FactType = factType

type ReferenceContext(label: Label, roles: ImmutableList<Role>) =
    member val Label = label
    member val Roles = roles with get, set
    static member From(label: Label) = ReferenceContext(label, ImmutableList<Role>.Empty)
    member this.Push(role: Role) = ReferenceContext(this.Label, this.Roles.Add(role))

type Role(name: string, targetType: string) =
    member val Name = name
    member val TargetType = targetType

type SourceContext(matches: ImmutableList<Match>, projection: Projection) =
    member val Matches = matches
    member val Projection = projection

type Match(unknown: Label, pathConditions: ImmutableList<obj>) =
    member val Unknown = unknown
    member val PathConditions = pathConditions

type SymbolTable(values: ImmutableDictionary<string, Projection>) =
    let values = values

    new() = SymbolTable(ImmutableDictionary<string, Projection>.Empty)

    member this.Set(name: string, value: Projection) =
        SymbolTable(values.SetItem(name, value))

    member this.Get(name: string) =
        match values.TryGetValue(name) with
        | true, value -> value
        | false, _ -> raise (Exception(sprintf "No value named %s." name))

    static member Empty = SymbolTable()

type SpecificationProcessor() =
    let mutable labels = ImmutableList<Label>.Empty
    let mutable givenLabels = ImmutableList<Label>.Empty

    member private this.ValidateMatches(matches: ImmutableList<Match>) =
        // Look for matches with no path conditions.
        let mutable priorMatch: Match option = None
        for m in matches do
            if not (m.PathConditions.Any()) then
                let unknown = match.Unknown.Name
                let prior = 
                    match priorMatch with
                    | Some priorMatch -> sprintf "prior variable \"%s\"" priorMatch.Unknown.Name
                    | None -> sprintf "parameter \"%s\"" (givenLabels.First().Name)
                raise (SpecificationException(sprintf "The variable \"%s\" should be joined to the %s." unknown prior))
            priorMatch <- Some m

    member private this.Given(parameters: seq<ParameterExpression>) =
        givenLabels <- parameters
            |> Seq.map (fun parameter -> this.NewLabel(parameter.Name, parameter.Type.FactTypeName()))
            |> ImmutableList.ToImmutableList
        parameters
        |> Seq.fold (fun table parameter -> table.Set(parameter.Name, SimpleProjection(parameter.Name, parameter.Type))) SymbolTable.Empty

    member private this.NewLabel(recommendedName: string, factType: string) =
        let source = Label(recommendedName, factType)
        labels <- labels.Add(source)
        source

    member private this.ProcessShorthand(expression: Expression, symbolTable: SymbolTable) =
        let reference = this.ProcessReference(expression, symbolTable)
        if reference.Roles.Any() then
            let lastRole = reference.Roles.Last()
            let unknown = Label(lastRole.Name, lastRole.TargetType)
            let self = ReferenceContext.From(unknown)
            LinqProcessor.Where(
                LinqProcessor.FactsOfType(unknown, expression.Type),
                LinqProcessor.Compare(reference, self)
            )
        else
            SourceContext(ImmutableList<Match>.Empty, SimpleProjection(reference.Label.Name, expression.Type))

    member private this.ProcessProjection(expression: Expression, symbolTable: SymbolTable) =
        match expression with
        | :? ParameterExpression as parameterExpression ->
            symbolTable.Get(parameterExpression.Name)
        | :? NewExpression as newExpression ->
            let names =
                if newExpression.Members <> null then
                    newExpression.Members |> Seq.map (fun m -> m.Name)
                else
                    newExpression.Constructor.GetParameters() |> Seq.map (fun parameter -> parameter.Name)
            let values = newExpression.Arguments |> Seq.map (fun arg -> this.ProcessProjection(arg, symbolTable))
            let fields = names.Zip(values, (fun name value -> KeyValuePair.Create(name, value))) |> ImmutableDictionary.ToImmutableDictionary
            CompoundProjection(fields, newExpression.Type)
        | :? MemberInitExpression as memberInit ->
            let parameters = memberInit.NewExpression.Constructor.GetParameters() |> Seq.zip memberInit.NewExpression.Arguments |> Seq.map (fun (parameter, arg) -> KeyValuePair.Create(parameter.Name, this.ProcessProjection(arg, symbolTable)))
            let fields = memberInit.Bindings |> Seq.map (fun binding -> this.ProcessProjectionMember(binding, symbolTable))
            let childProjections = parameters.Concat(fields) |> ImmutableDictionary.ToImmutableDictionary
            CompoundProjection(childProjections, memberInit.Type)
        | :? MemberExpression as memberExpression ->
            if memberExpression.Member :? PropertyInfo then
                let propertyInfo = memberExpression.Member :?> PropertyInfo
                if propertyInfo.PropertyType.IsGenericType && (propertyInfo.PropertyType.GetGenericTypeDefinition() = typeof<Relation<_>> || propertyInfo.PropertyType.GetGenericTypeDefinition() = typeof<IQueryable<_>>) then
                    let target = SpecificationProcessor.InstanceOfFact(propertyInfo.DeclaringType)
                    let relation = propertyInfo.GetGetMethod().Invoke(target, [||]) :?> IQueryable
                    let projection = this.ProcessProjection(memberExpression.Expression, symbolTable)
                    let childSymbolTable = SymbolTable.Empty.Set("this", projection)
                    this.ProcessProjection(relation.Expression, childSymbolTable)
                else
                    let head = this.ProcessProjection(memberExpression.Expression, symbolTable)
                    match head with
                    | :? CompoundProjection as compoundProjection -> compoundProjection.GetProjection(memberExpression.Member.Name)
                    | :? SimpleProjection as simpleProjection ->
                        if not memberExpression.Type.IsFactType() then
                            FieldProjection(simpleProjection.Tag, memberExpression.Expression.Type, memberExpression.Member.Name, memberExpression.Type)
                        else
                            raise (SpecificationException(sprintf "Cannot select %s directly. Give the fact a label first." memberExpression.Member.Name))
                    | _ -> raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
            else
                raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
        | :? MethodCallExpression as methodCallExpression ->
            if methodCallExpression.Method.DeclaringType = typeof<FactRepository> && methodCallExpression.Method.Name = nameof(FactRepository.Observable) then
                if methodCallExpression.Arguments.Count = 2 && typeof<Specification>.IsAssignableFrom(methodCallExpression.Arguments.[1].Type) then
                    let start = this.ProcessProjection(methodCallExpression.Arguments.[0], symbolTable)
                    let label = SpecificationProcessor.LabelOfProjection(start)
                    let lambdaExpression = Expression.Lambda<Func<obj>>(methodCallExpression.Arguments.[1])
                    let specification = lambdaExpression.Compile().Invoke() :?> Specification
                    let arguments = ImmutableList.Create(label)
                    let specification = specification.Apply(arguments)
                    CollectionProjection(specification.Matches, specification.Projection, methodCallExpression.Type)
                elif methodCallExpression.Arguments.Count = 1 && typeof<IQueryable>.IsAssignableFrom(methodCallExpression.Arguments.[0].Type) then
                    let value = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, "")
                    CollectionProjection(value.Matches, value.Projection, methodCallExpression.Type)
                else
                    raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
            elif expression.Type.IsGenericType && expression.Type.GetGenericTypeDefinition() = typeof<IQueryable<_>> then
                let value = this.ProcessSource(expression, symbolTable, "")
                CollectionProjection(value.Matches, value.Projection, expression.Type)
            elif methodCallExpression.Method.DeclaringType = typeof<JinagaClient> && methodCallExpression.Method.Name = nameof(JinagaClient.Hash) && methodCallExpression.Arguments.Count = 1 then
                let value = this.ProcessProjection(methodCallExpression.Arguments.[0], symbolTable)
                match value with
                | :? SimpleProjection as simpleProjection -> HashProjection(simpleProjection.Tag, methodCallExpression.Arguments.[0].Type)
                | _ -> raise (SpecificationException(sprintf "Cannot hash %s." (value.ToDescriptiveString())))
            else
                raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
        | _ -> raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))

    member private this.ProcessSource(expression: Expression, symbolTable: SymbolTable, recommendedLabel: string) =
        match expression with
        | :? MethodCallExpression as methodCallExpression ->
            if methodCallExpression.Method.DeclaringType = typeof<Queryable> then
                if methodCallExpression.Method.Name = nameof(Queryable.Where) && methodCallExpression.Arguments.Count = 2 then
                    let lambda = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[1])
                    let parameterName = lambda.Parameters.[0].Name
                    let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, parameterName)
                    let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                    let predicate = this.ProcessPredicate(lambda.Body, childSymbolTable)
                    LinqProcessor.Where(source, predicate)
                elif methodCallExpression.Method.Name = nameof(Queryable.Select) && methodCallExpression.Arguments.Count = 2 then
                    let selector = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[1])
                    let parameterName = selector.Parameters.[0].Name
                    let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, parameterName)
                    let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                    let projection = this.ProcessProjection(selector.Body, childSymbolTable)
                    LinqProcessor.Select(source, projection)
                elif methodCallExpression.Method.Name = nameof(Queryable.SelectMany) && methodCallExpression.Arguments.Count = 2 then
                    let collectionSelector = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[1])
                    let collectionSelectorParameterName = collectionSelector.Parameters.[0].Name
                    let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, collectionSelectorParameterName)
                    let collectionSelectorSymbolTable = symbolTable.Set(collectionSelectorParameterName, source.Projection)
                    let selector = this.ProcessSource(collectionSelector.Body, collectionSelectorSymbolTable, recommendedLabel)
                    LinqProcessor.SelectMany(source, selector)
                elif methodCallExpression.Method.Name = nameof(Queryable.SelectMany) && methodCallExpression.Arguments.Count = 3 then
                    let collectionSelector = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[1])
                    let collectionSelectorParameterName = collectionSelector.Parameters.[0].Name
                    let resultSelector = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[2])
                    let resultSelectorParameterName = resultSelector.Parameters.[1].Name
                    let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, collectionSelectorParameterName)
                    let collectionSelectorSymbolTable = symbolTable.Set(collectionSelectorParameterName, source.Projection)
                    let selector = this.ProcessSource(collectionSelector.Body, collectionSelectorSymbolTable, resultSelectorParameterName)
                    let resultSelectorSymbolTable = symbolTable.Set(resultSelector.Parameters.[0].Name, source.Projection).Set(resultSelector.Parameters.[1].Name, selector.Projection)
                    let projection = this.ProcessProjection(resultSelector.Body, resultSelectorSymbolTable)
                    LinqProcessor.Select(LinqProcessor.SelectMany(source, selector), projection)
                else
                    raise (SpecificationException(sprintf "Unsupported type of specification %A" expression))
            elif methodCallExpression.Method.DeclaringType = typeof<FactRepository> then
                if methodCallExpression.Method.Name = nameof(FactRepository.OfType) && methodCallExpression.Arguments.Count = 0 then
                    let factType = methodCallExpression.Method.GetGenericArguments().[0]
                    let t = factType.FactTypeName()
                    let label = Label(recommendedLabel, t)
                    LinqProcessor.FactsOfType(label, factType)
                elif methodCallExpression.Method.Name = nameof(FactRepository.OfType) && methodCallExpression.Arguments.Count = 1 then
                    let lambda = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[0])
                    let parameterName = lambda.Parameters.[0].Name
                    let genericArgument = methodCallExpression.Method.GetGenericArguments().[0]
                    let source = LinqProcessor.FactsOfType(Label(parameterName, genericArgument.FactTypeName()), genericArgument)
                    let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                    let predicate = this.ProcessPredicate(lambda.Body, childSymbolTable)
                    LinqProcessor.Where(source, predicate)
                else
                    raise (SpecificationException(sprintf "Unsupported type of specification %A" expression))
            else
                raise (SpecificationException(sprintf "Unsupported type of specification %A" expression))
        | :? MemberExpression as memberExpression ->
            if memberExpression.Member :? PropertyInfo then
                let propertyInfo = memberExpression.Member :?> PropertyInfo
                if propertyInfo.PropertyType.IsGenericType && (propertyInfo.PropertyType.GetGenericTypeDefinition() = typeof<IQueryable<_>> || propertyInfo.PropertyType.GetGenericTypeDefinition() = typeof<Relation<_>>) then
                    let target = SpecificationProcessor.InstanceOfFact(propertyInfo.DeclaringType)
                    let relation = propertyInfo.GetGetMethod().Invoke(target, [||]) :?> IQueryable
                    let projection = this.ProcessProjection(memberExpression.Expression, symbolTable)
                    let childSymbolTable = SymbolTable.Empty.Set("this", projection)
                    this.ProcessSource(relation.Expression, childSymbolTable, recommendedLabel)
                else
                    raise (SpecificationException(sprintf "Unsupported type of specification %A" expression))
            else
                raise (SpecificationException(sprintf "Unsupported type of specification %A" expression))
        | _ -> raise (SpecificationException(sprintf "Unsupported type of specification %A" expression))

    member private this.ProcessPredicate(body: Expression, symbolTable: SymbolTable) =
        match body with
        | :? BinaryExpression as binary when binary.NodeType = ExpressionType.Equal ->
            let left = this.ProcessReference(binary.Left, symbolTable)
            let right = this.ProcessReference(binary.Right, symbolTable)
            LinqProcessor.Compare(left, right)
        | :? UnaryExpression as unary when unary.NodeType = ExpressionType.Not ->
            LinqProcessor.Not(this.ProcessPredicate(unary.Operand, symbolTable))
        | :? MethodCallExpression as methodCallExpression ->
            if methodCallExpression.Method.DeclaringType = typeof<Queryable> then
                if methodCallExpression.Method.Name = nameof(Queryable.Any) && methodCallExpression.Arguments.Count = 1 then
                    let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, "")
                    LinqProcessor.Any(source)
                elif methodCallExpression.Method.Name = nameof(Queryable.Any) && methodCallExpression.Arguments.Count = 2 then
                    let lambda = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[1])
                    let parameterName = lambda.Parameters.[0].Name
                    let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, parameterName)
                    let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                    let predicate = this.ProcessPredicate(lambda.Body, childSymbolTable)
                    LinqProcessor.Any(LinqProcessor.Where(source, predicate))
                else
                    raise (SpecificationException(sprintf "Unsupported predicate type %A" body))
            elif methodCallExpression.Method.DeclaringType = typeof<Enumerable> && methodCallExpression.Method.Name = nameof(Enumerable.Contains) && methodCallExpression.Arguments.Count = 2 then
                let left = this.ProcessReference(methodCallExpression.Arguments.[0], symbolTable)
                let right = this.ProcessReference(methodCallExpression.Arguments.[1], symbolTable)
                LinqProcessor.Compare(left, right)
            elif methodCallExpression.Method.DeclaringType = typeof<FactRepository> && methodCallExpression.Method.Name = nameof(FactRepository.Any) then
                let lambda = SpecificationProcessor.GetLambda(methodCallExpression.Arguments.[0])
                let parameterName = lambda.Parameters.[0].Name
                let factType = lambda.Parameters.[0].Type
                let source = LinqProcessor.FactsOfType(Label(parameterName, factType.FactTypeName()), factType)
                let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                let predicate = this.ProcessPredicate(lambda.Body, childSymbolTable)
                LinqProcessor.Any(LinqProcessor.Where(source, predicate))
            else
                raise (SpecificationException(sprintf "Unsupported predicate type %A" body))
        | :? UnaryExpression as unaryExpression when unaryExpression.Operand :? MemberExpression && unaryExpression.NodeType = ExpressionType.Convert ->
            let m = unaryExpression.Operand :?> MemberExpression
            if m.Member :? PropertyInfo then
                let propertyInfo = m.Member :?> PropertyInfo
                if propertyInfo.PropertyType = typeof<Condition> && unaryExpression.Type = typeof<bool> then
                    let target = SpecificationProcessor.InstanceOfFact(propertyInfo.DeclaringType)
                    let condition = propertyInfo.GetGetMethod().Invoke(target, [||]) :?> Condition
                    let projection = this.ProcessProjection(m.Expression, symbolTable)
                    let childSymbolTable = SymbolTable.Empty.Set("this", projection)
                    this.ProcessPredicate(condition.Body.Body, childSymbolTable)
                else
                    raise (SpecificationException(sprintf "Unsupported predicate type %A" body))
            else
                raise (SpecificationException(sprintf "Unsupported predicate type %A" body))
        | :? BinaryExpression as andExpression when andExpression.NodeType = ExpressionType.AndAlso ->
            let left = this.ProcessPredicate(andExpression.Left, symbolTable)
            let right = this.ProcessPredicate(andExpression.Right, symbolTable)
            LinqProcessor.And(left, right)
        | _ -> raise (SpecificationException(sprintf "Unsupported predicate type %A" body))

    member private this.ProcessReference(expression: Expression, symbolTable: SymbolTable) =
        match expression with
        | :? ParameterExpression as parameterExpression ->
            let projection = symbolTable.Get(parameterExpression.Name)
            match projection with
            | :? SimpleProjection as simpleProjection ->
                let factType = parameterExpression.Type.FactTypeName()
                ReferenceContext.From(Label(simpleProjection.Tag, factType))
            | _ -> raise (SpecificationException(sprintf "Unsupported projection %A." projection))
        | :? ConstantExpression ->
            let projection = symbolTable.Get("this")
            match projection with
            | :? SimpleProjection as simpleProjection ->
                let factType = expression.Type.FactTypeName()
                ReferenceContext.From(Label(simpleProjection.Tag, factType))
            | _ -> raise (SpecificationException(sprintf "Unsupported projection %A." projection))
        | :? MemberExpression as memberExpression ->
            if memberExpression.Expression.Type.IsFactType() then
                let head = this.ProcessReference(memberExpression.Expression, symbolTable)
                head.Push(Role(memberExpression.Member.Name, memberExpression.Type.FactTypeName()))
            else
                let head = this.ProcessProjection(memberExpression.Expression, symbolTable)
                match head with
                | :? CompoundProjection as compoundProjection ->
                    let m = compoundProjection.GetProjection(memberExpression.Member.Name)
                    match m with
                    | :? SimpleProjection as simpleProjection ->
                        let factType = memberExpression.Type.FactTypeName()
                        ReferenceContext.From(Label(simpleProjection.Tag, factType))
                    | _ -> raise (SpecificationException(sprintf "Unsupported member projection %A." m))
                | _ -> raise (SpecificationException(sprintf "Unsupported head projection %A." head))
        | _ -> raise (SpecificationException(sprintf "Unsupported reference %A." expression))

    static member Queryable<'TProjection> (specExpression: LambdaExpression) =
        let processor = SpecificationProcessor()
        let symbolTable = processor.Given(specExpression.Parameters |> Seq.take (specExpression.Parameters.Count - 1))
        let result = processor.ProcessSource(specExpression.Body, symbolTable, "")
        processor.ValidateMatches(result.Matches)
        (processor.givenLabels, result.Matches, result.Projection)

    static member Scalar<'TProjection> (specExpression: LambdaExpression) =
        let processor = SpecificationProcessor()
        let symbolTable = processor.Given(specExpression.Parameters)
        let result = processor.ProcessShorthand(specExpression.Body, symbolTable)
        processor.ValidateMatches(result.Matches)
        (processor.givenLabels, result.Matches, result.Projection)

    static member Select<'TProjection> (specSelector: LambdaExpression) =
        let processor = SpecificationProcessor()
        let symbolTable = processor.Given(specSelector.Parameters |> Seq.take (specSelector.Parameters.Count - 1))
        let result = processor.ProcessProjection(specSelector.Body, symbolTable)
        (processor.givenLabels, ImmutableList<Match>.Empty, result)
