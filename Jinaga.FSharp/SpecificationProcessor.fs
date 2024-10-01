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
open System.Reflection
open System.Collections.Generic

module SpecificationProcessorFS =

    [<Literal>]
    let ObservableMethodName = "Observable"
    [<Literal>]
    let GivenMethodName = "Given"
    [<Literal>]
    let ThenMethodName = "Then"
    [<Literal>]
    let WhereMethodName = "Where"
    [<Literal>]
    let SelectMethodName = "Select"
    [<Literal>]
    let SelectManyMethodName = "SelectMany"
    [<Literal>]
    let ContainsMethodName = "Contains"
    [<Literal>]
    let AnyMethodName = "Any"
    [<Literal>]
    let HashMethodName = "Hash"
    [<Literal>]
    let OfTypeMethodName = "OfType"

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

    let (|ParameterExpr|_|) (expr: Expression) =
        match expr with
        | :? ParameterExpression as pe -> Some pe
        | _ -> None

    let (|NewExpr|_|) (expr: Expression) =
        match expr with
        | :? NewExpression as ne -> Some ne
        | _ -> None

    let (|MemberInitExpr|_|) (expr: Expression) =
        match expr with
        | :? MemberInitExpression as mie -> Some mie
        | _ -> None

    let (|MemberExpr|_|) (expr: Expression) =
        match expr with
        | :? MemberExpression as me -> Some me
        | _ -> None

    let (|MethodCallExpr|_|) (expr: Expression) =
        match expr with
        | :? MethodCallExpression as mce -> Some mce
        | _ -> None

    let private getLambda (argument: Expression) =
        match argument with
        | :? UnaryExpression as unaryExpression ->
            match unaryExpression.Operand with
            | :? LambdaExpression as lambdaExpression -> lambdaExpression
            | _ -> failwith $"Expected a lambda expression for {argument}."
        | _ -> failwith $"Expected a unary lambda expression for {argument}."

    let rec private instanceOfFact (factType: Type) =
        let constructor = factType.GetConstructors() |> Seq.head
        let parameters = 
            constructor.GetParameters()
            |> Seq.map (fun p -> p.ParameterType)
            |> Seq.map (fun t -> 
                if t.IsValueType then 
                    Activator.CreateInstance(t)
                else 
                    instanceOfFact t)
            |> Seq.toArray
        Activator.CreateInstance(factType, parameters)

    let private labelOfProjection (projection: Projection) =
        match projection with
        | :? SimpleProjection as simpleProjection -> simpleProjection.Tag
        | _ -> failwith $"Expected a simple projection, but got {projection.GetType().Name}."

    type SpecificationProcessorFS() =
        let mutable labels = []
        let mutable (givenLabels: Label list) = []

        member this.GivenLabels = givenLabels

        member private this.ValidateMatches(matches: ImmutableList<Match>) =
            // Look for matches with no path conditions.
            let mutable priorMatch: Match option = None
            for m in matches do
                if not (m.PathConditions.Any()) then
                    let unknown = m.Unknown.Name
                    let prior = 
                        match priorMatch with
                        | Some priorMatch -> sprintf "prior variable \"%s\"" priorMatch.Unknown.Name
                        | None -> sprintf "parameter \"%s\"" (givenLabels.Head.Name)
                    raise (SpecificationException(sprintf "The variable \"%s\" should be joined to the %s." unknown prior))
                priorMatch <- Some m

        member private this.Given(parameters: seq<ParameterExpression>) =
            givenLabels <- parameters
                |> Seq.map (fun parameter -> this.NewLabel(parameter.Name, parameter.Type.FactTypeName()))
                |> List.ofSeq
            parameters
            |> Seq.fold (fun (table: SymbolTable) parameter -> table.Set(parameter.Name, SimpleProjection(parameter.Name, parameter.Type))) (SymbolTable.Empty : SymbolTable)

        member private this.NewLabel(recommendedName: string, factType: string) =
            let source = Label(recommendedName, factType)
            labels <- source :: labels
            source

        member private this.ProcessShorthand(expression: Expression, symbolTable: SymbolTable) =
            let reference: ReferenceContext = this.ProcessReference(expression, symbolTable)
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
            | ParameterExpr pe ->
                symbolTable.Get(pe.Name)
            | NewExpr ne ->
                let names =
                    if ne.Members <> null then
                        ne.Members |> Seq.map (fun m -> m.Name)
                    else
                        ne.Constructor.GetParameters() |> Seq.map (fun parameter -> parameter.Name)
                let values = ne.Arguments |> Seq.map (fun arg -> this.ProcessProjection(arg, symbolTable))
                let fields = names.Zip(values, (fun name value -> KeyValuePair.Create(name, value))) |> ImmutableDictionary.ToImmutableDictionary
                CompoundProjection(fields, ne.Type)
            | MemberInitExpr mie ->
                let parameters = mie.NewExpression.Constructor.GetParameters() |> Seq.zip mie.NewExpression.Arguments |> Seq.map (fun (arg, parameter) -> KeyValuePair.Create(parameter.Name, this.ProcessProjection(arg, symbolTable)))
                let fields = mie.Bindings |> Seq.map (fun binding -> this.ProcessProjectionMember(binding, symbolTable))
                let childProjections = parameters.Concat(fields) |> ImmutableDictionary.ToImmutableDictionary
                CompoundProjection(childProjections, mie.Type)
            | MemberExpr me ->
                match me.Member with
                | :? PropertyInfo as propertyInfo ->
                    if propertyInfo.PropertyType.IsGenericType && (propertyInfo.PropertyType.GetGenericTypeDefinition() = typeof<Relation<_>> || propertyInfo.PropertyType.GetGenericTypeDefinition() = typeof<IQueryable<_>>) then
                        let target = instanceOfFact(propertyInfo.DeclaringType)
                        let relation = propertyInfo.GetGetMethod().Invoke(target, [||]) :?> IQueryable
                        let projection = this.ProcessProjection(me.Expression, symbolTable)
                        let childSymbolTable = SymbolTable.Empty.Set("this", projection)
                        this.ProcessProjection(relation.Expression, childSymbolTable)
                    else
                        let head = this.ProcessProjection(me.Expression, symbolTable)
                        match head with
                        | :? CompoundProjection as compoundProjection -> compoundProjection.GetProjection(me.Member.Name)
                        | :? SimpleProjection as simpleProjection ->
                            if not (me.Type.IsFactType()) then
                                FieldProjection(simpleProjection.Tag, me.Expression.Type, me.Member.Name, me.Type)
                            else
                                raise (SpecificationException(sprintf "Cannot select %s directly. Give the fact a label first." me.Member.Name))
                        | _ -> raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
                | _ -> raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
            | MethodCallExpr mce ->
                if mce.Method.DeclaringType = typeof<FactRepository> && mce.Method.Name = ObservableMethodName then
                    if mce.Arguments.Count = 2 && typeof<Specification>.IsAssignableFrom(mce.Arguments.[1].Type) then
                        let start = this.ProcessProjection(mce.Arguments.[0], symbolTable)
                        let label = labelOfProjection(start)
                        let lambdaExpression = Expression.Lambda<Func<obj>>(mce.Arguments.[1])
                        let specification = lambdaExpression.Compile().Invoke() :?> Specification
                        let arguments = [label] |> ImmutableList.CreateRange
                        let specification = specification.Apply(arguments)
                        CollectionProjection(specification.Matches, specification.Projection, mce.Type)
                    elif mce.Arguments.Count = 1 && typeof<IQueryable>.IsAssignableFrom(mce.Arguments.[0].Type) then
                        let (value: SourceContext) = this.ProcessSource(mce.Arguments.[0], symbolTable, "")
                        CollectionProjection(value.Matches, value.Projection, mce.Type)
                    else
                        raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
                elif expression.Type.IsGenericType && expression.Type.GetGenericTypeDefinition() = typeof<IQueryable<_>> then
                    let value = this.ProcessSource(expression, symbolTable, "")
                    CollectionProjection(value.Matches, value.Projection, expression.Type)
                elif mce.Method.DeclaringType = typeof<JinagaClient> && mce.Method.Name = HashMethodName && mce.Arguments.Count = 1 then
                    let value = this.ProcessProjection(mce.Arguments.[0], symbolTable)
                    match value with
                    | :? SimpleProjection as simpleProjection -> HashProjection(simpleProjection.Tag, mce.Arguments.[0].Type)
                    | _ -> raise (SpecificationException(sprintf "Cannot hash %s." (value.ToDescriptiveString())))
                else
                    raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))
            | _ -> raise (SpecificationException(sprintf "Unsupported type of projection %A" expression))

        member private this.ProcessSource(expression: Expression, symbolTable: SymbolTable, recommendedLabel: string) =
            match expression with
            | :? MethodCallExpression as methodCallExpression ->
                if methodCallExpression.Method.DeclaringType = typeof<Queryable> then
                    if methodCallExpression.Method.Name = WhereMethodName && methodCallExpression.Arguments.Count = 2 then
                        let (lambda: LambdaExpression) = getLambda(methodCallExpression.Arguments.[1])
                        let parameterName = lambda.Parameters.[0].Name
                        let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, parameterName)
                        let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                        let predicate = this.ProcessPredicate(lambda.Body, childSymbolTable)
                        LinqProcessor.Where(source, predicate)
                    elif methodCallExpression.Method.Name = SelectMethodName && methodCallExpression.Arguments.Count = 2 then
                        let selector = getLambda(methodCallExpression.Arguments.[1])
                        let parameterName = selector.Parameters.[0].Name
                        let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, parameterName)
                        let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                        let projection = this.ProcessProjection(selector.Body, childSymbolTable)
                        LinqProcessor.Select(source, projection)
                    elif methodCallExpression.Method.Name = SelectManyMethodName && methodCallExpression.Arguments.Count = 2 then
                        let collectionSelector = getLambda(methodCallExpression.Arguments.[1])
                        let collectionSelectorParameterName = collectionSelector.Parameters.[0].Name
                        let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, collectionSelectorParameterName)
                        let collectionSelectorSymbolTable = symbolTable.Set(collectionSelectorParameterName, source.Projection)
                        let selector = this.ProcessSource(collectionSelector.Body, collectionSelectorSymbolTable, recommendedLabel)
                        LinqProcessor.SelectMany(source, selector)
                    elif methodCallExpression.Method.Name = SelectManyMethodName && methodCallExpression.Arguments.Count = 3 then
                        let collectionSelector = getLambda(methodCallExpression.Arguments.[1])
                        let collectionSelectorParameterName = collectionSelector.Parameters.[0].Name
                        let resultSelector = getLambda(methodCallExpression.Arguments.[2])
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
                    if methodCallExpression.Method.Name = OfTypeMethodName && methodCallExpression.Arguments.Count = 0 then
                        let factType = methodCallExpression.Method.GetGenericArguments().[0]
                        let t = factType.FactTypeName()
                        let label = Label(recommendedLabel, t)
                        LinqProcessor.FactsOfType(label, factType)
                    elif methodCallExpression.Method.Name = OfTypeMethodName && methodCallExpression.Arguments.Count = 1 then
                        let lambda = getLambda(methodCallExpression.Arguments.[0])
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
                        let target = instanceOfFact(propertyInfo.DeclaringType)
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
                        let lambda = getLambda(methodCallExpression.Arguments.[1])
                        let parameterName = lambda.Parameters.[0].Name
                        let source = this.ProcessSource(methodCallExpression.Arguments.[0], symbolTable, parameterName)
                        let childSymbolTable = symbolTable.Set(parameterName, source.Projection)
                        let predicate = this.ProcessPredicate(lambda.Body, childSymbolTable)
                        LinqProcessor.Any(LinqProcessor.Where(source, predicate))
                    else
                        raise (SpecificationException(sprintf "Unsupported predicate type %A" body))
                elif methodCallExpression.Method.DeclaringType = typeof<Enumerable> && methodCallExpression.Method.Name = ContainsMethodName && methodCallExpression.Arguments.Count = 2 then
                    let left = this.ProcessReference(methodCallExpression.Arguments.[0], symbolTable)
                    let right = this.ProcessReference(methodCallExpression.Arguments.[1], symbolTable)
                    LinqProcessor.Compare(left, right)
                elif methodCallExpression.Method.DeclaringType = typeof<FactRepository> && methodCallExpression.Method.Name = AnyMethodName then
                    let lambda = getLambda(methodCallExpression.Arguments.[0])
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
                        let target = instanceOfFact(propertyInfo.DeclaringType)
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

        member private this.ProcessProjectionMember(binding: MemberBinding, symbolTable: SymbolTable) : KeyValuePair<string, Projection> =
            match binding with
            | :? MemberAssignment as assignment ->
                let name = assignment.Member.Name
                let value = this.ProcessProjection(assignment.Expression, symbolTable)
                KeyValuePair.Create(name, value)
            | _ ->
                raise (SpecificationException($"Unsupported projection member {binding}."))

        static member Queryable<'TProjection> (specExpression: LambdaExpression) =
            let processor = SpecificationProcessorFS()
            let symbolTable = processor.Given(specExpression.Parameters |> Seq.take (specExpression.Parameters.Count - 1))
            let result = processor.ProcessSource(specExpression.Body, symbolTable, "")
            processor.ValidateMatches(result.Matches)
            (processor.GivenLabels, result.Matches, result.Projection)

        static member Scalar<'TProjection> (specExpression: LambdaExpression) =
            let processor = SpecificationProcessorFS()
            let symbolTable = processor.Given(specExpression.Parameters)
            let result = processor.ProcessShorthand(specExpression.Body, symbolTable)
            processor.ValidateMatches(result.Matches)
            (processor.GivenLabels, result.Matches, result.Projection)

        static member Select<'TProjection> (specSelector: LambdaExpression) =
            let processor = SpecificationProcessorFS()
            let symbolTable = processor.Given(specSelector.Parameters |> Seq.take (specSelector.Parameters.Count - 1))
            let result = processor.ProcessProjection(specSelector.Body, symbolTable)
            (processor.GivenLabels, ImmutableList<Match>.Empty, result)
