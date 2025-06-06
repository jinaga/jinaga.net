﻿using System;
using System.Linq;
using Jinaga.Facts;

namespace Jinaga.Serialization
{
    static class Interrogate
    {
        public static bool IsField(Type type)
        {
            return
                type == typeof(string) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) ||
                type == typeof(int) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(bool) ||
                type == typeof(Guid) ||
                IsNullableField(type);
        }

        public static bool IsNullableField(Type type)
        {
            return
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                IsField(type.GetGenericArguments()[0]);
        }

        public static bool IsPredecessor(Type type)
        {
            return
                IsFactType(type) ||
                IsArrayOfFactType(type);
        }

        public static bool IsFactType(Type type)
        {
            return type
                .GetCustomAttributes(inherit: false)
                .OfType<FactTypeAttribute>()
                .Any();
        }

        public static bool IsArrayOfFactType(Type type)
        {
            return
                type.IsArray &&
                IsFactType(type.GetElementType());
        }

        public static bool IsHelper(Type propertyType)
        {
            if (propertyType.IsGenericType)
            {
                var genericTypeDefinition = propertyType.GetGenericTypeDefinition();
                return
                    genericTypeDefinition == typeof(Relation<>) ||
                    genericTypeDefinition == typeof(IQueryable<>);
            }

            return
                propertyType == typeof(FactGraph) ||
                propertyType == typeof(Condition);
        }
    }
}
