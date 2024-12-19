#if !HAS_NULLABLE_REF_TYPES
using System;

/// <summary>
/// These attributes replicate the ones from System.Diagnostics.CodeAnalysis, and are here so we can still compile against the older frameworks.
/// </summary>

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true)]
    public sealed class NotNullIfNotNullAttribute : Attribute
    {
        public NotNullIfNotNullAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class MaybeNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class NotNullAttribute : Attribute
    {
    }

    /// <summary>Specifies that when a method returns <see cref="ReturnValue" />, the parameter will not be null even if the corresponding type allows it.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the specified return value condition.</summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated parameter will not be null.
        /// </param>
        public NotNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }

        /// <summary>Gets the return value condition.</summary>
        public bool ReturnValue { get; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    public sealed class MemberNotNullAttribute : Attribute
    {
        /// <summary>Initializes the attribute with a field or property member.</summary>
        /// <param name="member">
        /// The field or property member that is promised to be not-null.
        /// </param>
        public MemberNotNullAttribute(string member) => Members = new[] { member };

        /// <summary>Initializes the attribute with the list of field and property members.</summary>
        /// <param name="members">
        /// The list of field and property members that are promised to be not-null.
        /// </param>
        public MemberNotNullAttribute(params string[] members) => Members = members;

        /// <summary>Gets field or property member names.</summary>
        public string[] Members { get; }
    }
    
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class MemberNotNullWhenAttribute : Attribute
    {
        public MemberNotNullWhenAttribute(bool returnValue, params string[] members) { }
        public MemberNotNullWhenAttribute(bool returnValue, string member) { }
    }
}
#endif