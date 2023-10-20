namespace FlowMaker;


[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class InputAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string displayName;

    // This is a positional argument
    public InputAttribute(string displayname)
    {
        this.displayName = displayname;
    }

    public string DisplayName
    {
        get { return displayName; }
    }
}

[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class OutputAttribute : Attribute
{
    readonly string displayName;

    public OutputAttribute(string displayname)
    {
        this.displayName = displayname;

    }

    public string DisplayName
    {
        get { return displayName; }
    }
}


[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public sealed class OptionAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string displayName;
    readonly string name;

    // This is a positional argument
    public OptionAttribute(string displayname, string name)
    {
        this.displayName = displayname;
        this.name = name;
    }

    public string DisplayName
    {
        get { return displayName; }
    }
    public string Name
    {
        get { return name; }
    }
}

[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class DefaultValueAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string value;

    // This is a positional argument
    public DefaultValueAttribute(string value)
    {
        this.value = value;
    }

    public string Value
    {
        get { return value; }
    }
}


[System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ParameterValueAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly ParameterType value;

    // This is a positional argument
    public ParameterValueAttribute(ParameterType value)
    {
        this.value = value;
    }

    public ParameterType Value
    {
        get { return value; }
    }
}

public enum ParameterType
{
    String,
    Int
}