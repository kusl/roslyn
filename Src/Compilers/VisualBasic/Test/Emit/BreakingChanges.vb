﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests
    Public Class BreakingChanges
        Inherits BasicTestBase

        ' In Dev10 (and earlier), this didn't generate an error.
        <Fact, WorkItem(529599)>
        Public Sub ParsePreprocessorEndIfInMethodBody()
            ParseAndVerify(<![CDATA[
                Module Module1
                    Sub Main()
                        #If True
                        #Endif    
                    End Sub
                End Module
            ]]>,
            <errors>
                <error id="30826"/>
            </errors>)
        End Sub

        ' This isn't strictly a breaking change since this used to be an error and now isn't.
        ' I am marking it because it is a language change from Dev10.
        'The Dev12 parser is able to resolve the ambiguity so xml is allowed here.
        <WorkItem(885304, "DevDiv/Personal")>
        <Fact>
        Public Sub BreakingChangeParseXmlRequiresParens()
            ParseAndVerify(<![CDATA[
                           Class Class1
                             Dim obj = New With {Key <xmlLiteral />.BaseUri}
                           End Class
                ]]>)
        End Sub

        <WorkItem(885416, "DevDiv/Personal")>
        <Fact>
        Public Sub ParseExpectedEOS()
            ParseAndVerify(<![CDATA[
                          Module Module1
                              Enum Test
                                [Class]
                                [GetXmlNamespace]
                              End Enum
                            Sub Main()
                               Dim t As Test
                               Select Case t
                               Case Test.Class
                               'COMPILEERROR: BC30205, " "  
                               Case Test.GetXmlNamespace _
     
                               'There must be space in between
                               End Select
                            End Sub
                          End Module
                ]]>)
        End Sub

        ''' Rosly doesn't give warning BC30934 while Dev10 does (Eval ctor as no const)
        '''  but gives new warning BC42025 for accessing const field through object instance
        ''' This is an improvement in Roslyn that we are able to eval the const access through 'new object()' instance
        <WorkItem(528223)>
        <Fact>
        Public Sub BC30934ERR_RequiredAttributeConstConversion2_1()
            Dim errs = CompilationUtils.CreateCompilationWithMscorlib(
    <compilation>
        <file name="a.vb"><![CDATA[
Imports System
Class Test
    Const x = ""
    <Obsolete(Foo(New Test()).x)>
    Shared Function Foo(ByVal x As Object) As Test
        Return Nothing
    End Function
End Class
        ]]></file>
    </compilation>).GetDiagnostics()
            Assert.Equal(1, errs.Length)
            Assert.Equal(42025, errs(0).Code)
            Assert.Equal(DiagnosticSeverity.Warning, errs(0).Severity)
        End Sub

        <Fact(), WorkItem(542389)>
        Public Sub BC30519_InferVariableAsRHSValueType()
            Dim text =
<compilation>
    <file name="a.vb">
Option Infer On

Imports System.Math

Namespace Round001
    Friend Module Math

        Sub Round001()
            Dim temp
            temp = CSng(1.235)
            Dim actual = Round(temp, 15)
        End Sub
    End Module
End Namespace
    </file>
</compilation>

            ' Not Breaking anymore - Dev11 gives NO error
            CreateCompilationWithMscorlibAndVBRuntime(text).VerifyDiagnostics()

        End Sub

        <WorkItem(531529)>
        <WorkItem(543241)>
        <Fact()>
        Public Sub BC42104WRN_DefAsgUseNullRef01()
            Dim errs = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(
    <compilation>
        <file name="a.vb"><![CDATA[
Imports System

Module ModuleA

    ReadOnly Property Prop As Long
        Get
            Dim a = New With { .id = Prop}
            Return a.id
       End Get
    End Property

    Sub Main()
        Console.Write(Prop)
    End Sub
End Module
        ]]></file>
    </compilation>).GetDiagnostics()

            ' Preserving backward compatibility: property do not warn on unassigned use
            Assert.Equal(0, errs.Length)
            'Assert.Equal(1, errs.Count)
            'Assert.Equal(42104, errs(0).Code)
            'Assert.Equal(DiagnosticSeverity.Warning, errs(0).Severity)
        End Sub

        <WorkItem(543241)>
        <WorkItem(531310)>
        <Fact()>
        Public Sub BC42104WRN_DefAsgUseNullRef02()
            Dim errs = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(
    <compilation>
        <file name="a.vb"><![CDATA[
Imports System

Module ModuleA

    Function Func() As Integer
        Dim x = Func
        Func = 1
        Exit Function
    End Function

    Sub Main()
        Console.Write(Func)
    End Sub
End Module
        ]]></file>
    </compilation>).GetDiagnostics()

            ' Preserving backward compatibility: property do not warn on unassigned use
            Assert.Equal(0, errs.Length)
            'Assert.Equal(1, errs.Count)
            'Assert.Equal(42104, errs(0).Code)
            'Assert.Equal(DiagnosticSeverity.Warning, errs(0).Severity)
        End Sub

        <WorkItem(543241)>
        <Fact()>
        Public Sub BC42109WRN_DefAsgUseNullRefStr01()
            Dim errs = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(
    <compilation>
        <file name="a.vb"><![CDATA[
Imports System

Module ModuleA

    Structure STR
        Public a As String
    End Structure

    ReadOnly Property Prop As STR
        Get
            Dim a = New With { .id = Prop}
            Return a.id
       End Get
    End Property

    Sub Main()
        Console.Write(Prop)
    End Sub
End Module
        ]]></file>
    </compilation>).GetDiagnostics()
            Assert.Equal(1, errs.Length)
            Assert.Equal(42109, errs(0).Code)
            Assert.Equal(DiagnosticSeverity.Warning, errs(0).Severity)
        End Sub

        <WorkItem(529262)>
        <Fact()>
        Public Sub PartialMethod_EmitNamesInProperCase()
            CompileAndVerify(
<compilation>
    <file name="a.vb">
Imports System        

Class C
    Partial Private Sub S(A As Integer)
    End Sub

    Private Sub s(a As Integer)
        Dim method = GetType(C).GetMethod("S", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        Console.Write(method.ToString())
        Console.Write("|")
        Dim param = method.GetParameters()(0)
        Console.Write(param.ToString())
    End Sub

    Shared Sub Main(args As String())
        Dim a As New C
        a.S(1)
    End Sub
End Class

    </file>
</compilation>, expectedOutput:="Void S(Int32)|Int32 A") 'Dev10 would emit "Void s(Int32)|Int32 a"
        End Sub

        <WorkItem(529261)>
        <Fact()>
        Public Sub PartialMethod_AllowNonExecutableStatements()
            'Dev10 used to report errors for each of the three cases identified below. Roslyn doesn't.
            'This is not strictly a breaking change since we just removed some errors that we used to give before.
            CompileAndVerify(<compilation>
                                 <file name="a.vb">
Imports System
Public Module Program
    'Case1: Preprocessor directives – but no executable statements
    Private Partial Sub M1
        #If False
            Console.WriteLine()
        #End If
        #if True
        #End If
    End Sub

    'Case2: Comments
    Private Partial Sub M2
        'A comment
    End Sub

    'Case3: Preprocessor constants that impact some other code later in the file
    Private Partial Sub M3
        #Const something = True
    End Sub

    Sub Main()
        M1() : M2() : M3()
        #If something
            Console.WriteLine("Success")
        #End If
    End Sub
End Module
                                 </file>
                             </compilation>, expectedOutput:="Success")
        End Sub

        <WorkItem(543241)>
        <Fact()>
        Public Sub BC42109WRN_DefAsgUseNullRefStr02()
            Dim errs = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(
    <compilation>
        <file name="a.vb"><![CDATA[
Imports System

Module ModuleA

    Structure STR
        Public a As String
    End Structure

    Function Func() As STR
        Dim x = Func
        Func = Nothing
        Exit Function
    End Function

    Sub Main()
        Console.Write(Func)
    End Sub
End Module
        ]]></file>
    </compilation>).GetDiagnostics()
            Assert.Equal(1, errs.Length)
            Assert.Equal(42109, errs(0).Code)
            Assert.Equal(DiagnosticSeverity.Warning, errs(0).Severity)
        End Sub

        <WorkItem(544500)>
        <Fact>
        Public Sub PartialConstructors()
            Dim compilation = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(
<compilation>
    <file name="a.vb">
Imports System
Partial Class C1(Of U, V)
    Class C1(Of T As U)
        Partial Private Sub New(x As T, y As C1(Of U), z As U, w As C1(Of U, V))
        End Sub

        Shared Function FactoryMethod(x As T, y As C1(Of U), z As U, w As C1(Of U, V)) As C1(Of T)
            Return New C1(Of T)(x, y, z, w)
        End Function
    End Class
End Class

Module Module1
    Sub Main()
        Dim e As New ArgumentException()
        Dim x = C1(Of Exception, Integer).C1(Of ArgumentException).FactoryMethod(e, Nothing, e, Nothing)
    End Sub
End Module
    </file>
    <file name="b.vb">
Imports System
Class C1(Of U, V)
    Partial Class C1(Of T As U)
        Private Sub New(x As T, y As C1(Of U), z As U, w As C1(Of U, V))
            Console.WriteLine("Success")
        End Sub
    End Class
End Class
    </file>
</compilation>)

            CompilationUtils.AssertTheseDeclarationDiagnostics(compilation,
<errors>
BC36969: 'Sub New' cannot be declared 'Partial'.
        Partial Private Sub New(x As T, y As C1(Of U), z As U, w As C1(Of U, V))
        ~~~~~~~
BC30269: 'Private Sub New(x As T, y As C1(Of U, V).C1(Of U), z As U, w As C1(Of U, V))' has multiple definitions with identical signatures.
        Partial Private Sub New(x As T, y As C1(Of U), z As U, w As C1(Of U, V))
                            ~~~
</errors>)
        End Sub

        <WorkItem(544500)>
        <Fact>
        Public Sub PartialConstructors2()
            Dim compilation = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(
<compilation>
    <file name="a.vb">
Imports System
Partial Class C1
    Partial Private Sub New()
    End Sub
End Class

Module Module1
    Sub Main()
        Dim e As New C1()
    End Sub
End Module
    </file>
</compilation>)

            CompilationUtils.AssertTheseDeclarationDiagnostics(compilation,
<errors>
BC36969: 'Sub New' cannot be declared 'Partial'.
    Partial Private Sub New()
    ~~~~~~~
</errors>)
        End Sub

        <WorkItem(528311)>
        <Fact()>
        Public Sub TestHideBySigChangeForOverridenMethods()
            Dim vbCompilation = CreateVisualBasicCompilation("TestHideBySigChangeForOverridenMethods",
            <![CDATA[Imports System

Public Class Class1
    Public Sub Foo(i As Integer)
        Console.WriteLine("Class1.Foo(i As Integer)")
    End Sub
    Public Overridable Sub Foo()
        Console.WriteLine("Class1.Foo()")
    End Sub
End Class

Public Class Class2 : Inherits Class1
    Public Overrides Sub Foo()
        Console.WriteLine("Class2.Foo()")
    End Sub
End Class

Public Module Program
    Sub Main(args As String())
        Dim b As Class1 = New Class2
        b.Foo
        b.Foo(1)
        Dim d As Class2 = New Class2
        d.Foo
        d.Foo(1)
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication))

            'In Dev10 the emitted signature for overriden methods did not 
            'include the 'hidebysig' flag. In Roslyn we decided to break
            'from Dev10 and include this flag. This was discussed at the
            'VB language design meeting. See roslyn bug 7299 for more details.
            '<quote>
            ' Per VB language design meeting 10/25/2011, we agreed that Dev10
            ' is incorrect here and this is the correct behavior and we wish 
            ' to make the change. 
            ' This makes some things in late binder change behavior, but 
            ' changes sometimes fix problems and sometimes cause new ones,
            ' but we think they are unlikely to hurt people in practice.
            '</quote>

            'TODO: Add tests for the latebinder breaks (See Dev10 bug 850631,849009).

            Dim vbVerifier = CompileAndVerify(vbCompilation,
                expectedOutput:=<![CDATA[Class2.Foo()
Class1.Foo(i As Integer)
Class2.Foo()
Class1.Foo(i As Integer)
]]>,
                expectedSignatures:=
                {
                    Signature("Class2", "Foo", ".method public hidebysig strict virtual instance System.Void Foo() cil managed")
                })

            vbVerifier.VerifyDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("CS",
            <![CDATA[public class Class3 : Class2
{
}

public class Program
{
    public static void Main()
    {
        Class3 d = new Class3();
        d.Foo();

        // The below line would fail to compile in Dev10 (with following error).
        // error CS1501: No overload for method 'Foo' takes 1 arguments.
        // In Roslyn this works fine.
        d.Foo(1);

        Class1 b = d;
        b.Foo();
        b.Foo(1);
    }
}]]>,
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={vbCompilation})
            csCompilation.VerifyDiagnostics() 'No errors
        End Sub

        <Fact(), WorkItem(529471)>
        Public Sub LiftedLogicalOperationsNoSideEffect()
            CompileAndVerify(
                <compilation>
                    <file name="a.vb">
Imports System
Module M

    Sub Main()
        Dim b As Boolean? = False
        Console.Write("F OrElse F=")
        If b OrElse Foo(b) Then
            Console.Write("True |")
        Else
            Console.Write("False |")
        End If

        b = True
        Console.Write("T AndAlso T=")
        If b AndAlso Bar(b) Then
            Console.Write("True | ")
        Else
            Console.Write("False | ")
        End If

        Dim bF As Boolean? = False
        Dim bT As Boolean? = True
        Console.Write("F Or F={0} | ", bF Or Foo(bF))
        Console.Write("T Or F={0} | ", bT Or Foo(bT))
        bF = False
        bT = True
        Console.Write("T And T={0} | ", bT And Bar(bT))
        Console.Write("F And T={0}", bF And Bar(bF))

    End Sub

    Function Foo(ByRef b As Boolean?) As Boolean?
        b = Not b
        Return False
    End Function

    Function Bar(ByRef b As Boolean?) As Boolean?
        b = Not b
        Return True
    End Function
End Module
                    </file>
                </compilation>, expectedOutput:="F OrElse F=False |T AndAlso T=True | F Or F=False | T Or F=True | T And T=True | F And T=False")
        End Sub

        <Fact(), WorkItem(545050)>
        Public Sub NoBC32126ERR_AddressOfNullableMethod_Static()

            ' Native: error BC32126: Methods of 'System.Nullable(Of T)' cannot be used as operands of the 'AddressOf' operator.
            ' Roslyn: No error
            CreateCompilationWithMscorlibAndVBRuntime(
    <compilation>
        <file name="a.vb">
            Imports System
            Imports System.Collections.Generic
            Module M
                Sub Main()
                    Dim ef As Action(Of Integer) = AddressOf Nullable(Of Integer).op_Implicit ' it is legal to take address of static method
                End Sub
            End Module
        </file>
    </compilation>).AssertNoDiagnostics()
        End Sub

        <Fact(), WorkItem(529544)>
        Public Sub TestMissingSynchronizedFlagForEvents()
            Dim comp = CreateVisualBasicCompilation("TestMissingSynchronizedFlagForEvents",
            <![CDATA[Public Class C1
    Public Event foo()
End Class]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary))

            ' In Dev11, we used to emit an additional 'synchronized' metadata flag in the signature of add_foo() and remove_foo() methods below.

            Dim verifier = CompileAndVerify(comp, expectedSignatures:=
            {
                Signature("C1", "add_foo", ".method [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] public specialname instance System.Void add_foo(C1+fooEventHandler obj) cil managed"),
                Signature("C1", "remove_foo", ".method [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] public specialname instance System.Void remove_foo(C1+fooEventHandler obj) cil managed")
            })

            verifier.VerifyDiagnostics()
        End Sub

        <Fact, WorkItem(529653)>
        Public Sub TestExecutionOrderForHandles()
            Dim vbCompilation = CreateVisualBasicCompilation("TestExecutionOrderForHandles",
            <![CDATA[Option Strict Off
Imports System
Imports AliasedType = Base

Public Class Base
    Protected WithEvents w As AliasedType = Me
    Protected Friend Event Ev1 As Action(Of Integer)

    Friend Sub A() Handles w.Ev1
        Console.WriteLine("Base A")
    End Sub

    Friend Sub B() Handles w.Ev1
        Console.WriteLine("Base B")
    End Sub

    Friend Sub C() Handles w.Ev1
        Console.WriteLine("Base C")
    End Sub

    Friend Sub D() Handles w.Ev1
        Console.WriteLine("Base D")
    End Sub

    Friend Sub E() Handles w.Ev1
        Console.WriteLine("Base E")
    End Sub

    Friend Sub F() Handles w.Ev1
        Console.WriteLine("Base F")
    End Sub

    Overridable Sub Raise()
        RaiseEvent Ev1(1)
    End Sub
End Class

Public Module Program
    Sub Main()
        Dim x = New Base()
        x.Raise()
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication))

            'Breaking Change: Dev11 processes event handlers in a different order than Roslyn.
            'This is basically because Dev11 has no deterministic order for processing these.
            'See bug 13880 for more details.
            Dim vbVerifier = CompileAndVerify(vbCompilation,
                expectedOutput:=<![CDATA[Base A
Base B
Base C
Base D
Base E
Base F]]>)
            vbVerifier.VerifyDiagnostics()
        End Sub

        <Fact, WorkItem(529574)>
        Public Sub TestCrossLanguageOptionalAndParamarrayForHandles()
            Dim csCompilation = CreateCSharpCompilation("TestCrossLanguageOptionalAndParamarrayForHandles_CS",
            <![CDATA[public class CSClass
{
    public delegate int bar(string x = "", params int[] y);
    public event bar ev;
    public void raise()
    {
        ev("hi", 1, 2, 3);
    }
}]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csCompilation.VerifyDiagnostics()
            Dim vbCompilation = CreateVisualBasicCompilation("TestCrossLanguageOptionalAndParamarrayForHandles_VB",
            <![CDATA[
Public Class VBClass : Inherits CSClass
    Public WithEvents w As CSClass = New CSClass
    Function Foo(x As String) Handles w.ev, MyBase.ev, MyClass.ev
        Return 0
    End Function
    Function Foo(x As String, ParamArray y() As Integer) Handles w.ev, MyBase.ev, MyClass.ev
        Return 0
    End Function
    Function Foo2(Optional x As String = "") Handles w.ev, MyBase.ev, MyClass.ev
        Return 0
    End Function
    Function Foo2(x As String, y() As Integer) Handles w.ev, MyBase.ev, MyClass.ev
        Return 0
    End Function
End Class
Public Module Program
    Sub Main()
        Dim x = New VBClass
        x.raise()
        x.w.raise()
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csCompilation})

            'Breaking Change: Dev11 allows above repro to compile while Roslyn reports following errors.
            'This was approved in VB LDB on 8/1/2012. See bug 13578 for more details.
            vbCompilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"))
        End Sub

        <Fact, WorkItem(569036)>
        Public Sub DifferenceInExceptionEvaluationWithRoslyn()
            CompileAndVerify(
<compilation>
    <file name="bug.vb">
Imports System
Module Module1
    Public Structure NullableRetStructure
        Public mem As Integer
        Public Shared Operator /(ByVal arg1 As NullableRetStructure, ByVal arg2 As Integer) As Short?
            Return arg1.mem / arg2
        End Operator
    End Structure

    Public Structure TestStruc
        Dim mem As Integer
    End Structure
 
        Public Function CheckType(Of T)(ByVal arg As T) As System.Type
            Return GetType(T)
        End Function
 
        Public Function foo_exc() As Integer
            Throw New ArgumentException
            foo_exc = 1
        End Function
 
        Public Function foo_eval_check(ByRef arg As Integer) As Integer?
            arg = arg + 1
            foo_eval_check = 1
        End Function

    Dim eval
 
    Sub Main()
        eval = 19
        Try
            Dim x = CheckType(foo_eval_check(eval) / foo_exc())
            Console.Write("Exception expected ")
        Catch ex As ArgumentException
            Console.Write("19:")
            Console.Write(eval)
        Catch ex As Exception
            Console.Write("Wrong Exception")
        End Try
    End Sub
End Module
    </file>
</compilation>, expectedOutput:="19:20") 'Dev11 would emit "19:19"
        End Sub
    End Class
End Namespace