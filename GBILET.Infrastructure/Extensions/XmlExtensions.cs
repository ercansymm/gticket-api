using System;
using System.Globalization;
using System.Xml.Linq;

namespace GBILET.Infrastructure.Extensions;

public static class XmlExtensions
{
    public static string? GetValue(this XDocument doc, string elementName)
    {
        var el = doc.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == elementName);
        if (el == null) return null;
        var nilAttr = el.Attributes().FirstOrDefault(a => a.Name.LocalName == "nil");
        if (nilAttr != null && string.Equals(nilAttr.Value, "true", StringComparison.OrdinalIgnoreCase))
            return null;
        return el.Value;
    }

    public static string? GetValue(this XElement element, string elementName)
    {
        var el = element.Elements()
            .FirstOrDefault(x => x.Name.LocalName == elementName);
        if (el == null) return null;
        // WCF nil attribute: <c:Field i:nil="true"/> → treat as null
        var nilAttr = el.Attributes().FirstOrDefault(a => a.Name.LocalName == "nil");
        if (nilAttr != null && string.Equals(nilAttr.Value, "true", StringComparison.OrdinalIgnoreCase))
            return null;
        return el.Value;
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