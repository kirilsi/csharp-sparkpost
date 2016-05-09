﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SparkPost.Utilities;
using SparkPost.ValueMappers;

namespace SparkPost
{
    public interface IDataMapper
    {
        IDictionary<string, object> ToDictionary(Transmission transmission);
        IDictionary<string, object> ToDictionary(Recipient recipient);
        IDictionary<string, object> ToDictionary(Address address);
        IDictionary<string, object> ToDictionary(Options options);
        IDictionary<string, object> ToDictionary(Content content);
        IDictionary<string, object> ToDictionary(Attachment attachment);
        IDictionary<string, object> ToDictionary(InlineImage inlineImage);
        IDictionary<string, object> ToDictionary(File file);
        IDictionary<string, object> ToDictionary(Suppression suppression);
        IDictionary<string, object> ToDictionary(Webhook webhook);
        IDictionary<string, object> ToDictionary(Subaccount subaccount);
        IDictionary<string, object> CatchAll(object anything);
        object GetTheValue(Type propertyType, object value);
        IDictionary<Type, MethodInfo> ToDictionaryMethods();
    }

    public class DataMapper : IDataMapper
    {
        private readonly IEnumerable<IValueMapper> valueMappers;

        public DataMapper(string version = "v1")
        {
            valueMappers = new List<IValueMapper>
            {
                new MapASingleItemUsingToDictionary(this),
                new MapASetOfItemsUsingToDictionary(this),
                new BooleanValueMapper(),
                new EnumValueMapper(),
                new DateTimeOffsetValueMapper(),
                new DateTimeValueMapper(),
                new StringObjectDictionaryValueMapper(this),
                new StringStringDictionaryValueMapper(),
                new EnumerableValueMapper(this),
                new AnonymousValueMapper(this)
            };
        }

        public virtual IDictionary<string, object> ToDictionary(Transmission transmission)
        {
            var data = new Dictionary<string, object>
            {
                ["substitution_data"] =
                    transmission.SubstitutionData != null && transmission.SubstitutionData.Keys.Any()
                        ? transmission.SubstitutionData
                        : null,
                ["recipients"] = transmission.ListId != null
                    ? (object) new Dictionary<string, object> {["list_id"] = transmission.ListId}
                    : transmission.Recipients.Select(ToDictionary)
            };

            var result = WithCommonConventions(transmission, data);

            CcHandling.SetAnyCCsInTheHeader(transmission, result);

            return result;
        }

        public virtual IDictionary<string, object> ToDictionary(Recipient recipient)
        {
            return WithCommonConventions(recipient, new Dictionary<string, object>()
            {
                ["type"] = null,
                ["substitution_data"] =
                    recipient.SubstitutionData != null && recipient.SubstitutionData.Keys.Any()
                        ? recipient.SubstitutionData
                        : null,
            });
        }

        public virtual IDictionary<string, object> ToDictionary(Suppression suppression)
        {
            return WithCommonConventions(suppression);
        }

        public virtual IDictionary<string, object> ToDictionary(Webhook webhook)
        {
            return WithCommonConventions(webhook);
        }

        public virtual IDictionary<string, object> ToDictionary(Address address)
        {
            return WithCommonConventions(address);
        }

        public virtual IDictionary<string, object> ToDictionary(Options options)
        {
            return AnyValuesWereSetOn(options) ? WithCommonConventions(options) : null;
        }

        public virtual IDictionary<string, object> ToDictionary(Content content)
        {
            return WithCommonConventions(content);
        }

        public virtual IDictionary<string, object> ToDictionary(Attachment attachment)
        {
            return ToDictionary(attachment as File);
        }

        public virtual IDictionary<string, object> ToDictionary(InlineImage inlineImage)
        {
            return ToDictionary(inlineImage as File);
        }

        public virtual IDictionary<string, object> ToDictionary(File file)
        {
            return WithCommonConventions(file);
        }

        public IDictionary<string, object> ToDictionary(Subaccount subaccount)
        {
            return WithCommonConventions(subaccount);
        }

        public IDictionary<string, object> ToDictionary(MessageEventsQuery query)
        {
            return WithCommonConventions(query, new Dictionary<string, object>()
            {
                ["events"] = string.Join(",", query.Events),
                ["campaign_ids"] = string.Join(",", query.CampaignIds),
                ["bounce_classes"] = string.Join(",", query.BounceClasses),
                ["campaign_ids"] = string.Join(",", query.CampaignIds),
                ["friendly_froms"] = string.Join(",", query.FriendlyFroms),
                ["message_ids"] = string.Join(",", query.MessageIds),
                ["recipients"] = string.Join(",", query.Recipients),
                ["subaccounts"] = string.Join(",", query.Subaccounts),
                ["template_ids"] = string.Join(",", query.TemplateIds),
                ["transmission_ids"] = string.Join(",", query.TransmissionIds)
            });
        }

        public IDictionary<string, object> CatchAll(object anything)
        {
            var converters = ToDictionaryMethods();
            if (converters.ContainsKey(anything.GetType()))
                return converters[anything.GetType()].Invoke(this, BindingFlags.Default, null,
                    new[] {anything}, CultureInfo.CurrentCulture) as IDictionary<string, object>;
            return WithCommonConventions(anything);
        }

        public IDictionary<Type, MethodInfo> ToDictionaryMethods()
        {
            return this.GetType().GetMethods()
                .Where(x => x.Name == "ToDictionary")
                .Where(x => x.GetParameters().Length == 1)
                .Select(x => new
                {
                    TheType = x.GetParameters().First().ParameterType,
                    TheMethod = x
                }).ToList()
                .ToDictionary(x => x.TheType, x => x.TheMethod);
        }

        private static bool AnyValuesWereSetOn(object target)
        {
            return target.GetType()
                .GetProperties()
                .Any(x => x.GetValue(target) != null);
        }

        private static IDictionary<string, object> RemoveNulls(IDictionary<string, object> dictionary)
        {
            var blanks = dictionary.Keys.Where(k => dictionary[k] == null).ToList();
            foreach (var key in blanks) dictionary.Remove(key);
            return dictionary;
        }

        private IDictionary<string, object> WithCommonConventions(object target, IDictionary<string, object> results = null)
        {
            if (results == null) results = new Dictionary<string, object>();
            foreach (var property in target.GetType().GetProperties())
            {
                var name = SnakeCase.Convert(property.Name);
                if (results.ContainsKey(name)) continue;

                results[name] = GetTheValue(property.PropertyType, property.GetValue(target));
            }
            return RemoveNulls(results);
        }

        public object GetTheValue(Type propertyType, object value)
        {
            var valueMapper = valueMappers.FirstOrDefault(x => x.CanMap(propertyType, value));
            return valueMapper == null ? value : valueMapper.Map(propertyType, value);
        }
    }
}