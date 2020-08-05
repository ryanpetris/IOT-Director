using System;
using System.Linq;
using System.Text.Json;

namespace IotDirector.Extensions
{
    public static class JsonElementExtensions
    {
        public static T GetEnum<T>(this JsonElement element) where T : Enum
        {
            var raw = element.GetString();
            var enumDict = Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(k => k.ToString(), v => v);
            
            if (!enumDict.ContainsKey(raw))
                throw new Exception($"Invalid enum value for type {typeof(T).Name}: {raw}");

            return enumDict[raw];
        }

        public static bool GetBoolean(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var propertyElement))
                return propertyElement.GetBoolean();

            return default;
        }

        public static T GetEnum<T>(this JsonElement element, string propertyName) where T : Enum
        {
            if (element.TryGetProperty(propertyName, out var propertyElement))
                return propertyElement.GetEnum<T>();

            return default;
        }

        public static int GetInt(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var propertyElement))
                return propertyElement.GetInt32();

            return default;
        }

        public static string GetString(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var propertyElement))
                return propertyElement.GetString();

            return null;
        }
    }
}