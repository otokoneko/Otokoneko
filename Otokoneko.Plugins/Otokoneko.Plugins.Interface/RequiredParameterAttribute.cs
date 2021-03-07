using System;
using System.Runtime.CompilerServices;

namespace Otokoneko.Plugins.Interface
{
    [AttributeUsage(AttributeTargets.Property)]
    public class RequiredParameterAttribute: Attribute
    {
        public Type Type { get; }
        public object DefaultValue { get; }
        public string Name { get; }
        public string Alias { get; }
        public string Comment { get; }
        public bool IsReadOnly { get; }

        public RequiredParameterAttribute(Type type, object defaultValue, [CallerMemberName]string name=null, bool isReadOnly=false, string alias=null, string comment=null)
        {
            Type = type;
            DefaultValue = defaultValue;
            Name = name;
            Alias = alias;
            Comment = comment;
            IsReadOnly = isReadOnly;
        }
    }
}
