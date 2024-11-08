namespace BrewLib.UserInterface;

using System;

public interface Field
{
    object FieldValue { get; set; }
    event EventHandler OnValueChanged, OnDisposed;
}