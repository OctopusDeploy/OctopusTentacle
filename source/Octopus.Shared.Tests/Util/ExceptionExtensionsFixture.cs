using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Assent;
using Assent.Namers;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class ExceptionExtensionsFixture
    {
        static readonly Assent.Configuration configuration = new Assent.Configuration()
            .UsingSanitiser(r => Regex.Replace(r, ":line [0-9]+", ":line <n>"))
            .UsingNamer(new SubdirectoryNamer("Approved"));

        readonly AssentRunner assentRunner = new AssentRunner(configuration);

        [Test]
        public async Task PrettyPrint_AsyncException()
        {
            try
            {
                await GenerateException();
            }
            catch (Exception e)
            {
                this.Assent(e.PrettyPrint(), configuration);
            }
        }

        [Test]
        public void PrettyPrint_ReflectionTypeLoadException()
        {
            try
            {
                Assembly.Load("Foo");
            }
            catch (Exception e)
            {
                this.Assent(e.PrettyPrint(), configuration);
            }
        }

        [Test]
        public void PrettyPrint_InnerExceptions()
        {
            try
            {
                GenerateInnerException();
            }
            catch (Exception e)
            {
                assentRunner.Assent(this, e.PrettyPrint());
            }
        }

        [Test]
        public void PrettyPrint_NoStack()
        {
            try
            {
                GenerateInnerException();
            }
            catch (Exception e)
            {
                e.PrettyPrint(false).Should().Be("Outer\r\nInner");
            }
        }


        [Test]
        public void PrettyPrint_ControlledFailure()
        {
            const string message = "Something went terribly terribly wrong";
            try
            {
                throw new ControlledFailureException(message);
            }
            catch (Exception e)
            {
                e.PrettyPrint().Should().Be(message);
            }
        }

        [Test]
        public void PrettyPrint_TaskCanceledException()
        {
            try
            {
                var cts = new CancellationTokenSource();
                cts.Cancel();
                Task.Delay(1000, cts.Token).Wait(cts.Token);
            }
            catch (Exception e)
            {
                e.PrettyPrint().Should().Be("The task was canceled");
            }
        }

        [Test]
        public void PrettyPrint_WithStack_SingleAggregateException()
        {
            try
            {
                RaiseAggregateException(1);
            }
            catch (Exception e)
            {
                assentRunner.Assent(this, e.PrettyPrint());
            }
        }

        [Test]
        public void PrettyPrint_WithStack_MultipleAggregateException()
        {
            try
            {
                RaiseAggregateException(3);
            }
            catch (Exception e)
            {
                assentRunner.Assent(this, e.PrettyPrint());
            }
        }

        [Test]
        public void PrettyPrint_NoStack_SingleAggregateException()
        {
            try
            {
                RaiseAggregateException(1);
            }
            catch (Exception e)
            {
                e.PrettyPrint(false).Should().Be("Inner");
            }
        }

        [Test]
        public void PrettyPrint_NoStack_MultipleAggregateException()
        {
            try
            {
                RaiseAggregateException(3);
            }
            catch (Exception e)
            {
                assentRunner.Assent(this, e.PrettyPrint(false));
            }
        }


        void RaiseAggregateException(int i)
        {
            var tasks = Enumerable.Range(0, i)
                .Select(_ => Task.Run(() => { throw new Exception("Inner"); }))
                .ToArray();
            Task.WaitAll(tasks);
        }

        void GenerateInnerException()
        {
            try
            {
                throw new Exception("Inner");
            }
            catch (Exception e)
            {
                throw new Exception("Outer", e);
            }
        }

        async Task GenerateException()
        {
            await Task.Yield();
            await GenerateException1();
        }

        async Task GenerateException1()
        {
            await Task.Yield();
            throw new System.NotImplementedException();
        }
    }
}