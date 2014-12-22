using System;
using Autofac;
using MarkdownSharp;
using Octopus.Shared.Variables.Templates;

namespace Octopus.Shared.Variables
{
    public class VariableFunctionsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            var options = new MarkdownOptions();
            options.AutoHyperlink = true;
            options.LinkEmails = true;

            BuiltInFunctions.Register("Markdown", s => new Markdown(options).Transform(s.Trim()));
        }
    }
}
