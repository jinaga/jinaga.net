namespace Jinaga.Azure.Functions;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class JinagaTriggerAttribute : Attribute
{
    public string ConfigurationKey { get; set; }
}