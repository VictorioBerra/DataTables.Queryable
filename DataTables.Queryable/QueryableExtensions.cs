﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DataTables.Queryable
{
    /// <summary>
    /// Set of DataTables.Queryable extensions for <see cref="IQueryable{T}"/>
    /// </summary>
    public static class QueryableExtensions
    {
        public static IPagedList<T> ToPagedList<T>(this IDataTablesQueryable<T> query)
        {           
            return new PagedList<T>(query);
        }

        public static IDataTablesQueryable<T> AsDataTablesQueryable<T>(this IQueryable<T> query, DataTablesRequest<T> request)
        {
            return new DataTablesQueryable<T>(query, request);
        }

        /// <summary>
        /// Modifies the <see cref="IQueryable{T}"/> by applying <see cref="DataTablesRequest{T}"/> filtering parameters.
        /// </summary>
        /// <typeparam name="T">Data type to be filtered</typeparam>
        /// <param name="query"><see cref="IQueryable{T}"/> instance to be filtered.</param>
        /// <param name="request"><see cref="DataTablesRequest{T}"/> instance that stores filterning request parameters</param>
        /// <returns><see cref="IQueryable{T}"/> with appied <see cref="DataTablesRequest{T}"/></returns>
        public static IDataTablesQueryable<T> Filter<T>(this IQueryable<T> query, DataTablesRequest<T> request)
        {
            var result = query
                .AsDataTablesQueryable(request)
                .CustomFilter()
                .GlobalSearch()
                .ColumnsSearch()
                .Order()
                .Paginate();

#if TRACE
            Trace.WriteLine($"DataTables.Queryable resulting query:\n {result}");   
#endif
            return result;
        }

        /// <summary>
        /// Modifies the <see cref="IQueryable{T}"/> by applying custom filter from <see cref="DataTablesRequest{T}"/>.
        /// </summary>
        /// <typeparam name="T">Data type to be filtered</typeparam>
        /// <param name="query"><see cref="IQueryable{T}"/> instance to be filtered.</param>
        /// <param name="request">DataTables request instance</param>
        /// <returns></returns>
        public static IDataTablesQueryable<T> CustomFilter<T>(this IDataTablesQueryable<T> query)
        {
            if (query.Request.CustomFilterPredicate != null)
            {
                return (IDataTablesQueryable<T>)query.Where(query.Request.CustomFilterPredicate);
            }
            return query;
        }

        /// <summary>
        /// Modifies the <see cref="IQueryable{T}"/> by applying global search from <see cref="DataTablesRequest{T}"/>.
        /// </summary>
        /// <typeparam name="T">Data type to be filtered</typeparam>
        /// <param name="query"><see cref="IQueryable{T}"/> instance to be filtered.</param>
        /// <param name="request">DataTables request instance</param>
        /// <returns><see cref="IQueryable{T}"/> with appied global search from <see cref="DataTablesRequest{T}"/></returns>
        public static IDataTablesQueryable<T> GlobalSearch<T>(this IDataTablesQueryable<T> query)
        {
            if (!String.IsNullOrEmpty(query.Request.GlobalSearchValue))
            {
                // all public properties names 
                var propertyNames = query.ElementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => p.Name);

                // searchable columns
                var columns = query.Request.Columns.Where(c =>
                    c.IsSearchable &&
                    propertyNames.Contains(c.PropertyName));

                if (columns.Any())
                {
                    Expression<Func<T, bool>> predicate = null;
                    foreach (var c in columns)
                    {
                        var expr = c.GlobalSearchPredicate ?? BuildStringContainsPredicate<T>(c.PropertyName, query.Request.GlobalSearchValue);
                        predicate = predicate == null ?
                            PredicateBuilder.Create(expr) :
                            predicate.Or(expr);
                    }
                    return (IDataTablesQueryable<T>)query.Where(predicate);
                }
            }
            return query;
        }

        /// <summary>
        /// Modifies the <see cref="IQueryable{T}"/> by applying individual column search from <see cref="DataTablesRequest{T}"/>.
        /// </summary>
        /// <typeparam name="T">Data type to be filtered</typeparam>
        /// <param name="query"><see cref="IQueryable{T}"/> instance to be filtered.</param>
        /// <param name="request">DataTables request instance</param>
        /// <returns><see cref="IQueryable{T}"/> with appied individual column search from <see cref="DataTablesRequest{T}"/></returns>
        public static IDataTablesQueryable<T> ColumnsSearch<T>(this IDataTablesQueryable<T> query)
        {
            // all public property names 
            var propertyNames = query.ElementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name);

            // searchable columns
            var columns = query.Request.Columns.Where(c =>
                c.IsSearchable &&
                !String.IsNullOrEmpty(c.SearchValue) &&
                propertyNames.Contains(c.PropertyName));

            if (columns.Any())
            {
                Expression<Func<T, bool>> predicate = null;
                foreach (var c in columns)
                {
                    var expr = c.ColumnSearchPredicate ?? BuildStringContainsPredicate<T>(c.PropertyName, c.SearchValue);
                    predicate = predicate == null ?
                        PredicateBuilder.Create(expr) :
                        predicate.And(expr);                
                }
                return (IDataTablesQueryable<T>)query.Where(predicate);
            }
            return query;
        }

        /// <summary>
        /// Modifies the <see cref="IQueryable{T}"/> by applying ordering operations defined by <see cref="DataTablesRequest{T}"/>.
        /// </summary>
        /// <typeparam name="T">Data type to be ordered</typeparam>
        /// <param name="query"><see cref="IQueryable{T}"/> instance to be ordered.</param>
        /// <param name="request">DataTables request instance</param>
        /// <returns><see cref="IQueryable{T}"/> with appied ordering from <see cref="DataTablesRequest{T}"/></returns>
        public static IDataTablesQueryable<T> Order<T>(this IDataTablesQueryable<T> query)
        {
            // all public property names 
            var propertyNames = query.ElementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name);

            // orderable columns
            var columns = query.Request.Columns.Where(c =>
                c.IsOrderable &&
                c.OrderingIndex != -1 &&
                propertyNames.Contains(c.PropertyName))
                .OrderBy(c => c.OrderingIndex);

            bool alreadyOrdered = false;

            IDataTablesQueryable<T> result = query;

            foreach (var c in columns)
            {
                result = (IDataTablesQueryable<T>)query.OrderBy(c.PropertyName, c.OrderingDirection, alreadyOrdered);
                alreadyOrdered = true;
            }

            return result;
        }

        public static IDataTablesQueryable<T> Paginate<T>(this IDataTablesQueryable<T> query)
        {
            int skipCount = (query.Request.PageNumber - 1) * query.Request.PageSize;
            int takeCount = query.Request.PageSize;
            return (IDataTablesQueryable<T>)query.Skip(skipCount).Take(takeCount);
        }

        /// <summary>
        /// <see cref="object.ToString()"/> method info. 
        /// Used for building search predicates when the searchable property has non-string type.
        /// </summary>
        private static readonly MethodInfo Object_ToString = typeof(object).GetMethod(nameof(object.ToString));

        /// <summary>
        /// <see cref="string.Contains(string)"/> method info. 
        /// Used for building default search predicates.
        /// </summary>
        private static readonly MethodInfo String_Contains = typeof(string).GetMethod(nameof(String.Contains), new[] { typeof(string) });

        /// <summary>
        /// Creates predicate expression like 
        /// <code>(T t) => t.SomeProperty.Contains("Constant")</code> 
        /// where "SomeProperty" name is defined by <paramref name="stringConstant"/> parameter, and "Constant" is the <paramref name="stringConstant"/>.
        /// If property has non-string type, it is converted to string with <see cref="object.ToString()"/> method.
        /// </summary>
        /// <typeparam name="T">Data type</typeparam>
        /// <param name="propertyName">Property name</param>
        /// <param name="stringConstant">String constant to construnt the <see cref="string.Contains(string)"/> expression.</param>
        /// <returns>Predicate instance</returns>
        private static Expression<Func<T, bool>> BuildStringContainsPredicate<T>(string propertyName, string stringConstant)
        {
            var parameterExp = Expression.Parameter(typeof(T), "e");
            var propertyExp = Expression.Property(parameterExp, propertyName);

            Expression exp = propertyExp;

            // if the property value type is not string, it needs to be casted at first
            if (typeof(T).GetProperty(propertyName).PropertyType != typeof(string))
            {
                exp = Expression.Call(propertyExp, Object_ToString);
            }

            var someValue = Expression.Constant(stringConstant, typeof(string));
            var containsMethodExp = Expression.Call(exp, String_Contains, someValue);
            return Expression.Lambda<Func<T, bool>>(containsMethodExp, parameterExp);
        }

        private static IQueryable<T> OrderBy<T>(this IQueryable<T> query, string propertyName, ListSortDirection direction, bool alreadyOrdered)
        {
            string methodName = null;

            if (direction == ListSortDirection.Ascending && !alreadyOrdered)
                methodName = nameof(System.Linq.Queryable.OrderBy);
            else if (direction == ListSortDirection.Descending && !alreadyOrdered)
                methodName = nameof(System.Linq.Queryable.OrderByDescending);
            if (direction == ListSortDirection.Ascending && alreadyOrdered)
                methodName = nameof(System.Linq.Queryable.ThenBy);
            else if (direction == ListSortDirection.Descending && alreadyOrdered)
                methodName = nameof(System.Linq.Queryable.ThenByDescending);

            var type = typeof(T);
            var property = type.GetProperty(propertyName);
            var parameter = Expression.Parameter(type, "e");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExp = Expression.Lambda(propertyAccess, parameter);
            var typeArguments = new Type[] { type, property.PropertyType };

            var resultExpr = Expression.Call(typeof(System.Linq.Queryable), methodName, typeArguments, query.Expression, Expression.Quote(orderByExp));

            return query.Provider.CreateQuery<T>(resultExpr);
        }
    }
}
