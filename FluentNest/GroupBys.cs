﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Nest;

namespace FluentNest
{
    public static class GroupBys
    {
        public static AggregationDescriptor<T> GroupBy<T>(this AggregationDescriptor<T> innerAggregation, Expression<Func<T, Object>> fieldGetter) where T : class
        {
            AggregationDescriptor<T> v = new AggregationDescriptor<T>();
            var fieldName = fieldGetter.GetName();
            v.Terms(fieldName, tr =>
            {
                TermsAggregationDescriptor<T> trmAggDescriptor = new TermsAggregationDescriptor<T>();
                trmAggDescriptor.Field(fieldGetter);
                return trmAggDescriptor.Aggregations(x => innerAggregation);
            });

            return v;
        }

        public static AggregationDescriptor<T> GroupBy<T>(this AggregationDescriptor<T> innerAggregation, String key) where T : class
        {
            AggregationDescriptor<T> v = new AggregationDescriptor<T>();
            v.Terms(key, tr =>
            {
                TermsAggregationDescriptor<T> trmAggDescriptor = new TermsAggregationDescriptor<T>();
                trmAggDescriptor.Field(key);
                return trmAggDescriptor.Aggregations(x => innerAggregation);
            });

            return v;
        }

        public static IEnumerable<KeyItem> GetGroupBy<T>(this BucketAggregationBase aggs, Expression<Func<T, Object>> fieldGetter)
        {
            var aggName = fieldGetter.GetName();
            var itemsTerms = aggs.Terms(aggName);
            return itemsTerms.Items;
        }

        public static IEnumerable<KeyItem> GetGroupBy<T>(this BucketAggregationBase aggs, String key)
        {
            var itemsTerms = aggs.Terms(key);
            return itemsTerms.Items;
        }

        public static IDictionary<String, KeyItem> GetDictioanry<T>(this BucketAggregationBase aggs, Expression<Func<T, Object>> fieldGetter)
        {
            var aggName = fieldGetter.GetName();
            var itemsTerms = aggs.Terms(aggName);
            return itemsTerms.Items.ToDictionary(x => x.Key);
        }

        public static IEnumerable<K> GetGroupBy<T,K>(this AggregationsHelper aggs, Expression<Func<T, Object>> fieldGetter, Func<KeyItem, K> objectTransformer)
        {
            var aggName = fieldGetter.GetName();
            var itemsTerms = aggs.Terms(aggName);
            return itemsTerms.Items.Select(objectTransformer);
        }

        public static IEnumerable<KeyItem> GetGroupBy<T>(this AggregationsHelper aggs, Expression<Func<T, Object>> fieldGetter)
        {
            var aggName = fieldGetter.GetName();
            var itemsTerms = aggs.Terms(aggName);
            return itemsTerms.Items;
        }

        public static IEnumerable<KeyItem> GetGroupBy<T>(this AggregationsHelper aggs, String key)
        {
            var itemsTerms = aggs.Terms(key);
            return itemsTerms.Items;
        }

        public static IDictionary<String, KeyItem> GetDictioanry<T>(this AggregationsHelper aggs, Expression<Func<T, Object>> fieldGetter)
        {
            var aggName = fieldGetter.GetName();
            var itemsTerms = aggs.Terms(aggName);
            return itemsTerms.Items.ToDictionary(x => x.Key);
        }

        public static IDictionary<String, K> GetDictioanry<T, K>(this AggregationsHelper aggs, Expression<Func<T, Object>> fieldGetter, Func<KeyItem, K> objectTransformer)
        {
            var aggName = fieldGetter.GetName();
            var itemsTerms = aggs.Terms(aggName);
            return itemsTerms.Items.ToDictionary(x => x.Key, objectTransformer);
        }
    }
}
