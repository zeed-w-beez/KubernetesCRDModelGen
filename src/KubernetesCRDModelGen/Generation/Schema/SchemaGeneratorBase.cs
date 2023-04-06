﻿using System;
using KubernetesCRDModelGen.Names;
using KubernetesCRDModelGen.Spec;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace KubernetesCRDModelGen.Generation.Schema
{
    public abstract class SchemaGeneratorBase : TypeGeneratorBase<OpenApiSchema>
    {
        protected OpenApiSchema Schema => Element.Element;

        protected abstract NameKind NameKind { get; }

        protected SchemaGeneratorBase(ILocatedOpenApiElement<OpenApiSchema> schemaElement, GenerationContext context,
            ITypeGenerator? parent)
            : base(schemaElement, context, parent)
        {
        }

        protected override YardarmTypeInfo GetTypeInfo()
        {
            if (Schema.Reference != null)
            {
                NameSyntax ns = Context.NamespaceProvider.GetNamespace(Element);

                //var formatter = Context.NameFormatterSelector.GetFormatter(NameKind);

                var className = CapitalizeFirstLetter((Schema.Properties["KubeApiVersion"].Default as OpenApiString).Value) + (Schema.Properties["KubeKind"].Default as OpenApiString).Value;

                return new YardarmTypeInfo(
                    QualifiedName(ns,
                        IdentifierName(className)),
                    NameKind);
            }

            if (Parent == null)
            {
                throw new InvalidOperationException(
                    $"Unable to generate schema for '{Element.Key}', it has no parent is not a component.");
            }

            QualifiedNameSyntax? name = Parent.GetChildName(Element, NameKind);
            if (name == null)
            {
                throw new InvalidOperationException($"Unable to generate schema for '{Element.Key}', parent did not provide a name.");
            }

            return new YardarmTypeInfo(name, NameKind);
        }

        public override QualifiedNameSyntax? GetChildName<TChild>(ILocatedOpenApiElement<TChild> child, NameKind nameKind) =>
            QualifiedName((NameSyntax)TypeInfo.Name, IdentifierName(
                Context.NameFormatterSelector.GetFormatter(nameKind).Format(child.Key + "-Model")));

        public string CapitalizeFirstLetter(string str)
        {
            if (str.Length == 0)
            {
                return string.Empty;
            }
            else if (str.Length == 1)
            {
                return char.ToUpper(str[0]).ToString();
            }
            else
            {
                return char.ToUpper(str[0]) + str.Substring(1);
            }
        }
    }
}
