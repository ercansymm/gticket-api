using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GBILET.Infrastructure.Extensions;

public static class XmlExtensions
{
    public static string GetValue(this XDocument doc, string elementName)
    {
        return doc.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == elementName)?
            .Value;
    }
}