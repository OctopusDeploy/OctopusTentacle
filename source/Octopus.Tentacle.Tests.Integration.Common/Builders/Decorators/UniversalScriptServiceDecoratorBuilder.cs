using System;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
{
    public class UniversalScriptServiceDecoratorBuilder
    {
        readonly List<Func<Task>> beforeStartScriptDecorators = new();
        readonly List<Func<Task>> afterStartScriptDecorators = new();
        readonly List<Func<Task>> beforeCancelScriptDecorators = new();
        readonly List<Func<Task>> beforeGetStatusDecorators = new();
        readonly List<Func<Task>> afterGetStatusDecorators = new();
        readonly List<Func<Task>> beforeCompleteScriptDecorators = new();

        public UniversalScriptServiceDecoratorBuilder BeforeStartScript(Func<Task> beforeStartScript)
        {
            beforeStartScriptDecorators.Add(beforeStartScript);
            return this;
        }

        public UniversalScriptServiceDecoratorBuilder AfterStartScript(Func<Task> afterStartScript)
        {
            afterStartScriptDecorators.Add(afterStartScript);
            return this;
        }

        public UniversalScriptServiceDecoratorBuilder BeforeCancelScript(Func<Task> beforeCancelScript)
        {
            beforeCancelScriptDecorators.Add(beforeCancelScript);
            return this;
        }

        public UniversalScriptServiceDecoratorBuilder BeforeGetStatus(Func<Task> beforeGetStatus)
        {
            beforeGetStatusDecorators.Add(beforeGetStatus);
            return this;
        }

        public UniversalScriptServiceDecoratorBuilder AfterGetStatus(Func<Task> afterGetStatus)
        {
            afterGetStatusDecorators.Add(afterGetStatus);
            return this;
        }

        public UniversalScriptServiceDecoratorBuilder BeforeCompleteScript(Func<Task> beforeCompleteScript)
        {
            beforeCompleteScriptDecorators.Add(beforeCompleteScript);
            return this;
        }

        public UniversalScriptServiceDecorator Build()
        {
            // We make a copy of the list here to prevent modifications after build
            return new UniversalScriptServiceDecorator(
                beforeStartScriptDecorators.ToList(),
                afterStartScriptDecorators.ToList(),
                beforeCancelScriptDecorators.ToList(),
                beforeGetStatusDecorators.ToList(),
                afterGetStatusDecorators.ToList(),
                beforeCompleteScriptDecorators.ToList()
            );
        }
    }
}