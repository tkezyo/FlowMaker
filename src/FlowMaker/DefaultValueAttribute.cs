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

    // This is a positional argument
    public OptionAttribute(string displayname, string value)
    {
        this.displayName = displayname;
        this.value = value;
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
