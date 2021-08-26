// ReSharper disable RedundantUsingDirective
using System;
using System.IO;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;

public class ModifiedFile : IDisposable
{
    readonly AbsolutePath FilePath;
    readonly string OriginalFileText;
    string FileText;
        
    public ModifiedFile(AbsolutePath filePath)
    {
        FilePath = filePath;
        OriginalFileText = File.ReadAllText(filePath);
        FileText = File.ReadAllText(filePath);
    }
        
    public void Dispose()
    {
        Logger.Info($"Restoring file {FilePath}");
        File.WriteAllText(FilePath, OriginalFileText);
    }
        
    public void ReplaceRegexInFiles(string matchingPattern, string replacement)
    {
        FileText = Regex.Replace(FileText, matchingPattern, replacement);
        File.WriteAllText(FilePath, FileText);
    }

    public void ReplaceTextInFile(string textToReplace, string replacementValue)
    {
        FileText = FileText.Replace(textToReplace, replacementValue);
        File.WriteAllText(FilePath, FileText);
    }
}