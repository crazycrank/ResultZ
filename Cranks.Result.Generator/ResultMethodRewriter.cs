﻿using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Cranks.Result.Generator
{
    internal class ResultMethodRewriter : CSharpSyntaxRewriter
    {
        private TypeSyntax? _resultType;
        private ParameterListSyntax? _parameters;

        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
        {
            return base.VisitCompilationUnit(node.WithLeadingTrivia(Comment($"// <auto-generated />{Environment.NewLine}")));
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var membersToRemove = node.Members.Where(member => member is MethodDeclarationSyntax method
                                                               && (!method.Modifiers.Any(t => t is { Text: "public" }) || !method.Modifiers.Any(t => t is { Text: "static" })));

            var modifiedNode = node.RemoveNodes(membersToRemove, SyntaxRemoveOptions.KeepNoTrivia)
                                   !.WithIdentifier(Identifier("Result"));

            return base.VisitClassDeclaration(modifiedNode)?.NormalizeWhitespace();
        }

        public override SyntaxNode VisitParameterList(ParameterListSyntax node)
        {
            _resultType = node.Parameters[0].Type!;

            var newNode = ParameterList(node.Parameters.RemoveAt(0));
            _parameters = newNode;

            return node.ReplaceNode(node, newNode);
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            VisitParameterList(node.ParameterList);

            if (node.Body is not null)
            {
                node = node.RemoveNode(node.Body, SyntaxRemoveOptions.KeepExteriorTrivia)!;
            }

            if (node.ExpressionBody is not null)
            {
                node = node.RemoveNode(node.ExpressionBody, SyntaxRemoveOptions.KeepExteriorTrivia)!;
            }

            // remove documentation of "this" parameter
            var commentNode = node.GetLeadingTrivia()
                                  .SingleOrDefault(t => t is { RawKind: (int)SyntaxKind.SingleLineCommentTrivia } comment
                                                        && comment.ToString().Contains("<param name=\"result\">"));
            if (commentNode != default)
            {
                var index = node.GetLeadingTrivia().IndexOf(commentNode) - 1;
                node = node.WithLeadingTrivia(node.GetLeadingTrivia().RemoveAt(index).RemoveAt(index).RemoveAt(index)); // remove whitespace before, comment and newline after
            }

            // new Passed() or new Passed<TValue>()
            var objectCreationExpression = ObjectCreationExpression(ParseTypeName(_resultType is GenericNameSyntax
                                                                                            ? "Passed<TValue>"
                                                                                            : "Passed"),
                                                                    ArgumentList(),
                                                                    null).NormalizeWhitespace();

            // the method name, eg WithError or WithError<TError>
            SimpleNameSyntax methodNameSyntax = node.TypeParameterList is null
                                                    ? IdentifierName(node.Identifier)
                                                    : GenericName(node.Identifier,
                                                                  TypeArgumentList(default(SeparatedSyntaxList<TypeSyntax>)
                                                                                       .AddRange(node.TypeParameterList.Parameters.Select(p => IdentifierName(p.Identifier.Text)))));

            // new Passed<TValue>().WithError
            var memberAccessExpression = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                                objectCreationExpression,
                                                                methodNameSyntax);

            // new Passed<TValue>().WithError(error)
            var invocationExpression = InvocationExpression(memberAccessExpression, ArgumentList(default(SeparatedSyntaxList<ArgumentSyntax>).AddRange(_parameters!.Parameters.Select(p => Argument(IdentifierName(p.Identifier))))));

            // => new Passed<TValue>().WithError(error)
            var arrowExpression = ArrowExpressionClause(invocationExpression);

            return base.VisitMethodDeclaration(node.WithExpressionBody(arrowExpression));
        }
    }
}