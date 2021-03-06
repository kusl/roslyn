﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeGeneration
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGeneration
    Friend Class ConstructorGenerator
        Inherits AbstractVisualBasicCodeGenerator

        Private Shared Function LastConstructorOrField(Of TDeclaration As SyntaxNode)(members As SyntaxList(Of TDeclaration)) As TDeclaration
            Return If(LastConstructor(members), LastField(members))
        End Function

        Friend Shared Function AddConstructorTo(destination As TypeBlockSyntax,
                                                constructor As IMethodSymbol,
                                                options As CodeGenerationOptions,
                                                availableIndices As IList(Of Boolean)) As TypeBlockSyntax
            Dim constructorDeclaration = GenerateConstructorDeclaration(constructor, GetDestination(destination), options)

            Dim members = Insert(destination.Members, constructorDeclaration, options, availableIndices,
                                 after:=AddressOf LastConstructorOrField,
                                 before:=AddressOf FirstMember)

            Return FixTerminators(destination.WithMembers(members))
        End Function

        Friend Shared Function GenerateConstructorDeclaration(constructor As IMethodSymbol,
                                                              destination As CodeGenerationDestination,
                                                              options As CodeGenerationOptions) As StatementSyntax
            Dim reusableSyntax = GetReuseableSyntaxNodeForSymbol(Of StatementSyntax)(constructor, options)
            If reusableSyntax IsNot Nothing Then
                Return reusableSyntax
            End If

            Dim constructorStatement =
                SyntaxFactory.SubNewStatement() _
                .WithAttributeLists(AttributeGenerator.GenerateAttributeBlocks(constructor.GetAttributes(), options)) _
                .WithModifiers(GenerateModifiers(constructor, destination, options)) _
                .WithParameterList(ParameterGenerator.GenerateParameterList(constructor.Parameters, options))

            Dim hasNoBody = Not options.GenerateMethodBodies

            Dim declaration =
                If(hasNoBody,
                   DirectCast(constructorStatement, StatementSyntax),
                   SyntaxFactory.ConstructorBlock(
                      constructorStatement,
                      statements:=GenerateStatements(constructor),
                      end:=SyntaxFactory.EndSubStatement()))

            Return AddCleanupAnnotationsTo(
                ConditionallyAddDocumentationCommentTo(declaration, constructor, options))
        End Function

        Private Shared Function GenerateArgumentList(arguments As IList(Of SyntaxNode)) As ArgumentListSyntax
            Return SyntaxFactory.ArgumentList(
                arguments:=SyntaxFactory.SeparatedList(arguments.Select(AddressOf ArgumentGenerator.GenerateArgument)))
        End Function

        Private Shared Function GenerateStatements(constructor As IMethodSymbol) As SyntaxList(Of StatementSyntax)
            If CodeGenerationConstructorInfo.GetStatements(constructor) Is Nothing AndAlso
               CodeGenerationConstructorInfo.GetBaseConstructorArgumentsOpt(constructor) Is Nothing AndAlso
               CodeGenerationConstructorInfo.GetThisConstructorArgumentsOpt(constructor) Is Nothing Then
                Return Nothing
            End If

            Dim statements = New List(Of StatementSyntax)
            If CodeGenerationConstructorInfo.GetBaseConstructorArgumentsOpt(constructor) IsNot Nothing Then
                statements.Add(CreateBaseConstructorCall(constructor))
            End If

            If CodeGenerationConstructorInfo.GetThisConstructorArgumentsOpt(constructor) IsNot Nothing Then
                statements.Add(CreateThisConstructorCall(CodeGenerationConstructorInfo.GetThisConstructorArgumentsOpt(constructor)))
            End If

            If CodeGenerationConstructorInfo.GetStatements(constructor) IsNot Nothing Then
                statements.AddRange(StatementGenerator.GenerateStatements(
                    CodeGenerationConstructorInfo.GetStatements(constructor)))
            End If

            Return SyntaxFactory.List(statements)
        End Function

        Private Shared Function GenerateModifiers(constructor As IMethodSymbol, destination As CodeGenerationDestination, options As CodeGenerationOptions) As SyntaxTokenList
            Dim tokens = New List(Of SyntaxToken)()
            If constructor.IsStatic Then
                tokens.Add(SyntaxFactory.Token(SyntaxKind.SharedKeyword))
            Else
                AddAccessibilityModifiers(constructor.DeclaredAccessibility, tokens, destination, options, Accessibility.Public)
            End If

            Return SyntaxFactory.TokenList(tokens)
        End Function

        Private Shared Function CreateBaseConstructorCall(constructor As IMethodSymbol) As StatementSyntax
            Return SyntaxFactory.ExpressionStatement(
                expression:=SyntaxFactory.InvocationExpression(
                    SyntaxFactory.SimpleMemberAccessExpression(
                        expression:=SyntaxFactory.MyBaseExpression(),
                        operatorToken:=SyntaxFactory.Token(SyntaxKind.DotToken),
                        name:=SyntaxFactory.IdentifierName("New")),
                argumentList:=SyntaxFactory.ArgumentList(
                    arguments:=SyntaxFactory.SeparatedList(constructor.Parameters.Select(Function(p) SyntaxFactory.SimpleArgument(SyntaxFactory.IdentifierName(p.Name))).OfType(Of ArgumentSyntax)))))
        End Function

        Private Shared Function CreateThisConstructorCall(arguments As IList(Of SyntaxNode)) As StatementSyntax
            Return SyntaxFactory.ExpressionStatement(
                expression:=SyntaxFactory.InvocationExpression(
                    SyntaxFactory.SimpleMemberAccessExpression(
                        expression:=SyntaxFactory.MeExpression(),
                        operatorToken:=SyntaxFactory.Token(SyntaxKind.DotToken),
                        name:=SyntaxFactory.IdentifierName("New")),
                argumentList:=ArgumentGenerator.GenerateArgumentList(arguments)))
        End Function
    End Class
End Namespace