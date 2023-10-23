namespace FlowMaker;



[System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FlowConverterAttribute<T> : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string group;
    readonly string displayName;

    // This is a positional argument
    public FlowConverterAttribute(string group, string displayName)
    {
        this.group = group;
        this.displayName = displayName;
    }

    public string Group
    {
        get { return group; }
    }
    public string DisplayName
    {
        get { return displayName; }
    }

}


[System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FlowStepAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string group;
    readonly string displayName;

    // This is a positional argument
    public FlowStepAttribute(string group, string displayName)
    {
        this.group = group;
        this.displayName = displayName;
    }

    public string Group
    {
        get { return group; }
    }
    public string DisplayName
    {
        get { return displayName; }
    }

}

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
