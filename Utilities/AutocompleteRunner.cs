﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEditor;
using Microsoft.CodeAnalysis.Scripting;
using System.Threading;

public class AutocompleteRunner
{
    /*AdhocWorkspace workspace;
    CSharpCompilationOptions compilationOptions;
    ProjectInfo scriptProjectInfo;
    Project scriptProject;
    DocumentInfo scriptDocumentInfo;
    Document scriptDocument;
    TextAndVersion autocompleteText;
    CompletionService completionService;*/
    
    List<MetadataReference> references;

    public List<CompletionItem> completions { get; private set; }

    Task potentialsTask;
    CancellationTokenSource tokenSource;

    public AutocompleteRunner()
    {

        // cursor position is at the end
        string editorPath = EditorApplication.applicationPath;
        string applicationPath = Application.dataPath;
        references = new List<MetadataReference>();

        references = LoadAllDllsInFolder(references, Path.Combine(Path.GetDirectoryName(editorPath), "Data", "Managed", "UnityEngine"));
        references = LoadAllDllsInFolder(references, Path.Combine(applicationPath, @"../", "Library", "ScriptAssemblies"));
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        completions = new List<CompletionItem>();
    }

    public void LoadPotentials(string text, int position, List<ScriptVariable> otherVars)
    {
        if (potentialsTask != null && tokenSource != null)
            tokenSource.Cancel();

        tokenSource = new CancellationTokenSource();
        CancellationToken token = tokenSource.Token;
        potentialsTask = GetPotentials(text, position, otherVars,token);
    }

    private async Task GetPotentials(string text, int position, List<ScriptVariable> otherVars, CancellationToken token)
    {
        await Task.Run(() => {
            string preword = "public GameObject selection;";
            foreach (ScriptVariable var in otherVars)
            {
                preword += "public " + var.Type + " " + var.Name + ";";
            }

            if (token.IsCancellationRequested)
                return;

            text = preword + text;
            position += preword.Length;
            var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            
            var workspace = new AdhocWorkspace(host);
            //var scriptCode = "Guid.N";

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                usings: new[] { "UnityEngine", "System.Collections", "System.Collections.Generic" });
            //compilationOptions.WithUsings("UnityEngine", "System.Collections", "System.Collections.Generic");

            if (token.IsCancellationRequested)
                return;

            var scriptProjectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Script", "Script", LanguageNames.CSharp,
                    isSubmission: true)
                .WithMetadataReferences(references)
                .WithCompilationOptions(compilationOptions);

            if (token.IsCancellationRequested)
                return;

            var scriptProject = workspace.AddProject(scriptProjectInfo);

            if (token.IsCancellationRequested)
                return;

            var scriptDocumentInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(scriptProject.Id), "Script",
                sourceCodeKind: SourceCodeKind.Script,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())));

            if (token.IsCancellationRequested)
                return;

            var scriptDocument = workspace.AddDocument(scriptDocumentInfo);
            var completionService = CompletionService.GetService(scriptDocument);

            Task<Microsoft.CodeAnalysis.SyntaxNode> tree= scriptDocument.GetSyntaxRootAsync(token);
            tree.Wait();

            SyntaxToken currentToken= tree.Result.FindToken(Mathf.Clamp( position-1, 0, int.MaxValue));
            
            Task<CompletionList> results =  completionService.GetCompletionsAsync(scriptDocument, Mathf.Clamp(position, 0, int.MaxValue),cancellationToken:token);

            results.Wait();
            List<CompletionItem> filteredItems = new List<CompletionItem>();
            if (results.Result != null)
                filteredItems = new List<CompletionItem>( completionService.FilterItems(scriptDocument, results.Result.Items, currentToken.ToString()));

            completions = filteredItems;
        });
        
    }

    private static List<MetadataReference> LoadAllDllsInFolder(List<MetadataReference> references, string directory)
    {

        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if (Path.GetExtension(file) == ".dll")
                references.Add( MetadataReference.CreateFromFile(file));
        }
        return references;
    }
    
    public string GetLastToken(string text, List<ScriptVariable> otherVars, int position)
    {
        string preword = "public GameObject selection;";
        foreach (ScriptVariable var in otherVars)
        {
            preword += "public " + var.Type + " " + var.Name + ";";
        }

        text = preword + text;
        position += preword.Length;
        var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);

        var workspace = new AdhocWorkspace(host);
        //var scriptCode = "Guid.N";

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            usings: new[] { "UnityEngine", "System.Collections", "System.Collections.Generic" });
        //compilationOptions.WithUsings("UnityEngine", "System.Collections", "System.Collections.Generic");



        var scriptProjectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Script", "Script", LanguageNames.CSharp,
                isSubmission: true)
            .WithMetadataReferences(references)
            .WithCompilationOptions(compilationOptions);


        var scriptProject = workspace.AddProject(scriptProjectInfo);

        var scriptDocumentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(scriptProject.Id), "Script",
            sourceCodeKind: SourceCodeKind.Script,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())));
        var scriptDocument = workspace.AddDocument(scriptDocumentInfo);
        var completionService = CompletionService.GetService(scriptDocument);

        Task<Microsoft.CodeAnalysis.SyntaxNode> tree = scriptDocument.GetSyntaxRootAsync();
        tree.Wait();

        SyntaxToken currentToken = tree.Result.FindToken(Mathf.Clamp(position - 1, 0, int.MaxValue));

        Task<CompletionList> results = completionService.GetCompletionsAsync(scriptDocument, Mathf.Clamp(position, 0, int.MaxValue));

        results.Wait();
        List<CompletionItem> filteredItems = new List<CompletionItem>();
        return currentToken.ToString();
    }

    
}
