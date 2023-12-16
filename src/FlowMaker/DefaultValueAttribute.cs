namespace FlowMaker;


[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class InputAttribute : Attribute
{
}


[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class OutputAttribute : Attribute
{
}


[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public sealed class OptionAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string displayName;
    readonly string value;
    readonly string? providerName;

    // This is a positional argument
    public OptionAttribute(string displayName, string value)
    {
        this.displayName = displayName;
        this.value = value;
    }
    public OptionAttribute(string providerName)
    {
        this.providerName = providerName;
        this.displayName = string.Empty;
        this.value = string.Empty;
    }

    public string? ProviderName
    {
        get { return providerName; }
    }
    public string DisplayName
    {
        get { return displayName; }
    }
    public string Value
    {
        get { return value; }
    }
}
