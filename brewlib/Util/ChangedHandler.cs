namespace BrewLib.Util;

public class ChangedEventArgs(string propertyName)
{
    public string PropertyName => propertyName;
}