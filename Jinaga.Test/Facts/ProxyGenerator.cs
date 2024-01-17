using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Jinaga.Facts;
using Jinaga.Serialization;

namespace Jinaga.Test.Facts;

public class ProxyGenerator
{
    public static T CreateProxy<T>(T instance)
    {
        Type type = typeof(T);
        var proxyType = CreateProxyType(type);
        // Get all of the properties of the instance
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        // Get the constructor for the proxy
        var constructor = proxyType.GetConstructors().Single();
        // Get the constructor parameters for the proxy
        var parameters = constructor.GetParameters();
        // TODO: Generate the fact
        Fact fact = Fact.Create("TODO", ImmutableList<Field>.Empty, ImmutableList<Predecessor>.Empty);
        // Create an array of arguments for the proxy constructor
        var arguments = properties
            .Select(property => property.GetValue(instance))
            .Concat(new object[] { fact })
            .ToArray();
        // Create the proxy
        var proxy = constructor.Invoke(arguments);
        return (T)proxy;
    }

    private static Type CreateProxyType(Type type)
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
        // Define a backing field for the fact
        var fieldBuilder = typeBuilder.DefineField(
            "fact",
            typeof(Fact),
            FieldAttributes.Private
        );
        // Define a property for the Fact
        var propertyBuilder = typeBuilder.DefineProperty(
            nameof(IFactProxy.Fact),
            PropertyAttributes.None,
            typeof(Fact),
            Type.EmptyTypes
        );
        // Define the getter for the FactReference
        var getterBuilder = typeBuilder.DefineMethod(
            "get_Fact",
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.HideBySig |
            MethodAttributes.Virtual,
            typeof(Fact),
            Type.EmptyTypes
        );
        // Implement the getter for the Fact
        var gil = getterBuilder.GetILGenerator();
        gil.Emit(OpCodes.Ldarg_0);
        gil.Emit(OpCodes.Ldfld, fieldBuilder);
        gil.Emit(OpCodes.Ret);
        propertyBuilder.SetGetMethod(getterBuilder);

        // Get the only constructor for T
        var constructor = type.GetConstructors().Single();
        // Get the constructor parameters for T
        var parameters = constructor.GetParameters();
        // Define a constructor for the proxy that takes all of the parameters for T plus a Fact
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            parameters.Select(p => p.ParameterType).Append(typeof(Fact)).ToArray()
        );
        // Call the base constructor for T
        var cil = constructorBuilder.GetILGenerator();
        cil.Emit(OpCodes.Ldarg_0);
        for (var i = 0; i < parameters.Length; i++)
        {
            cil.Emit(OpCodes.Ldarg, i + 1);
        }
        cil.Emit(OpCodes.Call, constructor);
        // Set the reference field
        cil.Emit(OpCodes.Ldarg_0);
        cil.Emit(OpCodes.Ldarg, parameters.Length + 1);
        cil.Emit(OpCodes.Stfld, fieldBuilder);
        cil.Emit(OpCodes.Ret);
        var proxyType = typeBuilder.CreateType();
        return proxyType;
    }
}
