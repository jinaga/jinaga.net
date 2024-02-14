using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Jinaga.Serialization
{
    class DeserializerCache
    {
        public static DeserializerCache Empty = new DeserializerCache(
            ImmutableDictionary<Type, Delegate>.Empty
        );

        private readonly ImmutableDictionary<Type, Delegate> deserializerByType;

        private DeserializerCache(ImmutableDictionary<Type, Delegate> deserializerByType)
        {
            this.deserializerByType = deserializerByType;
        }

        public (DeserializerCache, Func<Fact, Emitter, object>) GetDeserializer(Type type)
        {
            DeserializerCache after = this;
            if (!deserializerByType.TryGetValue(type, out var deserializer))
            {
                deserializer = Deserialize(type).Compile();
                after = new DeserializerCache(deserializerByType.Add(type, deserializer));
            }
            return (after, (fact, emitter) => deserializer.DynamicInvoke(fact, emitter));
        }

        private static LambdaExpression Deserialize(Type type)
        {
            var factParameter = Expression.Parameter(typeof(Fact));
            var emitterParameter = Expression.Parameter(typeof(Emitter));
            return Expression.Lambda(
                CreateObject(type, factParameter, emitterParameter),
                factParameter,
                emitterParameter
            );
        }

        private static Expression CreateObject(Type type, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            /*
            return Emitter.SetGraph<T>(fact, new T(
                GetFieldValue("creator", typeof(User), fact),
                GetFieldValue("identifier", typeof(string), fact)
            ));
            */
            var constructorInfos = type.GetConstructors();
            if (constructorInfos.Length != 1)
            {
                throw new NotImplementedException($"More than one constructor for {type.Name}");
            }
            var constructor = constructorInfos.Single();
            var parameters = constructor.GetParameters();
            var parameterExpressions = parameters
                .Select(parameter =>
                    Interrogate.IsField(parameter.ParameterType)
                        ? GetFieldValue(parameter.Name, parameter.ParameterType, factParameter) :
                    Interrogate.IsFactType(parameter.ParameterType)
                        ? GetPredecessor(parameter.Name, parameter.ParameterType, factParameter, emitterParameter) :
                    Interrogate.IsArrayOfFactType(parameter.ParameterType)
                        ? GetPredecessorArray(parameter.Name, parameter.ParameterType.GetElementType(), factParameter, emitterParameter) :
                    throw new ArgumentException($"Unknown parameter type {parameter.ParameterType.Name} for {type.Name} constructor, parameter {parameter.Name}")
                )
                .ToArray();

            // If the type does not implement IFactProxy, then try to create a proxy type.
            Type proxyType = typeof(IFactProxy).IsAssignableFrom(type) ? type : CreateProxyType(type);

            // Construct the runtime fact record
            var factConstructor = proxyType.GetConstructors().Single();
            var newExpression = Expression.New(
                factConstructor,
                parameterExpressions
            );

            // Set the graph for the fact
            var setGraphMethod = typeof(Emitter).GetMethod(nameof(Emitter.SetGraph)).MakeGenericMethod(type);
            var setGraph = Expression.Call(
                emitterParameter,
                setGraphMethod,
                factParameter,
                newExpression
            );
            return setGraph;
        }

        private static Expression GetFieldValue(string name, Type parameterType, ParameterExpression factParameter)
        {
            var getFieldValueMethod = typeof(Fact).GetMethod(nameof(Fact.GetFieldValue));
            var getFieldValue = Expression.Call(
                factParameter,
                getFieldValueMethod,
                Expression.Constant(name)
            );
            if (parameterType == typeof(string))
            {
                return Expression.Property(
                    getFieldValue,
                    nameof(FieldValue.StringValue)
                );
            }
            else if (parameterType == typeof(DateTime))
            {
                return Expression.Call(
                    typeof(FieldValue).GetMethod(nameof(FieldValue.FromIso8601String)),
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.StringValue)
                    )
                );
            }
            else if (parameterType == typeof(DateTime?))
            {
                var fromNullableIso8601String = typeof(FieldValue).GetMethod(nameof(FieldValue.FromNullableIso8601String));
                return Expression.Call(
                    fromNullableIso8601String,
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.StringValue)
                    )
                );
            }
            else if (parameterType == typeof(DateTimeOffset))
            {
                return Expression.Call(
                    typeof(FieldValue).GetMethod(nameof(FieldValue.FromIso8601String)),
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.StringValue)
                    )
                );
            }
            else if (parameterType == typeof(Guid))
            {
                return Expression.Call(
                    typeof(FieldValue).GetMethod(nameof(FieldValue.GuidFromString)),
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.StringValue)
                    )
                );
            }
            else if (parameterType == typeof(Guid?))
            {
                var guidFromNullableString = typeof(FieldValue).GetMethod(nameof(FieldValue.NullableGuidFromString));
                return Expression.Call(
                    guidFromNullableString,
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.StringValue)
                    )
                );
            }
            else if (parameterType == typeof(int))
            {
                return Expression.Convert(
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.DoubleValue)
                    ),
                    typeof(int)
                );
            }
            else if (parameterType == typeof(float))
            {
                return Expression.Convert(
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.DoubleValue)
                    ),
                    typeof(float)
                );
            }
            else if (parameterType == typeof(double))
            {
                return Expression.Property(
                    getFieldValue,
                    nameof(FieldValue.DoubleValue)
                );
            }
            else if (parameterType == typeof(bool))
            {
                return Expression.Property(
                    getFieldValue,
                    nameof(FieldValue.BoolValue)
                );
            }
            else
            {
                throw new ArgumentException($"Unknown field type {parameterType.Name}");
            }
        }

        private static Expression GetPredecessor(string role, Type parameterType, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            /*
            var predecessorSingle = fact.GetPredecessorSingle(role);
            return predecessorSingle == null
                ? (T)null
                : emitter.Deserialize<T>(predecessorSingle);
            */
            var predecessorSingle = Expression.Call(
                factParameter,
                typeof(Fact).GetMethod(nameof(Fact.GetPredecessorSingle)),
                Expression.Constant(role)
            );
            var deserialize = Expression.Condition(
                Expression.Equal(
                    predecessorSingle,
                    Expression.Constant(null)
                ),
                Expression.Constant(null, parameterType),
                Expression.Call(
                    emitterParameter,
                    typeof(Emitter).GetMethod(nameof(Emitter.Deserialize)).MakeGenericMethod(parameterType),
                    predecessorSingle
                )
            );
            return deserialize;
        }

        private static Expression GetPredecessorArray(string role, Type elementType, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            /*
            var predecessorMultiple = fact.GetPredecessorMultiple(role);
            return emitter.Deserialize<T[]>(predecessorMultiple);
            */
            var predecessorMultiple = Expression.Call(
                factParameter,
                typeof(Fact).GetMethod(nameof(Fact.GetPredecessorMultiple)),
                Expression.Constant(role)
            );
            var deserialize = Expression.Call(
                emitterParameter,
                typeof(Emitter).GetMethod(nameof(Emitter.DeserializeArray)).MakeGenericMethod(elementType),
                predecessorMultiple
            );
            return deserialize;
        }

        private static Type CreateProxyType(Type type)
        {
            try
            {
                var typeSignature = $"{type.Name}Proxy";
                var assemblyName = new AssemblyName(typeSignature);
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                // Inherit from T
                var typeBuilder = moduleBuilder.DefineType(typeSignature,
                        TypeAttributes.Public |
                        TypeAttributes.Class |
                        TypeAttributes.AutoClass |
                        TypeAttributes.AnsiClass |
                        TypeAttributes.BeforeFieldInit |
                        TypeAttributes.AutoLayout,
                        type);
                // Implement IFactProxy
                typeBuilder.AddInterfaceImplementation(typeof(IFactProxy));
                // Define a backing field for the fact graph
                var fieldBuilder = typeBuilder.DefineField(
                    "graph",
                    typeof(FactGraph),
                    FieldAttributes.Private
                );
                // Define a property for the fact graph that explicitly implements IFactProxy
                var propertyBuilder = typeBuilder.DefineProperty(
                    "IFactProxy.Graph",
                    PropertyAttributes.None,
                    typeof(FactGraph),
                    Type.EmptyTypes
                );
                // Define the getter for the FactReference that explicitly implements IFactProxy
                var getterBuilder = typeBuilder.DefineMethod(
                    "IFactProxy.get_Graph",
                    MethodAttributes.Private |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig |
                    MethodAttributes.NewSlot |
                    MethodAttributes.Virtual |
                    MethodAttributes.Final,
                    typeof(FactGraph),
                    Type.EmptyTypes
                );

                // Implement the getter for the Fact
                var gil = getterBuilder.GetILGenerator();
                gil.Emit(OpCodes.Ldarg_0); // Load 'this' onto the stack
                gil.Emit(OpCodes.Ldfld, fieldBuilder); // Load the value of the 'graph' field onto the stack
                gil.Emit(OpCodes.Ret); // Return the value on the top of the stack

                propertyBuilder.SetGetMethod(getterBuilder);

                // Define the get_Graph method as an override of the IFactProxy.Graph getter.
                typeBuilder.DefineMethodOverride(getterBuilder, typeof(IFactProxy).GetProperty(nameof(IFactProxy.Graph)).GetMethod);

                // Define the setter for the FactReference that explicitly implements IFactProxy
                var setterBuilder = typeBuilder.DefineMethod(
                    "IFactProxy.set_Graph",
                    MethodAttributes.Private |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig |
                    MethodAttributes.NewSlot |
                    MethodAttributes.Virtual |
                    MethodAttributes.Final,
                    typeof(void),
                    new[] { typeof(FactGraph) }
                );
                // Implement the setter for the Fact
                var sil = setterBuilder.GetILGenerator();
                sil.Emit(OpCodes.Ldarg_0);
                sil.Emit(OpCodes.Ldarg_1);
                sil.Emit(OpCodes.Stfld, fieldBuilder);
                sil.Emit(OpCodes.Ret);
                propertyBuilder.SetSetMethod(setterBuilder);

                // Define the set_Graph method as an override of the IFactProxy.Graph setter.
                typeBuilder.DefineMethodOverride(setterBuilder, typeof(IFactProxy).GetProperty(nameof(IFactProxy.Graph)).SetMethod);

                // Get the only constructor for T
                var constructor = type.GetConstructors().Single();
                // Get the constructor parameters for T
                var parameters = constructor.GetParameters();
                // Define a constructor for the proxy that takes the same parameters as T
                var constructorBuilder = typeBuilder.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    parameters.Select(p => p.ParameterType).ToArray()
                );
                // Call the base constructor for T
                var cil = constructorBuilder.GetILGenerator();
                cil.Emit(OpCodes.Ldarg_0);
                for (var i = 0; i < parameters.Length; i++)
                {
                    cil.Emit(OpCodes.Ldarg, i + 1);
                }
                cil.Emit(OpCodes.Call, constructor);
                cil.Emit(OpCodes.Ret);
                var proxyType = typeBuilder.CreateType();
                return proxyType;
            }
            catch (TypeLoadException ex)
            {
                throw new ArgumentException($"The type {type.FullName} must be public.", ex);
            }
            catch (PlatformNotSupportedException)
            {
                // The platform does not support reflection emit.
                return type;
            }
        }
    }
}
