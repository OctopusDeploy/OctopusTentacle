using System;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public static class TentacleServiceDecoratorBuilderExtensions
    {
        public static TentacleServiceDecoratorBuilder RecordMethodUsages(this TentacleServiceDecoratorBuilder builder, TentacleConfigurationTestCase testCase, out IRecordedMethodUsages recordedUsages)
        {
            var localMethodUsages = new MethodUsages();
            recordedUsages = localMethodUsages;

            return builder.RegisterProxyDecorator(testCase.ScriptServiceToTest, service => MethodUsageProxyDecorator.Create(testCase.ScriptServiceToTest, service, localMethodUsages));
        }
        
        public static TentacleServiceDecoratorBuilder HookServiceMethod(this TentacleServiceDecoratorBuilder builder, TentacleConfigurationTestCase testCase, string methodName, PreMethodInvocationHook<object, object> preInvocation)
            => HookServiceMethod(builder, testCase, methodName, preInvocation, null);

        public static TentacleServiceDecoratorBuilder HookServiceMethod(this TentacleServiceDecoratorBuilder builder, TentacleConfigurationTestCase testCase, string methodName, PreMethodInvocationHook<object, object>? preInvocation, PostMethodInvocationHook<object, object>? postInvocation)
        {
            var proxyType = typeof(MethodInvocationHookProxyDecorator<,,>);
            var concreteType = proxyType.MakeGenericType(testCase.ScriptServiceToTest, typeof(object), typeof(object));

            var createMethodInfo = concreteType.GetMethod(nameof(MethodInvocationHookProxyDecorator<object, object, object>.Create));

            return builder.RegisterProxyDecorator(testCase.ScriptServiceToTest, service =>
            {
                return createMethodInfo.Invoke(null, new object?[] { service, methodName, preInvocation, postInvocation });
            });
        }
    }
}