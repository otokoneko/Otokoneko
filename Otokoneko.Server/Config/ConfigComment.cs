﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;
using YamlDotNet.Serialization.TypeInspectors;

namespace Otokoneko.Server.Config
{
	public class CommentGatheringTypeInspector : TypeInspectorSkeleton
	{
		private readonly ITypeInspector _innerTypeDescriptor;

		public CommentGatheringTypeInspector(ITypeInspector innerTypeDescriptor)
		{
			_innerTypeDescriptor = innerTypeDescriptor;
		}

		public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
		{
			return _innerTypeDescriptor
				.GetProperties(type, container)
				.Select(d => new CommentsPropertyDescriptor(d));
		}

		private sealed class CommentsPropertyDescriptor : IPropertyDescriptor
		{
			private readonly IPropertyDescriptor _baseDescriptor;

			public CommentsPropertyDescriptor(IPropertyDescriptor baseDescriptor)
			{
				_baseDescriptor = baseDescriptor;
				Name = baseDescriptor.Name;
			}

			public string Name { get; }

			public Type Type => _baseDescriptor.Type;

            public Type TypeOverride
			{
				get => _baseDescriptor.TypeOverride;
                set => _baseDescriptor.TypeOverride = value;
            }

			public int Order { get; set; }

			public ScalarStyle ScalarStyle
			{
				get => _baseDescriptor.ScalarStyle;
                set => _baseDescriptor.ScalarStyle = value;
            }

			public bool CanWrite => _baseDescriptor.CanWrite;

            public void Write(object target, object value)
			{
				_baseDescriptor.Write(target, value);
			}

			public T GetCustomAttribute<T>() where T : Attribute
			{
				return _baseDescriptor.GetCustomAttribute<T>();
			}

			public IObjectDescriptor Read(object target)
			{
				var description = _baseDescriptor.GetCustomAttribute<DescriptionAttribute>();
                return description != null
                    ? new CommentsObjectDescriptor(_baseDescriptor.Read(target), description.Description)
                    : _baseDescriptor.Read(target);
			}
		}
	}

	public sealed class CommentsObjectDescriptor : IObjectDescriptor
	{
		private readonly IObjectDescriptor _innerDescriptor;

		public CommentsObjectDescriptor(IObjectDescriptor innerDescriptor, string comment)
		{
			_innerDescriptor = innerDescriptor;
			Comment = comment;
		}

		public string Comment { get; }

		public object Value => _innerDescriptor.Value;
        public Type Type => _innerDescriptor.Type;
        public Type StaticType => _innerDescriptor.StaticType;
        public ScalarStyle ScalarStyle => _innerDescriptor.ScalarStyle;
    }

	public class CommentsObjectGraphVisitor : ChainedObjectGraphVisitor
	{
		public CommentsObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
			: base(nextVisitor)
		{
		}

		public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
		{
            if (value is CommentsObjectDescriptor commentsDescriptor && commentsDescriptor.Comment != null)
			{
				context.Emit(new Comment(commentsDescriptor.Comment, false));
			}

			return base.EnterMapping(key, value, context);
		}
	}
}
