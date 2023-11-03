namespace FlowMaker;



[System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FlowConverterAttribute<T> : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string category;
    readonly string displayName;

    // This is a positional argument
    public FlowConverterAttribute(string category, string displayName)
    {
        this.category = category;
        this.displayName = displayName;
    }

    public string Category
    {
        get { return category; }
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
    readonly string category;
    readonly string displayName;

    // This is a positional argument
    public FlowStepAttribute(string category, string displayName)
    {
        this.category = category;
        this.displayName = displayName;
    }

    public string Category
    {
        get { return category; }
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
