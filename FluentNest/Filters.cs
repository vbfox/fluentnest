﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Nest;

namespace FluentNest
{
    public static class Filters
    {
        public static string FirstCharacterToLower(this string str)
        {
            if (string.IsNullOrEmpty(str) || char.IsLower(str, 0))
                return str;

            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        public static AggregationDescriptor<T> IntoDateHistogram<T>(this AggregationDescriptor<T> innerAggregation,
            Expression<Func<T, Object>> fieldGetter, DateInterval interval) where T : class
        {
            AggregationDescriptor<T> v = new AggregationDescriptor<T>();
            var fieldName = GetName(fieldGetter);
            v.DateHistogram(fieldName, dr =>
            {
                DateHistogramAggregationDescriptor<T> dateAggDesc = new DateHistogramAggregationDescriptor<T>();
                dateAggDesc.Field(fieldGetter).Interval(interval);
                return dateAggDesc.Aggregations(x => innerAggregation);
            });

            return v;
        }

        public static AggregationDescriptor<T> IntoHistogram<T>(this AggregationDescriptor<T> innerAggregation,
            Expression<Func<T, Object>> fieldGetter, int interval) where T : class
        {
            AggregationDescriptor<T> v = new AggregationDescriptor<T>();
            var fieldName = GetName(fieldGetter);
            v.Histogram(fieldName, dr =>
            {
                HistogramAggregationDescriptor<T> dateAggDesc = new HistogramAggregationDescriptor<T>();
                dateAggDesc.Field(fieldGetter).Interval(interval);
                return dateAggDesc.Aggregations(x => innerAggregation);
            });

            return v;
        }

        public static AggregationDescriptor<T> DateHistogram<T>(this AggregationDescriptor<T> agg,
            Expression<Func<T, Object>> fieldGetter, DateInterval dateInterval) where T : class
        {
            return agg.DateHistogram(GetName(fieldGetter), x => x.Field(fieldGetter).Interval(dateInterval));
        }

        private static string GetName<T, TK>(this Expression<Func<T, TK>> exp)
        {
            MemberExpression body = exp.Body as MemberExpression;

            if (body == null)
            {
                UnaryExpression ubody = (UnaryExpression) exp.Body;
                body = ubody.Operand as MemberExpression;
            }


            return body.Member.Name;
        }

        public static string GetAggName<T, TK>(this Expression<Func<T, TK>> exp, AggType type)
        {
            var body = exp.Body as MemberExpression;

            if (body == null)
            {
                var ubody = (UnaryExpression) exp.Body;
                body = ubody.Operand as MemberExpression;
            }


            return type + body.Member.Name;
        }


        public static IList<HistogramItem> GetDateHistogram<T>(this KeyItem item,
            Expression<Func<T, Object>> fieldGetter)
        {
            var histogramItem = item.DateHistogram(GetName(fieldGetter));
            return histogramItem.Items;
        }

        public static IList<HistogramItem> GetDateHistogram<T>(this AggregationsHelper aggs,
            Expression<Func<T, Object>> fieldGetter)
        {
            var histogramItem = aggs.DateHistogram(GetName(fieldGetter));
            return histogramItem.Items;
        }

        public static IList<HistogramItem> GetHistogram<T>(this AggregationsHelper aggs,
            Expression<Func<T, Object>> fieldGetter)
        {
            var histogramItem = aggs.Histogram(GetName(fieldGetter));
            return histogramItem.Items;
        }

        public static FilterContainer GenerateComparisonFilter<T>(this Expression expression, ExpressionType type)
            where T : class
        {
            var binaryExpression = (BinaryExpression) expression;
            var value = GetValue(binaryExpression);
            var memberAccessor = binaryExpression.Left as MemberExpression;
            var fieldName = GetFieldNameFromMember(memberAccessor);

            var filterDescriptor = new FilterDescriptor<T>();
            if (value is DateTime)
            {
                return filterDescriptor.Range(x => x.RangeOnDate(type, (DateTime) value).OnField(fieldName));
            }

            if (value is double || value is decimal)
            {
                return filterDescriptor.Range(x => x.RangeOnNumber(type, Convert.ToDouble(value)).OnField(fieldName));
            }

            if (value is int || value is long)
            {
                return filterDescriptor.Range(x => x.RangeOnNumber(type, Convert.ToDouble(value)).OnField(fieldName));
            }

            throw new InvalidOperationException("Comparison on non-supported type");
        }
        
        public static FilterContainer GenerateEqualityFilter<T>(this BinaryExpression expression) where T : class
        {
            var value = GetValue(expression);
            var filterDescriptor = new FilterDescriptor<T>();
            var name = GetFieldName(expression.Left);
            return filterDescriptor.Term(name, value);
        }

        public static FilterContainer GenerateNotEqualFilter<T>(this BinaryExpression expression) where T : class
        {
            var equalityFilter = GenerateEqualityFilter<T>(expression);
            var filterDescriptor = new FilterDescriptor<T>();
            return filterDescriptor.Not(x => equalityFilter);
        }

        public static FilterContainer GenerateBoolFilter<T>(this Expression expression) where T : class
        {
            var filterDescriptor = new FilterDescriptor<T>();
            var fieldName = GenerateFilterName(expression);
            return filterDescriptor.Term(fieldName, true);
        }


        public static string GetFieldName(this Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            if (memberExpression != null)
            {
                var member = memberExpression;              
                return GetFieldNameFromMember(member);
            }

            var unaryExpression = expression as UnaryExpression;
            if (unaryExpression != null)
            {
                var unary = unaryExpression;
                return GetFieldName(unary.Operand);
            }

            throw new NotImplementedException();
        }

        public static string GetFieldNameFromMember(this MemberExpression expression)
        {
            return FirstCharacterToLower(expression.Member.Name);
        }

        public static bool IsComparisonType(this ExpressionType expType)
        {
            return expType == ExpressionType.LessThan || expType == ExpressionType.GreaterThan || expType == ExpressionType.LessThanOrEqual || expType == ExpressionType.GreaterThanOrEqual;
        }

        public static string GetFieldNameOfBinaryExpression(this Expression exp)
        {
            var binary = (BinaryExpression)exp;
            var memberAccessor = binary.Left as MemberExpression;
            var fieldName = GetFieldNameFromMember(memberAccessor);
            return fieldName;
        }

        public static FilterContainer GenerateFilterDescription<T>(this Expression expression) where T:class
        {
            var expType = expression.NodeType;
            
            if (expType == ExpressionType.AndAlso)
            {
                var binaryExpression = (BinaryExpression)expression;

                // This is a hach for two side range expressions
                if (binaryExpression.Left.NodeType.IsComparisonType() && binaryExpression.Right.NodeType.IsComparisonType())
                {
                    if (binaryExpression.Left.GetFieldNameOfBinaryExpression() == binaryExpression.Right.GetFieldNameOfBinaryExpression())
                    {
                        //we supose that on left hand and right hand we have a binary expressions
                        var leftBinary = (BinaryExpression)binaryExpression.Left;
                        var leftValue = GetValue(leftBinary);

                        var memberAccessor = leftBinary.Left as MemberExpression;
                        var fieldName = GetFieldNameFromMember(memberAccessor);

                        var rightBinary = (BinaryExpression)binaryExpression.Right;
                        var rightValue = GetValue(rightBinary);
                        return Ranges.GenerateRangeFilter<T>(fieldName, leftValue, leftBinary.NodeType, rightValue, rightBinary.NodeType);
                    }
                }

                var filterDescriptor = new FilterDescriptor<T>();
                var rightFilter = GenerateFilterDescription<T>(binaryExpression.Right);

                    // Detecting a series of And filters
                    var leftSide = binaryExpression.Left;
                    var accumulatedExpressions = new List<Expression>();
                    while (leftSide.NodeType == ExpressionType.AndAlso)
                    {

                        var asBinary = (BinaryExpression) leftSide;
                        if (asBinary.Left.NodeType != ExpressionType.AndAlso)
                        {
                            accumulatedExpressions.Add(asBinary.Left);
                        }

                        if (asBinary.Right.NodeType != ExpressionType.AndAlso)
                        {
                            accumulatedExpressions.Add(asBinary.Right);
                        }

                        leftSide = asBinary.Left;
                    }
                    
                    if (accumulatedExpressions.Count > 0)
                    {
                        var filters = accumulatedExpressions.Select(GenerateFilterDescription<T>).ToList();
                        filters.Add(rightFilter);
                        return filterDescriptor.And(filters.ToArray());
                    }
                

                var leftFilter = GenerateFilterDescription<T>(binaryExpression.Left);
                return filterDescriptor.And(leftFilter, rightFilter);

            }

            if (expType == ExpressionType.Or || expType == ExpressionType.OrElse)
            {
                var binaryExpression = expression as BinaryExpression;
                var leftFilter = GenerateFilterDescription<T>(binaryExpression.Left);
                var rightFilter = GenerateFilterDescription<T>(binaryExpression.Right);
                var filterDescriptor = new FilterDescriptor<T>();
                return filterDescriptor.Or(leftFilter, rightFilter);
            }

            if (expType == ExpressionType.Equal)
            {
                return GenerateEqualityFilter<T>(expression as BinaryExpression);
            }

            if(expType == ExpressionType.LessThan || expType == ExpressionType.GreaterThan || expType == ExpressionType.LessThanOrEqual || expType == ExpressionType.GreaterThanOrEqual)
            {
                return GenerateComparisonFilter<T>(expression,expType);
            }

            if (expType == ExpressionType.MemberAccess)
            {
                var memberExpression = (MemberExpression)expression;

                //here we handle binary expressions in the from .field.hasValue
                if (memberExpression.Member.Name == "HasValue")
                {
                    var parentFieldExpression = (memberExpression.Expression as MemberExpression);
                    var parentFieldName = GetFieldNameFromMember(parentFieldExpression);

                    var filterDescriptor = new FilterDescriptor<T>();
                    return filterDescriptor.Exists(parentFieldName);
                }

                var isProperty = memberExpression.Member.MemberType == MemberTypes.Property;

                if (isProperty)
                {
                    var propertyType = ((PropertyInfo) memberExpression.Member).PropertyType;
                    if (propertyType == typeof (bool))
                    {
                        return GenerateBoolFilter<T>(memberExpression);
                    }
                }
            }

            if (expType == ExpressionType.Lambda)
            {
                var lambda = (LambdaExpression)expression;
                return GenerateFilterDescription<T>(lambda.Body);
            }

            if (expType == ExpressionType.NotEqual)
            {
                return GenerateNotEqualFilter<T>(expression as BinaryExpression);
            }

            throw  new NotImplementedException();
        }

        public static string GenerateFilterName(this Expression expression)
        {
            var expType = expression.NodeType;

            if (expType == ExpressionType.AndAlso || expType == ExpressionType.Or || expType == ExpressionType.OrElse || expType == ExpressionType.Equal 
                || expType == ExpressionType.LessThan || expType == ExpressionType.GreaterThan || expType == ExpressionType.LessThanOrEqual || expType == ExpressionType.GreaterThanOrEqual
                || expType == ExpressionType.NotEqual)
            {
                var binaryExpression = expression as BinaryExpression;
                var leftFilterName = GenerateFilterName(binaryExpression.Left);
                var rightFilterName = GenerateFilterName(binaryExpression.Right);
                return leftFilterName + "_" + expType + "_" + rightFilterName;
            }

            if (expType == ExpressionType.MemberAccess)
            {
                var memberExpression = (MemberExpression)expression;
                //here we handle binary expressions in the from .field.hasValue
                if (memberExpression.Member.Name == "HasValue")
                {
                    var parentFieldExpression = (memberExpression.Expression as MemberExpression);
                    var parentFieldName = GetFieldNameFromMember(parentFieldExpression);
                    return parentFieldName + ".hasValue";
                }
                return GetFieldNameFromMember(memberExpression);
            }

            if (expType == ExpressionType.Lambda)
            {
                var lambda = expression as LambdaExpression;
                return GenerateFilterName(lambda.Body);
            }

            if (expType == ExpressionType.Convert)
            {
                var unary = expression as UnaryExpression;
                return GenerateFilterName(unary.Operand);
            }

            if (expType == ExpressionType.Constant)
            {
                var constExp = expression as ConstantExpression;
                return constExp.Value.ToString();
            }
            throw new NotImplementedException();
        }

        public static SearchDescriptor<T> FilterOn<T>(this SearchDescriptor<T> searchDescriptor, Expression<Func<T, bool>> filterRule) where T : class
        {           
            var filterDescriptor = GenerateFilterDescription<T>(filterRule.Body);
            return searchDescriptor.Filter(f => filterDescriptor);           
        }

        public static SearchDescriptor<T> FilteredOn<T>(this SearchDescriptor<T> searchDescriptor, Expression<Func<T, bool>> filterRule) where T : class
        {           
            var binaryExpression = filterRule.Body as BinaryExpression;            
            return searchDescriptor.Query(q => q.Filtered(fil=>fil.Filter(f=>GenerateFilterDescription<T>(binaryExpression))));
        }

        public static SearchDescriptor<T> FilteredOn<T>(this SearchDescriptor<T> searchDescriptor, FilterContainer container) where T : class
        {
            return searchDescriptor.Query(q => q.Filtered(fil => fil.Filter(f => container)));
        }

        public static DeleteByQueryDescriptor<T> FilteredOn<T>(this DeleteByQueryDescriptor<T> deleteDescriptor, FilterContainer container) where T : class
        {
            return deleteDescriptor.Query(q => q.Filtered(fil => fil.Filter(f => container)));
        }

        public static DeleteByQueryDescriptor<T> FilteredOn<T>(this DeleteByQueryDescriptor<T> deleteDescriptor, Expression<Func<T, bool>> filterRule) where T : class
        {
            var binaryExpression = filterRule.Body as BinaryExpression;
            return deleteDescriptor.Query(q => q.Filtered(fil => fil.Filter(f => GenerateFilterDescription<T>(binaryExpression))));
        }


        public static FilterContainer AndFilteredOn<T>(this FilterContainer queryDescriptor, Expression<Func<T, bool>> filterRule) where T : class
        {
            var filterDescriptor = new FilterDescriptor<T>();
            var binaryExpression = filterRule.Body as BinaryExpression;
            var newPartOfQuery = GenerateFilterDescription<T>(binaryExpression);
            return filterDescriptor.And(newPartOfQuery, queryDescriptor);            
        }

        public static FilterContainer AndValueWithin<T>(this FilterContainer queryDescriptor, Expression<Func<T, Object>> fieldGetter, IEnumerable<Object> list) where T : class
        {
            var filterDescriptor = new FilterDescriptor<T>();
            var termsFilter = filterDescriptor.Terms(fieldGetter, list);
            return filterDescriptor.And(termsFilter, queryDescriptor);
        }

        public static FilterContainer CreateFilter<T>(Expression<Func<T, bool>> filterRule) where T : class
        {
            return GenerateFilterDescription<T>(filterRule);
        }

        public static FilterContainer ValueWithin<T>(Expression<Func<T, object>> propertyGetter, IEnumerable<object> list) where T: class
        {
            return new FilterDescriptor<T>().AndValueWithin(propertyGetter, list);
        }

        private static object GetValue(BinaryExpression binaryExpression)
        {
            var leftHand = binaryExpression.Left;
            var valueExpression = binaryExpression.Right;

            if (leftHand is UnaryExpression)
            {
                // This is necessary in order to avoid the automatic cast of enums to the underlying integer representation
                // In some cases the lambda comes in the shape (Convert(EngineType), 0), where 0 represents the first case of the EngineType enum
                // In such cases, we don't wante the value in the Terms to be 0, but rather we pass the enum value (eg. EngineType.Diesel)
                // and we let the serializer to do it's job and spit out Term("fieldName","diesel") or Term("fieldName","0") depending whether it is converting enums as integers or strings
                // or anything else
                var unaryExpression = leftHand as UnaryExpression;
                if (unaryExpression.Operand.Type.IsEnum)
                {
                    valueExpression = Expression.Convert(binaryExpression.Right, unaryExpression.Operand.Type);
                }
            }
            
            var objectMember = Expression.Convert(valueExpression, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();
            return getter();
            
        }

        public static T Parse<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value);
        }

        public static AggsContainer<T> AsContainer<T> (this AggregationsHelper aggs)
        {
            return new AggsContainer<T>(aggs);
        }

        public static K StringToAnything<K>(string value)
        {
            if ((typeof(K).IsEnum))
            {
                return Parse<K>(value);
            }
            else
            {
                TypeConverter typeConverter = TypeDescriptor.GetConverter(typeof(K));
                return (K)typeConverter.ConvertFromString(value);
            }
        }
    }
}