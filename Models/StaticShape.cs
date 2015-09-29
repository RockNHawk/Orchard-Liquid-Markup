﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Web;
using DotLiquid;
using Microsoft.CSharp.RuntimeBinder;
using Orchard.DisplayManagement.Shapes;
using Orchard.Localization;
using Orchard.Validation;

namespace Lombiq.LiquidMarkup.Models
{
    // Similar in idea to the StaticShape class in OrchardHUN.Scripting.Php
    public class StaticShape : IIndexable, ILiquidizable
    {
        private readonly dynamic _shape;
        public dynamic Shape { get { return _shape; } }
        public ShapeMetadata Metadata { get { return _shape.Metadata; } }
        public string Id { get { return _shape.Id; } }
        public IList<string> Classes { get { return _shape.Classes; } }
        public IDictionary<string, string> Attributes { get { return _shape.Attributes; } }
        private readonly Lazy<IEnumerable<dynamic>> _itemsLazy;
        public IEnumerable<dynamic> Items { get { return _itemsLazy.Value; } }


        public StaticShape(dynamic shape)
        {
            Argument.ThrowIfNull(shape, "shape");

            _shape = shape;

            _itemsLazy = new Lazy<IEnumerable<dynamic>>(() =>
            {
                var items = new List<StaticShape>();
                foreach (var item in _shape.Items)
                {
                    items.Add(new StaticShape(item));
                }
                return items;
            });
        }


        public bool ContainsKey(object key)
        {
            return true;
        }

        public object this[object key]
        {
            get
            {
                var keyString = key.ToString();

                // Is key referring to a property on this class?
                if (typeof(StaticShape).GetProperties().Any(property => property.Name == keyString))
                {
                    return typeof(StaticShape).GetProperty(keyString).GetValue(this, null);
                }

                dynamic item = null;
                if (!(_shape is Shape))
                {
                    Type shapeType = _shape.GetType();

                    // Is key referring to a property on _shape?
                    if (shapeType.GetProperties().Any(property => property.Name == keyString))
                    {
                        item = shapeType.GetProperty(keyString).GetValue(_shape, null);
                    }
                    else
                    {
                        // Does _shape has an indexer for key?
                        var indexer = shapeType.GetProperties()
                            .Where(p => p.GetIndexParameters().Length != 0)
                            .FirstOrDefault();

                        if (indexer != null)
                        {
                            object[] indexArgs = { key };
                            item = indexer.GetValue(_shape, indexArgs);
                        }
                        else
                        {
                            // Is this a dynamic object with a dynamic property (like with Model.ContentItem.TitlePart.Title)?
                            var dynamicMetaObjectProvider = _shape as IDynamicMetaObjectProvider;
                            if (dynamicMetaObjectProvider != null)
                            {
                                var objectParameter = Expression.Parameter(typeof(object));
                                var metaObject = dynamicMetaObjectProvider.GetMetaObject(objectParameter);
                                var binder = (GetMemberBinder)Microsoft.CSharp.RuntimeBinder.Binder.GetMember(0, keyString, shapeType, new CSharpArgumentInfo[] { CSharpArgumentInfo.Create(0, null) });
                                var getMemberBinding = metaObject.BindGetMember(binder);
                                var finalExpression = Expression.Block(Expression.Label(CallSiteBinder.UpdateLabel), getMemberBinding.Expression);
                                var lambda = Expression.Lambda(finalExpression, objectParameter);
                                var compiledDelegate = lambda.Compile();
                                item = compiledDelegate.DynamicInvoke(_shape);
                            }
                        }
                    }
                }
                else
                {
                    item = _shape[key];
                }

                if (item == null) return null;

                if (item is bool) return item;

                if (item.GetType().IsPrimitive || item is decimal || item is string || item is DateTime || item is LocalizedString)
                {
                    return item.ToString();
                }

                return new StaticShape(item);
            }
        }

        public object ToLiquid()
        {
            return this;
        }
    }
}