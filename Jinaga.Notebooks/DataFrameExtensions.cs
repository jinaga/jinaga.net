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

        // Every column is a projection of the same sequence, so read the sequence
        // once. Reading it per column gives a lazy source's columns values from
        // different enumerations, so a row denotes an item that never existed, and
        // a source that cannot be read twice yields no table at all.
        var items = source.ToList();

        // Create columns for each property
        foreach (var prop in properties)
        {
            var propertyType = prop.PropertyType;
            if (propertyType == typeof(string))
            {
                var values = items.Select(item => (string)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new StringDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                var values = items.Select(item => (int?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new Int32DataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(long) || propertyType == typeof(long?))
            {
                var values = items.Select(item => (long?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new Int64DataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                var values = items.Select(item => (double?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new DoubleDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(float) || propertyType == typeof(float?))
            {
                var values = items.Select(item => (float?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new SingleDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
            {
                var values = items.Select(item => (decimal?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new DecimalDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                var values = items.Select(item => (bool?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new BooleanDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                var values = items.Select(item => (DateTime?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new DateTimeDataFrameColumn(prop.Name, values));
            }
            else if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
            {
                var values = items.Select(item => (Guid?)prop.GetValue(item)).ToList();
                dataFrame.Columns.Add(new PrimitiveDataFrameColumn<Guid>(prop.Name, values));
            }
            else
            {
                var values = items.Select(item => prop.GetValue(item)?.ToString()).ToList();
                dataFrame.Columns.Add(new StringDataFrameColumn(prop.Name, values));
            }
        }

        return dataFrame;
    }
}