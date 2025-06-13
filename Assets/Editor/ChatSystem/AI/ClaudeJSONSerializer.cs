using UnityEngine;

public class ClaudeContentBlockContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
{
    protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(System.Reflection.MemberInfo member, Newtonsoft.Json.MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        
        if (property.DeclaringType == typeof(ClaudeContentBlock))
        {
            property.ShouldSerialize = instance =>
            {
                var value = property.ValueProvider.GetValue(instance);
                
                if (value == null) return false;
                if (value is string str && string.IsNullOrEmpty(str)) return false;
                
                if (property.PropertyName == "type") return true;
                if (property.PropertyName == "text" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "id" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "name" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "input" && value != null) return true;
                if (property.PropertyName == "tool_use_id" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "content" && !string.IsNullOrEmpty(value as string)) return true;
                
                return false;
            };
        }
        
        if (property.DeclaringType == typeof(ClaudeTool))
        {
            property.ShouldSerialize = instance =>
            {
                var tool = instance as ClaudeTool;
                var value = property.ValueProvider.GetValue(instance);
                
                if (!string.IsNullOrEmpty(tool?.type))
                {
                    return property.PropertyName == "type" || property.PropertyName == "name";
                }
                
                if (property.PropertyName == "name") return true;
                if (property.PropertyName == "description" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "input_schema" && value != null) return true;
                
                return false;
            };
        }
        
        return property;
    }
} 