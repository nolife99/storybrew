namespace BrewLib.Util;

public class ChangedEventArgs(string propertyName)
{
    public static readonly ChangedEventArgs All = new(null);
    public readonly string PropertyName = propertyName;
}