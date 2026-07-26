using System.Xml.Linq;

namespace Honey.Desktop.Tests;

internal static class JadeThemeXmlAssertions
{
    internal const string ThemeSource =
        "/Honey.Desktop;component/Assets/JadeControlTheme.xaml";
    internal static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    internal static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    internal static XDocument LoadTheme()
        => LoadFixture("JadeControlTheme.xaml");

    internal static XElement GetImplicitStyle(string targetType)
    {
        var style = LoadTheme()
            .Root?
            .Elements(Presentation + "Style")
            .SingleOrDefault(element =>
                element.Attribute(Xaml + "Key") is null &&
                string.Equals(
                    (string?)element.Attribute("TargetType"),
                    $"{{x:Type {targetType}}}",
                    StringComparison.Ordinal));

        return Assert.IsType<XElement>(style);
    }

    internal static XElement GetNamedStyle(string key)
    {
        var style = LoadTheme()
            .Root?
            .Elements(Presentation + "Style")
            .SingleOrDefault(element => string.Equals(
                (string?)element.Attribute(Xaml + "Key"),
                key,
                StringComparison.Ordinal));

        return Assert.IsType<XElement>(style);
    }

    internal static XElement GetControlTemplate(XElement style)
    {
        var template = style
            .Elements(Presentation + "Setter")
            .SingleOrDefault(setter => string.Equals(
                (string?)setter.Attribute("Property"),
                "Template",
                StringComparison.Ordinal))?
            .Element(Presentation + "Setter.Value")?
            .Element(Presentation + "ControlTemplate");

        return Assert.IsType<XElement>(template);
    }

    internal static XElement GetNamedElement(
        XElement template,
        string partName,
        string elementName)
    {
        var part = template
            .Descendants(Presentation + elementName)
            .SingleOrDefault(element => string.Equals(
                (string?)element.Attribute(Xaml + "Name"),
                partName,
                StringComparison.Ordinal));

        return Assert.IsType<XElement>(part);
    }

    internal static IEnumerable<XElement> GetTemplateTriggers(XElement template)
    {
        var triggers = template.Element(Presentation + "ControlTemplate.Triggers");

        return Assert
            .IsType<XElement>(triggers)
            .Elements(Presentation + "Trigger");
    }

    internal static XElement GetTemplateTrigger(
        XElement template,
        string property,
        string value)
    {
        var trigger = GetTemplateTriggers(template).SingleOrDefault(element =>
            string.Equals(
                (string?)element.Attribute("Property"),
                property,
                StringComparison.Ordinal) &&
            string.Equals(
                (string?)element.Attribute("Value"),
                value,
                StringComparison.Ordinal));

        return Assert.IsType<XElement>(trigger);
    }

    internal static void AssertStyleSetter(
        XElement style,
        string property,
        string value) =>
        AssertSetter(style, null, property, value);

    internal static void AssertEffectiveValue(
        XElement element,
        XElement style,
        string property,
        string value)
    {
        var localValue = (string?)element.Attribute(property);
        if (localValue is not null)
        {
            Assert.Equal(value, localValue);
            return;
        }

        AssertStyleSetter(style, property, value);
    }

    internal static void AssertSetter(
        XElement owner,
        string? targetName,
        string property,
        string value)
    {
        var setter = owner.Elements(Presentation + "Setter").SingleOrDefault(element =>
            string.Equals(
                (string?)element.Attribute("TargetName"),
                targetName,
                StringComparison.Ordinal) &&
            string.Equals(
                (string?)element.Attribute("Property"),
                property,
                StringComparison.Ordinal));

        Assert.Equal(value, GetAttribute(Assert.IsType<XElement>(setter), "Value"));
    }

    internal static void AssertTemplateBinding(
        XElement element,
        string attributeName,
        string property) =>
        Assert.Equal(
            $"{{TemplateBinding {property}}}",
            GetAttribute(element, attributeName));

    internal static void AssertBinding(
        XElement element,
        string attributeName,
        string? expectedPath,
        params (string Name, string Value)[] expectedOptions)
    {
        var binding = ParseBinding(GetAttribute(element, attributeName));

        Assert.Equal(expectedPath, binding.Path);
        Assert.Equal(expectedOptions.Length, binding.Options.Count);
        Assert.All(
            expectedOptions,
            expected =>
            {
                Assert.True(
                    binding.Options.TryGetValue(expected.Name, out var actual),
                    $"绑定缺少选项 {expected.Name}。");
                Assert.Equal(expected.Value, actual);
            });
    }

    internal static string GetAttribute(
        XElement element,
        string attributeName)
    {
        var attribute = element.Attribute(attributeName);
        return Assert.IsType<XAttribute>(attribute).Value;
    }

    internal static XDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            fileName);
        return XDocument.Load(path, LoadOptions.PreserveWhitespace);
    }

    internal static bool IsJadeThemeSource(string? source) =>
        string.Equals(
            source?
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(),
            "JadeControlTheme.xaml",
            StringComparison.Ordinal);

    private static ParsedBinding ParseBinding(string markup)
    {
        Assert.StartsWith("{Binding", markup, StringComparison.Ordinal);
        Assert.EndsWith("}", markup, StringComparison.Ordinal);

        var body = markup["{Binding".Length..^1].Trim();
        var parts = SplitMarkupArguments(body);
        string? path = null;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
            {
                Assert.Null(path);
                path = part;
                continue;
            }

            var name = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            Assert.True(options.TryAdd(name, value), $"绑定选项 {name} 重复。");
        }

        return new ParsedBinding(
            string.IsNullOrEmpty(path) ? null : path,
            options);
    }

    private static IReadOnlyList<string> SplitMarkupArguments(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < body.Length; index++)
        {
            switch (body[index])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(body[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }

        Assert.Equal(0, depth);
        parts.Add(body[start..].Trim());
        return parts;
    }

    private sealed record ParsedBinding(
        string? Path,
        IReadOnlyDictionary<string, string> Options);
}
