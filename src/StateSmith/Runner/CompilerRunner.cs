using StateSmith.output.C99BalancedCoder1;
using StateSmith.output.UserConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StateSmith.Compiling;
using System.IO;
using StateSmith.Input.Expansions;
using StateSmith.Input;
using StateSmith.compiler.Visitors;
using StateSmith.Input.PlantUML;
using StateSmith.compiler;
using StateSmith.Input.Yed;
using StateSmith.Input.DrawIo;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace StateSmith.Runner;

/// <summary>
/// Step 1: compile nodes to vertices
/// Step 2: select the state machine root to compile
/// Step 3: finish running the compiler
/// </summary>
public class CompilerRunner
{
    public Compiler compiler = new();
    public Statemachine? sm;
    public PrefixingModder prefixingModder = new();
    public CNameMangler mangler = new();
    public SsServiceProvider ssServiceProvider;

    /// <summary>
    /// This is not ready for widespread use. The API here will change. Feel free to play with it though.
    /// </summary>
    public Action<Statemachine> postParentAliasValidation = (_) => { };

    /// <summary>
    /// This is not ready for widespread use. The API here will change. Feel free to play with it though.
    /// </summary>
    public Action<Statemachine> preValidation = (_) => { };


    public CompilerRunner()
    {
        ssServiceProvider = new();
    }

    public CompilerRunner(SsServiceProvider ssServiceProvider)
    {
        this.ssServiceProvider = ssServiceProvider;
    }

    /// <summary>
    /// Step 1
    /// </summary>
    public void CompileDrawIoFileNodesToVertices(string filepath)
    {
        DrawIoToSmDiagramConverter converter = ssServiceProvider.GetServiceOrCreateInstance();
        converter.ProcessFile(filepath);
        CompileNodesToVertices(converter.Roots, converter.Edges);
    }

    /// <summary>
    /// Step 1
    /// </summary>
    public void CompileYedFileNodesToVertices(string filepath)
    {
        YedParser yedParser = new YedParser();
        yedParser.Parse(filepath);
        CompileNodesToVertices(yedParser.GetRootNodes(), yedParser.GetEdges());
    }

    /// <summary>
    /// Step 1
    /// </summary>
    public void CompilePlantUmlFileNodesToVertices(string filepath)
    {
        var text = File.ReadAllText(filepath);
        CompilePlantUmlTextNodesToVertices(text);
    }

    /// <summary>
    /// Step 1
    /// </summary>
    public void CompilePlantUmlTextNodesToVertices(string plantUmlText)
    {
        PlantUMLToNodesEdges translator = new();
        translator.ParseDiagramText(plantUmlText);

        if (translator.HasError())
        {
            string reasons = Compiler.ParserErrorsToReasonStrings(translator.GetErrors(), separator: "\n  - ");
            throw new FormatException("PlantUML input failed parsing. Reason(s):\n  - " + reasons);
        }

        CompileNodesToVertices(new List<DiagramNode> { translator.Root }, translator.Edges);
    }

    /// <summary>
    /// Step 1. Call this method when you want to support a custom input source.
    /// </summary>
    /// <param name="rootNodes"></param>
    /// <param name="edges"></param>
    public void CompileNodesToVertices(List<DiagramNode> rootNodes, List<DiagramEdge> edges)
    {
        compiler.CompileDiagramNodesEdges(rootNodes, edges);
    }

    /// <summary>
    /// Step 1. Call this method when you already have created state machine vertices. Probably from testing.
    /// </summary>
    public void SetRootVertices(List<Vertex> rootVertices)
    {
        compiler.rootVertices = rootVertices;
        compiler.SetupRoots();
    }

    /// <summary>
    /// Step 1. Call this method when you already have created a state machine vertex. Probably from testing.
    /// </summary>
    public void SetStateMachineRoot(Statemachine statemachine)
    {
        compiler.rootVertices = new List<Vertex>() { statemachine };
        compiler.SetupRoots();
    }

    //------------------------------------------------------------------------

    /// <summary>
    /// Step 2
    /// </summary>
    public void FindStateMachineByName(string stateMachineName)
    {
        sm = new Statemachine("non_null_dummy"); // todo_low: figure out how to not need this to appease nullable analysis
        var action = () => { sm = (Statemachine)compiler.GetVertex(stateMachineName); };
        action.RunOrWrapException((e) => new ArgumentException($"Couldn't find state machine in diagram with name `{stateMachineName}`.", e));
    }

    /// <summary>
    /// Step 2
    /// </summary>
    [MemberNotNull(nameof(sm))]
    public void FindSingleStateMachine()
    {
        sm = new Statemachine("non_null_dummy"); // todo_low: figure out how to not need this to appease nullable analysis. Maybe avoid action below.
        var action = () => { sm = compiler.rootVertices.OfType<Statemachine>().Single(); };
        action.RunOrWrapException((e) => new ArgumentException($"State machine name not specified. Expected diagram to have find 1 Statemachine node at root level. Instead, found {compiler.rootVertices.OfType<Statemachine>().Count()}.", e));
    }

    //------------------------------------------------------------------------

    /// <summary>
    /// Step 3
    /// </summary>
    public void FinishRunningCompiler()
    {
        SetupForSingleSm();
        mangler.SetStateMachine(sm);

        RemoveNotesVertices();
        compiler.SupportParentAlias();
        compiler.SupportEntryExitPoints();
        prefixingModder.Visit(sm); // must happen before history
        SupportHistory(sm, mangler);
        compiler.SupportElseTriggerAndOrderBehaviors();  // should happen last as it orders behaviors
        preValidation(sm);
        compiler.Validate();
        postParentAliasValidation(sm);

        compiler.Validate();
        compiler.DefaultToDoEventIfNoTrigger();
        compiler.FinalizeTrees();
        compiler.Validate();
    }

    [MemberNotNull(nameof(sm))]
    public void SetupForSingleSm()
    {
        compiler.SetupRoots();

        if (sm == null)
        {
            FindSingleStateMachine();
        }

        compiler.rootVertices = new List<Vertex> { sm };
    }

    // https://github.com/StateSmith/StateSmith/issues/63
    public static void SupportHistory(Statemachine sm, CNameMangler mangler)
    {
        var processor = new HistoryProcessor(sm, mangler);
        processor.Process();
    }

    public void RemoveNotesVertices()
    {
        var processor = new NotesProcessor();
        processor.ValidateAndRemoveNotes(sm!);
        UpdateNamedDescendantsMapping(sm!);
    }

    public static void UpdateNamedDescendantsMapping(Vertex startingVertex)
    {
        startingVertex.VisitRecursively(vertex =>
        {
            vertex.ResetNamedDescendantsMap();
        });

        startingVertex.VisitTypeRecursively<NamedVertex>(vertex =>
        {
            //add this vertex to ancestors
            var parent = vertex.Parent;
            while (parent != null)
            {
                parent._namedDescendants.AddIfMissing(vertex.Name, vertex);
                parent = parent.Parent;
            }
        });
    }
}