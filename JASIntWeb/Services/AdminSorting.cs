using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
namespace JASIntWeb.Services;

public static class AdminSorting
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, NavigationManager navigation,
        params (string Key, Expression<Func<T, object>> Order)[] columns)
    {
        var values = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
        var sort = values.GetValueOrDefault("sort").ToString();
        var expression = columns.FirstOrDefault(c => string.Equals(c.Key, sort, StringComparison.OrdinalIgnoreCase)).Order;
        if (expression is null) return query;
        var sorted = values.GetValueOrDefault("direction") == "desc" ? query.OrderByDescending(expression) : query.OrderBy(expression);
        var identity = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("VariantId") ?? typeof(T).GetProperty("CartId") ?? typeof(T).GetProperty("ProductId");
        if (identity is null) return sorted;
        var parameter = Expression.Parameter(typeof(T), "row");
        return sorted.ThenBy(Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, identity), typeof(object)), parameter));
    }
}
