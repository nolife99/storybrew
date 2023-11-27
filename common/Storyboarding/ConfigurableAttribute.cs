using System;

namespace StorybrewCommon.Storyboarding
{
    ///<summary> Configurable attribute for variables. </summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class ConfigurableAttribute : Attribute
    {
        ///<summary> Name of the configurable object, displayed in effect configuration list. </summary>
        public string DisplayName { get; set; }
    }

    ///<summary> Represents a description attribute that can be displayed on configurable variables. </summary>
    ///<remarks> Creates a new description attribute that applies to a variable with given description/content. </remarks>
    [AttributeUsage(AttributeTargets.Field)] public sealed class DescriptionAttribute(string content) : Attribute
    {
        ///<summary> Represents the content of the description attribute. </summary>
        public string Content => content;
    }

    ///<summary> Represents a grouping mechanism for configurable variables. </summary>
    ///<remarks> Creates a new group of configurable variables (below this attribute) with given display name. </remarks>
    [AttributeUsage(AttributeTargets.Field)] public sealed class GroupAttribute(string name) : Attribute
    {
        ///<summary> The name of the group. </summary>
        public string Name => name;
    }
}