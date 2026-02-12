using System.ComponentModel;
using System.Reflection;

namespace PredictionLeague.Web.Client.Utilities;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var memberInfo = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        if (memberInfo == null)
            return value.ToString();
        
        var descriptionAttribute = memberInfo.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description ?? value.ToString();
    }
}