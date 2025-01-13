using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Data.Analysis;

namespace Jinaga.Notebooks;

// Extension method to convert a list of objects into a DataFrame that the Notebook will display as a table.
public static class DataFrameExtensions
{
    public static DataFrame AsTable<T>(this IEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var dataFrame = new DataFrame();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (!properties.Any())
            throw new InvalidOperationException($"Type {typeof(T).Name} has no public instance properties.");

        // Create columns for each property
        foreach (var prop in properties)
        {
            var propertyType = prop.PropertyType;
            if (propertyType == typeof(string))
            {
                var values = source.Select(item => (string)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new StringDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                var values = source.Select(item => (int?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new Int32DataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                var values = source.Select(item => (double?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new DoubleDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                var values = source.Select(item => (bool?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new BooleanDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                var values = source.Select(item => (DateTime?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new PrimitiveDataFrameColumn<DateTime>(prop.Name, values));
            }
            else
                throw new NotSupportedException($"Property type {propertyType.Name} is not supported.");
        }

        return dataFrame;
    }
}