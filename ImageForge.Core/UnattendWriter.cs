using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ImageForge.Core;

public static class UnattendWriter
{
    public static string Generate(string computerName, string locale, string timeZone)
    {
        if (computerName is null || !Regex.IsMatch(computerName, @"^(?![0-9]+$)[A-Za-z0-9][A-Za-z0-9-]{0,14}$"))
            throw new ArgumentException("Computer name must be 1–15 letters, digits or hyphens, and cannot be all digits.");
        if (locale is null || !Regex.IsMatch(locale, @"^[a-zA-Z]{2,3}-[a-zA-Z]{2,4}$"))
            throw new ArgumentException("Use a Windows locale such as en-US.");
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 128 || timeZone.Any(char.IsControl))
            throw new ArgumentException("Enter a Windows time zone ID such as UTC or Pacific Standard Time.");
        XNamespace ns = "urn:schemas-microsoft-com:unattend";
        XElement Component(string name, params XElement[] children) => new(ns + "component",
            new XAttribute("name", name), new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"), new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS"), children);
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "unattend",
                new XElement(ns + "settings", new XAttribute("pass", "specialize"),
                    Component("Microsoft-Windows-Shell-Setup", new(ns + "ComputerName", computerName), new(ns + "TimeZone", timeZone))),
                new XElement(ns + "settings", new XAttribute("pass", "oobeSystem"),
                    Component("Microsoft-Windows-International-Core", new(ns + "InputLocale", locale), new(ns + "SystemLocale", locale),
                        new(ns + "UILanguage", locale), new(ns + "UserLocale", locale)))));
        return document.Declaration + Environment.NewLine + document;
    }
}
