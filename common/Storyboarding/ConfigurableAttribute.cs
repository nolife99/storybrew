﻿namespace StorybrewCommon.Storyboarding;

using System;

///<summary> Configurable attribute for variables. </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ConfigurableAttribute(string name = "") : Attribute
{
    ///<summary> Name of the configurable object, displayed in effect configuration list. </summary>
    public string DisplayName => name;
}

///<summary> Represents a description attribute that can be displayed on configurable variables. </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class DescriptionAttribute(string content) : Attribute
{
    ///<summary> Represents the content of the description attribute. </summary>
    public string Content => content;
}

///<summary> Represents a grouping mechanism for configurable variables. </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class GroupAttribute(string name) : Attribute
{
    ///<summary> The name of the group. </summary>
    public string Name => name;
}