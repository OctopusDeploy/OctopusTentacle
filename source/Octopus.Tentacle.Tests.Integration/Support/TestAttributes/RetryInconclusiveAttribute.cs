using System;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace NUnit.Framework
{
    /// <summary>
    /// Specifies that a test method should be rerun on an inconclusive result up to the specified
    /// maximum number of times.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RetryInconclusiveAttribute : NUnitAttribute, IRepeatTest
    {
        private readonly int tryCount;

        /// <summary>
        /// Construct a <see cref="RetryInconclusiveAttribute" />
        /// </summary>
        /// <param name="tryCount">The maximum number of times the test should be run if it fails</param>
        public RetryInconclusiveAttribute(int tryCount)
        {
            this.tryCount = tryCount;
        }

        /// <summary>
        /// Wrap a command and return the result.
        /// </summary>
        /// <param name="command">The command to be wrapped</param>
        /// <returns>The wrapped command</returns>
        public TestCommand Wrap(TestCommand command)
        {
            return new RetryCommand(command, tryCount);
        }

        /// <summary>
        /// The test command for the <see cref="RetryAttribute"/>
        /// </summary>
        public class RetryCommand : DelegatingTestCommand
        {
            private readonly int tryCount;

            /// <summary>
            /// Initializes a new instance of the <see cref="RetryCommand"/> class.
            /// </summary>
            /// <param name="innerCommand">The inner command.</param>
            /// <param name="tryCount">The maximum number of repetitions</param>
            public RetryCommand(TestCommand innerCommand, int tryCount)
                : base(innerCommand)
            {
                this.tryCount = tryCount;
            }

            /// <summary>
            /// Runs the test, saving a TestResult in the supplied TestExecutionContext.
            /// </summary>
            /// <param name="context">The context in which the test should run.</param>
            /// <returns>A TestResult</returns>
            public override TestResult Execute(TestExecutionContext context)
            {
                int count = tryCount;

                while (count-- > 0)
                {
                    try
                    {
                        context.CurrentResult = innerCommand.Execute(context);
                    }
                    // Commands are supposed to catch exceptions, but some don't
                    // and we want to look at restructuring the API in the future.
                    catch (Exception ex)
                    {
                        if (context.CurrentResult == null) context.CurrentResult = context.CurrentTest.MakeTestResult();
                        context.CurrentResult.RecordException(ex);
                    }

                    if (context.CurrentResult.ResultState != ResultState.Inconclusive)
                        break;

                    // Clear result for retry
                    if (count > 0)
                    {
                        context.CurrentResult = context.CurrentTest.MakeTestResult();
                        context.CurrentRepeatCount++; // increment Retry count for next iteration. will only happen if we are guaranteed another iteration
                    }
                }

                return context.CurrentResult;
            }
        }
    }
}
