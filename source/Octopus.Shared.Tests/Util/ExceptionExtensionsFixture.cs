using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
    [TestFixture]
    [WindowsTest]
    [Ignore("These will soon be replaced and the approval is different on netcoreapp3.1")]
    public class ExceptionExtensionsFixture
    {
        private static readonly Assent.Configuration configuration = new Assent.Configuration()
            .UsingSanitiser(new Sanitiser())
            .UsingNamer(new SubdirectoryNamer("Approved"));

        static readonly string framework = string.Concat(RuntimeInformation.FrameworkDescription.Split(' ').Take(2));

        readonly AssentRunner assentRunner = new AssentRunner(configuration);
        readonly AssentRunner assentFrameworkSpecificRunner = new AssentRunner(configuration.UsingNamer(new SubdirectoryNamer("Approved", framework)));

        class Sanitiser : ISanitiser<string>
        {
            public string Sanatise(string recieved)
            {
                recieved = Regex.Replace(recieved, ":line [0-9]+", ":line <n>");
                recieved = Regex.Replace(recieved, "__[0-9]+_", "__n_");
                var lines = recieved.Split(new[] {'\n'})
                    .Select(l => l.TrimEnd('\r'))
                    .Where(l => !l.Contains(".nLoad"))
                    .Where(l => !l.EndsWith("at System.Threading.Tasks.Task.WaitAll(Task[] tasks, Int32 millisecondsTimeout)"))
                    .Where(l => !l.EndsWith("at System.Threading.Tasks.Task.WaitAll(Task[] tasks)"));

                return string.Join("\r\n", lines);
            }
        }

        [Test]
        public async Task PrettyPrint_AsyncException()
        {
            try
            {
                await GenerateException();
            }
            catch (Exception e)
            {
                assentRunner.Assent(e.PrettyPrint(), configuration);
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
                assentFrameworkSpecificRunner.Assent(this, e.PrettyPrint());
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
                assentFrameworkSpecificRunner.Assent(this, e.PrettyPrint());
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
                assentFrameworkSpecificRunner.Assent(this, e.PrettyPrint());
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
                .Select(_ => Task.Run(() => throw new Exception("Inner")))
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
            throw new NotImplementedException();
        }
    }
}