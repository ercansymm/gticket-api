using System;
using System.Globalization;
using System.Xml.Linq;

namespace GBILET.Infrastructure.Extensions;

public static class XmlExtensions
{
    public static string? GetValue(this XDocument doc, string elementName)
    {
        return doc.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == elementName)?
            .Value;
    }

    public static string? GetValue(this XElement element, string elementName)
    {
        return element.Elements()
            .FirstOrDefault(x => x.Name.LocalName == elementName)?
            .Value;
    }

    public static IEnumerable<XElement> GetElements(this XElement element, string elementName)
    {
        return element.Elements()
            .Where(x => x.Name.LocalName == elementName);
    }

    public static IEnumerable<XElement> GetDescendants(this XElement element, string elementName)
    {
        return element.Descendants()
            .Where(x => x.Name.LocalName == elementName);
    }

    public static IEnumerable<XElement> GetDescendants(this XDocument doc, string elementName)
    {
        return doc.Descendants()
            .Where(x => x.Name.LocalName == elementName);
    }

    public static XElement? GetElement(this XElement element, string elementName)
    {
        return element.Elements()
            .FirstOrDefault(x => x.Name.LocalName == elementName);
    }

    public static decimal GetDecimalValue(this XElement element, string elementName)
    {
        var value = element.GetValue(elementName);
        return decimal.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    public static int GetIntValue(this XElement element, string elementName)
    {
        var value = element.GetValue(elementName);
        return int.TryParse(value, out var result) ? result : 0;
    }

    public static bool GetBoolValue(this XElement element, string elementName)
    {
        var value = element.GetValue(elementName);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}