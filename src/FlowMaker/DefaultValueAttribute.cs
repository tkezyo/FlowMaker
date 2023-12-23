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
public sealed class OptionAttribute(string displayName, string value) : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    readonly string displayName = displayName;
    readonly string value = value;

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
public sealed class OptionProviderAttribute<T> : Attribute
    where T : IOptionProvider
{

}
